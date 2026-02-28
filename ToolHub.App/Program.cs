using System.Text.Json;
using System.Text.Json.Serialization;
using Photino.NET;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [STAThread]
    private static void Main(string[] args)
    {
        var appRoot = PathUtils.ResolveProjectRoot();
        var toolsFilePath = Path.Combine(appRoot, "tools.json");
        var registry = new ToolRegistry(toolsFilePath, JsonOptions);
        var pythonPackageManager = new PythonPackageManager(appRoot);

        PhotinoWindow? window = null;

        void SendMessage(object payload)
        {
            if (window is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            window.SendWebMessage(json);
        }

        var processManager = new ProcessManager(SendMessage);
        var terminalManager = new TerminalManager(SendMessage);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            processManager.Dispose();
            terminalManager.Dispose();
        };

        var indexPath = ResolveFrontendIndexPath(appRoot);
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException(
                $"Frontend not found: {indexPath}. Run `cd frontend && npm install && npm run build` first."
            );
        }

        window = new PhotinoWindow().SetTitle("ToolHub Local Tool Platform")
            .SetUseOsDefaultLocation(true)
            .SetSize(1380, 900)
            .Center()
            .RegisterWebMessageReceivedHandler((sender, rawMessage) =>
            {
                HandleMessage(
                    rawMessage,
                    registry,
                    processManager,
                    pythonPackageManager,
                    terminalManager,
                    SendMessage,
                    defaultPath => window is null ? null : ShowPythonPicker(window, defaultPath)
                );
            });

        window.Load(indexPath);
        window.WaitForClose();
    }

    private static void HandleMessage(
        string rawMessage,
        ToolRegistry registry,
        ProcessManager processManager,
        PythonPackageManager pythonPackageManager,
        TerminalManager terminalManager,
        Action<object> sendMessage,
        Func<string?, string?> browsePython
    )
    {
        try
        {
            var baseMessage = JsonSerializer.Deserialize<IncomingMessage>(rawMessage, JsonOptions);
            if (baseMessage?.Type is null)
            {
                sendMessage(new ErrorMessage("Message is missing type field."));
                return;
            }

            switch (baseMessage.Type)
            {
                case BridgeMessageTypes.GetTools:
                    sendMessage(new ToolsMessage(registry.GetTools()));
                    break;
                case BridgeMessageTypes.GetRuns:
                    sendMessage(new RunsMessage(processManager.GetRuns()));
                    break;
                case BridgeMessageTypes.RunTool:
                {
                    var runRequest = JsonSerializer.Deserialize<RunToolRequest>(rawMessage, JsonOptions);
                    if (runRequest is null || string.IsNullOrWhiteSpace(runRequest.ToolId))
                    {
                        sendMessage(new ErrorMessage("runTool request is missing toolId."));
                        return;
                    }

                    var tool = registry.GetToolById(runRequest.ToolId);
                    if (tool is null)
                    {
                        sendMessage(new ErrorMessage($"Tool not found: {runRequest.ToolId}"));
                        return;
                    }

                    if (!tool.Valid)
                    {
                        sendMessage(new ErrorMessage($"Tool is invalid: {tool.Name}", tool.ValidationMessage));
                        return;
                    }

                    var pythonOverride = ResolvePythonOverride(runRequest.Python, tool);
                    if (
                        string.Equals(tool.Type, "python", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(pythonOverride)
                        && !File.Exists(pythonOverride)
                    )
                    {
                        sendMessage(new ErrorMessage($"Python interpreter not found: {pythonOverride}"));
                        return;
                    }

                    processManager.StartRun(
                        tool,
                        runRequest.Args ?? new Dictionary<string, string?>(),
                        pythonOverride
                    );
                    break;
                }
                case BridgeMessageTypes.RunToolInTerminal:
                {
                    var request = JsonSerializer.Deserialize<RunToolInTerminalRequest>(rawMessage, JsonOptions);
                    if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
                    {
                        sendMessage(new ErrorMessage("runToolInTerminal request is missing toolId."));
                        return;
                    }

                    var tool = registry.GetToolById(request.ToolId);
                    if (tool is null)
                    {
                        sendMessage(new ErrorMessage($"Tool not found: {request.ToolId}"));
                        return;
                    }

                    if (!tool.Valid)
                    {
                        sendMessage(new ErrorMessage($"Tool is invalid: {tool.Name}", tool.ValidationMessage));
                        return;
                    }

                    var pythonOverride = ResolvePythonOverride(request.Python, tool);
                    if (
                        string.Equals(tool.Type, "python", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(pythonOverride)
                        && !File.Exists(pythonOverride)
                    )
                    {
                        sendMessage(new ErrorMessage($"Python interpreter not found: {pythonOverride}"));
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await terminalManager.RunToolInTerminalAsync(
                                request.TerminalId,
                                tool,
                                request.Args ?? new Dictionary<string, string?>(),
                                pythonOverride
                            );
                        }
                        catch (Exception ex)
                        {
                            sendMessage(new ErrorMessage("Failed to run tool in terminal.", ex.Message));
                        }
                    });
                    break;
                }
                case BridgeMessageTypes.StopRun:
                {
                    var stopRequest = JsonSerializer.Deserialize<StopRunRequest>(rawMessage, JsonOptions);
                    if (stopRequest is null || string.IsNullOrWhiteSpace(stopRequest.RunId))
                    {
                        sendMessage(new ErrorMessage("stopRun request is missing runId."));
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var stopped = await processManager.StopRunAsync(stopRequest.RunId);
                            if (!stopped)
                            {
                                sendMessage(new ErrorMessage($"Run not found or already finished: {stopRequest.RunId}"));
                            }
                        }
                        catch (Exception ex)
                        {
                            sendMessage(new ErrorMessage("Failed to stop run.", ex.Message));
                        }
                    });
                    break;
                }
                case BridgeMessageTypes.BrowsePython:
                {
                    var browseRequest = JsonSerializer.Deserialize<BrowsePythonRequest>(rawMessage, JsonOptions);
                    var selectedPath = browsePython(browseRequest?.DefaultPath);
                    sendMessage(new PythonSelectedMessage(selectedPath, browseRequest?.Purpose));
                    break;
                }
                case BridgeMessageTypes.GetPythonPackages:
                {
                    var packagesRequest = JsonSerializer.Deserialize<GetPythonPackagesRequest>(rawMessage, JsonOptions);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await pythonPackageManager.GetInstalledPackagesAsync(packagesRequest?.PythonPath);
                            sendMessage(new PythonPackagesMessage(result.PythonPath, result.Packages));
                        }
                        catch (Exception ex)
                        {
                            sendMessage(new ErrorMessage("Failed to read installed Python packages.", ex.Message));
                        }
                    });
                    break;
                }
                case BridgeMessageTypes.InstallPythonPackage:
                {
                    var installRequest = JsonSerializer.Deserialize<InstallPythonPackageRequest>(rawMessage, JsonOptions);
                    if (installRequest is null || string.IsNullOrWhiteSpace(installRequest.PackageName))
                    {
                        sendMessage(new ErrorMessage("installPythonPackage request is missing packageName."));
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        string pythonForStatus = installRequest.PythonPath ?? "python";
                        try
                        {
                            sendMessage(new PythonPackageInstallStatusMessage(
                                installRequest.PackageName,
                                PythonPackageActions.Install,
                                PythonPackageInstallStates.Running,
                                pythonForStatus
                            ));

                            var result = await pythonPackageManager.InstallPackageAsync(
                                installRequest.PythonPath,
                                installRequest.PackageName
                            );

                            sendMessage(new PythonPackageInstallStatusMessage(
                                result.PackageName,
                                PythonPackageActions.Install,
                                result.Success ? PythonPackageInstallStates.Succeeded : PythonPackageInstallStates.Failed,
                                result.PythonPath,
                                result.Message
                            ));

                            if (result.Success)
                            {
                                var packageResult = await pythonPackageManager.GetInstalledPackagesAsync(result.PythonPath);
                                sendMessage(new PythonPackagesMessage(packageResult.PythonPath, packageResult.Packages));
                            }
                        }
                        catch (Exception ex)
                        {
                            sendMessage(new PythonPackageInstallStatusMessage(
                                installRequest.PackageName,
                                PythonPackageActions.Install,
                                PythonPackageInstallStates.Failed,
                                pythonForStatus,
                                ex.Message
                            ));
                        }
                    });
                    break;
                }
                case BridgeMessageTypes.UninstallPythonPackage:
                {
                    var uninstallRequest = JsonSerializer.Deserialize<UninstallPythonPackageRequest>(rawMessage, JsonOptions);
                    if (uninstallRequest is null || string.IsNullOrWhiteSpace(uninstallRequest.PackageName))
                    {
                        sendMessage(new ErrorMessage("uninstallPythonPackage request is missing packageName."));
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        string pythonForStatus = uninstallRequest.PythonPath ?? "python";
                        try
                        {
                            sendMessage(new PythonPackageInstallStatusMessage(
                                uninstallRequest.PackageName,
                                PythonPackageActions.Uninstall,
                                PythonPackageInstallStates.Running,
                                pythonForStatus
                            ));

                            var result = await pythonPackageManager.UninstallPackageAsync(
                                uninstallRequest.PythonPath,
                                uninstallRequest.PackageName
                            );

                            sendMessage(new PythonPackageInstallStatusMessage(
                                result.PackageName,
                                PythonPackageActions.Uninstall,
                                result.Success ? PythonPackageInstallStates.Succeeded : PythonPackageInstallStates.Failed,
                                result.PythonPath,
                                result.Message
                            ));

                            if (result.Success)
                            {
                                var packageResult = await pythonPackageManager.GetInstalledPackagesAsync(result.PythonPath);
                                sendMessage(new PythonPackagesMessage(packageResult.PythonPath, packageResult.Packages));
                            }
                        }
                        catch (Exception ex)
                        {
                            sendMessage(new PythonPackageInstallStatusMessage(
                                uninstallRequest.PackageName,
                                PythonPackageActions.Uninstall,
                                PythonPackageInstallStates.Failed,
                                pythonForStatus,
                                ex.Message
                            ));
                        }
                    });
                    break;
                }
                case BridgeMessageTypes.GetTerminals:
                {
                    var terminals = terminalManager.GetTerminals();
                    sendMessage(new TerminalsMessage(terminals.ToList()));
                    break;
                }
                case BridgeMessageTypes.StartTerminal:
                {
                    var request = JsonSerializer.Deserialize<StartTerminalRequest>(rawMessage, JsonOptions);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await terminalManager.StartTerminalAsync(request?.Shell, request?.Cwd);
                        }
                        catch (Exception ex)
                        {
                            sendMessage(new ErrorMessage("Failed to start terminal.", ex.Message));
                        }
                    });
                    break;
                }
                case BridgeMessageTypes.TerminalInput:
                {
                    var request = JsonSerializer.Deserialize<TerminalInputRequest>(rawMessage, JsonOptions);
                    if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
                    {
                        sendMessage(new ErrorMessage("terminalInput request is missing terminalId."));
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var ok = await terminalManager.SendInputAsync(request.TerminalId, request.Data ?? string.Empty);
                            if (!ok)
                            {
                                sendMessage(new ErrorMessage($"Terminal not found or not running: {request.TerminalId}"));
                            }
                        }
                        catch (Exception ex)
                        {
                            sendMessage(new ErrorMessage("Failed to write terminal input.", ex.Message));
                        }
                    });
                    break;
                }
                case BridgeMessageTypes.TerminalResize:
                {
                    var request = JsonSerializer.Deserialize<TerminalResizeRequest>(rawMessage, JsonOptions);
                    if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
                    {
                        sendMessage(new ErrorMessage("terminalResize request is missing terminalId."));
                        return;
                    }

                    terminalManager.ResizeTerminal(request.TerminalId, request.Cols, request.Rows);
                    break;
                }
                case BridgeMessageTypes.StopTerminal:
                {
                    var request = JsonSerializer.Deserialize<StopTerminalRequest>(rawMessage, JsonOptions);
                    if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
                    {
                        sendMessage(new ErrorMessage("stopTerminal request is missing terminalId."));
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            terminalManager.StopTerminal(request.TerminalId);
                        }
                        catch (Exception ex)
                        {
                            sendMessage(new ErrorMessage("Failed to stop terminal.", ex.Message));
                        }
                    });
                    break;
                }
                default:
                    sendMessage(new ErrorMessage($"Unsupported message type: {baseMessage.Type}"));
                    break;
            }
        }
        catch (JsonException ex)
        {
            sendMessage(new ErrorMessage("Failed to parse JSON message.", ex.Message));
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

    private static string? ResolvePythonOverride(string? python, ToolItem tool)
    {
        if (string.IsNullOrWhiteSpace(python))
        {
            return null;
        }

        var raw = python.Trim();
        if (Path.IsPathRooted(raw))
        {
            return Path.GetFullPath(raw);
        }

        var baseDirectory = !string.IsNullOrWhiteSpace(tool.Cwd)
            ? tool.Cwd
            : Directory.GetCurrentDirectory();

        return Path.GetFullPath(Path.Combine(baseDirectory, raw));
    }

    private static string? ShowPythonPicker(PhotinoWindow window, string? defaultPath)
    {
        var safeDefaultPath = ResolveDialogDefaultPath(defaultPath);

        var candidates = window.ShowOpenFile(
            title: "Select Python Interpreter",
            defaultPath: safeDefaultPath,
            multiSelect: false,
            filters: null
        );

        return candidates is { Length: > 0 }
            ? candidates[0]
            : null;
    }

    private static string? ResolveDialogDefaultPath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(rawPath);

            if (File.Exists(fullPath))
            {
                var directory = Path.GetDirectoryName(fullPath);
                return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                    ? directory
                    : null;
            }

            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }

            var parent = Path.GetDirectoryName(fullPath);
            return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent)
                ? parent
                : null;
        }
        catch
        {
            return null;
        }
    }
}
