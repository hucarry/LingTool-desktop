using System.Diagnostics;
using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

internal sealed class ToolCatalogMessageHandlers(IToolRegistry registry) : IMessageRouteRegistrar
{
    public void Register(IDictionary<string, MessageHandler> handlers)
    {
        handlers[BridgeMessageTypes.AddTool] = HandleAddTool;
        handlers[BridgeMessageTypes.UpdateTool] = HandleUpdateTool;
        handlers[BridgeMessageTypes.DeleteTools] = HandleDeleteTools;
        handlers[BridgeMessageTypes.OpenUrlTool] = HandleOpenUrlTool;
    }

    private void HandleAddTool(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<AddToolRequest>(rawMessage, context.JsonOptions);
        if (request?.Tool is null)
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.AddToolMissingPayload));
            return;
        }

        var addedTool = registry.AddTool(request.Tool);
        context.SendMessage(new ToolAddedMessage(addedTool.Id));
        context.SendMessage(new ToolsMessage(registry.GetTools()));
    }

    private void HandleUpdateTool(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<UpdateToolRequest>(rawMessage, context.JsonOptions);
        if (request?.Tool is null)
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.UpdateToolMissingPayload));
            return;
        }

        var updatedTool = registry.UpdateTool(request.Tool);
        context.SendMessage(new ToolUpdatedMessage(updatedTool.Id));
        context.SendMessage(new ToolsMessage(registry.GetTools()));
    }

    private void HandleDeleteTools(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<DeleteToolsRequest>(rawMessage, context.JsonOptions);
        var deletedCount = registry.DeleteTools(request?.ToolIds ?? new List<string>());

        context.SendMessage(new ToolsDeletedMessage(deletedCount));
        context.SendMessage(new ToolsMessage(registry.GetTools()));
    }

    private void HandleOpenUrlTool(MessageContext context, string rawMessage)
    {
        var request = JsonSerializer.Deserialize<OpenUrlToolRequest>(rawMessage, context.JsonOptions);
        if (request is null || string.IsNullOrWhiteSpace(request.ToolId))
        {
            context.SendMessage(new ErrorMessage(BridgeErrorMessages.OpenUrlToolMissingToolId));
            return;
        }

        var tool = registry.GetToolById(request.ToolId);
        if (tool is null)
        {
            context.SendMessage(new ErrorMessage(ToolErrorMessages.ToolNotFound(request.ToolId)));
            return;
        }

        if (!tool.Valid || !string.Equals(tool.Type, "url", StringComparison.OrdinalIgnoreCase))
        {
            context.SendMessage(new ErrorMessage(ToolErrorMessages.ToolCannotBeOpenedAsUrl(tool.Name), tool.ValidationMessage));
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
            context.SendMessage(new ErrorMessage(ToolErrorMessages.FailedToOpenUrlTool, ex.Message));
        }
    }
}
