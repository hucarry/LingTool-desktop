namespace ToolHub.App;

internal static class AppErrorMessages
{
    internal const string FailedToHandleMessage = "Failed to handle message.";
    internal const string FailedToExportDiagnosticBundle = "Failed to export diagnostic bundle.";

    internal static string FrontendNotFound(string indexPath) =>
        $"Frontend not found: {indexPath}. Run `cd frontend && npm install && npm run build` first.";
}
