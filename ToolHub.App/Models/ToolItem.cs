namespace ToolHub.App.Models;

public sealed class ToolItem
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? RuntimePath { get; set; }

    // Legacy field for older tools.json entries and clients.
    public string? Python { get; set; }

    public string? Cwd { get; set; }

    public string ArgsTemplate { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public string? Description { get; set; }

    public bool PathExists { get; set; }

    public bool Valid { get; set; }

    public string? ValidationMessage { get; set; }
}
