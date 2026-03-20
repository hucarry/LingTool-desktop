namespace ToolHub.App.Models;

public sealed class ToolDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? RuntimePath { get; set; }

    // Legacy field for backwards-compatible tools.json reads.
    public string? Python { get; set; }

    public string? Cwd { get; set; }

    public string ArgsTemplate { get; set; } = string.Empty;

    public ArgsSpecV1? ArgsSpec { get; set; }

    public List<string> Tags { get; set; } = new();

    public string? Description { get; set; }
}
