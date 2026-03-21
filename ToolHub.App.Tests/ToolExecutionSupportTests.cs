using System.Text.Json;
using ToolHub.App;
using ToolHub.App.Models;

namespace ToolHub.App.Tests;

public sealed class ToolExecutionSupportTests
{
    [Fact]
    public void CreateRunnableTool_ShouldProjectToolItem()
    {
        var support = new ToolExecutionSupport(new StubRegistry());
        var source = new ToolItem
        {
            Id = "py_demo",
            Name = "Python Demo",
            Type = "python",
            Path = @"D:\tools\demo.py",
            RuntimePath = @"D:\python\python.exe",
            Python = @"legacy\python.exe",
            Cwd = @"D:\tools",
            ArgsTemplate = "--name {{name}}",
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
            Tags = ["alpha", "beta"],
            Description = "demo",
            PathExists = true,
            Valid = true,
            ValidationMessage = "unused"
        };

        var runnable = support.CreateRunnableTool(source);

        Assert.Equal(source.Id, runnable.Id);
        Assert.Equal(source.Name, runnable.Name);
        Assert.Equal(source.Type, runnable.Type);
        Assert.Equal(source.Path, runnable.Path);
        Assert.Equal(source.RuntimePath, runnable.RuntimePath);
        Assert.Equal(source.Cwd, runnable.Cwd);
        Assert.Equal(source.ArgsTemplate, runnable.ArgsTemplate);
        Assert.NotNull(runnable.ArgsSpec);
        Assert.Equal("name", runnable.ArgsSpec!.Fields[0].Name);
    }

    [Fact]
    public void TryResolveRunnableTool_ShouldEmitError_WhenToolMissing()
    {
        var sent = new List<object>();
        var support = new ToolExecutionSupport(new StubRegistry());
        var context = CreateContext(sent);

        var resolved = support.TryResolveRunnableTool(context, "missing-tool", out _);

        Assert.False(resolved);
        var error = Assert.IsType<ErrorMessage>(sent.Single());
        Assert.Equal(ToolErrorMessages.ToolNotFound("missing-tool"), error.Message);
    }

    [Fact]
    public void TryBuildResolvedRunCommand_ShouldComposeCommandRun()
    {
        var sent = new List<object>();
        var support = new ToolExecutionSupport(new StubRegistry());
        var context = CreateContext(sent);
        var tool = new RunnableTool
        {
            Id = "cmd_demo",
            Name = "Command Demo",
            Type = "command",
            Path = "echo",
            Cwd = @"D:\workspace",
            ArgsTemplate = "--flag {value}"
        };

        var built = support.TryBuildResolvedRunCommand(
            context,
            tool,
            new Dictionary<string, string?> { ["value"] = "ok" },
            @"runtime\tool.exe",
            out var resolvedCommand
        );

        Assert.True(built);
        Assert.NotNull(resolvedCommand);
        Assert.Equal("echo", resolvedCommand.CommandPath);
        Assert.Equal(@"D:\workspace", resolvedCommand.WorkingDirectory);
        Assert.Equal(@"D:\workspace", resolvedCommand.WorkingDirectoryOverride);
        Assert.Null(resolvedCommand.RuntimePath);
        Assert.Equal(["--flag", "ok"], resolvedCommand.Arguments);
        Assert.Empty(sent);
    }

    [Fact]
    public void TryBuildResolvedRunCommand_ShouldEmitError_ForUnsupportedType()
    {
        var sent = new List<object>();
        var support = new ToolExecutionSupport(new StubRegistry());
        var context = CreateContext(sent);
        var tool = new RunnableTool
        {
            Id = "url_demo",
            Name = "Url Demo",
            Type = "url",
            Path = "https://example.com"
        };

        var built = support.TryBuildResolvedRunCommand(
            context,
            tool,
            new Dictionary<string, string?>(),
            null,
            out _
        );

        Assert.False(built);
        var error = Assert.IsType<ErrorMessage>(sent.Single());
        Assert.Equal(RuntimeErrorMessages.UnsupportedToolType("url"), error.Message);
    }

    private static MessageContext CreateContext(List<object> sent)
    {
        return new MessageContext(
            sent.Add,
            static _ => null,
            static (_, _, _) => null,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
    }

    private sealed class StubRegistry(ToolItem? tool = null) : IToolRegistry
    {
        public ToolItem? GetToolById(string toolId) => tool;

        public IReadOnlyList<ToolItem> GetTools() => throw new NotSupportedException();

        public ToolItem AddTool(ToolDefinition source) => throw new NotSupportedException();

        public ToolItem UpdateTool(ToolDefinition source) => throw new NotSupportedException();

        public int DeleteTools(IReadOnlyCollection<string> toolIds) => throw new NotSupportedException();
    }
}
