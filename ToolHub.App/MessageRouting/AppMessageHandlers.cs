using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal sealed class AppMessageHandlers(
    IToolRegistry registry,
    DiagnosticBundleService diagnosticBundleService) : IMessageRouteRegistrar
{
    public void Register(IDictionary<string, MessageHandler> handlers)
    {
        handlers[BridgeMessageTypes.GetTools] = (context, _) =>
        {
            context.SendMessage(new ToolsMessage(registry.GetTools()));
        };

        handlers[BridgeMessageTypes.GetAppDefaults] = (context, _) =>
        {
            var appRootPath = PathUtils.ResolveProjectRoot();
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            context.SendMessage(new AppDefaultsMessage(
                PythonInterpreterProbe.ResolveBundled(),
                appRootPath,
                string.IsNullOrWhiteSpace(desktopPath) ? null : desktopPath
            ));
        };
        handlers[BridgeMessageTypes.ExportDiagnosticBundle] = HandleExportDiagnosticBundle;
    }

    private void HandleExportDiagnosticBundle(MessageContext context, string rawMessage)
    {
        var request = System.Text.Json.JsonSerializer.Deserialize<ExportDiagnosticBundleRequest>(rawMessage, context.JsonOptions);

        _ = Task.Run(() =>
        {
            try
            {
                var result = diagnosticBundleService.Export(request?.OutputDirectory);
                context.SendMessage(new DiagnosticBundleExportedMessage(
                    result.BundlePath,
                    result.EntryCount,
                    result.ExportedAt
                ));
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage(AppErrorMessages.FailedToExportDiagnosticBundle, ex.Message));
            }
        });
    }
}
