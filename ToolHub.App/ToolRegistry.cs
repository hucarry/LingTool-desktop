using System.Text.Json;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class ToolRegistry
{
    private readonly string _toolsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _syncRoot = new();

    private DateTime _lastLoadedUtc = DateTime.MinValue;
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

    private void Reload(bool force = false)
    {
        lock (_syncRoot)
        {
            if (!File.Exists(_toolsFilePath))
            {
                _tools = new List<ToolItem>();
                _lastLoadedUtc = DateTime.UtcNow;
                return;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(_toolsFilePath);
            if (!force && lastWriteUtc <= _lastLoadedUtc)
            {
                return;
            }

            var parsed = ReadRegistryFile();

            var baseDirectory = Path.GetDirectoryName(_toolsFilePath) ?? Directory.GetCurrentDirectory();
            var validated = parsed.Tools
                .Select(item => ValidateTool(ToToolItem(item), baseDirectory))
                .ToList();

            _tools = validated;
            _lastLoadedUtc = DateTime.UtcNow;
        }
    }

    private void EnsureToolsFileExists()
    {
        if (File.Exists(_toolsFilePath))
        {
            return;
        }

        var template = """
{
  "tools": [
    {
      "id": "demo_py",
      "name": "示例 Python 工具",
      "type": "python",
      "path": "D:/tools/demo.py",
      "python": "D:/tools/venv/Scripts/python.exe",
      "cwd": "D:/tools",
      "argsTemplate": "--date {date} --mode {mode}",
      "tags": ["数据"]
    },
    {
      "id": "demo_exe",
      "name": "示例 EXE 工具",
      "type": "exe",
      "path": "D:/tools/demo.exe",
      "cwd": "D:/tools",
      "argsTemplate": "",
      "tags": ["系统"]
    }
  ]
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
        return JsonSerializer.Deserialize<ToolRegistryFile>(fileContent, _jsonOptions) ?? new ToolRegistryFile();
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
            Type = source.Type.Trim().ToLowerInvariant(),
            Path = source.Path.Trim(),
            Python = string.IsNullOrWhiteSpace(source.Python) ? null : source.Python.Trim(),
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
            Type = source.Type ?? string.Empty,
            Path = source.Path ?? string.Empty,
            Python = source.Python,
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
        tool.Type = tool.Type.Trim().ToLowerInvariant();
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

        if (tool.Type is not ("python" or "exe"))
        {
            errors.Add("type 只支持 python 或 exe");
        }

        if (string.IsNullOrWhiteSpace(tool.Path))
        {
            errors.Add("path 不能为空");
        }
        else
        {
            tool.Path = PathUtils.ResolvePath(tool.Path, baseDirectory);
            tool.PathExists = File.Exists(tool.Path);
            if (!tool.PathExists)
            {
                errors.Add($"工具路径不存在: {tool.Path}");
            }
        }

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

        if (tool.Type == "python" && !string.IsNullOrWhiteSpace(tool.Python))
        {
            tool.Python = PathUtils.ResolvePath(tool.Python, baseDirectory);
            if (!File.Exists(tool.Python))
            {
                errors.Add($"Python 解释器不存在: {tool.Python}");
            }
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

        public string? Python { get; set; }

        public string? Cwd { get; set; }

        public string ArgsTemplate { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = new();

        public string? Description { get; set; }
    }
}
