using System.Text;
using System.Text.Json;
using Photino.NET;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal static class AppBootstrap
{
    internal static void Run(JsonSerializerOptions jsonOptions, Action<Exception> reportStartupFailure)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ConsoleEncodingConfigurator.Configure();

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                reportStartupFailure(exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            reportStartupFailure(eventArgs.Exception);
            eventArgs.SetObserved();
        };

        var appRoot = PathUtils.ResolveProjectRoot();
        var toolsFilePath = Path.Combine(appRoot, "tools.json");
        var registry = new ToolRegistry(toolsFilePath, jsonOptions);
        var pythonPackageManager = new PythonPackageManager(appRoot);
        var dialogPicker = new AppDialogPicker(appRoot);

        PhotinoWindow? window = null;
        var sendLock = new object();

        void SendMessage(object payload)
        {
            lock (sendLock)
            {
                if (window is null)
                {
                    return;
                }

                var json = JsonSerializer.Serialize(payload, jsonOptions);
                window.SendWebMessage(json);
            }
        }

        var processManager = new ProcessManager(SendMessage);
        var terminalManager = new TerminalManager(SendMessage);
        var shutdownCoordinator = new AppShutdownCoordinator(terminalManager, processManager);

        AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdownCoordinator.Shutdown();

        var indexPath = ResolveFrontendIndexPath(appRoot);
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException(
                $"Frontend not found: {indexPath}. Run `cd frontend && npm install && npm run build` first."
            );
        }

        window = CreateWindow(
            appRoot,
            registry,
            processManager,
            pythonPackageManager,
            terminalManager,
            jsonOptions,
            shutdownCoordinator,
            dialogPicker,
            SendMessage
        );

        window.Load(indexPath);
        window.WaitForClose();
        shutdownCoordinator.Shutdown();
        Environment.Exit(0);
    }

    private static PhotinoWindow CreateWindow(
        string appRoot,
        ToolRegistry registry,
        ProcessManager processManager,
        PythonPackageManager pythonPackageManager,
        TerminalManager terminalManager,
        JsonSerializerOptions jsonOptions,
        AppShutdownCoordinator shutdownCoordinator,
        AppDialogPicker dialogPicker,
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
                    registry,
                    processManager,
                    pythonPackageManager,
                    terminalManager,
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

    private static void HandleMessage(
        string rawMessage,
        ToolRegistry registry,
        ProcessManager processManager,
        PythonPackageManager pythonPackageManager,
        TerminalManager terminalManager,
        JsonSerializerOptions jsonOptions,
        Action<object> sendMessage,
        Func<string?, string?> browsePython,
        Func<string?, string?, string?, string?> browseFile
    )
    {
        try
        {
            var context = new MessageContext(
                registry,
                processManager,
                pythonPackageManager,
                terminalManager,
                sendMessage,
                browsePython,
                browseFile,
                jsonOptions
            );

            MessageRouter.Route(context, rawMessage);
        }
        catch (Exception ex)
        {
            sendMessage(new ErrorMessage("Failed to handle message.", ex.Message));
        }
    }

    private static string ResolveFrontendIndexPath(string appRoot)
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
}
