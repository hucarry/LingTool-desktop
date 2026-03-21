using System.Text.Json;
using ToolHub.App;
using ToolHub.App.Models;

namespace ToolHub.App.Tests;

public sealed class ToolRegistryMapperTests
{
    [Fact]
    public void NormalizeDefinition_ShouldTrimFields_AndInferLegacyTemplate()
    {
        var source = new ToolDefinition
        {
            Id = " demo_py ",
            Name = " Demo Tool ",
            Type = " exe ",
            Path = " Tools\\demo.exe ",
            RuntimePath = " python.exe ",
            ArgsSpec = new ArgsSpecV1
            {
                Version = 1,
                Fields =
                [
                    new ArgFieldSpec { Name = "name", Kind = "text" }
                ],
                Argv =
                [
                    new ArgTokenSpec { Kind = "literal", Value = "--name" },
                    new ArgTokenSpec { Kind = "field", Field = "name" }
                ]
            },
            Tags = [" alpha ", "alpha", "beta "],
            Description = " demo "
        };

        var normalized = ToolRegistryMapper.NormalizeDefinition(source);

        Assert.Equal("demo_py", normalized.Id);
        Assert.Equal("Demo Tool", normalized.Name);
        Assert.Equal("executable", normalized.Type);
        Assert.Equal("Tools\\demo.exe", normalized.Path);
        Assert.Equal("python.exe", normalized.RuntimePath);
        Assert.Equal("--name {name}", normalized.ArgsTemplate);
        Assert.Equal(["alpha", "beta"], normalized.Tags);
        Assert.Equal("demo", normalized.Description);
    }

    [Fact]
    public void BuildToolView_ShouldResolveRelativePath_AndDefaultWorkingDirectory()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "toolhub-mapper-tests", Guid.NewGuid().ToString("N"));
        var toolsDirectory = Path.Combine(workspaceRoot, "Tools");
        Directory.CreateDirectory(toolsDirectory);

        try
        {
            var scriptPath = Path.Combine(toolsDirectory, "demo.py");
            File.WriteAllText(scriptPath, "print('ok')");

            var normalized = ToolRegistryMapper.NormalizeDefinition(new ToolDefinition
            {
                Id = "demo_py",
                Name = "",
                Type = "python",
                Path = @"Tools\demo.py",
                ArgsTemplate = " --flag {mode} "
            });

            var view = ToolRegistryMapper.BuildToolView(normalized, workspaceRoot);

            Assert.Equal(scriptPath, view.Path);
            Assert.Equal("demo_py", view.Name);
            Assert.Equal(toolsDirectory, view.Cwd);
            Assert.True(view.PathExists);
            Assert.True(view.Valid);
            Assert.Null(view.ValidationMessage);
            Assert.NotNull(view.ArgsSpec);
            Assert.Equal("mode", view.ArgsSpec!.Fields[0].Name);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
