using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

internal static class PythonMessageHandlers
{
    public static void Register(IDictionary<string, MessageHandler> handlers)
    {
        handlers[BridgeMessageTypes.BrowsePython] = HandleBrowsePython;
        handlers[BridgeMessageTypes.BrowseFile] = HandleBrowseFile;
        handlers[BridgeMessageTypes.GetPythonPackages] = HandleGetPythonPackages;
        handlers[BridgeMessageTypes.InstallPythonPackage] = HandleInstallPythonPackage;
        handlers[BridgeMessageTypes.UninstallPythonPackage] = HandleUninstallPythonPackage;
    }

    private static void HandleBrowsePython(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<BrowsePythonRequest>(rawMessage, context.JsonOptions);
        var selectedPath = context.BrowsePython(request?.DefaultPath);
        context.SendMessage(new PythonSelectedMessage(selectedPath, request?.Purpose));
    }

    private static void HandleBrowseFile(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<BrowseFileRequest>(rawMessage, context.JsonOptions);
        var selectedPath = context.BrowseFile(request?.DefaultPath, request?.Filter, request?.Purpose);
        context.SendMessage(new FileSelectedMessage(selectedPath, request?.Purpose));
    }

    private static void HandleGetPythonPackages(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<GetPythonPackagesRequest>(rawMessage, context.JsonOptions);
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await context.PythonPackageManager.GetInstalledPackagesAsync(request?.PythonPath);
                context.SendMessage(new PythonPackagesMessage(result.PythonPath, result.Packages));
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage("Failed to read installed Python packages.", ex.Message));
            }
        });
    }

    private static void HandleInstallPythonPackage(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<InstallPythonPackageRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.PackageName))
        {
            context.SendMessage(new ErrorMessage("installPythonPackage request is missing packageName."));
            return;
        }

        _ = Task.Run(async () =>
        {
            var pythonForStatus = request.PythonPath ?? "python";

            try
            {
                context.SendMessage(new PythonPackageInstallStatusMessage(
                    request.PackageName,
                    PythonPackageActions.Install,
                    PythonPackageInstallStates.Running,
                    pythonForStatus
                ));

                var result = await context.PythonPackageManager.InstallPackageAsync(
                    request.PythonPath,
                    request.PackageName
                );

                context.SendMessage(new PythonPackageInstallStatusMessage(
                    result.PackageName,
                    PythonPackageActions.Install,
                    result.Success ? PythonPackageInstallStates.Succeeded : PythonPackageInstallStates.Failed,
                    result.PythonPath,
                    result.Message
                ));

                if (result.Success)
                {
                    var packageResult = await context.PythonPackageManager.GetInstalledPackagesAsync(result.PythonPath);
                    context.SendMessage(new PythonPackagesMessage(packageResult.PythonPath, packageResult.Packages));
                }
            }
            catch (Exception ex)
            {
                context.SendMessage(new PythonPackageInstallStatusMessage(
                    request.PackageName,
                    PythonPackageActions.Install,
                    PythonPackageInstallStates.Failed,
                    pythonForStatus,
                    ex.Message
                ));
            }
        });
    }

    private static void HandleUninstallPythonPackage(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<UninstallPythonPackageRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.PackageName))
        {
            context.SendMessage(new ErrorMessage("uninstallPythonPackage request is missing packageName."));
            return;
        }

        _ = Task.Run(async () =>
        {
            var pythonForStatus = request.PythonPath ?? "python";

            try
            {
                context.SendMessage(new PythonPackageInstallStatusMessage(
                    request.PackageName,
                    PythonPackageActions.Uninstall,
                    PythonPackageInstallStates.Running,
                    pythonForStatus
                ));

                var result = await context.PythonPackageManager.UninstallPackageAsync(
                    request.PythonPath,
                    request.PackageName
                );

                context.SendMessage(new PythonPackageInstallStatusMessage(
                    result.PackageName,
                    PythonPackageActions.Uninstall,
                    result.Success ? PythonPackageInstallStates.Succeeded : PythonPackageInstallStates.Failed,
                    result.PythonPath,
                    result.Message
                ));

                if (result.Success)
                {
                    var packageResult = await context.PythonPackageManager.GetInstalledPackagesAsync(result.PythonPath);
                    context.SendMessage(new PythonPackagesMessage(packageResult.PythonPath, packageResult.Packages));
                }
            }
            catch (Exception ex)
            {
                context.SendMessage(new PythonPackageInstallStatusMessage(
                    request.PackageName,
                    PythonPackageActions.Uninstall,
                    PythonPackageInstallStates.Failed,
                    pythonForStatus,
                    ex.Message
                ));
            }
        });
    }
}
