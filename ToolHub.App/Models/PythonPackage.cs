namespace ToolHub.App.Models;

public sealed class PythonPackageItem
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;
}

public static class PythonPackageInstallStates
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

public static class PythonPackageActions
{
    public const string Install = "install";
    public const string Uninstall = "uninstall";
}
