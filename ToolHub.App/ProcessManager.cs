using System.Collections.Concurrent;
using System.Diagnostics;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class ProcessManager : IDisposable
{
    private readonly ConcurrentDictionary<string, RunContext> _runs = new();
    private readonly Action<object> _sendMessage;

    private bool _disposed;

    public ProcessManager(Action<object> sendMessage)
    {
        _sendMessage = sendMessage;
    }

    public RunInfo StartRun(
        ToolItem tool,
        IReadOnlyDictionary<string, string?> args,
        string? pythonOverride = null
    )
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

        var startInfo = BuildStartInfo(tool, args, pythonOverride);
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var context = new RunContext(run, process);
        if (!_runs.TryAdd(run.RunId, context))
        {
            throw new InvalidOperationException($"无法创建运行上下文: {run.RunId}");
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

            _sendMessage(new ErrorMessage($"启动工具失败: {tool.Name}", ex.Message));
            _sendMessage(new RunStatusMessage(CloneRun(run)));
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

        context.StopRequested = true;
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
                if (!IsProcessRunning(context.Process))
                {
                    continue;
                }

                context.StopRequested = true;
                ProcessKiller.KillProcessTreeAsync(context.Process).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore dispose errors to avoid blocking app shutdown.
            }
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
    }

    private static ProcessStartInfo BuildStartInfo(
        ToolItem tool,
        IReadOnlyDictionary<string, string?> args,
        string? pythonOverride
    )
    {
        var resolvedArgs = ArgTemplate.BuildArguments(tool.ArgsTemplate, args);

        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = tool.Cwd ?? Directory.GetCurrentDirectory()
        };

        if (string.Equals(tool.Type, "python", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = string.IsNullOrWhiteSpace(pythonOverride)
                ? (string.IsNullOrWhiteSpace(tool.Python) ? "python" : tool.Python)
                : pythonOverride;
            startInfo.ArgumentList.Add(tool.Path);

            foreach (var arg in resolvedArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }

            return startInfo;
        }

        if (string.Equals(tool.Type, "exe", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = tool.Path;

            foreach (var arg in resolvedArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }

            return startInfo;
        }

        throw new NotSupportedException($"不支持的工具类型: {tool.Type}");
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

        public bool StopRequested { get; set; }
    }
}
