using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ToolHub.App;

internal static class AppServiceRegistration
{
    internal static ServiceProvider BuildServiceProvider(
        string appRoot,
        string toolsFilePath,
        JsonSerializerOptions jsonOptions,
        Action<object> sendMessage
    )
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
        });
        services.AddSingleton(jsonOptions);
        services.AddSingleton(sendMessage);
        services.AddSingleton<IToolRegistry>(_ => new ToolRegistry(toolsFilePath, jsonOptions));
        services.AddSingleton(provider =>
            new DiagnosticBundleService(
                appRoot,
                toolsFilePath,
                provider.GetRequiredService<ILogger<DiagnosticBundleService>>()
            )
        );
        services.AddSingleton<IPythonPackageService>(provider =>
            new PythonPackageManager(appRoot, provider.GetRequiredService<ILogger<PythonPackageManager>>())
        );
        services.AddSingleton<IFileDialogService>(_ => new AppDialogPicker(appRoot));
        services.AddSingleton<IProcessManager>(provider =>
            new ProcessManager(
                provider.GetRequiredService<Action<object>>(),
                provider.GetRequiredService<ILogger<ProcessManager>>()
            )
        );
        services.AddSingleton<ITerminalManager>(provider =>
            new TerminalManager(
                provider.GetRequiredService<Action<object>>(),
                provider.GetRequiredService<ILogger<TerminalManager>>()
            )
        );
        services.AddSingleton<ToolExecutionSupport>();
        services.AddSingleton<IMessageRouteRegistrar, AppMessageHandlers>();
        services.AddSingleton<IMessageRouteRegistrar, ToolCatalogMessageHandlers>();
        services.AddSingleton<IMessageRouteRegistrar, ToolExecutionMessageHandlers>();
        services.AddSingleton<IMessageRouteRegistrar, PythonMessageHandlers>();
        services.AddSingleton<IMessageRouteRegistrar, TerminalMessageHandlers>();
        services.AddSingleton<IMessageRouter>(provider =>
            new MessageRouter(
                provider.GetServices<IMessageRouteRegistrar>(),
                provider.GetRequiredService<ILogger<MessageRouter>>()
            )
        );
        services.AddSingleton<AppShutdownCoordinator>();

        return services.BuildServiceProvider();
    }
}
