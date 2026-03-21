using System.Text.Json;
using ToolHub.App;
using ToolHub.App.Models;

namespace ToolHub.App.Tests;

public sealed class MessageRouterTests
{
    [Fact]
    public void Dispatch_ShouldUseRegisteredHandler()
    {
        var sent = new List<object>();
        var router = new MessageRouter([new TestRegistrar("ping", static (context, _) =>
        {
            context.SendMessage(new ErrorMessage("pong"));
        })]);
        var context = new MessageContext(
            sent.Add,
            static _ => null,
            static (_, _, _) => null,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );

        router.Dispatch(context, """{"type":"ping"}""");

        var message = Assert.IsType<ErrorMessage>(sent.Single());
        Assert.Equal("pong", message.Message);
    }

    [Fact]
    public void Dispatch_ShouldReturnMissingTypeError_WhenTypeIsAbsent()
    {
        var sent = new List<object>();
        var router = new MessageRouter([]);
        var context = new MessageContext(
            sent.Add,
            static _ => null,
            static (_, _, _) => null,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );

        router.Dispatch(context, """{"foo":"bar"}""");

        var error = Assert.IsType<ErrorMessage>(sent.Single());
        Assert.Equal(BridgeErrorMessages.MessageMissingTypeField, error.Message);
    }

    [Fact]
    public void Dispatch_ShouldReturnUnsupportedTypeError_WhenHandlerMissing()
    {
        var sent = new List<object>();
        var router = new MessageRouter([]);
        var context = new MessageContext(
            sent.Add,
            static _ => null,
            static (_, _, _) => null,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );

        router.Dispatch(context, """{"type":"unknown"}""");

        var error = Assert.IsType<ErrorMessage>(sent.Single());
        Assert.Equal(BridgeErrorMessages.UnsupportedMessageType("unknown"), error.Message);
    }

    private sealed class TestRegistrar(string type, MessageHandler handler) : IMessageRouteRegistrar
    {
        public void Register(IDictionary<string, MessageHandler> handlers)
        {
            handlers[type] = handler;
        }
    }
}
