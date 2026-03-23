namespace ToolHub.App.Utils;

internal static class ShellEscaper
{
    internal static string QuoteForPowerShell(string value)
    {
        var normalized = NormalizeForScript(value);
        return $"'{normalized.Replace("'", "''")}'";
    }

    internal static string QuoteForCmdArgument(string value)
    {
        var normalized = NormalizeForScript(value);
        return $"\"{normalized.Replace("\"", "\"\"").Replace("%", "%%")}\"";
    }

    internal static string QuoteForPosixShell(string value)
    {
        var normalized = NormalizeForScript(value);
        return $"'{normalized.Replace("'", "'\"'\"'")}'";
    }

    internal static string EscapeForCmdEnvironmentValue(string value)
    {
        return NormalizeForScript(value).Replace("%", "%%");
    }

    private static string NormalizeForScript(string value)
    {
        return value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}
