using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddSingleton(jsonOptions);
        services.AddSingleton(sendMessage);
        services.AddSingleton<IToolRegistry>(_ => new ToolRegistry(toolsFilePath, jsonOptions));
        services.AddSingleton<IPythonPackageService>(_ => new PythonPackageManager(appRoot));
        services.AddSingleton<IFileDialogService>(_ => new AppDialogPicker(appRoot));
        services.AddSingleton<IProcessManager>(provider =>
            new ProcessManager(provider.GetRequiredService<Action<object>>())
        );
        services.AddSingleton<ITerminalManager>(provider =>
            new TerminalManager(provider.GetRequiredService<Action<object>>())
        );
        services.AddSingleton<ToolExecutionSupport>();
        services.AddSingleton<IMessageRouteRegistrar, AppMessageHandlers>();
        services.AddSingleton<IMessageRouteRegistrar, ToolCatalogMessageHandlers>();
        services.AddSingleton<IMessageRouteRegistrar, ToolExecutionMessageHandlers>();
        services.AddSingleton<IMessageRouteRegistrar, PythonMessageHandlers>();
        services.AddSingleton<IMessageRouteRegistrar, TerminalMessageHandlers>();
        services.AddSingleton<IMessageRouter>(provider =>
            new MessageRouter(provider.GetServices<IMessageRouteRegistrar>())
        );
        services.AddSingleton<AppShutdownCoordinator>();

        return services.BuildServiceProvider();
    }
}
