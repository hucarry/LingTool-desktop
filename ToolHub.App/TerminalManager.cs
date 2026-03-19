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
    private static readonly string TerminalScriptDirectory = Path.Combine(
        Path.GetTempPath(),
        "ToolHub",
        "terminal-scripts"
    );

    private readonly Action<object> _sendMessage;
    private readonly ConcurrentDictionary<string, TerminalContext> _terminals = new();
    private bool _isDisposed;

    public TerminalManager(Action<object> sendMessage)
    {
        _sendMessage = sendMessage;
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
            var started = await StartTerminalAsync(title: tool.Name, cwd: tool.Cwd);
            targetTerminalId = started.TerminalId;
            await Task.Delay(500); // 等待 PTY 完成初始化，避免命令丢失
        }
        else
        {
            targetTerminalId = terminalId;
        }

        if (!_terminals.TryGetValue(targetTerminalId, out var context))
        {
            throw new InvalidOperationException($"Terminal not found: {targetTerminalId}");
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(tool.Name) ? null : tool.Name.Trim();
        if (!string.Equals(context.Info.Title, normalizedTitle, StringComparison.Ordinal))
        {
            context.Info.Title = normalizedTitle;
            _sendMessage(new TerminalStatusMessage(context.Info));
        }

        var command = BuildRunCommand(context.Info.Shell, tool, args, pythonPath);
        await SendInputAsync(targetTerminalId, command);
    }

    public void StopTerminal(string terminalId)
    {
        if (_terminals.TryRemove(terminalId, out var context))
        {
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
        var textBuffer = new System.Text.StringBuilder();
        var flushLock = new object();
        const int flushIntervalMs = 50;

        // 定时 flush 缓冲区
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
            // Expected during terminal shutdown.
        }
        catch (ObjectDisposedException) when (context.StopRequested)
        {
            // Expected during terminal shutdown.
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

        // 循环结束后发送缓冲区中剩余的数据
        FlushOutputBuffer(context, textBuffer, flushLock);
    }

    /// <summary>将缓冲区中的文本一次性发送给前端，减少高频输出时的消息数量。</summary>
    private void FlushOutputBuffer(TerminalContext context, System.Text.StringBuilder textBuffer, object flushLock)
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
            // 忽略发送错误（终端可能已关闭）
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
            // 优先使用 PowerShell，兼容中文路径且支持现代语法
            return "powershell.exe";
        }

        return "/bin/bash";
    }

    private static string BuildRunCommand(
        string shell,
        ToolItem tool,
        IReadOnlyDictionary<string, string?> args,
        string? runtimePath)
    {
        var shellKind = GetShellKind(shell);
        var resolvedArgs = ArgTemplate.BuildArguments(tool.ArgsTemplate, args);
        string? sessionSetup = null;

        var parts = new List<string>();
        if (string.Equals(tool.Type, "python", StringComparison.OrdinalIgnoreCase))
        {
            var interpreter = PythonInterpreterProbe.ResolvePreferred(runtimePath, tool.RuntimePath) ?? "python";
            sessionSetup = BuildSessionSetupCommand(shellKind, tool.Type, interpreter);

            parts.Add(interpreter);
            parts.Add(tool.Path);
        }
        else if (string.Equals(tool.Type, "node", StringComparison.OrdinalIgnoreCase))
        {
            var interpreter = NodeRuntimeProbe.ResolvePreferred(runtimePath, tool.RuntimePath) ?? "node";
            sessionSetup = BuildSessionSetupCommand(shellKind, tool.Type, interpreter);

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
            ShellKind.PowerShell => BuildPowerShellCommand(tool.Cwd, sessionSetup, parts),
            ShellKind.Cmd => BuildCmdCommand(tool.Cwd, sessionSetup, parts),
            _ => BuildGenericCommand(tool.Cwd, sessionSetup, parts)
        };
    }

    private static string? BuildTerminalBootstrapCommand(
        string shell,
        string? cwd,
        string? toolType,
        string? runtimePath)
    {
        var shellKind = GetShellKind(shell);
        var sessionSetup = string.IsNullOrWhiteSpace(toolType) || string.IsNullOrWhiteSpace(runtimePath)
            ? null
            : BuildSessionSetupCommand(shellKind, toolType, runtimePath);

        if (string.IsNullOrWhiteSpace(cwd) && string.IsNullOrWhiteSpace(sessionSetup))
        {
            return null;
        }

        return shellKind switch
        {
            ShellKind.PowerShell => BuildPowerShellBootstrapCommand(cwd, sessionSetup),
            ShellKind.Cmd => BuildCmdBootstrapCommand(cwd, sessionSetup),
            _ => BuildGenericBootstrapCommand(cwd, sessionSetup)
        };
    }

    private static string BuildPowerShellCommand(
        string? cwd,
        string? sessionSetup,
        IReadOnlyList<string> parts)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            lines.Add($"Set-Location -LiteralPath {ToPowerShellLiteral(cwd!)} -ErrorAction Stop");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            lines.Add(sessionSetup);
        }

        lines.Add($"& {string.Join(" ", parts.Select(ToPowerShellLiteral))}");
        return BuildPowerShellScriptInvocation(lines);
    }

    private static string BuildCmdCommand(
        string? cwd,
        string? sessionSetup,
        IReadOnlyList<string> parts)
    {
        var lines = new List<string>
        {
            "@echo off"
        };

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            lines.Add($"cd /d {ToCmdQuoted(cwd!)}");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            lines.Add(sessionSetup);
        }

        lines.Add(string.Join(" ", parts.Select(ToCmdQuoted)));
        return BuildCmdScriptInvocation(lines);
    }

    private static string BuildPowerShellBootstrapCommand(string? cwd, string? sessionSetup)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            lines.Add($"Set-Location -LiteralPath {ToPowerShellLiteral(cwd!)} -ErrorAction Stop");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            lines.Add(sessionSetup);
        }

        return BuildPowerShellScriptInvocation(lines);
    }

    private static string BuildCmdBootstrapCommand(string? cwd, string? sessionSetup)
    {
        var lines = new List<string>
        {
            "@echo off"
        };

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            lines.Add($"cd /d {ToCmdQuoted(cwd!)}");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            lines.Add(sessionSetup);
        }

        return BuildCmdScriptInvocation(lines);
    }

    private static string BuildGenericCommand(
        string? cwd,
        string? sessionSetup,
        IReadOnlyList<string> parts)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            sb.Append("cd ");
            sb.Append(ToGenericQuoted(cwd!));
            sb.Append(" && ");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            sb.Append(sessionSetup);
            sb.Append(" && ");
        }

        sb.Append(string.Join(" ", parts.Select(ToGenericQuoted)));
        sb.Append("\n");

        return sb.ToString();
    }

    private static string? BuildGenericBootstrapCommand(string? cwd, string? sessionSetup)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            parts.Add($"cd {ToGenericQuoted(cwd!)}");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            parts.Add(sessionSetup);
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(" && ", parts) + "\n";
    }

    private static string? BuildSessionSetupCommand(ShellKind shellKind, string toolType, string runtimePath)
    {
        if (string.IsNullOrWhiteSpace(runtimePath) || !Path.IsPathRooted(runtimePath))
        {
            return null;
        }

        var normalizedRuntime = Path.GetFullPath(runtimePath);
        return shellKind switch
        {
            ShellKind.PowerShell => BuildPowerShellSessionSetup(toolType, normalizedRuntime),
            ShellKind.Cmd => BuildCmdSessionSetup(toolType, normalizedRuntime),
            _ => null
        };
    }

    private static string? BuildPowerShellSessionSetup(string toolType, string runtimePath)
    {
        var commands = new List<string>();

        if (string.Equals(toolType, "python", StringComparison.OrdinalIgnoreCase))
        {
            commands.Add(BuildPowerShellPythonResetCommand());

            if (TryGetCondaEnvironmentRoot(runtimePath, out var condaRoot))
            {
                commands.Add($"conda activate {ToPowerShellLiteral(condaRoot)} | Out-Null");
                return string.Join("; ", commands);
            }

            if (TryGetVirtualEnvActivateScript(runtimePath, out var activateScript))
            {
                commands.Add($". {ToPowerShellLiteral(activateScript)}");
                return string.Join("; ", commands);
            }

            if (TryGetRuntimeDirectory(runtimePath, out var runtimeDirectory))
            {
                commands.Add(BuildPowerShellPathPrependCommand(runtimeDirectory));
                commands.AddRange(BuildPowerShellBundledPythonEnvironmentCommands(runtimePath));
                commands.Add($"function global:python {{ & {ToPowerShellLiteral(runtimePath)} @args }}");
                commands.Add($"function global:pip {{ & {ToPowerShellLiteral(runtimePath)} -m pip @args }}");
                return string.Join("; ", commands);
            }

            return commands.Count > 0 ? string.Join("; ", commands) : null;
        }

        if (string.Equals(toolType, "node", StringComparison.OrdinalIgnoreCase)
            && TryGetRuntimeDirectory(runtimePath, out var nodeRuntimeDirectory))
        {
            return string.Join("; ",
            [
                BuildPowerShellRuntimeFunctionResetCommand(),
                BuildPowerShellPathPrependCommand(nodeRuntimeDirectory),
                $"function global:node {{ & {ToPowerShellLiteral(runtimePath)} @args }}"
            ]);
        }

        return null;
    }

    private static string? BuildCmdSessionSetup(string toolType, string runtimePath)
    {
        if (string.Equals(toolType, "python", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetVirtualEnvActivateBatch(runtimePath, out var activateBatch))
            {
                return $"call {ToCmdQuoted(activateBatch)}";
            }

            if (TryGetCondaEnvironmentRoot(runtimePath, out var condaRoot))
            {
                return $"call conda activate {ToCmdQuoted(condaRoot)}";
            }

            if (TryGetRuntimeDirectory(runtimePath, out var runtimeDirectory))
            {
                var commands = new List<string>
                {
                    $"set \"PATH={runtimeDirectory};%PATH%\""
                };
                commands.AddRange(BuildCmdBundledPythonEnvironmentCommands(runtimePath));
                return string.Join(" && ", commands);
            }
        }

        if (string.Equals(toolType, "node", StringComparison.OrdinalIgnoreCase)
            && TryGetRuntimeDirectory(runtimePath, out var nodeRuntimeDirectory))
        {
            return $"set \"PATH={nodeRuntimeDirectory};%PATH%\"";
        }

        return null;
    }

    private static string BuildPowerShellPythonResetCommand()
    {
        return string.Join("; ",
        [
            BuildPowerShellRuntimeFunctionResetCommand(),
            "Remove-Item Env:PYTHONHOME -ErrorAction SilentlyContinue",
            "Remove-Item Env:TCL_LIBRARY -ErrorAction SilentlyContinue",
            "Remove-Item Env:TK_LIBRARY -ErrorAction SilentlyContinue",
            "if (Get-Command conda -ErrorAction SilentlyContinue) { while ($env:CONDA_SHLVL -and [int]$env:CONDA_SHLVL -gt 0) { conda deactivate | Out-Null } } elseif (Get-Command deactivate -ErrorAction SilentlyContinue) { try { deactivate | Out-Null } catch {} }"
        ]);
    }

    private static string BuildPowerShellRuntimeFunctionResetCommand()
    {
        return "foreach ($toolHubName in 'python', 'pip', 'node') { if (Test-Path (\"Function:\" + $toolHubName)) { Remove-Item (\"Function:\" + $toolHubName) -ErrorAction SilentlyContinue }; if (Test-Path (\"Alias:\" + $toolHubName)) { Remove-Item (\"Alias:\" + $toolHubName) -ErrorAction SilentlyContinue } }";
    }

    private static string BuildPowerShellPathPrependCommand(string runtimeDirectory)
    {
        var literal = ToPowerShellLiteral(runtimeDirectory);
        return $"$env:PATH = {literal} + ';' + ((($env:PATH -split ';') | Where-Object {{ $_ -and $_ -ne {literal} }}) -join ';')";
    }

    private static IEnumerable<string> BuildPowerShellBundledPythonEnvironmentCommands(string runtimePath)
    {
        if (!TryGetPythonRuntimeRoot(runtimePath, out var pythonRoot))
        {
            return [];
        }

        var commands = new List<string>
        {
            $"$env:PYTHONHOME = {ToPowerShellLiteral(pythonRoot)}"
        };

        if (TryGetTclLibraryPath(pythonRoot, out var tclLibrary))
        {
            commands.Add($"$env:TCL_LIBRARY = {ToPowerShellLiteral(tclLibrary)}");
        }

        if (TryGetTkLibraryPath(pythonRoot, out var tkLibrary))
        {
            commands.Add($"$env:TK_LIBRARY = {ToPowerShellLiteral(tkLibrary)}");
        }

        return commands;
    }

    private static IEnumerable<string> BuildCmdBundledPythonEnvironmentCommands(string runtimePath)
    {
        if (!TryGetPythonRuntimeRoot(runtimePath, out var pythonRoot))
        {
            return [];
        }

        var commands = new List<string>
        {
            $"set \"PYTHONHOME={pythonRoot}\""
        };

        if (TryGetTclLibraryPath(pythonRoot, out var tclLibrary))
        {
            commands.Add($"set \"TCL_LIBRARY={tclLibrary}\"");
        }

        if (TryGetTkLibraryPath(pythonRoot, out var tkLibrary))
        {
            commands.Add($"set \"TK_LIBRARY={tkLibrary}\"");
        }

        return commands;
    }

    private static bool TryGetPythonRuntimeRoot(string runtimePath, out string pythonRoot)
    {
        pythonRoot = string.Empty;
        try
        {
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                return false;
            }

            pythonRoot = Path.GetFullPath(runtimeDirectory);
            return Directory.Exists(pythonRoot);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetTclLibraryPath(string pythonRoot, out string tclLibraryPath)
    {
        return TryGetFirstExistingDirectory(
            out tclLibraryPath,
            Path.Combine(pythonRoot, "tcl", "tcl8.6"),
            Path.Combine(pythonRoot, "Library", "lib", "tcl8.6"),
            Path.Combine(pythonRoot, "lib", "tcl8.6")
        );
    }

    private static bool TryGetTkLibraryPath(string pythonRoot, out string tkLibraryPath)
    {
        return TryGetFirstExistingDirectory(
            out tkLibraryPath,
            Path.Combine(pythonRoot, "tcl", "tk8.6"),
            Path.Combine(pythonRoot, "Library", "lib", "tk8.6"),
            Path.Combine(pythonRoot, "lib", "tk8.6")
        );
    }

    private static bool TryGetFirstExistingDirectory(out string resolvedPath, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        resolvedPath = string.Empty;
        return false;
    }

    private static bool TryGetRuntimeDirectory(string runtimePath, out string runtimeDirectory)
    {
        runtimeDirectory = Path.GetDirectoryName(runtimePath) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(runtimeDirectory);
    }

    private static bool TryGetVirtualEnvActivateScript(string runtimePath, out string activateScriptPath)
    {
        activateScriptPath = string.Empty;
        var runtimeDirectory = Path.GetDirectoryName(runtimePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(runtimeDirectory), "Scripts", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var environmentRoot = Directory.GetParent(runtimeDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(environmentRoot)
            || !File.Exists(Path.Combine(environmentRoot, "pyvenv.cfg")))
        {
            return false;
        }

        var candidate = Path.Combine(runtimeDirectory, "Activate.ps1");
        if (!File.Exists(candidate))
        {
            return false;
        }

        activateScriptPath = candidate;
        return true;
    }

    private static bool TryGetVirtualEnvActivateBatch(string runtimePath, out string activateBatchPath)
    {
        activateBatchPath = string.Empty;
        var runtimeDirectory = Path.GetDirectoryName(runtimePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(runtimeDirectory), "Scripts", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var environmentRoot = Directory.GetParent(runtimeDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(environmentRoot)
            || !File.Exists(Path.Combine(environmentRoot, "pyvenv.cfg")))
        {
            return false;
        }

        var candidate = Path.Combine(runtimeDirectory, "activate.bat");
        if (!File.Exists(candidate))
        {
            return false;
        }

        activateBatchPath = candidate;
        return true;
    }

    private static bool TryGetCondaEnvironmentRoot(string runtimePath, out string environmentRoot)
    {
        environmentRoot = string.Empty;
        var runtimeDirectory = Path.GetDirectoryName(runtimePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return false;
        }

        var candidate = Path.GetFullPath(runtimeDirectory);
        if (Directory.Exists(Path.Combine(candidate, "conda-meta")))
        {
            environmentRoot = candidate;
            return true;
        }

        return false;
    }

    private static string BuildPowerShellScriptInvocation(IReadOnlyList<string> lines)
    {
        var allLines = lines
            .Concat(
            [
                "Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue"
            ])
            .ToArray();

        var scriptPath = CreateTerminalScriptFile(".ps1", allLines);
        return $". {ToPowerShellLiteral(scriptPath)}\r\n";
    }

    private static string BuildCmdScriptInvocation(IReadOnlyList<string> lines)
    {
        var allLines = lines
            .Concat(
            [
                "del \"%~f0\" >nul 2>nul"
            ])
            .ToArray();

        var scriptPath = CreateTerminalScriptFile(".cmd", allLines);
        return $"call {ToCmdQuoted(scriptPath)}\r\n";
    }

    private static string CreateTerminalScriptFile(string extension, IReadOnlyList<string> lines)
    {
        Directory.CreateDirectory(TerminalScriptDirectory);
        CleanupOldTerminalScripts();

        var scriptPath = Path.Combine(
            TerminalScriptDirectory,
            $"toolhub-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}"
        );

        File.WriteAllLines(scriptPath, lines, ResolveTerminalScriptEncoding(extension));
        return scriptPath;
    }

    private static Encoding ResolveTerminalScriptEncoding(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".ps1" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            ".cmd" or ".bat" => Encoding.Default,
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
    }

    private static void CleanupOldTerminalScripts()
    {
        try
        {
            if (!Directory.Exists(TerminalScriptDirectory))
            {
                return;
            }

            var threshold = DateTime.UtcNow.AddHours(-12);
            foreach (var file in Directory.EnumerateFiles(TerminalScriptDirectory, "toolhub-*.*"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < threshold)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore locked or already deleted temp script files.
                }
            }
        }
        catch
        {
            // Ignore temp directory cleanup errors.
        }
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
                // Ignore repeated cancellation.
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
                // Ignore repeated cancellation.
            }

            Connection.Dispose();
            ReadCancellation.Dispose();
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
