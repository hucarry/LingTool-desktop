using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

public static class MessageRouter
{
    private static readonly IReadOnlyDictionary<string, MessageHandler> Handlers = BuildHandlers();

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
            return;
        }

        context.SendMessage(new ErrorMessage($"Unsupported message type: {baseMessage.Type}"));
    }

    private static IReadOnlyDictionary<string, MessageHandler> BuildHandlers()
    {
        var handlers = new Dictionary<string, MessageHandler>();

        AppMessageHandlers.Register(handlers);
        ToolMessageHandlers.Register(handlers);
        PythonMessageHandlers.Register(handlers);
        TerminalMessageHandlers.Register(handlers);

        return handlers;
    }
}
