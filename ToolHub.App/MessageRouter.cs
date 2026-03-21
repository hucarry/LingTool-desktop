using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

public sealed class MessageRouter : IMessageRouter
{
    private readonly IReadOnlyDictionary<string, MessageHandler> _handlers;

    internal MessageRouter(IEnumerable<IMessageRouteRegistrar> registrars)
    {
        _handlers = BuildHandlers(registrars);
    }

    public void Dispatch(MessageContext context, string rawMessage)
    {
        var baseMessage = JsonSerializer.Deserialize<IncomingMessage>(rawMessage, context.JsonOptions);
        if (string.IsNullOrWhiteSpace(baseMessage?.Type))
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.MessageMissingTypeField));
            return;
        }

        if (_handlers.TryGetValue(baseMessage.Type, out var handler))
        {
            handler(context, rawMessage);
            return;
        }

        context.SendMessage(new ErrorMessage(BridgeErrorMessages.UnsupportedMessageType(baseMessage.Type)));
    }

    private static IReadOnlyDictionary<string, MessageHandler> BuildHandlers(IEnumerable<IMessageRouteRegistrar> registrars)
    {
        var handlers = new Dictionary<string, MessageHandler>();

        foreach (var registrar in registrars)
        {
            registrar.Register(handlers);
        }

        return handlers;
    }
}
