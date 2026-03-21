namespace ToolHub.App.Models;

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
        string? message = null)
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
    public AppDefaultsMessage(string? pythonPath, string? appRootPath, string? desktopPath)
    {
        PythonPath = pythonPath;
        AppRootPath = appRootPath;
        DesktopPath = desktopPath;
    }

    public string Type { get; } = BridgeMessageTypes.AppDefaults;

    public string? PythonPath { get; }

    public string? AppRootPath { get; }

    public string? DesktopPath { get; }
}

public sealed class DiagnosticBundleExportedMessage
{
    public DiagnosticBundleExportedMessage(string bundlePath, int entryCount, DateTimeOffset exportedAt)
    {
        BundlePath = bundlePath;
        EntryCount = entryCount;
        ExportedAt = exportedAt;
    }

    public string Type { get; } = BridgeMessageTypes.DiagnosticBundleExported;

    public string BundlePath { get; }

    public int EntryCount { get; }

    public DateTimeOffset ExportedAt { get; }
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
