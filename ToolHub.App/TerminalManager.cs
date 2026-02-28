using System.Collections.Concurrent;
using System.Text;
using Pty.Net;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class TerminalManager : IDisposable
{
    private const int DefaultCols = 120;
    private const int DefaultRows = 30;

    private readonly Action<object> _sendMessage;
    private readonly ConcurrentDictionary<string, TerminalContext> _terminals = new();
    private bool _isDisposed;

    public TerminalManager(Action<object> sendMessage)
    {
        _sendMessage = sendMessage;
    }

    public async Task<TerminalInfo> StartTerminalAsync(string? shell = null, string? cwd = null)
    {
        ThrowIfDisposed();

        var info = new TerminalInfo
        {
            TerminalId = Guid.NewGuid().ToString("n"),
            Shell = ResolveShell(shell),
            Cwd = ResolveWorkingDirectory(cwd),
            Status = TerminalStates.Running,
            StartTime = DateTimeOffset.UtcNow
        };

        try
        {
            var options = new PtyOptions
            {
                App = info.Shell,
                Cwd = info.Cwd,
                Cols = DefaultCols,
                Rows = DefaultRows,
                VerbatimCommandLine = true
            };

            var connection = await PtyProvider.SpawnAsync(options, CancellationToken.None);
            var context = new TerminalContext(info, connection);

            if (!_terminals.TryAdd(info.TerminalId, context))
            {
                connection.Dispose();
                throw new InvalidOperationException($"Terminal already exists: {info.TerminalId}");
            }

            info.Pid = connection.Pid;
            connection.ProcessExited += (_, _) => HandleExited(context);

            _ = ReadOutputAsync(context);
            _sendMessage(new TerminalStartedMessage(info));

            return info;
        }
        catch (Exception ex)
        {
            info.Status = TerminalStates.Failed;
            info.EndTime = DateTimeOffset.UtcNow;
            _sendMessage(new TerminalStatusMessage(info));
            throw new InvalidOperationException($"Failed to start terminal: {ex.Message}", ex);
        }
    }

    public async Task<bool> SendInputAsync(string terminalId, string data)
    {
        ThrowIfDisposed();

        if (!_terminals.TryGetValue(terminalId, out var context))
        {
            return false;
        }

        if (context.StopRequested)
        {
            return false;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            await context.WriteLock.WaitAsync();
            try
            {
                await context.Connection.WriterStream.WriteAsync(bytes, 0, bytes.Length);
                await context.Connection.WriterStream.FlushAsync();
            }
            finally
            {
                context.WriteLock.Release();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ResizeTerminal(string terminalId, int cols, int rows)
    {
        if (_terminals.TryGetValue(terminalId, out var context))
        {
            try
            {
                context.Connection.Resize(cols, rows);
            }
            catch
            {
                // Ignore resize errors.
            }
        }
    }

    public async Task RunToolInTerminalAsync(
        string? terminalId,
        ToolItem tool,
        Dictionary<string, string?> args,
        string? pythonPath)
    {
        string targetTerminalId;
        if (string.IsNullOrEmpty(terminalId) || !_terminals.ContainsKey(terminalId))
        {
            var started = await StartTerminalAsync(cwd: tool.Cwd);
            targetTerminalId = started.TerminalId;
            await Task.Delay(200);
        }
        else
        {
            targetTerminalId = terminalId;
        }

        if (!_terminals.TryGetValue(targetTerminalId, out var context))
        {
            throw new InvalidOperationException($"Terminal not found: {targetTerminalId}");
        }

        var command = BuildRunCommand(context.Info.Shell, tool, args, pythonPath);
        await SendInputAsync(targetTerminalId, command);
    }

    public void StopTerminal(string terminalId)
    {
        if (_terminals.TryRemove(terminalId, out var context))
        {
            context.StopRequested = true;
            ForceStopTerminalProcess(context);

            context.Info.Status = TerminalStates.Stopped;
            context.Info.EndTime = DateTimeOffset.UtcNow;
            context.Info.ExitCode = context.Connection.ExitCode;
            _sendMessage(new TerminalStatusMessage(context.Info));

            context.Dispose();
        }
    }

    public IEnumerable<TerminalInfo> GetTerminals() => _terminals.Values.Select(v => v.Info);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        foreach (var terminalId in _terminals.Keys)
        {
            StopTerminal(terminalId);
        }
    }

    private async Task ReadOutputAsync(TerminalContext context)
    {
        var buffer = new byte[4096];

        try
        {
            while (!context.StopRequested)
            {
                int count = await context.Connection.ReaderStream.ReadAsync(buffer, 0, buffer.Length);
                if (count <= 0)
                {
                    break;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, count);
                _sendMessage(new TerminalOutputMessage(context.Info.TerminalId, text, LogChannels.Stdout, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex)
        {
            if (!context.StopRequested)
            {
                _sendMessage(new TerminalOutputMessage(
                    context.Info.TerminalId,
                    $"\r\n[TerminalManager] Read output error: {ex.Message}\r\n",
                    LogChannels.Stderr,
                    DateTimeOffset.UtcNow
                ));
            }
        }
    }

    private void HandleExited(TerminalContext context)
    {
        if (!_terminals.TryRemove(context.Info.TerminalId, out _))
        {
            return;
        }

        context.Info.Status = context.StopRequested ? TerminalStates.Stopped : TerminalStates.Exited;
        context.Info.EndTime = DateTimeOffset.UtcNow;
        context.Info.ExitCode = context.Connection.ExitCode;

        _sendMessage(new TerminalStatusMessage(context.Info));
        context.Dispose();
    }

    private static string ResolveWorkingDirectory(string? cwd)
    {
        var raw = string.IsNullOrWhiteSpace(cwd)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : cwd.Trim();

        return Path.GetFullPath(raw);
    }

    private static string ResolveShell(string? shell)
    {
        if (!string.IsNullOrWhiteSpace(shell))
        {
            return shell.Trim();
        }

        if (OperatingSystem.IsWindows())
        {
            return "powershell.exe";
        }

        return "/bin/bash";
    }

    private static string BuildRunCommand(
        string shell,
        ToolItem tool,
        IReadOnlyDictionary<string, string?> args,
        string? pythonPath)
    {
        var shellKind = GetShellKind(shell);
        var resolvedArgs = ArgTemplate.BuildArguments(tool.ArgsTemplate, args);

        var parts = new List<string>();
        if (string.Equals(tool.Type, "python", StringComparison.OrdinalIgnoreCase))
        {
            var interpreter = !string.IsNullOrWhiteSpace(pythonPath)
                ? pythonPath!
                : (!string.IsNullOrWhiteSpace(tool.Python) ? tool.Python! : "python");

            parts.Add(interpreter);
            parts.Add(tool.Path);
        }
        else
        {
            parts.Add(tool.Path);
        }

        parts.AddRange(resolvedArgs);

        return shellKind switch
        {
            ShellKind.PowerShell => BuildPowerShellCommand(tool.Cwd, parts),
            ShellKind.Cmd => BuildCmdCommand(tool.Cwd, parts),
            _ => BuildGenericCommand(tool.Cwd, parts)
        };
    }

    private static string BuildPowerShellCommand(string? cwd, IReadOnlyList<string> parts)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            sb.Append("Set-Location -LiteralPath ");
            sb.Append(ToPowerShellLiteral(cwd!));
            sb.Append(" -ErrorAction Stop; ");
        }

        sb.Append("& ");
        sb.Append(string.Join(" ", parts.Select(ToPowerShellLiteral)));
        sb.Append("\r\n");

        return sb.ToString();
    }

    private static string BuildCmdCommand(string? cwd, IReadOnlyList<string> parts)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            sb.Append("cd /d ");
            sb.Append(ToCmdQuoted(cwd!));
            sb.Append(" && ");
        }

        sb.Append(string.Join(" ", parts.Select(ToCmdQuoted)));
        sb.Append("\r\n");

        return sb.ToString();
    }

    private static string BuildGenericCommand(string? cwd, IReadOnlyList<string> parts)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            sb.Append("cd ");
            sb.Append(ToGenericQuoted(cwd!));
            sb.Append(" && ");
        }

        sb.Append(string.Join(" ", parts.Select(ToGenericQuoted)));
        sb.Append("\n");

        return sb.ToString();
    }

    private static string ToPowerShellLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static string ToCmdQuoted(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string ToGenericQuoted(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static ShellKind GetShellKind(string shell)
    {
        var name = GetShellExecutableName(shell);
        return name switch
        {
            "powershell.exe" or "powershell" or "pwsh.exe" or "pwsh" => ShellKind.PowerShell,
            "cmd.exe" or "cmd" => ShellKind.Cmd,
            _ => ShellKind.Other
        };
    }

    private static string GetShellExecutableName(string shell)
    {
        var trimmed = shell.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        string executable;
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            executable = endQuote > 1
                ? trimmed[1..endQuote]
                : trimmed.Trim('"');
        }
        else
        {
            var firstSpace = trimmed.IndexOf(' ');
            executable = firstSpace < 0 ? trimmed : trimmed[..firstSpace];
        }

        return Path.GetFileName(executable).ToLowerInvariant();
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(TerminalManager));
        }
    }

    private static void ForceStopTerminalProcess(TerminalContext context)
    {
        try
        {
            context.Connection.Kill();
        }
        catch
        {
            // Ignore direct PTY kill errors.
        }

        if (context.Info.Pid is not int pid || pid <= 0)
        {
            return;
        }

        try
        {
            ProcessKiller.KillProcessTreeAsync(pid).GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore process tree kill errors.
        }
    }

    private sealed class TerminalContext : IDisposable
    {
        public TerminalContext(TerminalInfo info, IPtyConnection connection)
        {
            Info = info;
            Connection = connection;
        }

        public TerminalInfo Info { get; }
        public IPtyConnection Connection { get; }
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public bool StopRequested { get; set; }

        public void Dispose()
        {
            Connection.Dispose();
            WriteLock.Dispose();
        }
    }

    private enum ShellKind
    {
        Cmd,
        PowerShell,
        Other
    }
}
