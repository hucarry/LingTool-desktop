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
