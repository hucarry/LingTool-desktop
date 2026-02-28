using System.Text.RegularExpressions;

namespace ToolHub.App.Utils;

public static class ArgTemplate
{
    private static readonly Regex PlaceholderRegex = new(
        @"\{(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\}",
        RegexOptions.Compiled
    );

    public static IReadOnlyList<string> ExtractPlaceholders(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in PlaceholderRegex.Matches(template))
        {
            var key = match.Groups["name"].Value;
            if (seen.Add(key))
            {
                result.Add(key);
            }
        }

        return result;
    }

    public static IReadOnlyList<string> BuildArguments(
        string? template,
        IReadOnlyDictionary<string, string?> values
    )
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return Array.Empty<string>();
        }

        var tokens = PathUtils.SplitCommandLine(template);
        if (tokens.Count == 0)
        {
            return Array.Empty<string>();
        }

        return tokens
            .Select(token => ApplyValues(token, values))
            .ToList();
    }

    private static string ApplyValues(string token, IReadOnlyDictionary<string, string?> values)
    {
        return PlaceholderRegex.Replace(token, match =>
        {
            var key = match.Groups["name"].Value;
            if (values.TryGetValue(key, out var value) && value is not null)
            {
                return value;
            }

            return string.Empty;
        });
    }
}
