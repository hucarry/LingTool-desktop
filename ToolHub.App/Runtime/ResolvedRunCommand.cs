namespace ToolHub.App.Runtime;

public sealed class ResolvedRunCommand
{
    public string ToolType { get; init; } = string.Empty;

    public string CommandPath { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    public string? WorkingDirectoryOverride { get; init; }

    public string? RuntimePath { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}
