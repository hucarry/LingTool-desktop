using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

internal sealed class ToolExecutionMessageHandlers(
    IProcessManager processManager,
    ITerminalManager terminalManager,
    ToolExecutionSupport executionSupport
) : IMessageRouteRegistrar
{
    public void Register(IDictionary<string, MessageHandler> handlers)
    {
        handlers[BridgeMessageTypes.GetRuns] = (context, _) =>
        {
            context.SendMessage(new RunsMessage(processManager.GetRuns()));
        };
        handlers[BridgeMessageTypes.RunTool] = HandleRunTool;
        handlers[BridgeMessageTypes.RunToolInTerminal] = HandleRunToolInTerminal;
        handlers[BridgeMessageTypes.StopRun] = HandleStopRun;
    }

    private void HandleRunTool(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<RunToolRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.RunToolMissingToolId));
            return;
        }

        if (!executionSupport.TryResolveRunnableTool(context, request.ToolId, out var tool))
        {
            return;
        }

        if (!executionSupport.TryBuildResolvedRunCommand(
                context,
                tool,
                request.Args ?? new Dictionary<string, string?>(),
                request.RuntimePath ?? request.Python,
                out var resolvedCommand))
        {
            return;
        }

        processManager.StartRun(tool, resolvedCommand);
    }

    private void HandleRunToolInTerminal(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<RunToolInTerminalRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.RunToolInTerminalMissingToolId));
            return;
        }

        if (!executionSupport.TryResolveRunnableTool(context, request.ToolId, out var tool))
        {
            return;
        }

        if (!executionSupport.TryBuildResolvedRunCommand(
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
                await terminalManager.RunToolInTerminalAsync(
                    request.TerminalId,
                    tool,
                    resolvedCommand
                );
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage(TerminalErrorMessages.FailedToRunToolInTerminal, ex.Message));
            }
        });
    }

    private void HandleStopRun(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<StopRunRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.RunId))
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.StopRunMissingRunId));
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var stopped = await processManager.StopRunAsync(request.RunId);
                if (!stopped)
                {
                    context.SendMessage(new ErrorMessage(ProcessErrorMessages.RunNotFoundOrAlreadyFinished(request.RunId)));
                }
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage(ProcessErrorMessages.FailedToStopRun, ex.Message));
            }
        });
    }
}
