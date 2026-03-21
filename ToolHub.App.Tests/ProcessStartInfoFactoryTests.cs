using ToolHub.App;
using ToolHub.App.Models;
using ToolHub.App.Runtime;

namespace ToolHub.App.Tests;

public sealed class ProcessStartInfoFactoryTests
{
    [Fact]
    public void Build_ShouldComposePythonStartInfo()
    {
        var explicitRuntime = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe"
        );
        Assert.True(File.Exists(explicitRuntime));

        var tool = new RunnableTool
        {
            Id = "py_demo",
            Name = "Python Demo",
            Type = "python",
            Path = @"D:\tools\demo.py",
            Cwd = @"D:\tools",
            ArgsTemplate = "--name {name}"
        };

        var command = RunCommandBuilder.Build(
            tool,
            new Dictionary<string, string?> { ["name"] = "demo" },
            explicitRuntime
        );

        var startInfo = ProcessStartInfoFactory.Build(command);

        Assert.Equal(Path.GetFullPath(explicitRuntime), startInfo.FileName);
        Assert.Equal(tool.Cwd, startInfo.WorkingDirectory);
        Assert.Equal(3, startInfo.ArgumentList.Count);
        Assert.Equal(tool.Path, startInfo.ArgumentList[0]);
        Assert.Equal("--name", startInfo.ArgumentList[1]);
        Assert.Equal("demo", startInfo.ArgumentList[2]);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void Build_ShouldComposeNodeStartInfo()
    {
        var explicitRuntime = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe"
        );
        Assert.True(File.Exists(explicitRuntime));

        var tool = new RunnableTool
        {
            Id = "node_demo",
            Name = "Node Demo",
            Type = "node",
            Path = @"D:\tools\index.js",
            Cwd = @"D:\tools",
            ArgsTemplate = "--watch {mode}"
        };

        var command = RunCommandBuilder.Build(
            tool,
            new Dictionary<string, string?> { ["mode"] = "full" },
            explicitRuntime
        );

        var startInfo = ProcessStartInfoFactory.Build(command);

        Assert.Equal(Path.GetFullPath(explicitRuntime), startInfo.FileName);
        Assert.Equal(tool.Cwd, startInfo.WorkingDirectory);
        Assert.Equal([tool.Path, "--watch", "full"], startInfo.ArgumentList);
    }

    [Fact]
    public void Build_ShouldComposeCommandStartInfo()
    {
        var tool = new RunnableTool
        {
            Id = "cmd_demo",
            Name = "Command Demo",
            Type = "command",
            Path = "git",
            ArgsSpec = new ArgsSpecV1
            {
                Version = 1,
                Fields =
                [
                    new ArgFieldSpec { Name = "short", Kind = "flag" }
                ],
                Argv =
                [
                    new ArgTokenSpec { Kind = "literal", Value = "status" },
                    new ArgTokenSpec { Kind = "switch", Field = "short", WhenTrue = "--short" }
                ]
            }
        };

        var command = RunCommandBuilder.Build(
            tool,
            new Dictionary<string, string?> { ["short"] = "true" },
            null
        );

        var startInfo = ProcessStartInfoFactory.Build(command);

        Assert.Equal("git", startInfo.FileName);
        Assert.Equal(Directory.GetCurrentDirectory(), startInfo.WorkingDirectory);
        Assert.Equal(["status", "--short"], startInfo.ArgumentList);
    }

    [Fact]
    public void Build_ShouldComposeExecutableStartInfo()
    {
        var tool = new RunnableTool
        {
            Id = "exe_demo",
            Name = "Executable Demo",
            Type = "executable",
            Path = @"D:\apps\demo.exe",
            Cwd = @"D:\apps",
            ArgsTemplate = "--flag {value}"
        };

        var command = RunCommandBuilder.Build(
            tool,
            new Dictionary<string, string?> { ["value"] = "ok" },
            null
        );

        var startInfo = ProcessStartInfoFactory.Build(command);

        Assert.Equal(tool.Path, startInfo.FileName);
        Assert.Equal(tool.Cwd, startInfo.WorkingDirectory);
        Assert.Equal(["--flag", "ok"], startInfo.ArgumentList);
    }

    [Fact]
    public void Build_ShouldThrowForUnsupportedToolType()
    {
        var command = new ResolvedRunCommand
        {
            ToolType = "url",
            CommandPath = "https://example.com",
            Arguments = [],
            WorkingDirectory = Directory.GetCurrentDirectory(),
            WorkingDirectoryOverride = null,
            RuntimePath = null
        };

        var exception = Assert.Throws<NotSupportedException>(() => ProcessStartInfoFactory.Build(command));
        Assert.Equal(RuntimeErrorMessages.UnsupportedToolType("url"), exception.Message);
    }
}
