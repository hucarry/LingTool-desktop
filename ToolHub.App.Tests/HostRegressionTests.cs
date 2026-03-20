using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ToolHub.App;
using ToolHub.App.Models;
using ToolHub.App.Utils;

#nullable enable

internal static class HostRegressionTests
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Bridge message contract", BridgeMessageTypes_ShouldMatchStableContract),
            ("Runtime override resolution", ResolveRuntimeOverride_ShouldUseExpectedRules),
            ("Tool registry projection", ToolRegistry_ShouldProjectDefinitionsIntoViews),
            ("Runnable tool projection", RunnableToolProjection_ShouldMatchExecutionBoundary),
            ("Tool runtime resolution", ToolRuntimeResolution_ShouldRespectToolTypeRules),
            ("Message router boundary", MessageRouter_ShouldReturnExpectedErrors),
            ("Process start info boundary", ProcessStartInfo_ShouldComposeCommandsForSupportedToolTypes),
            ("Terminal run command boundary", TerminalRunCommand_ShouldComposeShellCommandsForSupportedShellKinds)
        };

        try
        {
            foreach (var test in tests)
            {
                test.Run();
                Console.WriteLine($"[PASS] {test.Name}");
            }

            Console.WriteLine($"Host regression tests passed: {tests.Length}/{tests.Length}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

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

    private static void ToolRegistry_ShouldProjectDefinitionsIntoViews()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "toolhub-registry-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var toolsFilePath = Path.Combine(workspaceRoot, "tools.json");
            var toolsDirectory = Path.Combine(workspaceRoot, "Tools");
            Directory.CreateDirectory(toolsDirectory);

            var scriptPath = Path.Combine(toolsDirectory, "demo.py");
            File.WriteAllText(scriptPath, "print('ok')");

            var registry = new ToolRegistry(toolsFilePath, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var added = registry.AddTool(new ToolDefinition
            {
                Id = " demo_py ",
                Name = " ",
                Type = "python",
                Path = @"Tools\demo.py",
                RuntimePath = @"missing\python.exe",
                ArgsTemplate = " --flag ",
                Tags = new List<string> { "alpha", " alpha ", "beta" }
            });

            AssertEqual("demo_py", added.Id, "Tool id should be trimmed.");
            AssertEqual("demo_py", added.Name, "Blank tool name should fall back to tool id.");
            AssertEqual("python", added.Type, "Tool type should remain python.");
            AssertEqual(scriptPath, added.Path, "Relative tool path should resolve to an absolute path.");
            AssertEqual(toolsDirectory, added.Cwd, "Cwd should default to the tool directory.");
            AssertEqual("--flag", added.ArgsTemplate, "ArgsTemplate should be trimmed.");
            AssertEqual("alpha,beta", string.Join(',', added.Tags), "Tags should be trimmed and de-duplicated.");
            AssertTrue(added.PathExists, "Existing tool path should be marked as present.");
            AssertTrue(added.Valid, "Valid tool should remain valid after projection.");
            AssertNull(added.RuntimePath, "Unusable runtime path should be cleared.");
            AssertNull(added.ValidationMessage, "Valid tool should not expose a validation message.");

            var persistedJson = File.ReadAllText(toolsFilePath);
            AssertFalse(persistedJson.Contains("\"valid\"", StringComparison.OrdinalIgnoreCase), "Persisted config should not store Valid.");
            AssertFalse(persistedJson.Contains("\"pathExists\"", StringComparison.OrdinalIgnoreCase), "Persisted config should not store PathExists.");
            AssertFalse(persistedJson.Contains("\"validationMessage\"", StringComparison.OrdinalIgnoreCase), "Persisted config should not store ValidationMessage.");

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

            var invalidRegistry = new ToolRegistry(toolsFilePath, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var tool = invalidRegistry.GetToolById("missing_py");

            AssertNotNull(tool, "Stored tool should be retrievable.");
            AssertEqual(Path.Combine(workspaceRoot, "Tools", "missing.py"), tool!.Path, "Stored relative path should resolve during projection.");
            AssertEqual(workspaceRoot, tool.Cwd, "Missing tool should fall back to the registry base directory as cwd.");
            AssertFalse(tool.PathExists, "Missing tool path should be reported as absent.");
            AssertFalse(tool.Valid, "Missing tool should be marked invalid.");
            AssertNull(tool.RuntimePath, "Invalid runtime path should be cleared during projection.");
            AssertContains(tool.ValidationMessage, "missing.py", "Validation message should mention the missing tool path.");
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static void RunnableToolProjection_ShouldMatchExecutionBoundary()
    {
        var createMethod = typeof(MessageRouter).Assembly.GetType("ToolHub.App.ToolMessageHandlers", throwOnError: true)!
            .GetMethod("CreateRunnableTool", BindingFlags.Static | BindingFlags.NonPublic);

        AssertNotNull(createMethod, "CreateRunnableTool should remain available.");

        var view = new ToolItem
        {
            Id = "py_demo",
            Name = "Python Demo",
            Type = "python",
            Path = @"D:\tools\demo.py",
            RuntimePath = @"D:\python\python.exe",
            Python = @"legacy\python.exe",
            Cwd = @"D:\tools",
            ArgsTemplate = "--name {{name}}",
            Tags = new List<string> { "alpha", "beta" },
            Description = "demo",
            PathExists = true,
            Valid = true,
            ValidationMessage = "unused"
        };

        var runnable = (RunnableTool?)createMethod!.Invoke(null, new object[] { view });

        AssertNotNull(runnable, "CreateRunnableTool should return a runnable tool.");
        AssertEqual(view.Id, runnable!.Id, "RunnableTool should preserve Id.");
        AssertEqual(view.Name, runnable.Name, "RunnableTool should preserve Name.");
        AssertEqual(view.Type, runnable.Type, "RunnableTool should preserve Type.");
        AssertEqual(view.Path, runnable.Path, "RunnableTool should preserve Path.");
        AssertEqual(view.RuntimePath, runnable.RuntimePath, "RunnableTool should preserve RuntimePath.");
        AssertEqual(view.Cwd, runnable.Cwd, "RunnableTool should preserve Cwd.");
        AssertEqual(view.ArgsTemplate, runnable.ArgsTemplate, "RunnableTool should preserve ArgsTemplate.");
    }

    private static void ToolRuntimeResolution_ShouldRespectToolTypeRules()
    {
        var handlerType = typeof(MessageRouter).Assembly.GetType("ToolHub.App.ToolMessageHandlers", throwOnError: true)!;
        var resolveMethod = handlerType.GetMethod("ResolveToolRuntime", BindingFlags.Static | BindingFlags.NonPublic);
        AssertNotNull(resolveMethod, "ResolveToolRuntime should remain available.");

        var sentMessages = new List<object>();
        using var processManager = new ProcessManager(sentMessages.Add);
        using var terminalManager = new TerminalManager(sentMessages.Add);

        var registryRoot = Path.Combine(Path.GetTempPath(), "toolhub-runtime-resolution", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(registryRoot);

        try
        {
            var registry = new ToolRegistry(
                Path.Combine(registryRoot, "tools.json"),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            );
            var context = new MessageContext(
                registry,
                processManager,
                new PythonPackageManager(registryRoot),
                terminalManager,
                sentMessages.Add,
                static _ => null,
                static (_, _, _) => null,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            );

            var commandTool = new RunnableTool
            {
                Id = "cmd_demo",
                Name = "Command Demo",
                Type = "command",
                Path = "echo",
                Cwd = registryRoot,
                ArgsTemplate = string.Empty
            };

            var resolvedCommandRuntime = (string?)resolveMethod!.Invoke(null, new object?[] { context, commandTool, @"runtime\tool.exe" });
            var expectedCommandRuntime = Path.GetFullPath(Path.Combine(registryRoot, "runtime", "tool.exe"));
            AssertEqual(expectedCommandRuntime, resolvedCommandRuntime, "Command tool runtime override should resolve against cwd.");
            AssertEqual(0, sentMessages.Count, "Command runtime resolution should not emit errors.");

            var pythonTool = new RunnableTool
            {
                Id = "py_demo",
                Name = "Python Demo",
                Type = "python",
                Path = @"D:\tools\demo.py",
                Cwd = registryRoot
            };

            var expectedPythonRuntime = PythonInterpreterProbe.ResolvePreferred(null, pythonTool.RuntimePath);
            var pythonResult = (string?)resolveMethod.Invoke(null, new object?[] { context, pythonTool, null });

            if (string.IsNullOrWhiteSpace(expectedPythonRuntime))
            {
                AssertEqual("\0__RESOLVE_FAILED__", pythonResult, "Python runtime resolution should return the failure sentinel when no interpreter is available.");

                var error = sentMessages[^1] as ErrorMessage;
                AssertNotNull(error, "Python runtime resolution should emit an error message when no interpreter is available.");
                AssertEqual("No usable Python interpreter was found.", error!.Message, "Python runtime resolution should report a clear error message.");
            }
            else
            {
                AssertEqual(expectedPythonRuntime, pythonResult, "Python runtime resolution should align with the interpreter probe.");
                AssertEqual(0, sentMessages.Count, "Python runtime resolution should not emit errors when an interpreter is available.");
            }
        }
        finally
        {
            Directory.Delete(registryRoot, recursive: true);
        }
    }

    private static void MessageRouter_ShouldReturnExpectedErrors()
    {
        var sentMessages = new List<object>();
        using var processManager = new ProcessManager(sentMessages.Add);
        using var terminalManager = new TerminalManager(sentMessages.Add);

        var registryRoot = Path.Combine(Path.GetTempPath(), "toolhub-message-router", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(registryRoot);

        try
        {
            var context = new MessageContext(
                new ToolRegistry(
                    Path.Combine(registryRoot, "tools.json"),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                ),
                processManager,
                new PythonPackageManager(registryRoot),
                terminalManager,
                sentMessages.Add,
                static _ => null,
                static (_, _, _) => null,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            );

            MessageRouter.Route(context, """{"type":"runTool"}""");
            var missingToolIdError = sentMessages[^1] as ErrorMessage;
            AssertNotNull(missingToolIdError, "Missing toolId should emit an error message.");
            AssertEqual("runTool request is missing toolId.", missingToolIdError!.Message, "runTool without toolId should report a stable error.");

            MessageRouter.Route(context, """{"type":"runTool","toolId":"missing-tool"}""");
            var missingToolError = sentMessages[^1] as ErrorMessage;
            AssertNotNull(missingToolError, "Unknown tool should emit an error message.");
            AssertEqual("Tool not found: missing-tool", missingToolError!.Message, "Unknown tool should report not found.");

            MessageRouter.Route(context, """{"type":"addTool"}""");
            var missingPayloadError = sentMessages[^1] as ErrorMessage;
            AssertNotNull(missingPayloadError, "addTool without payload should emit an error message.");
            AssertEqual("addTool request is missing tool payload.", missingPayloadError!.Message, "addTool without payload should report a stable error.");

            MessageRouter.Route(context, """{"type":"unknownType"}""");
            var unsupportedError = sentMessages[^1] as ErrorMessage;
            AssertNotNull(unsupportedError, "Unsupported message type should emit an error message.");
            AssertEqual("Unsupported message type: unknownType", unsupportedError!.Message, "Unsupported message type should report a stable error.");
        }
        finally
        {
            Directory.Delete(registryRoot, recursive: true);
        }
    }

    private static void ProcessStartInfo_ShouldComposeCommandsForSupportedToolTypes()
    {
        var processManagerType = typeof(ProcessManager);
        var buildMethod = processManagerType.GetMethod("BuildStartInfo", BindingFlags.Static | BindingFlags.NonPublic);
        AssertNotNull(buildMethod, "BuildStartInfo should remain available.");

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
        var pythonStartInfo = (ProcessStartInfo?)buildMethod!.Invoke(null, new object?[]
        {
            pythonTool,
            new Dictionary<string, string?> { ["name"] = "demo", ["count"] = "2" },
            explicitRuntime
        });
        AssertNotNull(pythonStartInfo, "Python start info should be created.");
        AssertEqual(Path.GetFullPath(explicitRuntime), pythonStartInfo!.FileName, "Python start info should honor the resolved runtime.");
        AssertEqual(pythonTool.Cwd, pythonStartInfo.WorkingDirectory, "Python start info should preserve cwd.");
        AssertEqual(5, pythonStartInfo.ArgumentList.Count, "Python start info should prepend the script path before template args.");
        AssertEqual(pythonTool.Path, pythonStartInfo.ArgumentList[0], "Python start info should pass the tool path as the first argument.");
        AssertEqual("--name", pythonStartInfo.ArgumentList[1], "Python start info should expand template tokens in order.");
        AssertEqual("demo", pythonStartInfo.ArgumentList[2], "Python start info should substitute placeholder values.");
        AssertEqual("--count", pythonStartInfo.ArgumentList[3], "Python start info should preserve later template flags.");
        AssertEqual("2", pythonStartInfo.ArgumentList[4], "Python start info should substitute later placeholder values.");
        AssertTrue(pythonStartInfo.RedirectStandardOutput, "Python start info should capture stdout.");
        AssertTrue(pythonStartInfo.RedirectStandardError, "Python start info should capture stderr.");
        AssertFalse(pythonStartInfo.UseShellExecute, "Python start info should disable shell execute.");
        AssertTrue(pythonStartInfo.CreateNoWindow, "Python start info should run without creating a console window.");

        var nodeTool = new RunnableTool
        {
            Id = "node_demo",
            Name = "Node Demo",
            Type = "node",
            Path = @"D:\tools\index.js",
            Cwd = @"D:\tools",
            ArgsTemplate = "--watch {mode}"
        };
        var nodeStartInfo = (ProcessStartInfo?)buildMethod.Invoke(null, new object?[]
        {
            nodeTool,
            new Dictionary<string, string?> { ["mode"] = "full" },
            explicitRuntime
        });
        AssertNotNull(nodeStartInfo, "Node start info should be created.");
        AssertEqual(Path.GetFullPath(explicitRuntime), nodeStartInfo!.FileName, "Node start info should honor the resolved runtime.");
        AssertEqual(3, nodeStartInfo.ArgumentList.Count, "Node start info should prepend the script path before template args.");
        AssertEqual(nodeTool.Path, nodeStartInfo.ArgumentList[0], "Node start info should pass the tool path as the first argument.");
        AssertEqual("--watch", nodeStartInfo.ArgumentList[1], "Node start info should expand template tokens in order.");
        AssertEqual("full", nodeStartInfo.ArgumentList[2], "Node start info should substitute placeholder values.");

        var commandTool = new RunnableTool
        {
            Id = "cmd_demo",
            Name = "Command Demo",
            Type = "command",
            Path = "git",
            ArgsTemplate = "status --short"
        };
        var commandStartInfo = (ProcessStartInfo?)buildMethod.Invoke(null, new object?[]
        {
            commandTool,
            new Dictionary<string, string?>(),
            null
        });
        AssertNotNull(commandStartInfo, "Command start info should be created.");
        AssertEqual("git", commandStartInfo!.FileName, "Command start info should use the tool path directly.");
        AssertEqual(Directory.GetCurrentDirectory(), commandStartInfo.WorkingDirectory, "Command start info should fall back to the current directory when cwd is absent.");
        AssertEqual("status", commandStartInfo.ArgumentList[0], "Command start info should preserve the first template token.");
        AssertEqual("--short", commandStartInfo.ArgumentList[1], "Command start info should preserve the second template token.");

        var executableTool = new RunnableTool
        {
            Id = "exe_demo",
            Name = "Executable Demo",
            Type = "executable",
            Path = @"D:\apps\demo.exe",
            Cwd = @"D:\apps",
            ArgsTemplate = "--flag {value}"
        };
        var executableStartInfo = (ProcessStartInfo?)buildMethod.Invoke(null, new object?[]
        {
            executableTool,
            new Dictionary<string, string?> { ["value"] = "ok" },
            null
        });
        AssertNotNull(executableStartInfo, "Executable start info should be created.");
        AssertEqual(executableTool.Path, executableStartInfo!.FileName, "Executable start info should use the executable path directly.");
        AssertEqual(executableTool.Cwd, executableStartInfo.WorkingDirectory, "Executable start info should preserve cwd.");
        AssertEqual("--flag", executableStartInfo.ArgumentList[0], "Executable start info should expand template tokens in order.");
        AssertEqual("ok", executableStartInfo.ArgumentList[1], "Executable start info should substitute placeholder values.");

        try
        {
            _ = buildMethod.Invoke(null, new object?[]
            {
                new RunnableTool
                {
                    Id = "url_demo",
                    Name = "Url Demo",
                    Type = "url",
                    Path = "https://example.com",
                    ArgsTemplate = string.Empty
                },
                new Dictionary<string, string?>(),
                null
            });
            throw new InvalidOperationException("Unsupported tool type should throw.");
        }
        catch (TargetInvocationException ex)
        {
            AssertNotNull(ex.InnerException, "Unsupported tool type should surface an inner exception.");
            AssertEqual(typeof(NotSupportedException), ex.InnerException!.GetType(), "Unsupported tool type should throw NotSupportedException.");
        }
    }

    private static void TerminalRunCommand_ShouldComposeShellCommandsForSupportedShellKinds()
    {
        var terminalManagerType = typeof(TerminalManager);
        var buildMethod = terminalManagerType.GetMethod("BuildRunCommand", BindingFlags.Static | BindingFlags.NonPublic);
        AssertNotNull(buildMethod, "BuildRunCommand should remain available.");

        var explicitRuntime = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe"
        );
        AssertTrue(File.Exists(explicitRuntime), "A stable explicit runtime should exist on Windows.");

        var tempScriptDirectory = Path.Combine(Path.GetTempPath(), "th");

        var pythonTool = new RunnableTool
        {
            Id = "py_term",
            Name = "Python Terminal Demo",
            Type = "python",
            Path = @"D:\tools\demo.py",
            Cwd = @"D:\tools",
            ArgsTemplate = "--flag {value}"
        };

        var powerShellCommand = (string?)buildMethod!.Invoke(null, new object?[]
        {
            "powershell.exe",
            pythonTool,
            new Dictionary<string, string?> { ["value"] = "ok" },
            explicitRuntime
        });
        AssertNotNull(powerShellCommand, "PowerShell run command should be created.");
        AssertContains(powerShellCommand, ". \"$env:TEMP\\th\\", "PowerShell run command should execute a temp script.");

        var powerShellScript = ReadGeneratedTerminalScript(powerShellCommand!, tempScriptDirectory, "$env:TEMP\\th\\");
        AssertContains(powerShellScript, "Set-Location -LiteralPath 'D:\\tools' -ErrorAction Stop", "PowerShell script should switch to the tool cwd.");
        AssertContains(powerShellScript, "function global:python", "PowerShell script should inject the runtime helper function for python tools.");
        AssertContains(powerShellScript, "'D:\\tools\\demo.py' '--flag' 'ok'", "PowerShell script should append the tool path and expanded args.");

        var nodeTool = new RunnableTool
        {
            Id = "node_term",
            Name = "Node Terminal Demo",
            Type = "node",
            Path = @"D:\tools\index.js",
            Cwd = @"D:\tools",
            ArgsTemplate = "--mode {mode}"
        };

        var cmdCommand = (string?)buildMethod.Invoke(null, new object?[]
        {
            "cmd.exe",
            nodeTool,
            new Dictionary<string, string?> { ["mode"] = "dev" },
            explicitRuntime
        });
        AssertNotNull(cmdCommand, "cmd run command should be created.");
        AssertContains(cmdCommand, "call \"%TEMP%\\th\\", "cmd run command should execute a temp batch file.");

        var cmdScript = ReadGeneratedTerminalScript(cmdCommand!, tempScriptDirectory, "%TEMP%\\th\\");
        AssertContains(cmdScript, "@echo off", "cmd script should disable command echo.");
        AssertContains(cmdScript, "cd /d \"D:\\tools\"", "cmd script should switch to the tool cwd.");
        AssertContains(cmdScript, "\"D:\\tools\\index.js\" \"--mode\" \"dev\"", "cmd script should include the tool path and expanded args.");
        AssertContains(cmdScript, "set \"PATH=", "cmd script should prepend the runtime directory for node tools.");

        var executableTool = new RunnableTool
        {
            Id = "exe_term",
            Name = "Executable Terminal Demo",
            Type = "executable",
            Path = @"D:\apps\demo.exe",
            Cwd = @"D:\apps",
            ArgsTemplate = "--flag {value}"
        };

        var genericCommand = (string?)buildMethod.Invoke(null, new object?[]
        {
            "/bin/bash",
            executableTool,
            new Dictionary<string, string?> { ["value"] = "ok" },
            null
        });
        AssertNotNull(genericCommand, "Generic shell run command should be created.");
        AssertEqual("cd \"D:\\apps\" && \"D:\\apps\\demo.exe\" \"--flag\" \"ok\"\r", genericCommand, "Generic shell command should be composed inline.");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}{Environment.NewLine}Expected: {expected}{Environment.NewLine}Actual: {actual}");
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool value, string message)
    {
        if (value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertNull(object? value, string message)
    {
        if (value is not null)
        {
            throw new InvalidOperationException($"{message}{Environment.NewLine}Actual: {value}");
        }
    }

    private static void AssertNotNull(object? value, string message)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertContains(string? text, string fragment, string message)
    {
        if (string.IsNullOrWhiteSpace(text) || text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0)
        {
            throw new InvalidOperationException($"{message}{Environment.NewLine}Expected fragment: {fragment}{Environment.NewLine}Actual: {text}");
        }
    }

    private static string ReadGeneratedTerminalScript(string command, string tempScriptDirectory, string marker)
    {
        var startIndex = command.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            throw new InvalidOperationException($"Terminal command marker was not found.{Environment.NewLine}Command: {command}");
        }

        startIndex += marker.Length;

        var endIndex = command.IndexOf('"', startIndex);
        if (endIndex < 0)
        {
            throw new InvalidOperationException($"Terminal command end quote was not found.{Environment.NewLine}Command: {command}");
        }

        var scriptName = command[startIndex..endIndex];
        var scriptPath = Path.Combine(tempScriptDirectory, scriptName);
        AssertTrue(File.Exists(scriptPath), $"Generated terminal script should exist: {scriptPath}");

        return File.ReadAllText(scriptPath);
    }

}
