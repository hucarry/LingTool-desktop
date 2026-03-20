using System.Text.RegularExpressions;
using ToolHub.App.Models;

namespace ToolHub.App.Utils;

public static class ArgsSpecCompiler
{
    private static readonly Regex PlaceholderRegex = new(
        @"\{(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\}",
        RegexOptions.Compiled
    );

    public static IReadOnlyList<string> ExtractPlaceholders(ArgsSpecV1? spec, string? fallbackTemplate = null)
    {
        var normalized = Normalize(spec);
        if (normalized?.Fields.Count > 0)
        {
            return normalized.Fields
                .Select(field => field.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return ExtractPlaceholdersFromTemplate(fallbackTemplate);
    }

    public static IReadOnlyList<string> ExtractPlaceholdersFromTemplate(string? template)
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
        ArgsSpecV1? spec,
        string? fallbackTemplate,
        IReadOnlyDictionary<string, string?> values)
    {
        var normalized = Normalize(spec);
        if (normalized is not null && normalized.Argv.Count > 0)
        {
            return BuildArgumentsFromSpec(normalized, values);
        }

        return BuildArgumentsFromTemplate(fallbackTemplate, values);
    }

    public static IReadOnlyList<string> BuildArgumentsFromTemplate(
        string? template,
        IReadOnlyDictionary<string, string?> values)
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
            .Select(token => ApplyTemplateValues(token, values))
            .ToList();
    }

    public static ArgsSpecV1? Normalize(ArgsSpecV1? source)
    {
        if (source is null)
        {
            return null;
        }

        var fields = source.Fields?
            .Where(field => !string.IsNullOrWhiteSpace(field?.Name))
            .GroupBy(field => field.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var field = group.First();
                return new ArgFieldSpec
                {
                    Name = field.Name.Trim(),
                    Label = NormalizeOptional(field.Label),
                    Description = NormalizeOptional(field.Description),
                    Kind = NormalizeKind(field.Kind),
                    Required = field.Required,
                    DefaultValue = NormalizeOptional(field.DefaultValue),
                    Placeholder = NormalizeOptional(field.Placeholder),
                    Options = field.Options?
                        .Where(option => option is not null)
                        .Select(option => new ArgFieldOption
                        {
                            Label = string.IsNullOrWhiteSpace(option.Label)
                                ? option.Value?.Trim() ?? string.Empty
                                : option.Label.Trim(),
                            Value = option.Value?.Trim() ?? string.Empty
                        })
                        .Where(option => option.Value.Length > 0)
                        .ToList() ?? new List<ArgFieldOption>()
                };
            })
            .ToList() ?? new List<ArgFieldSpec>();

        var argv = source.Argv?
            .Where(token => token is not null)
            .Select(token => new ArgTokenSpec
            {
                Kind = NormalizeTokenKind(token.Kind),
                Value = NormalizeOptional(token.Value),
                Field = NormalizeOptional(token.Field),
                Prefix = NormalizeOptional(token.Prefix),
                Suffix = NormalizeOptional(token.Suffix),
                OmitWhenEmpty = token.OmitWhenEmpty,
                WhenTrue = NormalizeOptional(token.WhenTrue),
                WhenFalse = NormalizeOptional(token.WhenFalse)
            })
            .Where(IsMeaningfulToken)
            .ToList() ?? new List<ArgTokenSpec>();

        if (fields.Count == 0 && argv.Count == 0)
        {
            return null;
        }

        return new ArgsSpecV1
        {
            Version = source.Version <= 0 ? 1 : source.Version,
            Fields = fields,
            Argv = argv
        };
    }

    public static ArgsSpecV1? InferFromTemplate(string? template)
    {
        var trimmedTemplate = template?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTemplate))
        {
            return null;
        }

        var placeholders = ExtractPlaceholdersFromTemplate(trimmedTemplate)
            .Select(name => new ArgFieldSpec
            {
                Name = name,
                Kind = "text"
            })
            .ToList();

        var argv = new List<ArgTokenSpec>();
        var tokens = PathUtils.SplitCommandLine(trimmedTemplate);
        foreach (var token in tokens)
        {
            var converted = ConvertLegacyTokenToSpec(token);
            if (converted is null)
            {
                argv.Clear();
                break;
            }

            argv.Add(converted);
        }

        return new ArgsSpecV1
        {
            Version = 1,
            Fields = placeholders,
            Argv = argv
        };
    }

    public static string BuildLegacyTemplate(ArgsSpecV1? spec, string? fallbackTemplate = null)
    {
        var normalized = Normalize(spec);
        if (normalized is null || normalized.Argv.Count == 0)
        {
            return fallbackTemplate?.Trim() ?? string.Empty;
        }

        return string.Join(" ", normalized.Argv.Select(RenderLegacyToken));
    }

    private static IReadOnlyList<string> BuildArgumentsFromSpec(
        ArgsSpecV1 spec,
        IReadOnlyDictionary<string, string?> values)
    {
        var fields = spec.Fields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var token in spec.Argv)
        {
            switch (token.Kind)
            {
                case "literal":
                    if (!string.IsNullOrWhiteSpace(token.Value))
                    {
                        result.Add(token.Value);
                    }
                    break;

                case "field":
                    var fieldValue = ResolveFieldValue(token.Field, fields, values);
                    if (string.IsNullOrEmpty(fieldValue) && token.OmitWhenEmpty)
                    {
                        break;
                    }

                    result.Add($"{token.Prefix ?? string.Empty}{fieldValue}{token.Suffix ?? string.Empty}");
                    break;

                case "switch":
                    var switchValue = ResolveFieldValue(token.Field, fields, values);
                    var literal = IsTruthy(switchValue) ? token.WhenTrue : token.WhenFalse;
                    if (!string.IsNullOrWhiteSpace(literal))
                    {
                        result.Add(literal);
                    }
                    break;
            }
        }

        return result;
    }

    private static string ApplyTemplateValues(string token, IReadOnlyDictionary<string, string?> values)
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

    private static ArgTokenSpec? ConvertLegacyTokenToSpec(string token)
    {
        var matches = PlaceholderRegex.Matches(token);
        if (matches.Count == 0)
        {
            return new ArgTokenSpec
            {
                Kind = "literal",
                Value = token
            };
        }

        if (matches.Count == 1)
        {
            var match = matches[0];
            return new ArgTokenSpec
            {
                Kind = "field",
                Field = match.Groups["name"].Value,
                Prefix = match.Index > 0 ? token[..match.Index] : null,
                Suffix = match.Index + match.Length < token.Length
                    ? token[(match.Index + match.Length)..]
                    : null,
                OmitWhenEmpty = false
            };
        }

        return null;
    }

    private static string RenderLegacyToken(ArgTokenSpec token)
    {
        return token.Kind switch
        {
            "literal" => token.Value ?? string.Empty,
            "field" => $"{token.Prefix ?? string.Empty}{{{token.Field ?? string.Empty}}}{token.Suffix ?? string.Empty}",
            "switch" => $"{{{token.Field ?? string.Empty}}}",
            _ => token.Value ?? string.Empty
        };
    }

    private static string ResolveFieldValue(
        string? fieldName,
        IReadOnlyDictionary<string, ArgFieldSpec> fields,
        IReadOnlyDictionary<string, string?> values)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return string.Empty;
        }

        if (values.TryGetValue(fieldName, out var explicitValue) && explicitValue is not null)
        {
            return explicitValue;
        }

        return fields.TryGetValue(fieldName, out var field)
            ? field.DefaultValue ?? string.Empty
            : string.Empty;
    }

    private static bool IsTruthy(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "true" => true,
            "yes" => true,
            "on" => true,
            _ => false
        };
    }

    private static string NormalizeKind(string? kind)
    {
        var normalized = kind?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "path" => "path",
            "number" => "number",
            "flag" => "flag",
            "select" => "select",
            "secret" => "secret",
            _ => "text"
        };
    }

    private static string NormalizeTokenKind(string? kind)
    {
        var normalized = kind?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "field" => "field",
            "switch" => "switch",
            _ => "literal"
        };
    }

    private static bool IsMeaningfulToken(ArgTokenSpec token)
    {
        return token.Kind switch
        {
            "literal" => !string.IsNullOrWhiteSpace(token.Value),
            "field" => !string.IsNullOrWhiteSpace(token.Field),
            "switch" => !string.IsNullOrWhiteSpace(token.Field)
                && (!string.IsNullOrWhiteSpace(token.WhenTrue) || !string.IsNullOrWhiteSpace(token.WhenFalse)),
            _ => false
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
