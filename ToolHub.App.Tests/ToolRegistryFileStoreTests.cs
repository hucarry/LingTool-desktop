using System.Text.Json;
using ToolHub.App;
using ToolHub.App.Models;

namespace ToolHub.App.Tests;

public sealed class ToolRegistryFileStoreTests
{
    [Fact]
    public void EnsureExists_ShouldCreateDefaultTemplate_WhenExampleDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "toolhub-filestore-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var toolsFilePath = Path.Combine(root, "tools.json");
            var store = new ToolRegistryFileStore(toolsFilePath, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            store.EnsureExists();

            Assert.True(File.Exists(toolsFilePath));
            var content = File.ReadAllText(toolsFilePath);
            Assert.Contains("\"tools\": []", content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Read_ShouldClearLegacyFixedPathTemplate()
    {
        var root = Path.Combine(Path.GetTempPath(), "toolhub-filestore-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var toolsFilePath = Path.Combine(root, "tools.json");
            var legacy = """
{
  "tools": [
    {
      "id": "demo_py",
      "name": "Demo Py",
      "type": "python",
      "path": "D:/tools/demo.py",
      "runtimePath": "D:/tools/python.exe",
      "cwd": "D:/tools"
    },
    {
      "id": "demo_exe",
      "name": "Demo Exe",
      "type": "executable",
      "path": "D:/tools/demo.exe",
      "cwd": "D:/tools"
    }
  ]
}
""";
            File.WriteAllText(toolsFilePath, legacy);

            var store = new ToolRegistryFileStore(toolsFilePath, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var parsed = store.Read();

            Assert.Empty(parsed.Tools);

            var saved = File.ReadAllText(toolsFilePath);
            Assert.Contains("\"tools\": []", saved, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
