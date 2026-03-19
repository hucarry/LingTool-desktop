namespace ToolHub.App.Models;

public static class BridgeMessageTypes
{
    public const string GetTools = "getTools";
    public const string AddTool = "addTool";
    public const string UpdateTool = "updateTool";
    public const string DeleteTools = "deleteTools";
    public const string RunTool = "runTool";
    public const string RunToolInTerminal = "runToolInTerminal";
    public const string OpenUrlTool = "openUrlTool";
    public const string StopRun = "stopRun";
    public const string GetRuns = "getRuns";
    public const string BrowsePython = "browsePython";
    public const string BrowseFile = "browseFile";
    public const string GetPythonPackages = "getPythonPackages";
    public const string InstallPythonPackage = "installPythonPackage";
    public const string UninstallPythonPackage = "uninstallPythonPackage";
    public const string GetTerminals = "getTerminals";
    public const string StartTerminal = "startTerminal";
    public const string TerminalInput = "terminalInput";
    public const string TerminalResize = "terminalResize";
    public const string StopTerminal = "stopTerminal";
    public const string GetAppDefaults = "getAppDefaults";

    public const string Tools = "tools";
    public const string RunStarted = "runStarted";
    public const string Log = "log";
    public const string RunStatus = "runStatus";
    public const string Runs = "runs";
    public const string PythonSelected = "pythonSelected";
    public const string FileSelected = "fileSelected";
    public const string ToolAdded = "toolAdded";
    public const string ToolUpdated = "toolUpdated";
    public const string ToolsDeleted = "toolsDeleted";
    public const string PythonPackages = "pythonPackages";
    public const string PythonPackageInstallStatus = "pythonPackageInstallStatus";
    public const string Terminals = "terminals";
    public const string TerminalStarted = "terminalStarted";
    public const string TerminalOutput = "terminalOutput";
    public const string TerminalStatus = "terminalStatus";
    public const string AppDefaults = "appDefaults";
    public const string Error = "error";
}

public static class LogChannels
{
    public const string Stdout = "stdout";
    public const string Stderr = "stderr";
}

public class IncomingMessage
{
    public string Type { get; set; } = string.Empty;
}

public sealed class RunToolRequest : IncomingMessage
{
    public string ToolId { get; set; } = string.Empty;

    public Dictionary<string, string?>? Args { get; set; }

    public string? RuntimePath { get; set; }

    // Legacy field from older frontend versions.
    public string? Python { get; set; }
}

public sealed class AddToolRequest : IncomingMessage
{
    public ToolItem? Tool { get; set; }
}

public sealed class UpdateToolRequest : IncomingMessage
{
    public ToolItem? Tool { get; set; }
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

    // Legacy field from older frontend versions.
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
    public string? Shell { get; set; }

    public string? Cwd { get; set; }
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

public sealed class ToolsMessage
{
    public ToolsMessage(IReadOnlyList<ToolItem> tools)
    {
        Tools = tools;
    }

    public string Type { get; } = BridgeMessageTypes.Tools;

    public IReadOnlyList<ToolItem> Tools { get; }
}

public sealed class RunStartedMessage
{
    public RunStartedMessage(RunInfo run)
    {
        Run = run;
    }

    public string Type { get; } = BridgeMessageTypes.RunStarted;

    public RunInfo Run { get; }
}

public sealed class RunStatusMessage
{
    public RunStatusMessage(RunInfo run)
    {
        Run = run;
    }

    public string Type { get; } = BridgeMessageTypes.RunStatus;

    public RunInfo Run { get; }
}

public sealed class LogMessage
{
    public LogMessage(string runId, string channel, string line, DateTimeOffset ts)
    {
        RunId = runId;
        Channel = channel;
        Line = line;
        Ts = ts;
    }

    public string Type { get; } = BridgeMessageTypes.Log;

    public string RunId { get; }

    public string Channel { get; }

    public string Line { get; }

    public DateTimeOffset Ts { get; }
}

public sealed class RunsMessage
{
    public RunsMessage(IReadOnlyList<RunInfo> runs)
    {
        Runs = runs;
    }

    public string Type { get; } = BridgeMessageTypes.Runs;

    public IReadOnlyList<RunInfo> Runs { get; }
}

public sealed class PythonSelectedMessage
{
    public PythonSelectedMessage(string? path, string? purpose = null)
    {
        Path = path;
        Purpose = purpose;
    }

    public string Type { get; } = BridgeMessageTypes.PythonSelected;

    public string? Path { get; }

    public string? Purpose { get; }
}

public sealed class FileSelectedMessage
{
    public FileSelectedMessage(string? path, string? purpose = null)
    {
        Path = path;
        Purpose = purpose;
    }

    public string Type { get; } = BridgeMessageTypes.FileSelected;

    public string? Path { get; }

    public string? Purpose { get; }
}

public sealed class ToolAddedMessage
{
    public ToolAddedMessage(string toolId)
    {
        ToolId = toolId;
    }

    public string Type { get; } = BridgeMessageTypes.ToolAdded;

    public string ToolId { get; }
}

public sealed class ToolUpdatedMessage
{
    public ToolUpdatedMessage(string toolId)
    {
        ToolId = toolId;
    }

    public string Type { get; } = BridgeMessageTypes.ToolUpdated;

    public string ToolId { get; }
}

public sealed class ToolsDeletedMessage
{
    public ToolsDeletedMessage(int deletedCount)
    {
        DeletedCount = deletedCount;
    }

    public string Type { get; } = BridgeMessageTypes.ToolsDeleted;

    public int DeletedCount { get; }
}

public sealed class PythonPackagesMessage
{
    public PythonPackagesMessage(string pythonPath, IReadOnlyList<PythonPackageItem> packages)
    {
        PythonPath = pythonPath;
        Packages = packages;
    }

    public string Type { get; } = BridgeMessageTypes.PythonPackages;

    public string PythonPath { get; }

    public IReadOnlyList<PythonPackageItem> Packages { get; }
}

public sealed class PythonPackageInstallStatusMessage
{
    public PythonPackageInstallStatusMessage(
        string packageName,
        string action,
        string status,
        string pythonPath,
        string? message = null
    )
    {
        PackageName = packageName;
        Action = action;
        Status = status;
        Message = message;
        PythonPath = pythonPath;
    }

    public string Type { get; } = BridgeMessageTypes.PythonPackageInstallStatus;

    public string PackageName { get; }

    public string Action { get; }

    public string Status { get; }

    public string PythonPath { get; }

    public string? Message { get; }
}

public sealed class TerminalsMessage
{
    public TerminalsMessage(IReadOnlyList<TerminalInfo> terminals)
    {
        Terminals = terminals;
    }

    public string Type { get; } = BridgeMessageTypes.Terminals;

    public IReadOnlyList<TerminalInfo> Terminals { get; }
}

public sealed class TerminalStartedMessage
{
    public TerminalStartedMessage(TerminalInfo terminal)
    {
        Terminal = terminal;
    }

    public string Type { get; } = BridgeMessageTypes.TerminalStarted;

    public TerminalInfo Terminal { get; }
}

public sealed class TerminalOutputMessage
{
    public TerminalOutputMessage(string terminalId, string data, string channel, DateTimeOffset ts)
    {
        TerminalId = terminalId;
        Data = data;
        Channel = channel;
        Ts = ts;
    }

    public string Type { get; } = BridgeMessageTypes.TerminalOutput;

    public string TerminalId { get; }

    public string Data { get; }

    public string Channel { get; }

    public DateTimeOffset Ts { get; }
}

public sealed class TerminalStatusMessage
{
    public TerminalStatusMessage(TerminalInfo terminal)
    {
        Terminal = terminal;
    }

    public string Type { get; } = BridgeMessageTypes.TerminalStatus;

    public TerminalInfo Terminal { get; }
}

public sealed class AppDefaultsMessage
{
    public AppDefaultsMessage(string? pythonPath)
    {
        PythonPath = pythonPath;
    }

    public string Type { get; } = BridgeMessageTypes.AppDefaults;

    public string? PythonPath { get; }
}

public sealed class ErrorMessage
{
    public ErrorMessage(string message, object? details = null)
    {
        Message = message;
        Details = details;
    }

    public string Type { get; } = BridgeMessageTypes.Error;

    public string Message { get; }

    public object? Details { get; }
}
