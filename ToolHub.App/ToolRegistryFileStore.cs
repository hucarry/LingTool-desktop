using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

internal sealed class ToolRegistryFileStore(string toolsFilePath, JsonSerializerOptions jsonOptions)
{
    internal void EnsureExists()
    {
        if (File.Exists(toolsFilePath))
        {
            return;
        }

        var baseDirectory = Path.GetDirectoryName(toolsFilePath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(baseDirectory);

        var exampleFilePath = Path.Combine(baseDirectory, "tools.example.json");
        if (File.Exists(exampleFilePath))
        {
            File.Copy(exampleFilePath, toolsFilePath);
            return;
        }

        var template = """
{
  "tools": []
}
""";

        File.WriteAllText(toolsFilePath, template);
    }

    internal ToolRegistryFile Read()
    {
        EnsureExists();

        var fileContent = File.ReadAllText(toolsFilePath);
        var parsed = JsonSerializer.Deserialize<ToolRegistryFile>(fileContent, jsonOptions) ?? new ToolRegistryFile();

        if (ToolRegistryMapper.IsLegacyFixedPathTemplate(parsed))
        {
            parsed.Tools.Clear();
            Save(parsed);
        }

        return parsed;
    }

    internal void Save(ToolRegistryFile file)
    {
        var directory = Path.GetDirectoryName(toolsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var writeOptions = new JsonSerializerOptions(jsonOptions)
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(file, writeOptions);
        File.WriteAllText(toolsFilePath, json);
    }

    internal string GetBaseDirectory()
    {
        return Path.GetDirectoryName(toolsFilePath) ?? Directory.GetCurrentDirectory();
    }

    internal bool Exists()
    {
        return File.Exists(toolsFilePath);
    }

    internal DateTime GetLastWriteTimeUtc()
    {
        return File.GetLastWriteTimeUtc(toolsFilePath);
    }
}

internal sealed class ToolRegistryFile
{
    public List<ToolDefinition> Tools { get; set; } = new();
}
