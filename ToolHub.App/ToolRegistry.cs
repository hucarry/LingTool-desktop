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
    private List<ToolItem> _tools = new();

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
            return _tools.Select(CloneTool).ToList();
        }
    }

    public ToolItem? GetToolById(string toolId)
    {
        Reload();

        lock (_syncRoot)
        {
            var tool = _tools.FirstOrDefault(item => string.Equals(item.Id, toolId, StringComparison.Ordinal));
            return tool is null ? null : CloneTool(tool);
        }
    }

    public ToolItem AddTool(ToolItem source)
    {
        lock (_syncRoot)
        {
            var registryFile = ReadRegistryFile();
            var candidate = NormalizeDraft(source);

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

            var baseDirectory = Path.GetDirectoryName(_toolsFilePath) ?? Directory.GetCurrentDirectory();
            var validatedCandidate = ValidateTool(ToToolItem(candidate), baseDirectory);
            if (!validatedCandidate.Valid)
            {
                throw new InvalidOperationException(
                    validatedCandidate.ValidationMessage ?? "Tool configuration is invalid."
                );
            }

            registryFile.Tools.Add(candidate);
            SaveRegistryFile(registryFile);

            _tools = registryFile.Tools
                .Select(item => ValidateTool(ToToolItem(item), baseDirectory))
                .ToList();
            _lastLoadedUtc = DateTime.UtcNow;

            return CloneTool(validatedCandidate);
        }
    }

    public ToolItem UpdateTool(ToolItem source)
    {
        lock (_syncRoot)
        {
            var registryFile = ReadRegistryFile();
            var candidate = NormalizeDraft(source);

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

            var baseDirectory = Path.GetDirectoryName(_toolsFilePath) ?? Directory.GetCurrentDirectory();
            var validatedCandidate = ValidateTool(ToToolItem(candidate), baseDirectory);
            if (!validatedCandidate.Valid)
            {
                throw new InvalidOperationException(
                    validatedCandidate.ValidationMessage ?? "Tool configuration is invalid."
                );
            }

            registryFile.Tools[existingIndex] = candidate;
            SaveRegistryFile(registryFile);

            _tools = registryFile.Tools
                .Select(item => ValidateTool(ToToolItem(item), baseDirectory))
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

            var baseDirectory = Path.GetDirectoryName(_toolsFilePath) ?? Directory.GetCurrentDirectory();
            _tools = registryFile.Tools
                .Select(item => ValidateTool(ToToolItem(item), baseDirectory))
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
                _tools = new List<ToolItem>();
                _lastLoadedUtc = now;
                return;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(_toolsFilePath);
            if (!force && lastWriteUtc <= _lastLoadedUtc)
            {
                return;
            }

            var parsed = ReadRegistryFile();

            var baseDirectory = Path.GetDirectoryName(_toolsFilePath) ?? Directory.GetCurrentDirectory();
            _tools = parsed.Tools
                .Select(item => ValidateTool(ToToolItem(item), baseDirectory))
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

        // Keep initial config portable for published builds on any drive.
        var template = """
{
  "tools": []
}
""";

        var directory = Path.GetDirectoryName(_toolsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

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

    private static ToolDraft NormalizeDraft(ToolItem source)
    {
        return new ToolDraft
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
            ArgsTemplate = source.ArgsTemplate?.Trim() ?? string.Empty,
            Tags = (source.Tags ?? new List<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Description = string.IsNullOrWhiteSpace(source.Description) ? null : source.Description.Trim()
        };
    }

    private static ToolItem ToToolItem(ToolDraft source)
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
            Tags = source.Tags?.ToList() ?? new List<string>(),
            Description = source.Description
        };
    }

    private static ToolItem ValidateTool(ToolItem source, string baseDirectory)
    {
        var tool = CloneTool(source);
        tool.Id = tool.Id.Trim();
        tool.Name = string.IsNullOrWhiteSpace(tool.Name) ? tool.Id : tool.Name.Trim();
        tool.Type = NormalizeToolType(tool.Type);
        tool.RuntimePath = string.IsNullOrWhiteSpace(tool.RuntimePath)
            ? (string.IsNullOrWhiteSpace(tool.Python) ? null : tool.Python.Trim())
            : tool.RuntimePath.Trim();
        tool.Python = null;
        tool.ArgsTemplate ??= string.Empty;
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
            Tags = source.Tags?.ToList() ?? new List<string>(),
            Description = source.Description,
            PathExists = source.PathExists,
            Valid = source.Valid,
            ValidationMessage = source.ValidationMessage
        };
    }

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

        return file.Tools.All(tool =>
        {
            var normalizedPath = (tool.Path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            var normalizedCwd = (tool.Cwd ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
            var normalizedPython = (tool.Python ?? string.Empty).Replace('\\', '/').ToLowerInvariant();

            return normalizedPath.StartsWith("d:/tools/")
                || normalizedCwd.StartsWith("d:/tools")
                || normalizedPython.StartsWith("d:/tools/");
        });
    }

    private sealed class ToolRegistryFile
    {
        public List<ToolDraft> Tools { get; set; } = new();
    }

    private sealed class ToolDraft
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string? RuntimePath { get; set; }

        // Legacy field for backwards-compatible tools.json reads.
        public string? Python { get; set; }

        public string? Cwd { get; set; }

        public string ArgsTemplate { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = new();

        public string? Description { get; set; }
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
