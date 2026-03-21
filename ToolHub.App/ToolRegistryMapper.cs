using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal static class ToolRegistryMapper
{
    internal static ToolDefinition NormalizeDefinition(ToolDefinition source)
    {
        var normalizedArgsSpec = ArgsSpecCompiler.Normalize(source.ArgsSpec)
            ?? ArgsSpecCompiler.InferFromTemplate(source.ArgsTemplate);
        var normalizedArgsTemplate = source.ArgsTemplate?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedArgsTemplate) && normalizedArgsSpec is not null)
        {
            normalizedArgsTemplate = ArgsSpecCompiler.BuildLegacyTemplate(normalizedArgsSpec);
        }

        return new ToolDefinition
        {
            Id = source.Id.Trim(),
            Name = source.Name.Trim(),
            Type = NormalizeToolType(source.Type),
            Path = source.Path.Trim(),
            RuntimePath = string.IsNullOrWhiteSpace(source.RuntimePath)
                ? (string.IsNullOrWhiteSpace(source.Python) ? null : source.Python.Trim())
                : source.RuntimePath.Trim(),
            Python = null,
            Cwd = string.IsNullOrWhiteSpace(source.Cwd) ? null : source.Cwd.Trim(),
            ArgsTemplate = normalizedArgsTemplate,
            ArgsSpec = normalizedArgsSpec,
            Tags = (source.Tags ?? new List<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Description = string.IsNullOrWhiteSpace(source.Description) ? null : source.Description.Trim()
        };
    }

    internal static ToolItem BuildToolView(ToolDefinition source, string baseDirectory)
    {
        var tool = CreateToolView(source);
        tool.Id = tool.Id.Trim();
        tool.Name = string.IsNullOrWhiteSpace(tool.Name) ? tool.Id : tool.Name.Trim();
        tool.Type = NormalizeToolType(tool.Type);
        tool.RuntimePath = string.IsNullOrWhiteSpace(tool.RuntimePath)
            ? (string.IsNullOrWhiteSpace(tool.Python) ? null : tool.Python.Trim())
            : tool.RuntimePath.Trim();
        tool.Python = null;
        tool.ArgsSpec = ArgsSpecCompiler.Normalize(tool.ArgsSpec)
            ?? ArgsSpecCompiler.InferFromTemplate(tool.ArgsTemplate);
        tool.ArgsTemplate = string.IsNullOrWhiteSpace(tool.ArgsTemplate)
            ? (tool.ArgsSpec is null ? string.Empty : ArgsSpecCompiler.BuildLegacyTemplate(tool.ArgsSpec))
            : tool.ArgsTemplate.Trim();
        tool.Tags = (tool.Tags ?? new List<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(tool.Id))
        {
            errors.Add(ToolErrorMessages.ToolIdRequired);
        }

        if (tool.Type is not ("python" or "node" or "command" or "executable" or "url"))
        {
            errors.Add("Tool type must be one of: python, node, command, executable, url.");
        }

        if (string.IsNullOrWhiteSpace(tool.Path))
        {
            errors.Add("Tool path is required.");
        }

        if (tool.Type == "url")
        {
            tool.Path = tool.Path.Trim();
            tool.PathExists = IsSupportedUrl(tool.Path);
            if (!tool.PathExists)
            {
                errors.Add($"Unsupported URL: {tool.Path}");
            }
        }
        else if (tool.Type == "command")
        {
            tool.Path = tool.Path.Trim();
            tool.PathExists = true;
        }
        else if (!string.IsNullOrWhiteSpace(tool.Path))
        {
            tool.Path = PathUtils.ResolvePath(tool.Path, baseDirectory);
            tool.PathExists = File.Exists(tool.Path);
            if (!tool.PathExists)
            {
                errors.Add($"Tool path does not exist: {tool.Path}");
            }
        }

        if (tool.Type == "url")
        {
            tool.Cwd = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(tool.Cwd))
            {
                tool.Cwd = tool.PathExists
                    ? Path.GetDirectoryName(tool.Path)
                    : baseDirectory;
            }
            else
            {
                tool.Cwd = PathUtils.ResolvePath(tool.Cwd, baseDirectory);
            }

            if (string.IsNullOrWhiteSpace(tool.Cwd) || !Directory.Exists(tool.Cwd))
            {
                errors.Add($"Working directory does not exist: {tool.Cwd}");
            }
        }

        if ((tool.Type == "python" || tool.Type == "node") && !string.IsNullOrWhiteSpace(tool.RuntimePath))
        {
            tool.RuntimePath = PathUtils.ResolvePathOrCommand(tool.RuntimePath, baseDirectory);
            var runtimeUsable = tool.Type == "python"
                ? PythonInterpreterProbe.IsUsable(tool.RuntimePath)
                : NodeRuntimeProbe.IsUsable(tool.RuntimePath);

            if (!runtimeUsable)
            {
                tool.RuntimePath = null;
            }
        }
        else
        {
            tool.RuntimePath = null;
        }

        tool.Valid = errors.Count == 0;
        tool.ValidationMessage = errors.Count == 0 ? null : string.Join("; ", errors);

        return tool;
    }

    internal static ToolItem CloneTool(ToolItem source)
    {
        return new ToolItem
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            Path = source.Path,
            RuntimePath = source.RuntimePath,
            Python = source.Python,
            Cwd = source.Cwd,
            ArgsTemplate = source.ArgsTemplate,
            ArgsSpec = ArgsSpecCompiler.Normalize(source.ArgsSpec),
            Tags = source.Tags?.ToList() ?? new List<string>(),
            Description = source.Description,
            PathExists = source.PathExists,
            Valid = source.Valid,
            ValidationMessage = source.ValidationMessage
        };
    }

    internal static bool IsLegacyFixedPathTemplate(ToolRegistryFile file)
    {
        if (file.Tools.Count != 2)
        {
            return false;
        }

        var ids = file.Tools
            .Select(tool => tool.Id.Trim().ToLowerInvariant())
            .OrderBy(id => id)
            .ToArray();

        if (ids[0] != "demo_exe" || ids[1] != "demo_py")
        {
            return false;
        }

        return file.Tools.All(tool =>
        {
            var normalizedPath = (tool.Path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            var normalizedCwd = (tool.Cwd ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            var normalizedRuntime = (tool.RuntimePath ?? tool.Python ?? string.Empty).Replace('\\', '/').ToLowerInvariant();

            var hasFixedAbsPath = normalizedPath.StartsWith("d:/tools/")
                || normalizedPath.StartsWith("c:/tools/");
            var hasFixedAbsCwd = normalizedCwd.StartsWith("d:/tools")
                || normalizedCwd.StartsWith("c:/tools");
            var hasFixedAbsRuntime = normalizedRuntime.StartsWith("d:/tools/")
                || normalizedRuntime.StartsWith("c:/tools/");

            return hasFixedAbsPath || hasFixedAbsCwd || hasFixedAbsRuntime;
        });
    }

    private static ToolItem CreateToolView(ToolDefinition source)
    {
        return new ToolItem
        {
            Id = source.Id ?? string.Empty,
            Name = source.Name ?? string.Empty,
            Type = NormalizeToolType(source.Type),
            Path = source.Path ?? string.Empty,
            RuntimePath = source.RuntimePath ?? source.Python,
            Python = null,
            Cwd = source.Cwd,
            ArgsTemplate = source.ArgsTemplate ?? string.Empty,
            ArgsSpec = ArgsSpecCompiler.Normalize(source.ArgsSpec),
            Tags = source.Tags?.ToList() ?? new List<string>(),
            Description = source.Description
        };
    }

    private static string NormalizeToolType(string? rawType)
    {
        var normalized = rawType?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "exe" => "executable",
            _ => normalized
        };
    }

    private static bool IsSupportedUrl(string path)
    {
        return Uri.TryCreate(path, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
