namespace ToolHub.App;

internal static class ToolErrorMessages
{
    internal const string ToolIdRequired = "Tool id is required.";
    internal const string ToolConfigurationInvalid = "Tool configuration is invalid.";
    internal const string FailedToOpenUrlTool = "Failed to open URL tool.";

    internal static string ToolNotFound(string toolId) => $"Tool not found: {toolId}";

    internal static string ToolIdAlreadyExists(string toolId) => $"Tool id already exists: {toolId}";

    internal static string ToolIdNotFound(string toolId) => $"Tool id not found: {toolId}";

    internal static string ToolInvalid(string toolName) => $"Tool is invalid: {toolName}";

    internal static string ToolCannotBeOpenedAsUrl(string toolName) => $"Tool cannot be opened as URL: {toolName}";
}
