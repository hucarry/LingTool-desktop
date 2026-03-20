using Xunit;

namespace ToolHub.App.Tests;

public sealed class HostRegressionSuiteTests
{
    [Fact]
    public void HostRegressionSuite_ShouldPass()
    {
        global::HostRegressionTests.RunAll();
    }
}
