using ToolHub.App.Models;

namespace ToolHub.App;

public interface IPythonPackageService
{
    Task<PythonPackageQueryResult> GetInstalledPackagesAsync(
        string? pythonPath,
        CancellationToken cancellationToken = default
    );

    Task<PythonPackageInstallResult> InstallPackageAsync(
        string? pythonPath,
        string packageName,
        CancellationToken cancellationToken = default
    );

    Task<PythonPackageInstallResult> UninstallPackageAsync(
        string? pythonPath,
        string packageName,
        CancellationToken cancellationToken = default
    );
}
