using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

public record MessageContext(
    ToolRegistry Registry,
    ProcessManager ProcessManager,
    PythonPackageManager PythonPackageManager,
    TerminalManager TerminalManager,
    Action<object> SendMessage,
    Func<string?, string?> BrowsePython,
    Func<string?, string?, string?, string?> BrowseFile,
    JsonSerializerOptions JsonOptions
);

internal delegate void MessageHandler(MessageContext context, string rawMessage);
