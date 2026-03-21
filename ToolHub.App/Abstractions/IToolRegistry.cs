using ToolHub.App.Models;

namespace ToolHub.App;

public interface IToolRegistry
{
    IReadOnlyList<ToolItem> GetTools();

    ToolItem? GetToolById(string toolId);

    ToolItem AddTool(ToolDefinition source);

    ToolItem UpdateTool(ToolDefinition source);

    int DeleteTools(IReadOnlyCollection<string> toolIds);
}
