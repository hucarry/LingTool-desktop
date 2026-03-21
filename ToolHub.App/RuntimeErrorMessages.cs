namespace ToolHub.App;

internal static class RuntimeErrorMessages
{
    internal const string NoUsablePythonInterpreter = "No usable Python interpreter was found.";
    internal const string NoUsableNodeRuntime = "No usable Node.js runtime was found.";

    internal static string UnsupportedToolType(string type) => $"Unsupported tool type: {type}";
}
