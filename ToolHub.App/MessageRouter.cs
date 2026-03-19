using System.Text.Json;
using System.Diagnostics;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

public record MessageContext(
    ToolRegistry Registry,
    ProcessManager ProcessManager,
    PythonPackageManager PythonPackageManager,
    TerminalManager TerminalManager,
    Action<object> SendMessage,
    Func<string?, string?> BrowsePython,
    Func<string?, string?, string?> BrowseFile,
    JsonSerializerOptions JsonOptions
);

public static class MessageRouter
{
    private delegate void MessageHandler(MessageContext context, string rawMessage);

    private static readonly Dictionary<string, MessageHandler> Handlers = new()
    {
        [BridgeMessageTypes.GetTools] = (ctx, _) =>
        {
            ctx.SendMessage(new ToolsMessage(ctx.Registry.GetTools()));
            return;
        },

        [BridgeMessageTypes.AddTool] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<AddToolRequest>(raw, ctx.JsonOptions);
            if (request?.Tool is null)
            {
                ctx.SendMessage(new ErrorMessage("addTool request is missing tool payload."));
                return;
            }

            var addedTool = ctx.Registry.AddTool(request.Tool);
            ctx.SendMessage(new ToolAddedMessage(addedTool.Id));
            ctx.SendMessage(new ToolsMessage(ctx.Registry.GetTools()));
            return;
        },

        [BridgeMessageTypes.UpdateTool] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<UpdateToolRequest>(raw, ctx.JsonOptions);
            if (request?.Tool is null)
            {
                ctx.SendMessage(new ErrorMessage("updateTool request is missing tool payload."));
                return;
            }

            var updatedTool = ctx.Registry.UpdateTool(request.Tool);
            ctx.SendMessage(new ToolUpdatedMessage(updatedTool.Id));
            ctx.SendMessage(new ToolsMessage(ctx.Registry.GetTools()));
            return;
        },

        [BridgeMessageTypes.DeleteTools] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<DeleteToolsRequest>(raw, ctx.JsonOptions);
            var ids = request?.ToolIds ?? new List<string>();
            var deletedCount = ctx.Registry.DeleteTools(ids);
            ctx.SendMessage(new ToolsDeletedMessage(deletedCount));
            ctx.SendMessage(new ToolsMessage(ctx.Registry.GetTools()));
            return;
        },

        [BridgeMessageTypes.GetRuns] = (ctx, _) =>
        {
            ctx.SendMessage(new RunsMessage(ctx.ProcessManager.GetRuns()));
            return;
        },

        [BridgeMessageTypes.RunTool] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<RunToolRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
            {
                ctx.SendMessage(new ErrorMessage("runTool request is missing toolId."));
                return;
            }

            var tool = ctx.Registry.GetToolById(request.ToolId);
            if (tool is null)
            {
                ctx.SendMessage(new ErrorMessage($"Tool not found: {request.ToolId}"));
                return;
            }

            if (!tool.Valid)
            {
                ctx.SendMessage(new ErrorMessage($"Tool is invalid: {tool.Name}", tool.ValidationMessage));
                return;
            }

            var runtimeOverride = ResolveToolRuntime(ctx, tool, request.RuntimePath ?? request.Python);
            if (runtimeOverride == ResolveRuntimeFailed)
            {
                return; // 错误消息已在 ResolveToolRuntime 中发送
            }

            ctx.ProcessManager.StartRun(
                tool,
                request.Args ?? new Dictionary<string, string?>(),
                runtimeOverride
            );
            return;
        },

        [BridgeMessageTypes.RunToolInTerminal] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<RunToolInTerminalRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
            {
                ctx.SendMessage(new ErrorMessage("runToolInTerminal request is missing toolId."));
                return;
            }

            var tool = ctx.Registry.GetToolById(request.ToolId);
            if (tool is null)
            {
                ctx.SendMessage(new ErrorMessage($"Tool not found: {request.ToolId}"));
                return;
            }

            if (!tool.Valid)
            {
                ctx.SendMessage(new ErrorMessage($"Tool is invalid: {tool.Name}", tool.ValidationMessage));
                return;
            }

            var runtimeOverride = ResolveToolRuntime(ctx, tool, request.RuntimePath ?? request.Python);
            if (runtimeOverride == ResolveRuntimeFailed)
            {
                return; // 错误消息已在 ResolveToolRuntime 中发送
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await ctx.TerminalManager.RunToolInTerminalAsync(
                        request.TerminalId,
                        tool,
                        request.Args ?? new Dictionary<string, string?>(),
                        runtimeOverride
                    );
                }
                catch (Exception ex)
                {
                    ctx.SendMessage(new ErrorMessage("Failed to run tool in terminal.", ex.Message));
                }
            });
            return;
        },

        [BridgeMessageTypes.OpenUrlTool] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<OpenUrlToolRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
            {
                ctx.SendMessage(new ErrorMessage("openUrlTool request is missing toolId."));
                return;
            }

            var tool = ctx.Registry.GetToolById(request.ToolId);
            if (tool is null)
            {
                ctx.SendMessage(new ErrorMessage($"Tool not found: {request.ToolId}"));
                return;
            }

            if (!tool.Valid || !string.Equals(tool.Type, "url", StringComparison.OrdinalIgnoreCase))
            {
                ctx.SendMessage(new ErrorMessage($"Tool cannot be opened as URL: {tool.Name}", tool.ValidationMessage));
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tool.Path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                ctx.SendMessage(new ErrorMessage("Failed to open URL tool.", ex.Message));
            }

            return;
        },

        [BridgeMessageTypes.StopRun] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<StopRunRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.RunId))
            {
                ctx.SendMessage(new ErrorMessage("stopRun request is missing runId."));
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var stopped = await ctx.ProcessManager.StopRunAsync(request.RunId);
                    if (!stopped)
                    {
                        ctx.SendMessage(new ErrorMessage($"Run not found or already finished: {request.RunId}"));
                    }
                }
                catch (Exception ex)
                {
                    ctx.SendMessage(new ErrorMessage("Failed to stop run.", ex.Message));
                }
            });
            return;
        },

        [BridgeMessageTypes.BrowsePython] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<BrowsePythonRequest>(raw, ctx.JsonOptions);
            var selectedPath = ctx.BrowsePython(request?.DefaultPath);
            ctx.SendMessage(new PythonSelectedMessage(selectedPath, request?.Purpose));
            return;
        },

        [BridgeMessageTypes.BrowseFile] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<BrowseFileRequest>(raw, ctx.JsonOptions);
            var selectedPath = ctx.BrowseFile(request?.DefaultPath, request?.Filter);
            ctx.SendMessage(new FileSelectedMessage(selectedPath, request?.Purpose));
            return;
        },

        [BridgeMessageTypes.GetPythonPackages] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<GetPythonPackagesRequest>(raw, ctx.JsonOptions);
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await ctx.PythonPackageManager.GetInstalledPackagesAsync(request?.PythonPath);
                    ctx.SendMessage(new PythonPackagesMessage(result.PythonPath, result.Packages));
                }
                catch (Exception ex)
                {
                    ctx.SendMessage(new ErrorMessage("Failed to read installed Python packages.", ex.Message));
                }
            });
            return;
        },

        [BridgeMessageTypes.InstallPythonPackage] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<InstallPythonPackageRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.PackageName))
            {
                ctx.SendMessage(new ErrorMessage("installPythonPackage request is missing packageName."));
                return;
            }

            _ = Task.Run(async () =>
            {
                string pythonForStatus = request.PythonPath ?? "python";
                try
                {
                    ctx.SendMessage(new PythonPackageInstallStatusMessage(
                        request.PackageName,
                        PythonPackageActions.Install,
                        PythonPackageInstallStates.Running,
                        pythonForStatus
                    ));

                    var result = await ctx.PythonPackageManager.InstallPackageAsync(
                        request.PythonPath,
                        request.PackageName
                    );

                    ctx.SendMessage(new PythonPackageInstallStatusMessage(
                        result.PackageName,
                        PythonPackageActions.Install,
                        result.Success ? PythonPackageInstallStates.Succeeded : PythonPackageInstallStates.Failed,
                        result.PythonPath,
                        result.Message
                    ));

                    if (result.Success)
                    {
                        var packageResult = await ctx.PythonPackageManager.GetInstalledPackagesAsync(result.PythonPath);
                        ctx.SendMessage(new PythonPackagesMessage(packageResult.PythonPath, packageResult.Packages));
                    }
                }
                catch (Exception ex)
                {
                    ctx.SendMessage(new PythonPackageInstallStatusMessage(
                        request.PackageName,
                        PythonPackageActions.Install,
                        PythonPackageInstallStates.Failed,
                        pythonForStatus,
                        ex.Message
                    ));
                }
            });
            return;
        },

        [BridgeMessageTypes.UninstallPythonPackage] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<UninstallPythonPackageRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.PackageName))
            {
                ctx.SendMessage(new ErrorMessage("uninstallPythonPackage request is missing packageName."));
                return;
            }

            _ = Task.Run(async () =>
            {
                string pythonForStatus = request.PythonPath ?? "python";
                try
                {
                    ctx.SendMessage(new PythonPackageInstallStatusMessage(
                        request.PackageName,
                        PythonPackageActions.Uninstall,
                        PythonPackageInstallStates.Running,
                        pythonForStatus
                    ));

                    var result = await ctx.PythonPackageManager.UninstallPackageAsync(
                        request.PythonPath,
                        request.PackageName
                    );

                    ctx.SendMessage(new PythonPackageInstallStatusMessage(
                        result.PackageName,
                        PythonPackageActions.Uninstall,
                        result.Success ? PythonPackageInstallStates.Succeeded : PythonPackageInstallStates.Failed,
                        result.PythonPath,
                        result.Message
                    ));

                    if (result.Success)
                    {
                        var packageResult = await ctx.PythonPackageManager.GetInstalledPackagesAsync(result.PythonPath);
                        ctx.SendMessage(new PythonPackagesMessage(packageResult.PythonPath, packageResult.Packages));
                    }
                }
                catch (Exception ex)
                {
                    ctx.SendMessage(new PythonPackageInstallStatusMessage(
                        request.PackageName,
                        PythonPackageActions.Uninstall,
                        PythonPackageInstallStates.Failed,
                        pythonForStatus,
                        ex.Message
                    ));
                }
            });
            return;
        },

        [BridgeMessageTypes.GetTerminals] = (ctx, _) =>
        {
            var terminals = ctx.TerminalManager.GetTerminals();
            ctx.SendMessage(new TerminalsMessage(terminals.ToList()));
            return;
        },

        [BridgeMessageTypes.StartTerminal] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<StartTerminalRequest>(raw, ctx.JsonOptions);
            _ = Task.Run(async () =>
            {
                try
                {
                    await ctx.TerminalManager.StartTerminalAsync(request?.Shell, request?.Cwd);
                }
                catch (Exception ex)
                {
                    ctx.SendMessage(new ErrorMessage("Failed to start terminal.", ex.Message));
                }
            });
            return;
        },

        [BridgeMessageTypes.TerminalInput] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<TerminalInputRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
            {
                ctx.SendMessage(new ErrorMessage("terminalInput request is missing terminalId."));
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var ok = await ctx.TerminalManager.SendInputAsync(request.TerminalId, request.Data ?? string.Empty);
                    if (!ok)
                    {
                        ctx.SendMessage(new ErrorMessage($"Terminal not found or not running: {request.TerminalId}"));
                    }
                }
                catch (Exception ex)
                {
                    ctx.SendMessage(new ErrorMessage("Failed to write terminal input.", ex.Message));
                }
            });
            return;
        },

        [BridgeMessageTypes.TerminalResize] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<TerminalResizeRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
            {
                ctx.SendMessage(new ErrorMessage("terminalResize request is missing terminalId."));
                return;
            }

            ctx.TerminalManager.ResizeTerminal(request.TerminalId, request.Cols, request.Rows);
            return;
        },

        [BridgeMessageTypes.StopTerminal] = (ctx, raw) =>
        {
            var request = JsonSerializer.Deserialize<StopTerminalRequest>(raw, ctx.JsonOptions);
            if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
            {
                ctx.SendMessage(new ErrorMessage("stopTerminal request is missing terminalId."));
                return;
            }

            try
            {
                ctx.TerminalManager.StopTerminal(request.TerminalId);
            }
            catch (Exception ex)
            {
                ctx.SendMessage(new ErrorMessage("Failed to stop terminal.", ex.Message));
            }
            return;
        }
    };

    public static void Route(MessageContext context, string rawMessage)
    {
        var baseMessage = JsonSerializer.Deserialize<IncomingMessage>(rawMessage, context.JsonOptions);
        if (baseMessage?.Type is null)
        {
            context.SendMessage(new ErrorMessage("Message is missing type field."));
            return;
        }

        if (Handlers.TryGetValue(baseMessage.Type, out var handler))
        {
            handler(context, rawMessage);
        }
        else
        {
            context.SendMessage(new ErrorMessage($"Unsupported message type: {baseMessage.Type}"));
        }
    }

    /// <summary>运行时解析失败时返回的哨兵值。</summary>
    private static readonly string ResolveRuntimeFailed = "\x00__RESOLVE_FAILED__";

    /// <summary>统一解析工具运行时路径，解析失败时自动发送错误消息并返回哨兵值。</summary>
    private static string? ResolveToolRuntime(MessageContext ctx, ToolItem tool, string? rawRuntimePath)
    {
        var runtimeOverride = Program.ResolveRuntimeOverride(rawRuntimePath, tool);

        if (string.Equals(tool.Type, "python", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = PythonInterpreterProbe.ResolvePreferred(runtimeOverride, tool.RuntimePath);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                ctx.SendMessage(new ErrorMessage(
                    "未找到可用 Python 解释器",
                    "请在沙盒内安装 Python，或在工具详情里手动选择可用 python.exe。"
                ));
                return ResolveRuntimeFailed;
            }

            return resolved;
        }

        if (string.Equals(tool.Type, "node", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = NodeRuntimeProbe.ResolvePreferred(runtimeOverride, tool.RuntimePath);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                ctx.SendMessage(new ErrorMessage(
                    "未找到可用 Node.js 运行时。",
                    "请安装 Node.js，或在工具详情里手动选择可用的 node.exe。"
                ));
                return ResolveRuntimeFailed;
            }

            return resolved;
        }

        return runtimeOverride;
    }
}
