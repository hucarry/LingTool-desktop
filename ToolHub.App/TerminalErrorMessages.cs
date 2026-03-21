namespace ToolHub.App;

internal static class TerminalErrorMessages
{
    internal const string FailedToStartTerminal = "Failed to start terminal.";
    internal const string FailedToRunToolInTerminal = "Failed to run tool in terminal.";
    internal const string FailedToWriteTerminalInput = "Failed to write terminal input.";
    internal const string FailedToStopTerminal = "Failed to stop terminal.";

    internal static string TerminalAlreadyExists(string terminalId) => $"Terminal already exists: {terminalId}";

    internal static string FailedToStartTerminalException(string details) => $"Failed to start terminal: {details}";

    internal static string TerminalNotFound(string terminalId) => $"Terminal not found: {terminalId}";

    internal static string TerminalNotFoundOrNotRunning(string terminalId) => $"Terminal not found or not running: {terminalId}";
}
