using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ToolHub.App.Models;

namespace ToolHub.App;

public sealed class MessageRouter : IMessageRouter
{
    private readonly IReadOnlyDictionary<string, MessageHandler> _handlers;
    private readonly ILogger<MessageRouter> _logger;

    internal MessageRouter(IEnumerable<IMessageRouteRegistrar> registrars, ILogger<MessageRouter>? logger = null)
    {
        _logger = logger ?? NullLogger<MessageRouter>.Instance;
        _handlers = BuildHandlers(registrars);
        _logger.LogInformation("Message router initialized with {HandlerCount} handlers.", _handlers.Count);
    }

    public void Dispatch(MessageContext context, string rawMessage)
    {
        using var scope = BridgeLogging.BeginMessageScope(rawMessage);
        var baseMessage = JsonSerializer.Deserialize<IncomingMessage>(rawMessage, context.JsonOptions);
        if (string.IsNullOrWhiteSpace(baseMessage?.Type))
        {
            _logger.LogWarning("Rejected bridge message without type field. RawMessage={RawMessage}", rawMessage);
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.MessageMissingTypeField));
            return;
        }

        if (_handlers.TryGetValue(baseMessage.Type, out var handler))
        {
            _logger.LogDebug("Dispatching bridge message {MessageType}.", baseMessage.Type);
            handler(context, rawMessage);
            return;
        }

        _logger.LogWarning("Rejected unsupported bridge message type {MessageType}.", baseMessage.Type);
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
