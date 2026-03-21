using System;

#nullable enable

internal static partial class HostRegressionTests
{
    private static readonly (string Name, Action Run)[] Tests =
    {
        ("Bridge message contract", BridgeMessageTypes_ShouldMatchStableContract),
        ("Runtime override resolution", ResolveRuntimeOverride_ShouldUseExpectedRules),
        ("Args spec compilation", ArgsSpecCompiler_ShouldSupportStructuredAndLegacyFallback),
        ("Message router boundary", MessageRouter_ShouldReturnExpectedErrors),
        ("Resolved run command boundary", ResolvedRunCommand_ShouldUnifyRuntimeAndArguments)
    };

    internal static IEnumerable<object[]> GetCases()
    {
        foreach (var test in Tests)
        {
            yield return [test.Name];
        }
    }

    internal static void RunCase(string name)
    {
        foreach (var test in Tests)
        {
            if (string.Equals(test.Name, name, StringComparison.Ordinal))
            {
                test.Run();
                return;
            }
        }

        throw new InvalidOperationException($"Unknown host regression case: {name}");
    }

    internal static void RunAll()
    {
        foreach (var test in Tests)
        {
            test.Run();
            Console.WriteLine($"[PASS] {test.Name}");
        }
    }

    #if HOST_REGRESSION_CONSOLE
    private static int Main()
    {
        try
        {
            RunAll();
            Console.WriteLine($"Host regression tests passed: {Tests.Length}/{Tests.Length}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
    #endif
}
