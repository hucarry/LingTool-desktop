namespace ToolHub.App.Models;

public static class TerminalStates
{
    public const string Running = "running";
    public const string Exited = "exited";
    public const string Stopped = "stopped";
    public const string Failed = "failed";
}

public sealed class TerminalInfo
{
    public string TerminalId { get; set; } = string.Empty;

    public string Shell { get; set; } = string.Empty;

    public string Cwd { get; set; } = string.Empty;

    public string Status { get; set; } = TerminalStates.Running;

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public int? ExitCode { get; set; }

    public int? Pid { get; set; }
}

