using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Photino.NET;
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

        using var serviceProvider = AppServiceRegistration.BuildServiceProvider(
            appRoot,
            toolsFilePath,
            jsonOptions,
            SendMessage
        );

        var messageRouter = serviceProvider.GetRequiredService<IMessageRouter>();
        var shutdownCoordinator = serviceProvider.GetRequiredService<AppShutdownCoordinator>();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdownCoordinator.Shutdown();

        var indexPath = AppWindowFactory.ResolveFrontendIndexPath(appRoot);
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException(AppErrorMessages.FrontendNotFound(indexPath));
        }

        window = AppWindowFactory.CreateMainWindow(
            messageRouter,
            jsonOptions,
            shutdownCoordinator,
            serviceProvider.GetRequiredService<IFileDialogService>(),
            SendMessage
        );

        window.Load(indexPath);
        window.WaitForClose();
        shutdownCoordinator.Shutdown();
        Environment.Exit(0);
    }
}
