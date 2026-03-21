namespace ToolHub.App;

internal interface IMessageRouteRegistrar
{
    void Register(IDictionary<string, MessageHandler> handlers);
}
