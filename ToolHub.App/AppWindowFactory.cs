using System.Text.Json;
using Photino.NET;
using ToolHub.App.Models;

namespace ToolHub.App;

internal static class AppWindowFactory
{
    internal static PhotinoWindow CreateMainWindow(
        IMessageRouter messageRouter,
        JsonSerializerOptions jsonOptions,
        AppShutdownCoordinator shutdownCoordinator,
        IFileDialogService dialogPicker,
        Action<object> sendMessage
    )
    {
        PhotinoWindow? window = null;
        window = new PhotinoWindow().SetTitle("ToolHub Local Tool Platform")
            .SetUseOsDefaultLocation(true)
            .SetSize(1380, 900)
            .Center()
            .RegisterWindowClosingHandler((_, _) =>
            {
                shutdownCoordinator.Shutdown();
                return false;
            })
            .RegisterWebMessageReceivedHandler((_, rawMessage) =>
            {
                HandleMessage(
                    rawMessage,
                    messageRouter,
                    jsonOptions,
                    sendMessage,
                    defaultPath => window is null ? null : dialogPicker.ShowPythonPicker(window, defaultPath),
                    (defaultPath, filter, purpose) => window is null ? null : dialogPicker.ShowFilePicker(
                        window,
                        "Select File",
                        defaultPath,
                        filter,
                        purpose
                    )
                );
            });

        return window;
    }

    internal static string ResolveFrontendIndexPath(string appRoot)
    {
        var fromOutputFolder = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(fromOutputFolder))
        {
            return fromOutputFolder;
        }

        var fromProjectFolder = Path.Combine(appRoot, "ToolHub.App", "wwwroot", "index.html");
        if (File.Exists(fromProjectFolder))
        {
            return fromProjectFolder;
        }

        return fromProjectFolder;
    }

    private static void HandleMessage(
        string rawMessage,
        IMessageRouter messageRouter,
        JsonSerializerOptions jsonOptions,
        Action<object> sendMessage,
        Func<string?, string?> browsePython,
        Func<string?, string?, string?, string?> browseFile
    )
    {
        try
        {
            var context = new MessageContext(
                sendMessage,
                browsePython,
                browseFile,
                jsonOptions
            );

            messageRouter.Dispatch(context, rawMessage);
        }
        catch (Exception ex)
        {
            sendMessage(new ErrorMessage(AppErrorMessages.FailedToHandleMessage, ex.Message));
        }
    }
}
