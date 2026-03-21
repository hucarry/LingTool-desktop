using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

public sealed class ToolRegistry : IToolRegistry
{
    private static readonly TimeSpan MinReloadInterval = TimeSpan.FromSeconds(1);

    private readonly ToolRegistryFileStore _fileStore;
    private readonly object _syncRoot = new();

    private DateTime _lastLoadedUtc = DateTime.MinValue;
    private DateTime _lastCheckedUtc = DateTime.MinValue;
    private List<ToolDefinition> _tools = new();

    public ToolRegistry(string toolsFilePath, JsonSerializerOptions jsonOptions)
    {
        _fileStore = new ToolRegistryFileStore(toolsFilePath, jsonOptions);

        _fileStore.EnsureExists();
        Reload(force: true);
    }

    public IReadOnlyList<ToolItem> GetTools()
    {
        Reload();
        lock (_syncRoot)
        {
            var baseDirectory = _fileStore.GetBaseDirectory();
            return _tools
                .Select(tool => ToolRegistryMapper.BuildToolView(tool, baseDirectory))
                .ToList();
        }
    }

    public ToolItem? GetToolById(string toolId)
    {
        Reload();

        lock (_syncRoot)
        {
            var tool = _tools.FirstOrDefault(item => string.Equals(item.Id, toolId, StringComparison.Ordinal));
            return tool is null
                ? null
                : ToolRegistryMapper.BuildToolView(tool, _fileStore.GetBaseDirectory());
        }
    }

    public ToolItem AddTool(ToolDefinition source)
    {
        lock (_syncRoot)
        {
            var registryFile = _fileStore.Read();
            var candidate = ToolRegistryMapper.NormalizeDefinition(source);

            if (string.IsNullOrWhiteSpace(candidate.Id))
            {
                throw new InvalidOperationException(ToolErrorMessages.ToolIdRequired);
            }

            var duplicate = registryFile.Tools.Any(item =>
                string.Equals(item.Id?.Trim(), candidate.Id, StringComparison.OrdinalIgnoreCase)
            );
            if (duplicate)
            {
                throw new InvalidOperationException(ToolErrorMessages.ToolIdAlreadyExists(candidate.Id));
            }

            var validatedCandidate = ToolRegistryMapper.BuildToolView(candidate, _fileStore.GetBaseDirectory());
            if (!validatedCandidate.Valid)
            {
                throw new InvalidOperationException(
                    validatedCandidate.ValidationMessage ?? ToolErrorMessages.ToolConfigurationInvalid
                );
            }

            registryFile.Tools.Add(candidate);
            _fileStore.Save(registryFile);

            _tools = registryFile.Tools
                .Select(ToolRegistryMapper.NormalizeDefinition)
                .ToList();
            _lastLoadedUtc = DateTime.UtcNow;

            return ToolRegistryMapper.CloneTool(validatedCandidate);
        }
    }

    public ToolItem UpdateTool(ToolDefinition source)
    {
        lock (_syncRoot)
        {
            var registryFile = _fileStore.Read();
            var candidate = ToolRegistryMapper.NormalizeDefinition(source);

            if (string.IsNullOrWhiteSpace(candidate.Id))
            {
                throw new InvalidOperationException(ToolErrorMessages.ToolIdRequired);
            }

            var existingIndex = registryFile.Tools.FindIndex(item =>
                string.Equals(item.Id?.Trim(), candidate.Id, StringComparison.OrdinalIgnoreCase)
            );
            if (existingIndex < 0)
            {
                throw new InvalidOperationException(ToolErrorMessages.ToolIdNotFound(candidate.Id));
            }

            var validatedCandidate = ToolRegistryMapper.BuildToolView(candidate, _fileStore.GetBaseDirectory());
            if (!validatedCandidate.Valid)
            {
                throw new InvalidOperationException(
                    validatedCandidate.ValidationMessage ?? ToolErrorMessages.ToolConfigurationInvalid
                );
            }

            registryFile.Tools[existingIndex] = candidate;
            _fileStore.Save(registryFile);

            _tools = registryFile.Tools
                .Select(ToolRegistryMapper.NormalizeDefinition)
                .ToList();
            _lastLoadedUtc = DateTime.UtcNow;

            return ToolRegistryMapper.CloneTool(validatedCandidate);
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

            var registryFile = _fileStore.Read();
            var before = registryFile.Tools.Count;
            registryFile.Tools = registryFile.Tools
                .Where(item => !normalizedIds.Contains((item.Id ?? string.Empty).Trim()))
                .ToList();

            var deleted = before - registryFile.Tools.Count;
            if (deleted <= 0)
            {
                return 0;
            }

            _fileStore.Save(registryFile);

            _tools = registryFile.Tools
                .Select(ToolRegistryMapper.NormalizeDefinition)
                .ToList();
            _lastLoadedUtc = DateTime.UtcNow;

            return deleted;
        }
    }

    private void Reload(bool force = false)
    {
        lock (_syncRoot)
        {
            var now = DateTime.UtcNow;
            if (!force && (now - _lastCheckedUtc) < MinReloadInterval)
            {
                return;
            }

            _lastCheckedUtc = now;

            if (!_fileStore.Exists())
            {
                _tools = new List<ToolDefinition>();
                _lastLoadedUtc = now;
                return;
            }

            var lastWriteUtc = _fileStore.GetLastWriteTimeUtc();
            if (!force && lastWriteUtc <= _lastLoadedUtc)
            {
                return;
            }

            var parsed = _fileStore.Read();
            _tools = parsed.Tools
                .Select(ToolRegistryMapper.NormalizeDefinition)
                .ToList();

            _lastLoadedUtc = now;
        }
    }
}
