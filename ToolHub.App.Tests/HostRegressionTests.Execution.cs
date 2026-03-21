using System.Collections.Generic;
using System.IO;
using ToolHub.App;
using ToolHub.App.Models;
using ToolHub.App.Runtime;

#nullable enable

internal static partial class HostRegressionTests
{
    private static void ResolvedRunCommand_ShouldUnifyRuntimeAndArguments()
    {
        var explicitRuntime = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe"
        );
        AssertTrue(File.Exists(explicitRuntime), "A stable explicit runtime should exist on Windows.");

        var pythonTool = new RunnableTool
        {
            Id = "py_demo",
            Name = "Python Demo",
            Type = "python",
            Path = @"D:\tools\demo.py",
            Cwd = @"D:\tools",
            ArgsTemplate = "--name {name} --count {count}"
        };
        var pythonCommand = RunCommandBuilder.Build(
            pythonTool,
            new Dictionary<string, string?> { ["name"] = "demo", ["count"] = "2" },
            explicitRuntime
        );

        AssertEqual("python", pythonCommand.ToolType, "Resolved command should preserve tool type.");
        AssertEqual(Path.GetFullPath(explicitRuntime), pythonCommand.CommandPath, "Resolved command should use the resolved runtime.");
        AssertEqual(pythonTool.Cwd, pythonCommand.WorkingDirectory, "Resolved command should preserve cwd.");
        AssertEqual(pythonTool.Cwd, pythonCommand.WorkingDirectoryOverride, "Resolved command should preserve the explicit shell cwd.");
        AssertEqual(Path.GetFullPath(explicitRuntime), pythonCommand.RuntimePath, "Resolved command should expose the runtime for shell session setup.");
        AssertEqual(5, pythonCommand.Arguments.Count, "Resolved command should prepend the tool path before expanded args.");
        AssertEqual(pythonTool.Path, pythonCommand.Arguments[0], "Resolved command should prepend the tool path.");
        AssertEqual("--name", pythonCommand.Arguments[1], "Resolved command should preserve literal template tokens.");
        AssertEqual("demo", pythonCommand.Arguments[2], "Resolved command should substitute placeholder values.");
        AssertEqual("--count", pythonCommand.Arguments[3], "Resolved command should preserve later literal tokens.");
        AssertEqual("2", pythonCommand.Arguments[4], "Resolved command should substitute later placeholder values.");

        var commandTool = new RunnableTool
        {
            Id = "cmd_demo",
            Name = "Command Demo",
            Type = "command",
            Path = "git",
            ArgsTemplate = string.Empty,
            ArgsSpec = new ArgsSpecV1
            {
                Version = 1,
                Fields = new List<ArgFieldSpec>
                {
                    new() { Name = "short", Kind = "flag" }
                },
                Argv = new List<ArgTokenSpec>
                {
                    new() { Kind = "literal", Value = "status" },
                    new() { Kind = "switch", Field = "short", WhenTrue = "--short" }
                }
            }
        };
        var commandRun = RunCommandBuilder.Build(
            commandTool,
            new Dictionary<string, string?> { ["short"] = "true" },
            null
        );

        AssertEqual("git", commandRun.CommandPath, "Command tools should use the tool path directly.");
        AssertEqual(Directory.GetCurrentDirectory(), commandRun.WorkingDirectory, "Command tools should fall back to the current directory when cwd is absent.");
        AssertNull(commandRun.WorkingDirectoryOverride, "Command tools without cwd should not force a terminal cd.");
        AssertNull(commandRun.RuntimePath, "Command tools should not expose a runtime path.");
        AssertEqual("status", commandRun.Arguments[0], "Command tools should preserve literal args.");
        AssertEqual("--short", commandRun.Arguments[1], "Command tools should preserve switch args.");
    }
}
