using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal static class AppMessageHandlers
{
    public static void Register(IDictionary<string, MessageHandler> handlers)
    {
        handlers[BridgeMessageTypes.GetTools] = (context, _) =>
        {
            context.SendMessage(new ToolsMessage(context.Registry.GetTools()));
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
    }
}
