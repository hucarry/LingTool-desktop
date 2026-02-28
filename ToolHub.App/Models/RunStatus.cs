namespace ToolHub.App.Models;

public static class RunStates
{
    public const string Running = "running";
    public const string Exited = "exited";
    public const string Stopped = "stopped";
    public const string Failed = "failed";
}

public sealed class RunInfo
{
    public string RunId { get; set; } = string.Empty;

    public string ToolId { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string Status { get; set; } = RunStates.Running;

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public int? ExitCode { get; set; }

    public int? Pid { get; set; }
}

public sealed class RunLogEntry
{
    public string RunId { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Line { get; set; } = string.Empty;

    public DateTimeOffset Ts { get; set; }
}
