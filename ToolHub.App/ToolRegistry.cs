using System.Text.Json;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class ToolRegistry
{
    /// <summary>非强制刷新时的最小间隔，避免高频文件系统访问。</summary>
    private static readonly TimeSpan MinReloadInterval = TimeSpan.FromSeconds(1);

    private readonly string _toolsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _syncRoot = new();

    private DateTime _lastLoadedUtc = DateTime.MinValue;
    private DateTime _lastCheckedUtc = DateTime.MinValue;
    private List<ToolDefinition> _tools = new();

    public ToolRegistry(string toolsFilePath, JsonSerializerOptions jsonOptions)
    {
        _toolsFilePath = toolsFilePath;
        _jsonOptions = jsonOptions;

        EnsureToolsFileExists();
        Reload(force: true);
    }

    public IReadOnlyList<ToolItem> GetTools()
    {
        Reload();
        lock (_syncRoot)
        {
            return _tools
                .Select(tool => BuildToolView(tool, GetRegistryBaseDirectory()))
                .ToList();
        }
    }

    public ToolItem? GetToolById(string toolId)
    {
        Reload();

        lock (_syncRoot)
        {
            var tool = _tools.FirstOrDefault(item => string.Equals(item.Id, toolId, StringComparison.Ordinal));
            return tool is null ? null : BuildToolView(tool, GetRegistryBaseDirectory());
        }
    }

    public ToolItem AddTool(ToolDefinition source)
    {
        lock (_syncRoot)
        {
            var registryFile = ReadRegistryFile();
            var candidate = NormalizeDefinition(source);

            if (string.IsNullOrWhiteSpace(candidate.Id))
            {
                throw new InvalidOperationException("Tool id is required.");
            }

            var duplicate = registryFile.Tools.Any(item =>
                string.Equals(item.Id?.Trim(), candidate.Id, StringComparison.OrdinalIgnoreCase)
            );
            if (duplicate)
            {
                throw new InvalidOperationException($"Tool id already exists: {candidate.Id}");
            }

            var baseDirectory = GetRegistryBaseDirectory();
            var validatedCandidate = BuildToolView(candidate, baseDirectory);
            if (!validatedCandidate.Valid)
            {
                throw new InvalidOperationException(
                    validatedCandidate.ValidationMessage ?? "Tool configuration is invalid."
                );
            }

            registryFile.Tools.Add(candidate);
            SaveRegistryFile(registryFile);

            _tools = registryFile.Tools
                .Select(NormalizeDefinition)
                .ToList();
            _lastLoadedUtc = DateTime.UtcNow;

            return CloneTool(validatedCandidate);
        }
    }

    public ToolItem UpdateTool(ToolDefinition source)
    {
        lock (_syncRoot)
        {
            var registryFile = ReadRegistryFile();
            var candidate = NormalizeDefinition(source);

            if (string.IsNullOrWhiteSpace(candidate.Id))
            {
                throw new InvalidOperationException("Tool id is required.");
            }

            var existingIndex = registryFile.Tools.FindIndex(item =>
                string.Equals(item.Id?.Trim(), candidate.Id, StringComparison.OrdinalIgnoreCase)
            );
            if (existingIndex < 0)
            {
                throw new InvalidOperationException($"Tool id not found: {candidate.Id}");
            }

            var baseDirectory = GetRegistryBaseDirectory();
            var validatedCandidate = BuildToolView(candidate, baseDirectory);
            if (!validatedCandidate.Valid)
            {
                throw new InvalidOperationException(
                    validatedCandidate.ValidationMessage ?? "Tool configuration is invalid."
                );
            }

            registryFile.Tools[existingIndex] = candidate;
            SaveRegistryFile(registryFile);

            _tools = registryFile.Tools
                .Select(NormalizeDefinition)
                .ToList();
            _lastLoadedUtc = DateTime.UtcNow;

            return CloneTool(validatedCandidate);
        }
    }

    public int DeleteTools(IReadOnlyCollection<string> toolIds)
    {
        lock (_syncRoot)
        {
            var normalizedIds = new HashSet<string>(
                toolIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim()),
                StringComparer.OrdinalIgnoreCase
            );

            if (normalizedIds.Count == 0)
            {
                return 0;
            }

            var registryFile = ReadRegistryFile();
            var before = registryFile.Tools.Count;
            registryFile.Tools = registryFile.Tools
                .Where(item => !normalizedIds.Contains((item.Id ?? string.Empty).Trim()))
                .ToList();

            var deleted = before - registryFile.Tools.Count;
            if (deleted <= 0)
            {
                return 0;
            }

            SaveRegistryFile(registryFile);

            var baseDirectory = GetRegistryBaseDirectory();
            _tools = registryFile.Tools
                .Select(NormalizeDefinition)
                .ToList();
            _lastLoadedUtc = DateTime.UtcNow;

            return deleted;
        }
    }

    private void Reload(bool force = false)
    {
        lock (_syncRoot)
        {
            // 非强制刷新时，如果距上次检查不足最小间隔则跳过
            var now = DateTime.UtcNow;
            if (!force && (now - _lastCheckedUtc) < MinReloadInterval)
            {
                return;
            }

            _lastCheckedUtc = now;

            if (!File.Exists(_toolsFilePath))
            {
                _tools = new List<ToolDefinition>();
                _lastLoadedUtc = now;
                return;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(_toolsFilePath);
            if (!force && lastWriteUtc <= _lastLoadedUtc)
            {
                return;
            }

            var parsed = ReadRegistryFile();

            _tools = parsed.Tools
                .Select(NormalizeDefinition)
                .ToList();

            _lastLoadedUtc = now;
        }
    }

    private void EnsureToolsFileExists()
    {
        if (File.Exists(_toolsFilePath))
        {
            return;
        }

        var baseDirectory = Path.GetDirectoryName(_toolsFilePath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(baseDirectory);

        var exampleFilePath = Path.Combine(baseDirectory, "tools.example.json");
        if (File.Exists(exampleFilePath))
        {
            File.Copy(exampleFilePath, _toolsFilePath);
            return;
        }

        // Keep initial config portable for published builds on any drive.
        var template = """
{
  "tools": []
}
""";

        File.WriteAllText(_toolsFilePath, template);
    }

    private ToolRegistryFile ReadRegistryFile()
    {
        EnsureToolsFileExists();

        var fileContent = File.ReadAllText(_toolsFilePath);
        var parsed = JsonSerializer.Deserialize<ToolRegistryFile>(fileContent, _jsonOptions) ?? new ToolRegistryFile();

        if (IsLegacyFixedPathTemplate(parsed))
        {
            parsed.Tools.Clear();
            SaveRegistryFile(parsed);
        }

        return parsed;
    }

    private void SaveRegistryFile(ToolRegistryFile file)
    {
        var directory = Path.GetDirectoryName(_toolsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var writeOptions = new JsonSerializerOptions(_jsonOptions)
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(file, writeOptions);
        File.WriteAllText(_toolsFilePath, json);
    }

    private string GetRegistryBaseDirectory()
    {
        return Path.GetDirectoryName(_toolsFilePath) ?? Directory.GetCurrentDirectory();
    }

    private static ToolDefinition NormalizeDefinition(ToolDefinition source)
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

    private static ToolItem BuildToolView(ToolDefinition source, string baseDirectory)
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
            errors.Add("id 不能为空");
        }

        if (tool.Type is not ("python" or "node" or "command" or "executable" or "url"))
        {
            errors.Add("type 只支持 python、node、command、executable 或 url");
        }

        if (string.IsNullOrWhiteSpace(tool.Path))
        {
            errors.Add("path 不能为空");
        }

        if (tool.Type == "url")
        {
            tool.Path = tool.Path.Trim();
            tool.PathExists = IsSupportedUrl(tool.Path);
            if (!tool.PathExists)
            {
                errors.Add($"链接无效: {tool.Path}");
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
                errors.Add($"工具路径不存在: {tool.Path}");
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
                errors.Add($"工作目录不存在: {tool.Cwd}");
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

    private static ToolItem CloneTool(ToolItem source)
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

    /// <summary>检测 tools.json 是否包含旧版固定路径模板（仅匹配已知的精确旧模板值）。</summary>
    private static bool IsLegacyFixedPathTemplate(ToolRegistryFile file)
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

        // 只有所有工具的路径都指向已知旧模板路径（不含相对路径）才视为遗留模板
        return file.Tools.All(tool =>
        {
            var normalizedPath = (tool.Path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            var normalizedCwd = (tool.Cwd ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            var normalizedRuntime = (tool.RuntimePath ?? tool.Python ?? string.Empty).Replace('\\', '/').ToLowerInvariant();

            // 必须同时满足：路径是硬编码的绝对路径，且指向旧版默认模板目录
            var hasFixedAbsPath = normalizedPath.StartsWith("d:/tools/")
                || normalizedPath.StartsWith("c:/tools/");
            var hasFixedAbsCwd = normalizedCwd.StartsWith("d:/tools")
                || normalizedCwd.StartsWith("c:/tools");
            var hasFixedAbsRuntime = normalizedRuntime.StartsWith("d:/tools/")
                || normalizedRuntime.StartsWith("c:/tools/");

            return hasFixedAbsPath || hasFixedAbsCwd || hasFixedAbsRuntime;
        });
    }

    private sealed class ToolRegistryFile
    {
        public List<ToolDefinition> Tools { get; set; } = new();
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
