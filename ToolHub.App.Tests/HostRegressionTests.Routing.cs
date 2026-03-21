using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ToolHub.App;
using ToolHub.App.Models;

#nullable enable

internal static partial class HostRegressionTests
{
    private static void MessageRouter_ShouldReturnExpectedErrors()
    {
        var sentMessages = new List<object>();
        using var processManager = new ProcessManager(sentMessages.Add);
        using var terminalManager = new TerminalManager(sentMessages.Add);

        var registryRoot = Path.Combine(Path.GetTempPath(), "toolhub-message-router", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(registryRoot);

        try
        {
            var registry = new ToolRegistry(
                Path.Combine(registryRoot, "tools.json"),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            );
            var router = new MessageRouter(
            [
                new AppMessageHandlers(
                    registry,
                    new DiagnosticBundleService(
                        registryRoot,
                        Path.Combine(registryRoot, "tools.json")
                    )
                ),
                new ToolCatalogMessageHandlers(registry),
                new ToolExecutionMessageHandlers(processManager, terminalManager, new ToolExecutionSupport(registry)),
                new PythonMessageHandlers(new PythonPackageManager(registryRoot)),
                new TerminalMessageHandlers(terminalManager)
            ]);
            var context = new MessageContext(
                sentMessages.Add,
                static _ => null,
                static (_, _, _) => null,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            );

            router.Dispatch(context, """{"type":"runTool"}""");
            var missingToolIdError = sentMessages[^1] as ErrorMessage;
            AssertNotNull(missingToolIdError, "Missing toolId should emit an error message.");
            AssertEqual(BridgeErrorMessages.RunToolMissingToolId, missingToolIdError!.Message, "runTool without toolId should report a stable error.");

            router.Dispatch(context, """{"type":"runTool","toolId":"missing-tool"}""");
            var missingToolError = sentMessages[^1] as ErrorMessage;
            AssertNotNull(missingToolError, "Unknown tool should emit an error message.");
            AssertEqual(ToolErrorMessages.ToolNotFound("missing-tool"), missingToolError!.Message, "Unknown tool should report not found.");

            router.Dispatch(context, """{"type":"addTool"}""");
            var missingPayloadError = sentMessages[^1] as ErrorMessage;
            AssertNotNull(missingPayloadError, "addTool without payload should emit an error message.");
            AssertEqual(BridgeErrorMessages.AddToolMissingPayload, missingPayloadError!.Message, "addTool without payload should report a stable error.");

            router.Dispatch(context, """{"type":"unknownType"}""");
            var unsupportedError = sentMessages[^1] as ErrorMessage;
            AssertNotNull(unsupportedError, "Unsupported message type should emit an error message.");
            AssertEqual(BridgeErrorMessages.UnsupportedMessageType("unknownType"), unsupportedError!.Message, "Unsupported message type should report a stable error.");
        }
        finally
        {
            Directory.Delete(registryRoot, recursive: true);
        }
    }
}
