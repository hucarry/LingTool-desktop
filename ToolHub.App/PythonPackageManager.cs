using System.Diagnostics;
using System.Text.Json;
using ToolHub.App.Models;

namespace ToolHub.App;

public sealed class PythonPackageManager
{
    private readonly string _appRoot;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public PythonPackageManager(string appRoot)
    {
        _appRoot = appRoot;
    }

    public async Task<PythonPackageQueryResult> GetInstalledPackagesAsync(
        string? pythonPath,
        CancellationToken cancellationToken = default
    )
    {
        var executable = ResolvePythonExecutable(pythonPath);
        ValidatePythonExecutable(executable);

        var startInfo = BuildBaseStartInfo(executable);
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("pip");
        startInfo.ArgumentList.Add("list");
        startInfo.ArgumentList.Add("--format=json");
        startInfo.ArgumentList.Add("--disable-pip-version-check");

        var run = await RunProcessCaptureAsync(startInfo, cancellationToken);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"读取包列表失败（exit={run.ExitCode}）：{PickErrorMessage(run.StdErr, run.StdOut)}"
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
        ValidatePythonExecutable(executable);

        var startInfo = BuildBaseStartInfo(executable);
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("pip");
        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add(packageName.Trim());
        startInfo.ArgumentList.Add("--disable-pip-version-check");

        var run = await RunProcessCaptureAsync(startInfo, cancellationToken);
        var success = run.ExitCode == 0;
        var message = success
            ? "安装完成。"
            : PickErrorMessage(run.StdErr, run.StdOut);

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
        ValidatePythonExecutable(executable);

        var startInfo = BuildBaseStartInfo(executable);
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("pip");
        startInfo.ArgumentList.Add("uninstall");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(packageName.Trim());
        startInfo.ArgumentList.Add("--disable-pip-version-check");

        var run = await RunProcessCaptureAsync(startInfo, cancellationToken);
        var success = run.ExitCode == 0;
        var message = success
            ? "卸载完成。"
            : PickErrorMessage(run.StdErr, run.StdOut);

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
            CreateNoWindow = true
        };

        startInfo.Environment["PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
        return startInfo;
    }

    private async Task<ProcessRunResult> RunProcessCaptureAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var readStdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var readStdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdOut = await readStdOutTask;
        var stdErr = await readStdErrTask;

        return new ProcessRunResult(process.ExitCode, stdOut, stdErr);
    }

    private string ResolvePythonExecutable(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return "python";
        }

        var trimmed = rawPath.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return Path.GetFullPath(Path.Combine(_appRoot, trimmed));
    }

    private static void ValidatePythonExecutable(string executable)
    {
        if (string.Equals(executable, "python", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!File.Exists(executable))
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
            ? "未知错误。"
            : message.Trim();
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
