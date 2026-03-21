using System.Text.Json;
using ToolHub.App;
using ToolHub.App.Models;

namespace ToolHub.App.Tests;

public sealed class ToolRegistryTests
{
    [Fact]
    public void AddTool_ShouldPersistStructuredArgs_AndExcludeDerivedFields()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "toolhub-registry-tests", Guid.NewGuid().ToString("N"));
        var toolsDirectory = Path.Combine(workspaceRoot, "Tools");
        Directory.CreateDirectory(toolsDirectory);

        try
        {
            var scriptPath = Path.Combine(toolsDirectory, "demo.py");
            File.WriteAllText(scriptPath, "print('ok')");

            var toolsFilePath = Path.Combine(workspaceRoot, "tools.json");
            var registry = new ToolRegistry(toolsFilePath, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            var added = registry.AddTool(new ToolDefinition
            {
                Id = "structured_py",
                Name = "Structured Py",
                Type = "python",
                Path = @"Tools\demo.py",
                ArgsSpec = new ArgsSpecV1
                {
                    Version = 1,
                    Fields =
                    [
                        new ArgFieldSpec { Name = "input", Kind = "path", Required = true }
                    ],
                    Argv =
                    [
                        new ArgTokenSpec { Kind = "literal", Value = "--input" },
                        new ArgTokenSpec { Kind = "field", Field = "input" }
                    ]
                }
            });

            Assert.Equal("--input {input}", added.ArgsTemplate);
            Assert.NotNull(added.ArgsSpec);
            Assert.Equal(scriptPath, added.Path);

            var persistedJson = File.ReadAllText(toolsFilePath);
            Assert.DoesNotContain("\"valid\"", persistedJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"pathExists\"", persistedJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"validationMessage\"", persistedJson, StringComparison.OrdinalIgnoreCase);

            var reloadedRegistry = new ToolRegistry(toolsFilePath, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var stored = reloadedRegistry.GetToolById("structured_py");

            Assert.NotNull(stored);
            Assert.Equal(scriptPath, stored!.Path);
            Assert.Equal("--input {input}", stored.ArgsTemplate);
            Assert.NotNull(stored.ArgsSpec);
            Assert.True(stored.Valid);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void GetToolById_ShouldProjectInvalidStoredDefinition()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "toolhub-registry-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var toolsFilePath = Path.Combine(workspaceRoot, "tools.json");
            var seededJson = """
{
  "tools": [
    {
      "id": "missing_py",
      "name": "Missing Py",
      "type": "python",
      "path": "Tools/missing.py",
      "runtimePath": "Tools/missing-python.exe",
      "argsTemplate": "",
      "tags": ["ops"]
    }
  ]
}
""";
            File.WriteAllText(toolsFilePath, seededJson);

            var registry = new ToolRegistry(toolsFilePath, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var tool = registry.GetToolById("missing_py");

            Assert.NotNull(tool);
            Assert.Equal(Path.Combine(workspaceRoot, "Tools", "missing.py"), tool!.Path);
            Assert.Equal(workspaceRoot, tool.Cwd);
            Assert.False(tool.PathExists);
            Assert.False(tool.Valid);
            Assert.Null(tool.RuntimePath);
            Assert.Contains("missing.py", tool.ValidationMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
