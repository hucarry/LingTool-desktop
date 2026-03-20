using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ToolHub.App.Models;
using ToolHub.App.Runtime;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class ProcessManager : IDisposable
{
    /// <summary>宸插畬鎴愯繍琛岀殑鏈€澶т繚鐣欐潯鏁帮紝瓒呭嚭鍚庤嚜鍔ㄦ竻鐞嗘渶鏃х殑璁板綍銆?/summary>
    private const int MaxCompletedRunHistory = 200;

    private readonly ConcurrentDictionary<string, RunContext> _runs = new();
    private readonly Action<object> _sendMessage;

    private bool _disposed;

    public ProcessManager(Action<object> sendMessage)
    {
        _sendMessage = sendMessage;
    }

    public RunInfo StartRun(RunnableTool tool, ResolvedRunCommand resolvedCommand)
    {
        ThrowIfDisposed();

        var run = new RunInfo
        {
            RunId = Guid.NewGuid().ToString("N"),
            ToolId = tool.Id,
            ToolName = tool.Name,
            Status = RunStates.Running,
            StartTime = DateTimeOffset.UtcNow
        };

        var startInfo = BuildStartInfo(resolvedCommand);
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var context = new RunContext(run, process);
        if (!_runs.TryAdd(run.RunId, context))
        {
            throw new InvalidOperationException($"鏃犳硶鍒涘缓杩愯涓婁笅鏂? {run.RunId}");
        }

        HookProcessEvents(context);
        _sendMessage(new RunStartedMessage(CloneRun(run)));

        try
        {
            process.Start();
            run.Pid = process.Id;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            lock (context.SyncRoot)
            {
                run.Status = RunStates.Failed;
                run.EndTime = DateTimeOffset.UtcNow;
                run.ExitCode = -1;
            }

            _sendMessage(new ErrorMessage($"鍚姩宸ュ叿澶辫触: {tool.Name}", ex.Message));
            _sendMessage(new RunStatusMessage(CloneRun(run)));

            // 鍚姩澶辫触鏃朵篃闇€瑕侀噴鏀?Process 瀵硅薄锛岄槻姝㈠彞鏌勬硠婕?
            DisposeProcessSafe(process);
        }

        return CloneRun(run);
    }

    public async Task<bool> StopRunAsync(string runId)
    {
        ThrowIfDisposed();

        if (!_runs.TryGetValue(runId, out var context))
        {
            return false;
        }

        if (!IsProcessRunning(context.Process))
        {
            return false;
        }

        context.RequestStop();
        await ProcessKiller.KillProcessTreeAsync(context.Process);
        return true;
    }

    public IReadOnlyList<RunInfo> GetRuns()
    {
        return _runs.Values
            .Select(context => CloneRun(context.Run))
            .OrderByDescending(run => run.StartTime)
            .ToList();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var context in _runs.Values)
        {
            try
            {
                if (IsProcessRunning(context.Process))
                {
                    context.RequestStop();
                    // 浣跨敤甯﹁秴鏃剁殑 Wait 閬垮厤姝婚攣锛屾渶澶氱瓑 3 绉?
                    ProcessKiller.KillProcessTreeAsync(context.Process)
                        .Wait(TimeSpan.FromSeconds(3));
                }
            }
            catch
            {
                // 蹇界暐缁堟閿欒锛岄伩鍏嶉樆濉炲簲鐢ㄥ叧闂?
            }

            // 鏃犺杩涚▼鏄惁杩樺湪杩愯锛岄兘閲婃斁 Process 瀵硅薄
            DisposeProcessSafe(context.Process);
        }
    }

    private void HookProcessEvents(RunContext context)
    {
        context.Process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            _sendMessage(new LogMessage(
                context.Run.RunId,
                LogChannels.Stdout,
                e.Data,
                DateTimeOffset.UtcNow
            ));
        };

        context.Process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            _sendMessage(new LogMessage(
                context.Run.RunId,
                LogChannels.Stderr,
                e.Data,
                DateTimeOffset.UtcNow
            ));
        };

        context.Process.Exited += (_, _) => HandleProcessExited(context);
    }

    private void HandleProcessExited(RunContext context)
    {
        var run = context.Run;

        lock (context.SyncRoot)
        {
            if (run.EndTime is not null)
            {
                return;
            }

            run.EndTime = DateTimeOffset.UtcNow;
            run.ExitCode = TryReadExitCode(context.Process);

            if (context.StopRequested)
            {
                run.Status = RunStates.Stopped;
            }
            else
            {
                run.Status = run.ExitCode == 0
                    ? RunStates.Exited
                    : RunStates.Failed;
            }
        }

        _sendMessage(new RunStatusMessage(CloneRun(run)));

        // 閲婃斁宸插畬鎴愮殑 Process 瀵硅薄锛屽洖鏀剁郴缁熷彞鏌?
        DisposeProcessSafe(context.Process);

        // 娓呯悊杩囨棫鐨勮繍琛岃褰曪紝闃叉鍐呭瓨鏃犻檺澧為暱
        TrimCompletedRuns();
    }

    private static ProcessStartInfo BuildStartInfo(ResolvedRunCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = command.WorkingDirectory
        };

        if (string.Equals(command.ToolType, "python", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = command.CommandPath;
            ApplyBundledPythonEnvironment(startInfo);
        }
        else if (string.Equals(command.ToolType, "node", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ToolType, "command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ToolType, "executable", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = command.CommandPath;
        }
        else
        {
            throw new NotSupportedException($"Unsupported tool type: {command.ToolType}");
        }

        foreach (var arg in command.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
    }

    private static bool IsProcessRunning(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyBundledPythonEnvironment(ProcessStartInfo startInfo)
    {
        if (string.IsNullOrWhiteSpace(startInfo.FileName) || !Path.IsPathRooted(startInfo.FileName))
        {
            return;
        }

        var pythonRoot = Path.GetDirectoryName(startInfo.FileName);
        if (string.IsNullOrWhiteSpace(pythonRoot) || !Directory.Exists(pythonRoot))
        {
            return;
        }

        startInfo.Environment["PYTHONHOME"] = pythonRoot;

        var tclLibrary = ResolveFirstExistingDirectory(
            Path.Combine(pythonRoot, "tcl", "tcl8.6"),
            Path.Combine(pythonRoot, "Library", "lib", "tcl8.6"),
            Path.Combine(pythonRoot, "lib", "tcl8.6")
        );
        if (!string.IsNullOrWhiteSpace(tclLibrary))
        {
            startInfo.Environment["TCL_LIBRARY"] = tclLibrary;
        }

        var tkLibrary = ResolveFirstExistingDirectory(
            Path.Combine(pythonRoot, "tcl", "tk8.6"),
            Path.Combine(pythonRoot, "Library", "lib", "tk8.6"),
            Path.Combine(pythonRoot, "lib", "tk8.6")
        );
        if (!string.IsNullOrWhiteSpace(tkLibrary))
        {
            startInfo.Environment["TK_LIBRARY"] = tkLibrary;
        }
    }

    private static string? ResolveFirstExistingDirectory(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static int? TryReadExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }

    private static RunInfo CloneRun(RunInfo source)
    {
        return new RunInfo
        {
            RunId = source.RunId,
            ToolId = source.ToolId,
            ToolName = source.ToolName,
            Status = source.Status,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            ExitCode = source.ExitCode,
            Pid = source.Pid
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProcessManager));
        }
    }

    /// <summary>瀹夊叏閲婃斁 Process 瀵硅薄锛屽拷鐣ヤ换浣曢噴鏀鹃敊璇€?/summary>
    private static void DisposeProcessSafe(Process process)
    {
        try
        {
            process.Dispose();
        }
        catch
        {
            // 蹇界暐閲婃斁閿欒
        }
    }

    /// <summary>娓呯悊杩囨棫鐨勫凡瀹屾垚杩愯璁板綍锛岄槻姝?_runs 瀛楀吀鏃犻檺澧為暱銆?/summary>
    private void TrimCompletedRuns()
    {
        var completedEntries = _runs
            .Where(pair => pair.Value.Run.EndTime is not null)
            .OrderBy(pair => pair.Value.Run.EndTime)
            .ToList();

        if (completedEntries.Count <= MaxCompletedRunHistory)
        {
            return;
        }

        var excessCount = completedEntries.Count - MaxCompletedRunHistory;
        foreach (var entry in completedEntries.Take(excessCount))
        {
            _runs.TryRemove(entry.Key, out _);
        }
    }

    private sealed class RunContext
    {
        public RunContext(RunInfo run, Process process)
        {
            Run = run;
            Process = process;
        }

        public RunInfo Run { get; }

        public Process Process { get; }

        public object SyncRoot { get; } = new();

        private int _stopRequested;

        public bool StopRequested => Volatile.Read(ref _stopRequested) == 1;

        public void RequestStop()
        {
            Volatile.Write(ref _stopRequested, 1);
        }
    }
}
