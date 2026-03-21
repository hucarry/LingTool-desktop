using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App;

public sealed class PythonPackageManager : IPythonPackageService
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MutationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PipBootstrapTimeout = TimeSpan.FromMinutes(2);

    private readonly string _appRoot;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<PythonPackageManager> _logger;

    public PythonPackageManager(string appRoot, ILogger<PythonPackageManager>? logger = null)
    {
        _appRoot = appRoot;
        _logger = logger ?? NullLogger<PythonPackageManager>.Instance;
    }

    public async Task<PythonPackageQueryResult> GetInstalledPackagesAsync(
        string? pythonPath,
        CancellationToken cancellationToken = default
    )
    {
        var executable = ResolvePythonExecutable(pythonPath);
        _logger.LogInformation("Querying installed Python packages. PythonPath={PythonPath}", executable);
        ValidatePythonExecutable(executable);
        await EnsurePipAvailableAsync(executable, cancellationToken);

        var startInfo = BuildBaseStartInfo(executable);
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("pip");
        startInfo.ArgumentList.Add("list");
        startInfo.ArgumentList.Add("--format=json");
        startInfo.ArgumentList.Add("--disable-pip-version-check");

        var run = await RunProcessCaptureAsync(startInfo, QueryTimeout, cancellationToken);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to read installed packages (exit={run.ExitCode}): {PickErrorMessage(run.StdErr, run.StdOut)}"
            );
        }

        var raw = string.IsNullOrWhiteSpace(run.StdOut) ? "[]" : run.StdOut;
        var parsed = JsonSerializer.Deserialize<List<PipListItem>>(raw, _jsonOptions) ?? new List<PipListItem>();

        var packages = parsed
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new PythonPackageItem
            {
                Name = item.Name.Trim(),
                Version = (item.Version ?? string.Empty).Trim()
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PythonPackageQueryResult(executable, packages);
    }

    public async Task<PythonPackageInstallResult> InstallPackageAsync(
        string? pythonPath,
        string packageName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new ArgumentException("Package name cannot be empty.", nameof(packageName));
        }

        var executable = ResolvePythonExecutable(pythonPath);
        _logger.LogInformation(
            "Installing Python package {PackageName}. PythonPath={PythonPath}",
            packageName.Trim(),
            executable
        );
        ValidatePythonExecutable(executable);
        await EnsurePipAvailableAsync(executable, cancellationToken);

        var startInfo = BuildBaseStartInfo(executable);
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("pip");
        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add(packageName.Trim());
        startInfo.ArgumentList.Add("--disable-pip-version-check");

        var run = await RunProcessCaptureAsync(startInfo, MutationTimeout, cancellationToken);
        var success = run.ExitCode == 0;
        var message = success
            ? "Installation completed."
            : PickErrorMessage(run.StdErr, run.StdOut);

        if (success)
        {
            _logger.LogInformation(
                "Installed Python package {PackageName}. PythonPath={PythonPath}",
                packageName.Trim(),
                executable
            );
        }
        else
        {
            _logger.LogWarning(
                "Failed to install Python package {PackageName}. PythonPath={PythonPath} ExitCode={ExitCode}",
                packageName.Trim(),
                executable,
                run.ExitCode
            );
        }

        return new PythonPackageInstallResult(executable, packageName.Trim(), success, message);
    }

    public async Task<PythonPackageInstallResult> UninstallPackageAsync(
        string? pythonPath,
        string packageName,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new ArgumentException("Package name cannot be empty.", nameof(packageName));
        }

        var executable = ResolvePythonExecutable(pythonPath);
        _logger.LogInformation(
            "Uninstalling Python package {PackageName}. PythonPath={PythonPath}",
            packageName.Trim(),
            executable
        );
        ValidatePythonExecutable(executable);
        await EnsurePipAvailableAsync(executable, cancellationToken);

        var startInfo = BuildBaseStartInfo(executable);
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("pip");
        startInfo.ArgumentList.Add("uninstall");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(packageName.Trim());
        startInfo.ArgumentList.Add("--disable-pip-version-check");

        var run = await RunProcessCaptureAsync(startInfo, MutationTimeout, cancellationToken);
        var success = run.ExitCode == 0;
        var message = success
            ? "Uninstallation completed."
            : PickErrorMessage(run.StdErr, run.StdOut);

        if (success)
        {
            _logger.LogInformation(
                "Uninstalled Python package {PackageName}. PythonPath={PythonPath}",
                packageName.Trim(),
                executable
            );
        }
        else
        {
            _logger.LogWarning(
                "Failed to uninstall Python package {PackageName}. PythonPath={PythonPath} ExitCode={ExitCode}",
                packageName.Trim(),
                executable,
                run.ExitCode
            );
        }

        return new PythonPackageInstallResult(executable, packageName.Trim(), success, message);
    }

    private ProcessStartInfo BuildBaseStartInfo(string executable)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = _appRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true
        };

        startInfo.Environment["PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
        // Ensure pip output is encoded as UTF-8.
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        return startInfo;
    }

    private async Task<ProcessRunResult> RunProcessCaptureAsync(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var token = timeoutCts.Token;
            var readStdOutTask = process.StandardOutput.ReadToEndAsync(token);
            var readStdErrTask = process.StandardError.ReadToEndAsync(token);

            await process.WaitForExitAsync(token);
            var stdOut = await readStdOutTask;
            var stdErr = await readStdErrTask;

            return new ProcessRunResult(process.ExitCode, stdOut, stdErr);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryTerminateProcess(process);
            throw new TimeoutException($"Python package command timed out after {timeout.TotalSeconds:0} seconds.");
        }
        catch
        {
            TryTerminateProcess(process);
            throw;
        }
    }

    private async Task EnsurePipAvailableAsync(string executable, CancellationToken cancellationToken)
    {
        if (await CanRunPipAsync(executable, cancellationToken))
        {
            return;
        }

        _logger.LogInformation("pip is unavailable. Bootstrapping ensurepip for {PythonPath}.", executable);
        var bootstrapInfo = BuildBaseStartInfo(executable);
        bootstrapInfo.ArgumentList.Add("-m");
        bootstrapInfo.ArgumentList.Add("ensurepip");
        bootstrapInfo.ArgumentList.Add("--upgrade");

        var bootstrapRun = await RunProcessCaptureAsync(bootstrapInfo, PipBootstrapTimeout, cancellationToken);
        if (bootstrapRun.ExitCode != 0)
        {
            _logger.LogWarning(
                "Automatic pip bootstrap failed for {PythonPath}. ExitCode={ExitCode}",
                executable,
                bootstrapRun.ExitCode
            );
            throw new InvalidOperationException(
                $"Python does not have pip available and automatic bootstrap failed (exit={bootstrapRun.ExitCode}): {PickErrorMessage(bootstrapRun.StdErr, bootstrapRun.StdOut)}"
            );
        }

        if (await CanRunPipAsync(executable, cancellationToken))
        {
            _logger.LogInformation("pip bootstrap completed for {PythonPath}.", executable);
            return;
        }

        throw new InvalidOperationException("Python pip bootstrap completed, but pip is still unavailable.");
    }

    private async Task<bool> CanRunPipAsync(string executable, CancellationToken cancellationToken)
    {
        var startInfo = BuildBaseStartInfo(executable);
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("pip");
        startInfo.ArgumentList.Add("--version");

        var run = await RunProcessCaptureAsync(startInfo, QueryTimeout, cancellationToken);
        return run.ExitCode == 0;
    }

    private string ResolvePythonExecutable(string? rawPath)
    {
        string? preferred = null;
        if (!string.IsNullOrWhiteSpace(rawPath))
        {
            var trimmed = rawPath.Trim();
            preferred = Path.IsPathRooted(trimmed)
                ? Path.GetFullPath(trimmed)
                : Path.GetFullPath(Path.Combine(_appRoot, trimmed));
        }

        var resolved = PythonInterpreterProbe.ResolvePreferred(preferred, null);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        throw new FileNotFoundException(RuntimeErrorMessages.NoUsablePythonInterpreter);
    }

    private static void ValidatePythonExecutable(string executable)
    {
        if (!PythonInterpreterProbe.IsUsable(executable))
        {
            throw new FileNotFoundException($"Python interpreter not found: {executable}");
        }
    }

    private static string PickErrorMessage(string? stdErr, string? stdOut)
    {
        var message = string.IsNullOrWhiteSpace(stdErr)
            ? stdOut
            : stdErr;

        return string.IsNullOrWhiteSpace(message)
            ? "Unknown error."
            : message.Trim();
    }

    private static void TryTerminateProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore cleanup failures after cancellation or timeout.
        }
    }

    private sealed class PipListItem
    {
        public string Name { get; set; } = string.Empty;

        public string? Version { get; set; }
    }

    private sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
}

public sealed class PythonPackageQueryResult
{
    public PythonPackageQueryResult(string pythonPath, IReadOnlyList<PythonPackageItem> packages)
    {
        PythonPath = pythonPath;
        Packages = packages;
    }

    public string PythonPath { get; }

    public IReadOnlyList<PythonPackageItem> Packages { get; }
}

public sealed class PythonPackageInstallResult
{
    public PythonPackageInstallResult(string pythonPath, string packageName, bool success, string message)
    {
        PythonPath = pythonPath;
        PackageName = packageName;
        Success = success;
        Message = message;
    }

    public string PythonPath { get; }

    public string PackageName { get; }

    public bool Success { get; }

    public string Message { get; }
}
