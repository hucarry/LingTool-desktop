namespace ToolHub.App.Tests;

public sealed class HostRegressionSuiteTests
{
    public static IEnumerable<object[]> Cases => global::HostRegressionTests.GetCases();

    [Theory]
    [MemberData(nameof(Cases))]
    public void HostRegressionCase_ShouldPass(string caseName)
    {
        global::HostRegressionTests.RunCase(caseName);
    }
}
