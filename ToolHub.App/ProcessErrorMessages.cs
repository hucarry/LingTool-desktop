namespace ToolHub.App;

internal static class ProcessErrorMessages
{
    internal const string FailedToStopRun = "Failed to stop run.";

    internal static string RunContextAlreadyExists(string runId) => $"Run context already exists: {runId}";

    internal static string FailedToStartTool(string toolName) => $"Failed to start tool: {toolName}";

    internal static string RunNotFoundOrAlreadyFinished(string runId) => $"Run not found or already finished: {runId}";
}
