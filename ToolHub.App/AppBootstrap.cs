using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.NET;
using Serilog;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal static class AppBootstrap
{
    internal static void Run(JsonSerializerOptions jsonOptions, Action<Exception> reportStartupFailure)
    {
        Log.Logger = AppLogging.CreateLogger();

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ConsoleEncodingConfigurator.Configure();

            Log.Information(
                "Starting ToolHub desktop host. BaseDirectory={BaseDirectory} CurrentDirectory={CurrentDirectory} LogDirectory={LogDirectory}",
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory(),
                AppLogging.ResolveLogDirectory()
            );

            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                if (eventArgs.ExceptionObject is Exception exception)
                {
                    Log.Fatal(exception, "Unhandled application exception.");
                    reportStartupFailure(exception);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            {
                Log.Error(eventArgs.Exception, "Unobserved task exception.");
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
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(AppBootstrap).FullName ?? nameof(AppBootstrap));

            logger.LogInformation(
                "Service provider initialized. AppRoot={AppRoot} ToolsFilePath={ToolsFilePath}",
                appRoot,
                toolsFilePath
            );

            var messageRouter = serviceProvider.GetRequiredService<IMessageRouter>();
            var shutdownCoordinator = serviceProvider.GetRequiredService<AppShutdownCoordinator>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                Log.Information("Process exit detected. Triggering coordinated shutdown.");
                shutdownCoordinator.Shutdown();
            };

            var indexPath = AppWindowFactory.ResolveFrontendIndexPath(appRoot);
            if (!File.Exists(indexPath))
            {
                throw new FileNotFoundException(AppErrorMessages.FrontendNotFound(indexPath));
            }

            logger.LogInformation("Resolved frontend entrypoint to {IndexPath}", indexPath);

            window = AppWindowFactory.CreateMainWindow(
                messageRouter,
                jsonOptions,
                shutdownCoordinator,
                serviceProvider.GetRequiredService<IFileDialogService>(),
                SendMessage,
                loggerFactory
            );

            logger.LogInformation("Main window created. Loading frontend.");

            window.Load(indexPath);
            window.WaitForClose();
            logger.LogInformation("Main window closed. Shutting down host.");
            shutdownCoordinator.Shutdown();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application startup failed.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
