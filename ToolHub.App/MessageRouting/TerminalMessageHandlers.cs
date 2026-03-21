using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

internal sealed class TerminalMessageHandlers(ITerminalManager terminalManager) : IMessageRouteRegistrar
{
    public void Register(IDictionary<string, MessageHandler> handlers)
    {
        handlers[BridgeMessageTypes.GetTerminals] = (context, _) =>
        {
            var terminals = terminalManager.GetTerminals();
            context.SendMessage(new TerminalsMessage(terminals.ToList()));
        };
        handlers[BridgeMessageTypes.StartTerminal] = HandleStartTerminal;
        handlers[BridgeMessageTypes.TerminalInput] = HandleTerminalInput;
        handlers[BridgeMessageTypes.TerminalResize] = HandleTerminalResize;
        handlers[BridgeMessageTypes.StopTerminal] = HandleStopTerminal;
    }

    private void HandleStartTerminal(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<StartTerminalRequest>(rawMessage, context.JsonOptions);
        _ = Task.Run(async () =>
        {
            try
            {
                await terminalManager.StartTerminalAsync(
                    request?.Title,
                    request?.Shell,
                    request?.Cwd,
                    request?.ToolType,
                    request?.RuntimePath
                );
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage(TerminalErrorMessages.FailedToStartTerminal, ex.Message));
            }
        });
    }

    private void HandleTerminalInput(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<TerminalInputRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.TerminalInputMissingTerminalId));
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var ok = await terminalManager.SendInputAsync(request.TerminalId, request.Data ?? string.Empty);
                if (!ok)
                {
                    context.SendMessage(new ErrorMessage(TerminalErrorMessages.TerminalNotFoundOrNotRunning(request.TerminalId)));
                }
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage(TerminalErrorMessages.FailedToWriteTerminalInput, ex.Message));
            }
        });
    }

    private void HandleTerminalResize(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<TerminalResizeRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.TerminalResizeMissingTerminalId));
            return;
        }

        terminalManager.ResizeTerminal(request.TerminalId, request.Cols, request.Rows);
    }

    private void HandleStopTerminal(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<StopTerminalRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.StopTerminalMissingTerminalId));
            return;
        }

        try
        {
            terminalManager.StopTerminal(request.TerminalId);
        }
        catch (Exception ex)
        {
            context.SendMessage(new ErrorMessage(TerminalErrorMessages.FailedToStopTerminal, ex.Message));
        }
    }
}
