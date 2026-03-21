namespace ToolHub.App;

internal static class BridgeErrorMessages
{
    internal const string MessageMissingTypeField = "Message is missing type field.";
    internal const string AddToolMissingPayload = "addTool request is missing tool payload.";
    internal const string UpdateToolMissingPayload = "updateTool request is missing tool payload.";
    internal const string OpenUrlToolMissingToolId = "openUrlTool request is missing toolId.";
    internal const string RunToolMissingToolId = "runTool request is missing toolId.";
    internal const string RunToolInTerminalMissingToolId = "runToolInTerminal request is missing toolId.";
    internal const string StopRunMissingRunId = "stopRun request is missing runId.";
    internal const string TerminalInputMissingTerminalId = "terminalInput request is missing terminalId.";
    internal const string TerminalResizeMissingTerminalId = "terminalResize request is missing terminalId.";
    internal const string StopTerminalMissingTerminalId = "stopTerminal request is missing terminalId.";
    internal const string InstallPythonPackageMissingPackageName = "installPythonPackage request is missing packageName.";
    internal const string UninstallPythonPackageMissingPackageName = "uninstallPythonPackage request is missing packageName.";

    internal static string UnsupportedMessageType(string type) => $"Unsupported message type: {type}";
}
