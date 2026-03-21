namespace ToolHub.App.Models;

public class IncomingMessage
{
    public string Type { get; set; } = string.Empty;
}

public sealed class RunToolRequest : IncomingMessage
{
    public string ToolId { get; set; } = string.Empty;

    public Dictionary<string, string?>? Args { get; set; }

    public string? RuntimePath { get; set; }

    public string? Python { get; set; }
}

public sealed class AddToolRequest : IncomingMessage
{
    public ToolDefinition? Tool { get; set; }
}

public sealed class UpdateToolRequest : IncomingMessage
{
    public ToolDefinition? Tool { get; set; }
}

public sealed class DeleteToolsRequest : IncomingMessage
{
    public List<string>? ToolIds { get; set; }
}

public sealed class RunToolInTerminalRequest : IncomingMessage
{
    public string ToolId { get; set; } = string.Empty;

    public Dictionary<string, string?>? Args { get; set; }

    public string? RuntimePath { get; set; }

    public string? Python { get; set; }

    public string? TerminalId { get; set; }
}

public sealed class OpenUrlToolRequest : IncomingMessage
{
    public string ToolId { get; set; } = string.Empty;
}

public sealed class StopRunRequest : IncomingMessage
{
    public string RunId { get; set; } = string.Empty;
}

public sealed class BrowsePythonRequest : IncomingMessage
{
    public string? DefaultPath { get; set; }

    public string? Purpose { get; set; }
}

public sealed class BrowseFileRequest : IncomingMessage
{
    public string? DefaultPath { get; set; }

    public string? Filter { get; set; }

    public string? Purpose { get; set; }
}

public sealed class GetPythonPackagesRequest : IncomingMessage
{
    public string? PythonPath { get; set; }
}

public sealed class InstallPythonPackageRequest : IncomingMessage
{
    public string? PythonPath { get; set; }

    public string PackageName { get; set; } = string.Empty;
}

public sealed class UninstallPythonPackageRequest : IncomingMessage
{
    public string? PythonPath { get; set; }

    public string PackageName { get; set; } = string.Empty;
}

public sealed class StartTerminalRequest : IncomingMessage
{
    public string? Title { get; set; }

    public string? Shell { get; set; }

    public string? Cwd { get; set; }

    public string? ToolType { get; set; }

    public string? RuntimePath { get; set; }
}

public sealed class TerminalInputRequest : IncomingMessage
{
    public string TerminalId { get; set; } = string.Empty;

    public string Data { get; set; } = string.Empty;
}

public sealed class TerminalResizeRequest : IncomingMessage
{
    public string TerminalId { get; set; } = string.Empty;

    public int Cols { get; set; }

    public int Rows { get; set; }
}

public sealed class StopTerminalRequest : IncomingMessage
{
    public string TerminalId { get; set; } = string.Empty;
}

public sealed class GetAppDefaultsRequest : IncomingMessage
{
}

public sealed class ExportDiagnosticBundleRequest : IncomingMessage
{
    public string? OutputDirectory { get; set; }
}
