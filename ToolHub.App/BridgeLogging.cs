using System.Text.Json;
using Serilog.Context;

namespace ToolHub.App;

internal static class BridgeLogging
{
    internal static IDisposable BeginMessageScope(string rawMessage, string? messageType = null)
    {
        var scopeItems = new List<IDisposable>
        {
            LogContext.PushProperty("BridgeMessageId", Guid.NewGuid().ToString("n"))
        };

        if (!string.IsNullOrWhiteSpace(messageType))
        {
            scopeItems.Add(LogContext.PushProperty("BridgeMessageType", messageType));
        }

        try
        {
            using var document = JsonDocument.Parse(rawMessage);
            var root = document.RootElement;

            TryPushProperty(scopeItems, root, "type", "BridgeMessageType");
            TryPushProperty(scopeItems, root, "toolId", "ToolId");
            TryPushProperty(scopeItems, root, "runId", "RunId");
            TryPushProperty(scopeItems, root, "terminalId", "TerminalId");
            TryPushProperty(scopeItems, root, "packageName", "PackageName");
        }
        catch (JsonException)
        {
            scopeItems.Add(LogContext.PushProperty("BridgeMessageMalformed", true));
        }

        return new ScopeGroup(scopeItems);
    }

    private static void TryPushProperty(
        ICollection<IDisposable> scopeItems,
        JsonElement root,
        string sourcePropertyName,
        string targetPropertyName)
    {
        if (!root.TryGetProperty(sourcePropertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        scopeItems.Add(LogContext.PushProperty(targetPropertyName, value));
    }

    private sealed class ScopeGroup(IReadOnlyList<IDisposable> items) : IDisposable
    {
        public void Dispose()
        {
            for (var index = items.Count - 1; index >= 0; index -= 1)
            {
                items[index].Dispose();
            }
        }
    }
}
