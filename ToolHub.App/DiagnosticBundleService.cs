using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ToolHub.App;

internal sealed class DiagnosticBundleService
{
    private readonly string _appRoot;
    private readonly string _toolsFilePath;
    private readonly ILogger<DiagnosticBundleService> _logger;

    internal DiagnosticBundleService(
        string appRoot,
        string toolsFilePath,
        ILogger<DiagnosticBundleService>? logger = null)
    {
        _appRoot = appRoot;
        _toolsFilePath = toolsFilePath;
        _logger = logger ?? NullLogger<DiagnosticBundleService>.Instance;
    }

    internal DiagnosticBundleResult Export(string? outputDirectory = null)
    {
        var targetDirectory = ResolveOutputDirectory(outputDirectory);
        Directory.CreateDirectory(targetDirectory);

        var bundlePath = Path.Combine(
            targetDirectory,
            $"toolhub-diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip"
        );

        if (File.Exists(bundlePath))
        {
            File.Delete(bundlePath);
        }

        _logger.LogInformation(
            "Exporting diagnostic bundle. BundlePath={BundlePath} TargetDirectory={TargetDirectory}",
            bundlePath,
            targetDirectory
        );

        var entryCount = 0;
        using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);

        entryCount += AddJsonEntry(archive, "manifest/host-info.json", BuildHostInfo());
        entryCount += TryAddFile(archive, _toolsFilePath, "config/tools.json");
        entryCount += TryAddFile(
            archive,
            Path.Combine(AppContext.BaseDirectory, "startup-error.log"),
            "logs/startup-error.log"
        );
        entryCount += TryAddFile(
            archive,
            Path.Combine(AppContext.BaseDirectory, "release-manifest.txt"),
            "release/release-manifest.txt"
        );
        entryCount += TryAddDirectory(archive, AppLogging.ResolveLogDirectory(), "logs/runtime");

        _logger.LogInformation(
            "Diagnostic bundle exported successfully. BundlePath={BundlePath} EntryCount={EntryCount}",
            bundlePath,
            entryCount
        );

        return new DiagnosticBundleResult(bundlePath, entryCount, DateTimeOffset.Now);
    }

    private string ResolveOutputDirectory(string? outputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            return Path.IsPathRooted(outputDirectory)
                ? Path.GetFullPath(outputDirectory)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outputDirectory));
        }

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktopPath))
        {
            return Path.Combine(desktopPath, "ToolHub Diagnostics");
        }

        return Path.Combine(AppContext.BaseDirectory, "diagnostics");
    }

    private object BuildHostInfo()
    {
        var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        return new
        {
            GeneratedAt = DateTimeOffset.Now,
            Application = "ToolHub.App",
            Version = entryAssembly.GetName().Version?.ToString(),
            BaseDirectory = AppContext.BaseDirectory,
            CurrentDirectory = Directory.GetCurrentDirectory(),
            AppRoot = _appRoot,
            ToolsFilePath = _toolsFilePath,
            ToolsFileExists = File.Exists(_toolsFilePath),
            RuntimeLogDirectory = AppLogging.ResolveLogDirectory(),
            Framework = RuntimeInformation.FrameworkDescription,
            OSDescription = RuntimeInformation.OSDescription,
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            MachineName = Environment.MachineName,
            UserInteractive = Environment.UserInteractive
        };
    }

    private static int AddJsonEntry(ZipArchive archive, string entryName, object payload)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(
            stream,
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }
        );
        return 1;
    }

    private static int TryAddDirectory(ZipArchive archive, string sourceDirectory, string entryDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return 0;
        }

        var entryCount = 0;
        foreach (var filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            entryCount += TryAddFile(archive, filePath, $"{entryDirectory}/{Path.GetFileName(filePath)}");
        }

        return entryCount;
    }

    private static int TryAddFile(ZipArchive archive, string sourcePath, string entryName)
    {
        if (!File.Exists(sourcePath))
        {
            return 0;
        }

        archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
        return 1;
    }
}

internal sealed record DiagnosticBundleResult(
    string BundlePath,
    int EntryCount,
    DateTimeOffset ExportedAt
);
