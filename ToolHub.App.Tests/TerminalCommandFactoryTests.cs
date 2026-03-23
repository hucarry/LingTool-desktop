using ToolHub.App;
using ToolHub.App.Models;
using ToolHub.App.Runtime;

namespace ToolHub.App.Tests;

public sealed class TerminalCommandFactoryTests
{
    [Fact]
    public void GetShellExecutableName_ShouldHandleQuotedCommand()
    {
        var name = TerminalShellUtilities.GetShellExecutableName("\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\" -NoLogo");
        Assert.Equal("pwsh.exe", name);
    }

    [Fact]
    public void BuildRunCommand_ShouldGenerateGenericShellCommand()
    {
        var command = TerminalCommandFactory.BuildRunCommand(
            "/bin/bash",
            new RunnableTool
            {
                Id = "exe_demo",
                Name = "Executable Demo",
                Type = "executable",
                Path = @"D:\apps\demo.exe",
                Cwd = @"D:\apps",
                ArgsSpec = new ArgsSpecV1
                {
                    Version = 1,
                    Fields =
                    [
                        new ArgFieldSpec { Name = "value", Kind = "text" }
                    ],
                    Argv =
                    [
                        new ArgTokenSpec { Kind = "literal", Value = "--flag" },
                        new ArgTokenSpec { Kind = "field", Field = "value" }
                    ]
                }
            },
            new Dictionary<string, string?> { ["value"] = "ok" },
            null
        );

        Assert.Equal("cd 'D:\\apps' && 'D:\\apps\\demo.exe' '--flag' 'ok'\r", command);
    }

    [Fact]
    public void BuildRunCommand_ShouldEscapeGenericShellArguments()
    {
        var command = TerminalCommandFactory.BuildRunCommand(
            "/bin/bash",
            new RunnableTool
            {
                Id = "sh_demo",
                Name = "Shell Demo",
                Type = "executable",
                Path = "/tmp/o'connor/demo.sh",
                Cwd = "/tmp/o'connor",
                ArgsTemplate = "--flag {value}"
            },
            new Dictionary<string, string?> { ["value"] = "$(touch /tmp/pwned) it's" },
            null
        );

        Assert.Equal("cd '/tmp/o'\"'\"'connor' && '/tmp/o'\"'\"'connor/demo.sh' '--flag' '$(touch /tmp/pwned) it'\"'\"'s'\r", command);
    }

    [Fact]
    public void BuildRunCommand_ShouldGeneratePowerShellScriptInvocation()
    {
        var explicitRuntime = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe"
        );
        Assert.True(File.Exists(explicitRuntime));

        var command = TerminalCommandFactory.BuildRunCommand(
            "powershell.exe",
            new RunnableTool
            {
                Id = "py_term",
                Name = "Python Terminal Demo",
                Type = "python",
                Path = @"D:\tools\demo.py",
                Cwd = @"D:\tools",
                ArgsTemplate = "--flag {value}"
            },
            new Dictionary<string, string?> { ["value"] = "ok" },
            explicitRuntime
        );

        Assert.Contains(". \"$env:TEMP\\th\\", command, StringComparison.OrdinalIgnoreCase);

        var script = ReadGeneratedScript(command, "$env:TEMP\\th\\");
        Assert.Contains("Set-Location -LiteralPath 'D:\\tools' -ErrorAction Stop", script);
        Assert.Contains("function global:python", script);
        Assert.Contains("'D:\\tools\\demo.py' '--flag' 'ok'", script);
    }

    [Fact]
    public void BuildRunCommand_ShouldEscapePowerShellArguments()
    {
        var command = TerminalCommandFactory.BuildRunCommand(
            "powershell.exe",
            new ResolvedRunCommand
            {
                ToolType = "executable",
                CommandPath = @"D:\tools\demo.exe",
                WorkingDirectory = @"D:\tools",
                WorkingDirectoryOverride = @"D:\tools",
                Arguments =
                [
                    "--flag",
                    "$(Get-ChildItem) O'Brien"
                ]
            }
        );

        var script = ReadGeneratedScript(command, "$env:TEMP\\th\\");
        Assert.Contains("'D:\\tools\\demo.exe' '--flag' '$(Get-ChildItem) O''Brien'", script);
    }

    [Fact]
    public void BuildRunCommand_ShouldGenerateCmdScriptInvocationForNode()
    {
        var explicitRuntime = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe"
        );
        Assert.True(File.Exists(explicitRuntime));

        var command = TerminalCommandFactory.BuildRunCommand(
            "cmd.exe",
            new RunnableTool
            {
                Id = "node_term",
                Name = "Node Terminal Demo",
                Type = "node",
                Path = @"D:\tools\index.js",
                Cwd = @"D:\tools",
                ArgsTemplate = "--mode {mode}"
            },
            new Dictionary<string, string?> { ["mode"] = "dev" },
            explicitRuntime
        );

        Assert.Contains("call \"%TEMP%\\th\\", command, StringComparison.OrdinalIgnoreCase);

        var script = ReadGeneratedScript(command, "%TEMP%\\th\\");
        Assert.Contains("@setlocal DisableDelayedExpansion", script);
        Assert.Contains("@echo off", script);
        Assert.Contains("cd /d \"D:\\tools\"", script);
        Assert.Contains("\"D:\\tools\\index.js\" \"--mode\" \"dev\"", script);
        Assert.Contains("set \"PATH=", script);
    }

    [Fact]
    public void BuildRunCommand_ShouldEscapeCmdArguments()
    {
        var command = TerminalCommandFactory.BuildRunCommand(
            "cmd.exe",
            new ResolvedRunCommand
            {
                ToolType = "executable",
                CommandPath = @"D:\tools\demo.exe",
                WorkingDirectory = @"D:\tools",
                WorkingDirectoryOverride = @"D:\tools",
                Arguments =
                [
                    "--flag",
                    "%TEMP%!USER! & del C:\\temp\\*.log"
                ]
            }
        );

        var script = ReadGeneratedScript(command, "%TEMP%\\th\\");
        Assert.Contains("@setlocal DisableDelayedExpansion", script);
        Assert.Contains("\"D:\\tools\\demo.exe\" \"--flag\" \"%%TEMP%%!USER! & del C:\\temp\\*.log\"", script);
    }

    private static string ReadGeneratedScript(string command, string marker)
    {
        var startIndex = command.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        Assert.True(startIndex >= 0);

        startIndex += marker.Length;
        var endIndex = command.IndexOf('"', startIndex);
        Assert.True(endIndex > startIndex);

        var scriptName = command[startIndex..endIndex];
        var scriptPath = Path.Combine(Path.GetTempPath(), "th", scriptName);
        Assert.True(File.Exists(scriptPath), $"Generated script was not found: {scriptPath}");

        return File.ReadAllText(scriptPath);
    }
}
