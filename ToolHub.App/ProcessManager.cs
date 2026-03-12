using System.Collections.Concurrent;
using System.Diagnostics;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class ProcessManager : IDisposable
{
    /// <summary>已完成运行的最大保留条数，超出后自动清理最旧的记录。</summary>
    private const int MaxCompletedRunHistory = 200;

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

            // 启动失败时也需要释放 Process 对象，防止句柄泄漏
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
                    ProcessKiller.KillProcessTreeAsync(context.Process).GetAwaiter().GetResult();
                }
            }
            catch
            {
                // 忽略终止错误，避免阻塞应用关闭
            }

            // 无论进程是否还在运行，都释放 Process 对象
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

        // 释放已完成的 Process 对象，回收系统句柄
        DisposeProcessSafe(context.Process);

        // 清理过旧的运行记录，防止内存无限增长
        TrimCompletedRuns();
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
            startInfo.FileName = PythonInterpreterProbe.ResolvePreferred(pythonOverride, tool.Python) ?? "python";
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

    /// <summary>安全释放 Process 对象，忽略任何释放错误。</summary>
    private static void DisposeProcessSafe(Process process)
    {
        try
        {
            process.Dispose();
        }
        catch
        {
            // 忽略释放错误
        }
    }

    /// <summary>清理过旧的已完成运行记录，防止 _runs 字典无限增长。</summary>
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
