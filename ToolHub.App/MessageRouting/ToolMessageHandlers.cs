using System.Diagnostics;
using System.Text.Json;
using ToolHub.App.Models;
using ToolHub.App.Runtime;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal static class ToolMessageHandlers
{
    public static void Register(IDictionary<string, MessageHandler> handlers)
    {
        handlers[BridgeMessageTypes.AddTool] = HandleAddTool;
        handlers[BridgeMessageTypes.UpdateTool] = HandleUpdateTool;
        handlers[BridgeMessageTypes.DeleteTools] = HandleDeleteTools;
        handlers[BridgeMessageTypes.GetRuns] = (context, _) =>
        {
            context.SendMessage(new RunsMessage(context.ProcessManager.GetRuns()));
        };
        handlers[BridgeMessageTypes.RunTool] = HandleRunTool;
        handlers[BridgeMessageTypes.RunToolInTerminal] = HandleRunToolInTerminal;
        handlers[BridgeMessageTypes.OpenUrlTool] = HandleOpenUrlTool;
        handlers[BridgeMessageTypes.StopRun] = HandleStopRun;
    }

    private static void HandleAddTool(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<AddToolRequest>(rawMessage, context.JsonOptions);
        if (request?.Tool is null)
        {
            context.SendMessage(new ErrorMessage("addTool request is missing tool payload."));
            return;
        }

        var addedTool = context.Registry.AddTool(request.Tool);
        context.SendMessage(new ToolAddedMessage(addedTool.Id));
        context.SendMessage(new ToolsMessage(context.Registry.GetTools()));
    }

    private static void HandleUpdateTool(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<UpdateToolRequest>(rawMessage, context.JsonOptions);
        if (request?.Tool is null)
        {
            context.SendMessage(new ErrorMessage("updateTool request is missing tool payload."));
            return;
        }

        var updatedTool = context.Registry.UpdateTool(request.Tool);
        context.SendMessage(new ToolUpdatedMessage(updatedTool.Id));
        context.SendMessage(new ToolsMessage(context.Registry.GetTools()));
    }

    private static void HandleDeleteTools(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<DeleteToolsRequest>(rawMessage, context.JsonOptions);
        var deletedCount = context.Registry.DeleteTools(request?.ToolIds ?? new List<string>());

        context.SendMessage(new ToolsDeletedMessage(deletedCount));
        context.SendMessage(new ToolsMessage(context.Registry.GetTools()));
    }

    private static void HandleRunTool(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<RunToolRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
        {
            context.SendMessage(new ErrorMessage("runTool request is missing toolId."));
            return;
        }

        if (!TryGetRunnableTool(context, request.ToolId, out var tool))
        {
            return;
        }

        if (!TryBuildResolvedRunCommand(
                context,
                tool,
                request.Args ?? new Dictionary<string, string?>(),
                request.RuntimePath ?? request.Python,
                out var resolvedCommand))
        {
            return;
        }

        context.ProcessManager.StartRun(tool, resolvedCommand);
    }

    private static void HandleRunToolInTerminal(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<RunToolInTerminalRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
        {
            context.SendMessage(new ErrorMessage("runToolInTerminal request is missing toolId."));
            return;
        }

        if (!TryGetRunnableTool(context, request.ToolId, out var tool))
        {
            return;
        }

        if (!TryBuildResolvedRunCommand(
                context,
                tool,
                request.Args ?? new Dictionary<string, string?>(),
                request.RuntimePath ?? request.Python,
                out var resolvedCommand))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await context.TerminalManager.RunToolInTerminalAsync(
                    request.TerminalId,
                    tool,
                    resolvedCommand
                );
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage("Failed to run tool in terminal.", ex.Message));
            }
        });
    }

    private static void HandleOpenUrlTool(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<OpenUrlToolRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
        {
            context.SendMessage(new ErrorMessage("openUrlTool request is missing toolId."));
            return;
        }

        var tool = context.Registry.GetToolById(request.ToolId);
        if (tool is null)
        {
            context.SendMessage(new ErrorMessage($"Tool not found: {request.ToolId}"));
            return;
        }

        if (!tool.Valid || !string.Equals(tool.Type, "url", StringComparison.OrdinalIgnoreCase))
        {
            context.SendMessage(new ErrorMessage($"Tool cannot be opened as URL: {tool.Name}", tool.ValidationMessage));
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
            context.SendMessage(new ErrorMessage("Failed to open URL tool.", ex.Message));
        }
    }

    private static void HandleStopRun(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<StopRunRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.RunId))
        {
            context.SendMessage(new ErrorMessage("stopRun request is missing runId."));
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var stopped = await context.ProcessManager.StopRunAsync(request.RunId);
                if (!stopped)
                {
                    context.SendMessage(new ErrorMessage($"Run not found or already finished: {request.RunId}"));
                }
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage("Failed to stop run.", ex.Message));
            }
        });
    }

    private static bool TryGetRunnableTool(MessageContext context, string toolId, out RunnableTool tool)
    {
        tool = null!;

        var view = context.Registry.GetToolById(toolId);
        if (view is null)
        {
            context.SendMessage(new ErrorMessage($"Tool not found: {toolId}"));
            return false;
        }

        if (!view.Valid)
        {
            context.SendMessage(new ErrorMessage($"Tool is invalid: {view.Name}", view.ValidationMessage));
            return false;
        }

        tool = CreateRunnableTool(view);
        return true;
    }

    private static RunnableTool CreateRunnableTool(ToolItem source)
    {
        return new RunnableTool
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            Path = source.Path,
            RuntimePath = source.RuntimePath,
            Cwd = source.Cwd,
            ArgsTemplate = source.ArgsTemplate,
            ArgsSpec = ArgsSpecCompiler.Normalize(source.ArgsSpec)
        };
    }

    private static bool TryBuildResolvedRunCommand(
        MessageContext context,
        RunnableTool tool,
        IReadOnlyDictionary<string, string?> args,
        string? rawRuntimePath,
        out ResolvedRunCommand resolvedCommand)
    {
        resolvedCommand = null!;
        var runtimeOverride = Program.ResolveRuntimeOverride(rawRuntimePath, tool.Cwd);

        try
        {
            resolvedCommand = RunCommandBuilder.Build(tool, args, runtimeOverride);
            return true;
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "No usable Python interpreter was found.", StringComparison.Ordinal))
        {
            context.SendMessage(new ErrorMessage(
                ex.Message,
                "Install Python in the sandbox or choose a valid python.exe in the tool settings."
            ));
            return false;
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "No usable Node.js runtime was found.", StringComparison.Ordinal))
        {
            context.SendMessage(new ErrorMessage(
                ex.Message,
                "Install Node.js or choose a valid node.exe in the tool settings."
            ));
            return false;
        }
        catch (NotSupportedException ex)
        {
            context.SendMessage(new ErrorMessage(ex.Message));
            return false;
        }
    }
}
