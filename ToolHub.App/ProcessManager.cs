using System.Collections.Concurrent;
using System.Diagnostics;
using ToolHub.App.Models;
using ToolHub.App.Runtime;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class ProcessManager : IProcessManager
{
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

        var process = new Process
        {
            StartInfo = BuildStartInfo(resolvedCommand),
            EnableRaisingEvents = true
        };

        var context = new RunContext(run, process);
        if (!_runs.TryAdd(run.RunId, context))
        {
            throw new InvalidOperationException(ProcessErrorMessages.RunContextAlreadyExists(run.RunId));
        }

        HookProcessEvents(context);
        _sendMessage(new RunStartedMessage(ProcessRunUtilities.CloneRun(run)));

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

            _sendMessage(new ErrorMessage(ProcessErrorMessages.FailedToStartTool(tool.Name), ex.Message));
            _sendMessage(new RunStatusMessage(ProcessRunUtilities.CloneRun(run)));
            ProcessRunUtilities.DisposeProcessSafe(process);
        }

        return ProcessRunUtilities.CloneRun(run);
    }

    public async Task<bool> StopRunAsync(string runId)
    {
        ThrowIfDisposed();

        if (!_runs.TryGetValue(runId, out var context))
        {
            return false;
        }

        if (!ProcessRunUtilities.IsProcessRunning(context.Process))
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
            .Select(context => ProcessRunUtilities.CloneRun(context.Run))
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
                if (ProcessRunUtilities.IsProcessRunning(context.Process))
                {
                    context.RequestStop();
                    ProcessKiller.KillProcessTreeAsync(context.Process)
                        .Wait(TimeSpan.FromSeconds(3));
                }
            }
            catch
            {
                // Ignore shutdown failures.
            }

            ProcessRunUtilities.DisposeProcessSafe(context.Process);
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
            run.ExitCode = ProcessRunUtilities.TryReadExitCode(context.Process);
            run.Status = context.StopRequested
                ? RunStates.Stopped
                : run.ExitCode == 0
                    ? RunStates.Exited
                    : RunStates.Failed;
        }

        _sendMessage(new RunStatusMessage(ProcessRunUtilities.CloneRun(run)));
        ProcessRunUtilities.DisposeProcessSafe(context.Process);
        TrimCompletedRuns();
    }

    private static ProcessStartInfo BuildStartInfo(ResolvedRunCommand command)
    {
        return ProcessStartInfoFactory.Build(command);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProcessManager));
        }
    }

    private void TrimCompletedRuns()
    {
        ProcessRunUtilities.TrimCompletedRuns(_runs, context => context.Run, MaxCompletedRunHistory);
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
