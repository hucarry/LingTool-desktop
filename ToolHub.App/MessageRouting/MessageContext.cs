using System.Text.Json;

namespace ToolHub.App;

public sealed record MessageContext(
    Action<object> SendMessage,
    Func<string?, string?> BrowsePython,
    Func<string?, string?, string?, string?> BrowseFile,
    JsonSerializerOptions JsonOptions
);

internal delegate void MessageHandler(MessageContext context, string rawMessage);
