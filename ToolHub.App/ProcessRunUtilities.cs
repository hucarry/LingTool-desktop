using System.Collections.Concurrent;
using System.Diagnostics;
using ToolHub.App.Models;

namespace ToolHub.App;

internal static class ProcessRunUtilities
{
    internal static bool IsProcessRunning(Process process)
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

    internal static int? TryReadExitCode(Process process)
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

    internal static RunInfo CloneRun(RunInfo source)
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

    internal static void DisposeProcessSafe(Process process)
    {
        try
        {
            process.Dispose();
        }
        catch
        {
            // Ignore process cleanup failures.
        }
    }

    internal static void TrimCompletedRuns<TContext>(
        ConcurrentDictionary<string, TContext> runs,
        Func<TContext, RunInfo> getRun,
        int maxCompletedRunHistory)
    {
        var completedEntries = runs
            .Where(pair => getRun(pair.Value).EndTime is not null)
            .OrderBy(pair => getRun(pair.Value).EndTime)
            .ToList();

        if (completedEntries.Count <= maxCompletedRunHistory)
        {
            return;
        }

        var excessCount = completedEntries.Count - maxCompletedRunHistory;
        foreach (var entry in completedEntries.Take(excessCount))
        {
            runs.TryRemove(entry.Key, out _);
        }
    }
}
