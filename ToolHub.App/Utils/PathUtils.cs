using System.Text;

namespace ToolHub.App.Utils;

public static class PathUtils
{
    public static string ResolvePath(string path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (System.IO.Path.IsPathRooted(path))
        {
            return System.IO.Path.GetFullPath(path);
        }

        return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, path));
    }

    public static string ResolveProjectRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var candidate in candidates)
        {
            var sln = FindFileUpwards(candidate, "ToolHub.sln");
            if (sln is not null)
            {
                return System.IO.Path.GetDirectoryName(sln) ?? candidate;
            }

            var tools = FindFileUpwards(candidate, "tools.json");
            if (tools is not null)
            {
                return System.IO.Path.GetDirectoryName(tools) ?? candidate;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    public static IReadOnlyList<string> SplitCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var ch = commandLine[index];

            if (ch == '\\' && index + 1 < commandLine.Length && commandLine[index + 1] == '"')
            {
                current.Append('"');
                index++;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    public static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Any(ch => char.IsWhiteSpace(ch) || ch == '"'))
        {
            return value;
        }

        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static string? FindFileUpwards(string startPath, string fileName)
    {
        var current = new DirectoryInfo(startPath);

        while (current is not null)
        {
            var candidate = System.IO.Path.Combine(current.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
