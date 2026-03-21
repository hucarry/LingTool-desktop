namespace ToolHub.App;

internal static class AppErrorMessages
{
    internal const string FailedToHandleMessage = "Failed to handle message.";

    internal static string FrontendNotFound(string indexPath) =>
        $"Frontend not found: {indexPath}. Run `cd frontend && npm install && npm run build` first.";
}
