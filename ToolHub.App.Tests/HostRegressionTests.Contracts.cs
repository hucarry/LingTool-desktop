using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ToolHub.App;
using ToolHub.App.Models;
using ToolHub.App.Utils;

#nullable enable

internal static partial class HostRegressionTests
{
    private static void BridgeMessageTypes_ShouldMatchStableContract()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(BridgeMessageTypes.GetTools)] = "getTools",
            [nameof(BridgeMessageTypes.AddTool)] = "addTool",
            [nameof(BridgeMessageTypes.UpdateTool)] = "updateTool",
            [nameof(BridgeMessageTypes.DeleteTools)] = "deleteTools",
            [nameof(BridgeMessageTypes.RunTool)] = "runTool",
            [nameof(BridgeMessageTypes.RunToolInTerminal)] = "runToolInTerminal",
            [nameof(BridgeMessageTypes.OpenUrlTool)] = "openUrlTool",
            [nameof(BridgeMessageTypes.StopRun)] = "stopRun",
            [nameof(BridgeMessageTypes.GetRuns)] = "getRuns",
            [nameof(BridgeMessageTypes.BrowsePython)] = "browsePython",
            [nameof(BridgeMessageTypes.BrowseFile)] = "browseFile",
            [nameof(BridgeMessageTypes.GetPythonPackages)] = "getPythonPackages",
            [nameof(BridgeMessageTypes.InstallPythonPackage)] = "installPythonPackage",
            [nameof(BridgeMessageTypes.UninstallPythonPackage)] = "uninstallPythonPackage",
            [nameof(BridgeMessageTypes.GetTerminals)] = "getTerminals",
            [nameof(BridgeMessageTypes.StartTerminal)] = "startTerminal",
            [nameof(BridgeMessageTypes.TerminalInput)] = "terminalInput",
            [nameof(BridgeMessageTypes.TerminalResize)] = "terminalResize",
            [nameof(BridgeMessageTypes.StopTerminal)] = "stopTerminal",
            [nameof(BridgeMessageTypes.GetAppDefaults)] = "getAppDefaults",
            [nameof(BridgeMessageTypes.Tools)] = "tools",
            [nameof(BridgeMessageTypes.RunStarted)] = "runStarted",
            [nameof(BridgeMessageTypes.Log)] = "log",
            [nameof(BridgeMessageTypes.RunStatus)] = "runStatus",
            [nameof(BridgeMessageTypes.Runs)] = "runs",
            [nameof(BridgeMessageTypes.PythonSelected)] = "pythonSelected",
            [nameof(BridgeMessageTypes.FileSelected)] = "fileSelected",
            [nameof(BridgeMessageTypes.ToolAdded)] = "toolAdded",
            [nameof(BridgeMessageTypes.ToolUpdated)] = "toolUpdated",
            [nameof(BridgeMessageTypes.ToolsDeleted)] = "toolsDeleted",
            [nameof(BridgeMessageTypes.PythonPackages)] = "pythonPackages",
            [nameof(BridgeMessageTypes.PythonPackageInstallStatus)] = "pythonPackageInstallStatus",
            [nameof(BridgeMessageTypes.Terminals)] = "terminals",
            [nameof(BridgeMessageTypes.TerminalStarted)] = "terminalStarted",
            [nameof(BridgeMessageTypes.TerminalOutput)] = "terminalOutput",
            [nameof(BridgeMessageTypes.TerminalStatus)] = "terminalStatus",
            [nameof(BridgeMessageTypes.AppDefaults)] = "appDefaults",
            [nameof(BridgeMessageTypes.Error)] = "error"
        };

        var actual = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(BridgeMessageTypes.GetTools)] = BridgeMessageTypes.GetTools,
            [nameof(BridgeMessageTypes.AddTool)] = BridgeMessageTypes.AddTool,
            [nameof(BridgeMessageTypes.UpdateTool)] = BridgeMessageTypes.UpdateTool,
            [nameof(BridgeMessageTypes.DeleteTools)] = BridgeMessageTypes.DeleteTools,
            [nameof(BridgeMessageTypes.RunTool)] = BridgeMessageTypes.RunTool,
            [nameof(BridgeMessageTypes.RunToolInTerminal)] = BridgeMessageTypes.RunToolInTerminal,
            [nameof(BridgeMessageTypes.OpenUrlTool)] = BridgeMessageTypes.OpenUrlTool,
            [nameof(BridgeMessageTypes.StopRun)] = BridgeMessageTypes.StopRun,
            [nameof(BridgeMessageTypes.GetRuns)] = BridgeMessageTypes.GetRuns,
            [nameof(BridgeMessageTypes.BrowsePython)] = BridgeMessageTypes.BrowsePython,
            [nameof(BridgeMessageTypes.BrowseFile)] = BridgeMessageTypes.BrowseFile,
            [nameof(BridgeMessageTypes.GetPythonPackages)] = BridgeMessageTypes.GetPythonPackages,
            [nameof(BridgeMessageTypes.InstallPythonPackage)] = BridgeMessageTypes.InstallPythonPackage,
            [nameof(BridgeMessageTypes.UninstallPythonPackage)] = BridgeMessageTypes.UninstallPythonPackage,
            [nameof(BridgeMessageTypes.GetTerminals)] = BridgeMessageTypes.GetTerminals,
            [nameof(BridgeMessageTypes.StartTerminal)] = BridgeMessageTypes.StartTerminal,
            [nameof(BridgeMessageTypes.TerminalInput)] = BridgeMessageTypes.TerminalInput,
            [nameof(BridgeMessageTypes.TerminalResize)] = BridgeMessageTypes.TerminalResize,
            [nameof(BridgeMessageTypes.StopTerminal)] = BridgeMessageTypes.StopTerminal,
            [nameof(BridgeMessageTypes.GetAppDefaults)] = BridgeMessageTypes.GetAppDefaults,
            [nameof(BridgeMessageTypes.Tools)] = BridgeMessageTypes.Tools,
            [nameof(BridgeMessageTypes.RunStarted)] = BridgeMessageTypes.RunStarted,
            [nameof(BridgeMessageTypes.Log)] = BridgeMessageTypes.Log,
            [nameof(BridgeMessageTypes.RunStatus)] = BridgeMessageTypes.RunStatus,
            [nameof(BridgeMessageTypes.Runs)] = BridgeMessageTypes.Runs,
            [nameof(BridgeMessageTypes.PythonSelected)] = BridgeMessageTypes.PythonSelected,
            [nameof(BridgeMessageTypes.FileSelected)] = BridgeMessageTypes.FileSelected,
            [nameof(BridgeMessageTypes.ToolAdded)] = BridgeMessageTypes.ToolAdded,
            [nameof(BridgeMessageTypes.ToolUpdated)] = BridgeMessageTypes.ToolUpdated,
            [nameof(BridgeMessageTypes.ToolsDeleted)] = BridgeMessageTypes.ToolsDeleted,
            [nameof(BridgeMessageTypes.PythonPackages)] = BridgeMessageTypes.PythonPackages,
            [nameof(BridgeMessageTypes.PythonPackageInstallStatus)] = BridgeMessageTypes.PythonPackageInstallStatus,
            [nameof(BridgeMessageTypes.Terminals)] = BridgeMessageTypes.Terminals,
            [nameof(BridgeMessageTypes.TerminalStarted)] = BridgeMessageTypes.TerminalStarted,
            [nameof(BridgeMessageTypes.TerminalOutput)] = BridgeMessageTypes.TerminalOutput,
            [nameof(BridgeMessageTypes.TerminalStatus)] = BridgeMessageTypes.TerminalStatus,
            [nameof(BridgeMessageTypes.AppDefaults)] = BridgeMessageTypes.AppDefaults,
            [nameof(BridgeMessageTypes.Error)] = BridgeMessageTypes.Error
        };

        AssertEqual(expected.Count, actual.Count, "Bridge message count changed unexpectedly.");
        foreach (var pair in expected)
        {
            AssertEqual(pair.Value, actual[pair.Key], $"Bridge message value changed for {pair.Key}.");
        }

        AssertEqual("stdout", LogChannels.Stdout, "LogChannels.Stdout changed unexpectedly.");
        AssertEqual("stderr", LogChannels.Stderr, "LogChannels.Stderr changed unexpectedly.");
    }

    private static void ResolveRuntimeOverride_ShouldUseExpectedRules()
    {
        var programType = typeof(ToolRegistry).Assembly.GetType("ToolHub.App.Program", throwOnError: true)!;
        var resolveMethod = programType.GetMethod(
            "ResolveRuntimeOverride",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        AssertNotNull(resolveMethod, "ResolveRuntimeOverride should remain available.");

        AssertNull(resolveMethod!.Invoke(null, new object?[] { null, @"C:\workspace" }), "Blank runtime override should return null.");
        AssertNull(resolveMethod.Invoke(null, new object?[] { "   ", @"C:\workspace" }), "Whitespace runtime override should return null.");
        AssertEqual("python", (string?)resolveMethod.Invoke(null, new object?[] { "python", @"C:\workspace" }), "Bare command should remain unchanged.");

        var cwd = Path.Combine(Path.GetTempPath(), "toolhub-program-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cwd);

        try
        {
            var resolved = (string?)resolveMethod.Invoke(null, new object?[] { @"runtime\python.exe", cwd });
            var expected = Path.GetFullPath(Path.Combine(cwd, "runtime", "python.exe"));
            AssertEqual(expected, resolved, "Relative runtime override should resolve against cwd.");
        }
        finally
        {
            Directory.Delete(cwd, recursive: true);
        }
    }

    private static void ArgsSpecCompiler_ShouldSupportStructuredAndLegacyFallback()
    {
        var structured = new ArgsSpecV1
        {
            Version = 1,
            Fields = new List<ArgFieldSpec>
            {
                new() { Name = "input", Kind = "path", Required = true },
                new() { Name = "format", Kind = "select", DefaultValue = "json" },
                new() { Name = "verbose", Kind = "flag", DefaultValue = "false" }
            },
            Argv = new List<ArgTokenSpec>
            {
                new() { Kind = "literal", Value = "--input" },
                new() { Kind = "field", Field = "input" },
                new() { Kind = "switch", Field = "verbose", WhenTrue = "--verbose" },
                new() { Kind = "literal", Value = "--format" },
                new() { Kind = "field", Field = "format", OmitWhenEmpty = false }
            }
        };

        var structuredArgs = ArgsSpecCompiler.BuildArguments(
            structured,
            null,
            new Dictionary<string, string?> { ["input"] = @"D:\data\input.txt", ["verbose"] = "true" }
        );

        AssertEqual(5, structuredArgs.Count, "Structured args should expand into ordered argv tokens.");
        AssertEqual("--input", structuredArgs[0], "Structured args should preserve literal tokens.");
        AssertEqual(@"D:\data\input.txt", structuredArgs[1], "Structured args should inject field values.");
        AssertEqual("--verbose", structuredArgs[2], "Structured args should emit boolean switches.");
        AssertEqual("--format", structuredArgs[3], "Structured args should preserve later literal tokens.");
        AssertEqual("json", structuredArgs[4], "Structured args should fall back to default field values.");

        var inferred = ArgsSpecCompiler.InferFromTemplate("--name {name} --pair={pair}");
        AssertNotNull(inferred, "Legacy templates should infer a basic args spec.");
        AssertEqual(2, inferred!.Fields.Count, "Legacy inference should discover placeholder fields.");

        var inferredArgs = ArgsSpecCompiler.BuildArguments(
            inferred,
            null,
            new Dictionary<string, string?> { ["name"] = "demo", ["pair"] = "42" }
        );
        AssertEqual("--name", inferredArgs[0], "Inferred args spec should preserve literal tokens.");
        AssertEqual("demo", inferredArgs[1], "Inferred args spec should substitute bare placeholder tokens.");
        AssertEqual("--pair=42", inferredArgs[2], "Inferred args spec should preserve inline prefix/suffix tokens.");
    }
}
