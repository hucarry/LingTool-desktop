using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

internal static class TerminalMessageHandlers
{
    public static void Register(IDictionary<string, MessageHandler> handlers)
    {
        handlers[BridgeMessageTypes.GetTerminals] = (context, _) =>
        {
            var terminals = context.TerminalManager.GetTerminals();
            context.SendMessage(new TerminalsMessage(terminals.ToList()));
        };
        handlers[BridgeMessageTypes.StartTerminal] = HandleStartTerminal;
        handlers[BridgeMessageTypes.TerminalInput] = HandleTerminalInput;
        handlers[BridgeMessageTypes.TerminalResize] = HandleTerminalResize;
        handlers[BridgeMessageTypes.StopTerminal] = HandleStopTerminal;
    }

    private static void HandleStartTerminal(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<StartTerminalRequest>(rawMessage, context.JsonOptions);
        _ = Task.Run(async () =>
        {
            try
            {
                await context.TerminalManager.StartTerminalAsync(
                    request?.Title,
                    request?.Shell,
                    request?.Cwd,
                    request?.ToolType,
                    request?.RuntimePath
                );
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage("Failed to start terminal.", ex.Message));
            }
        });
    }

    private static void HandleTerminalInput(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<TerminalInputRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
        {
            context.SendMessage(new ErrorMessage("terminalInput request is missing terminalId."));
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var ok = await context.TerminalManager.SendInputAsync(request.TerminalId, request.Data ?? string.Empty);
                if (!ok)
                {
                    context.SendMessage(new ErrorMessage($"Terminal not found or not running: {request.TerminalId}"));
                }
            }
            catch (Exception ex)
            {
                context.SendMessage(new ErrorMessage("Failed to write terminal input.", ex.Message));
            }
        });
    }

    private static void HandleTerminalResize(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<TerminalResizeRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
        {
            context.SendMessage(new ErrorMessage("terminalResize request is missing terminalId."));
            return;
        }

        context.TerminalManager.ResizeTerminal(request.TerminalId, request.Cols, request.Rows);
    }

    private static void HandleStopTerminal(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<StopTerminalRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.TerminalId))
        {
            context.SendMessage(new ErrorMessage("stopTerminal request is missing terminalId."));
            return;
        }

        try
        {
            context.TerminalManager.StopTerminal(request.TerminalId);
        }
        catch (Exception ex)
        {
            context.SendMessage(new ErrorMessage("Failed to stop terminal.", ex.Message));
        }
    }
}
