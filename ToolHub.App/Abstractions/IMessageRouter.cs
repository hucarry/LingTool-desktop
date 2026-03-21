namespace ToolHub.App;

public interface IMessageRouter
{
    void Dispatch(MessageContext context, string rawMessage);
}
