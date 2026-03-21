using System.Collections.Concurrent;
using ToolHub.App;
using ToolHub.App.Models;

namespace ToolHub.App.Tests;

public sealed class ProcessRunUtilitiesTests
{
    [Fact]
    public void CloneRun_ShouldCopyAllFields()
    {
        var source = new RunInfo
        {
            RunId = "run-1",
            ToolId = "tool-1",
            ToolName = "Tool One",
            Status = "running",
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndTime = DateTimeOffset.UtcNow,
            ExitCode = 2,
            Pid = 1234
        };

        var cloned = ProcessRunUtilities.CloneRun(source);

        Assert.NotSame(source, cloned);
        Assert.Equal(source.RunId, cloned.RunId);
        Assert.Equal(source.ToolId, cloned.ToolId);
        Assert.Equal(source.ToolName, cloned.ToolName);
        Assert.Equal(source.Status, cloned.Status);
        Assert.Equal(source.StartTime, cloned.StartTime);
        Assert.Equal(source.EndTime, cloned.EndTime);
        Assert.Equal(source.ExitCode, cloned.ExitCode);
        Assert.Equal(source.Pid, cloned.Pid);
    }

    [Fact]
    public void TrimCompletedRuns_ShouldKeepNewestCompletedEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var runs = new ConcurrentDictionary<string, RunInfo>(StringComparer.Ordinal)
        {
            ["completed-oldest"] = new() { RunId = "completed-oldest", EndTime = now.AddMinutes(-5) },
            ["completed-middle"] = new() { RunId = "completed-middle", EndTime = now.AddMinutes(-3) },
            ["completed-newest"] = new() { RunId = "completed-newest", EndTime = now.AddMinutes(-1) },
            ["running"] = new() { RunId = "running", EndTime = null }
        };

        ProcessRunUtilities.TrimCompletedRuns(runs, static run => run, maxCompletedRunHistory: 2);

        Assert.False(runs.ContainsKey("completed-oldest"));
        Assert.True(runs.ContainsKey("completed-middle"));
        Assert.True(runs.ContainsKey("completed-newest"));
        Assert.True(runs.ContainsKey("running"));
    }
}
