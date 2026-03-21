using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pty.Net;
using ToolHub.App.Models;
using ToolHub.App.Runtime;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class TerminalManager : ITerminalManager
{
    private const int DefaultCols = 120;
    private const int DefaultRows = 30;

    private readonly Action<object> _sendMessage;
    private readonly ConcurrentDictionary<string, TerminalContext> _terminals = new();
    private readonly ILogger<TerminalManager> _logger;
    private bool _isDisposed;

    public TerminalManager(Action<object> sendMessage, ILogger<TerminalManager>? logger = null)
    {
        _sendMessage = sendMessage;
        _logger = logger ?? NullLogger<TerminalManager>.Instance;
    }

    public async Task<TerminalInfo> StartTerminalAsync(
        string? title = null,
        string? shell = null,
        string? cwd = null,
        string? toolType = null,
        string? runtimePath = null)
    {
        ThrowIfDisposed();

        var info = new TerminalInfo
        {
            TerminalId = Guid.NewGuid().ToString("n"),
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
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
                CommandLine = GetShellArgs(info.Shell),
                Cwd = info.Cwd,
                Cols = DefaultCols,
                Rows = DefaultRows,
                VerbatimCommandLine = false
            };

            _logger.LogInformation(
                "Starting terminal {TerminalId}. Shell={Shell} Cwd={Cwd} Title={Title}",
                info.TerminalId,
                info.Shell,
                info.Cwd,
                info.Title
            );

            var connection = await PtyProvider.SpawnAsync(options, CancellationToken.None);
            var context = new TerminalContext(info, connection);

            if (!_terminals.TryAdd(info.TerminalId, context))
            {
                connection.Dispose();
                throw new InvalidOperationException(TerminalErrorMessages.TerminalAlreadyExists(info.TerminalId));
            }

            info.Pid = connection.Pid;
            connection.ProcessExited += (_, _) => HandleExited(context);

            _ = ReadOutputAsync(context);
            _sendMessage(new TerminalStartedMessage(info));

            var bootstrapCommand = BuildTerminalBootstrapCommand(info.Shell, info.Cwd, toolType, runtimePath);
            if (!string.IsNullOrWhiteSpace(bootstrapCommand))
            {
                await Task.Delay(350);
                await SendInputAsync(info.TerminalId, bootstrapCommand);
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start terminal {TerminalId}. Shell={Shell} Cwd={Cwd}",
                info.TerminalId,
                info.Shell,
                info.Cwd
            );
            info.Status = TerminalStates.Failed;
            info.EndTime = DateTimeOffset.UtcNow;
            _sendMessage(new TerminalStatusMessage(info));
            throw new InvalidOperationException(TerminalErrorMessages.FailedToStartTerminalException(ex.Message), ex);
        }
    }

    public async Task<bool> SendInputAsync(string terminalId, string data)
    {
        ThrowIfDisposed();

        if (!_terminals.TryGetValue(terminalId, out var context) || context.StopRequested)
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send input to terminal {TerminalId}.", terminalId);
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
        RunnableTool tool,
        ResolvedRunCommand resolvedCommand)
    {
        string targetTerminalId;
        if (string.IsNullOrEmpty(terminalId) || !_terminals.ContainsKey(terminalId))
        {
            var started = await StartTerminalAsync(title: tool.Name, cwd: tool.Cwd);
            targetTerminalId = started.TerminalId;
            await Task.Delay(500);
        }
        else
        {
            targetTerminalId = terminalId;
        }

        if (!_terminals.TryGetValue(targetTerminalId, out var context))
        {
            throw new InvalidOperationException(TerminalErrorMessages.TerminalNotFound(targetTerminalId));
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(tool.Name) ? null : tool.Name.Trim();
        if (!string.Equals(context.Info.Title, normalizedTitle, StringComparison.Ordinal))
        {
            context.Info.Title = normalizedTitle;
            _sendMessage(new TerminalStatusMessage(context.Info));
        }

        var command = BuildRunCommand(context.Info.Shell, resolvedCommand);
        _logger.LogInformation(
            "Running tool {ToolId} in terminal {TerminalId}. Shell={Shell}",
            tool.Id,
            targetTerminalId,
            context.Info.Shell
        );
        await SendInputAsync(targetTerminalId, command);
    }

    public void StopTerminal(string terminalId)
    {
        if (_terminals.TryRemove(terminalId, out var context))
        {
            _logger.LogInformation("Stopping terminal {TerminalId}.", terminalId);
            context.RequestStop();
            ForceStopTerminalProcess(context);

            context.Info.Status = TerminalStates.Stopped;
            context.Info.EndTime = DateTimeOffset.UtcNow;
            context.Info.ExitCode = TryReadExitCode(context);

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
        var textBuffer = new StringBuilder();
        var flushLock = new object();
        const int flushIntervalMs = 50;

        using var flushTimer = new System.Threading.Timer(_ =>
        {
            FlushOutputBuffer(context, textBuffer, flushLock);
        }, null, flushIntervalMs, flushIntervalMs);

        try
        {
            while (!context.StopRequested)
            {
                int count = await context.Connection.ReaderStream.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    context.ReadCancellation.Token
                );
                if (count <= 0)
                {
                    break;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, count);
                lock (flushLock)
                {
                    textBuffer.Append(text);
                }
            }
        }
        catch (OperationCanceledException) when (context.StopRequested || context.ReadCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (context.StopRequested)
        {
        }
        catch (Exception ex)
        {
            if (!context.StopRequested)
            {
                _logger.LogWarning(ex, "Terminal output reader failed for terminal {TerminalId}.", context.Info.TerminalId);
                _sendMessage(new TerminalOutputMessage(
                    context.Info.TerminalId,
                    $"\r\n[TerminalManager] Read output error: {ex.Message}\r\n",
                    LogChannels.Stderr,
                    DateTimeOffset.UtcNow
                ));
            }
        }

        FlushOutputBuffer(context, textBuffer, flushLock);
    }

    private void FlushOutputBuffer(TerminalContext context, StringBuilder textBuffer, object flushLock)
    {
        string chunk;
        lock (flushLock)
        {
            if (textBuffer.Length == 0)
            {
                return;
            }

            chunk = textBuffer.ToString();
            textBuffer.Clear();
        }

        try
        {
            _sendMessage(new TerminalOutputMessage(
                context.Info.TerminalId,
                chunk,
                LogChannels.Stdout,
                DateTimeOffset.UtcNow
            ));
        }
        catch
        {
            // Ignore send failures while terminal is closing.
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
        context.Info.ExitCode = TryReadExitCode(context);

        _sendMessage(new TerminalStatusMessage(context.Info));
        _logger.LogInformation(
            "Terminal {TerminalId} exited with status {Status} and exit code {ExitCode}.",
            context.Info.TerminalId,
            context.Info.Status,
            context.Info.ExitCode
        );
        context.Dispose();
    }

    private static string ResolveWorkingDirectory(string? cwd)
    {
        return TerminalShellUtilities.ResolveWorkingDirectory(cwd);
    }

    private static string[] GetShellArgs(string shell)
    {
        return TerminalShellUtilities.GetShellArgs(shell);
    }

    private static string ResolveShell(string? shell)
    {
        return TerminalShellUtilities.ResolveShell(shell);
    }

    private static string BuildRunCommand(
        string shell,
        RunnableTool tool,
        IReadOnlyDictionary<string, string?> args,
        string? runtimePath)
    {
        return TerminalCommandFactory.BuildRunCommand(shell, tool, args, runtimePath);
    }

    private static string BuildRunCommand(string shell, ResolvedRunCommand command)
    {
        return TerminalCommandFactory.BuildRunCommand(shell, command);
    }

    private static string? BuildTerminalBootstrapCommand(
        string shell,
        string? cwd,
        string? toolType,
        string? runtimePath)
    {
        return TerminalCommandFactory.BuildTerminalBootstrapCommand(shell, cwd, toolType, runtimePath);
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

    private static int? TryReadExitCode(TerminalContext context)
    {
        try
        {
            return context.Connection.ExitCode;
        }
        catch
        {
            return null;
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
        public CancellationTokenSource ReadCancellation { get; } = new();
        public SemaphoreSlim WriteLock { get; } = new(1, 1);

        private int _stopRequested;

        public bool StopRequested => Volatile.Read(ref _stopRequested) == 1;

        public void RequestStop()
        {
            Volatile.Write(ref _stopRequested, 1);

            try
            {
                ReadCancellation.Cancel();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            try
            {
                ReadCancellation.Cancel();
            }
            catch
            {
            }

            Connection.Dispose();
            ReadCancellation.Dispose();
            WriteLock.Dispose();
        }
    }
}
