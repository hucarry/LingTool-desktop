using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;

namespace ToolHub.App.Tests;

public sealed class DiagnosticBundleServiceTests
{
    [Fact]
    public void Export_ShouldCreateZipWithExpectedEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), "toolhub-diagnostics-test", Guid.NewGuid().ToString("N"));
        var appRoot = Path.Combine(root, "app");
        var outputDirectory = Path.Combine(root, "output");
        var toolsFilePath = Path.Combine(root, "tools.json");
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        var startupErrorPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
        var runtimeLogPath = Path.Combine(logDirectory, $"test-{Guid.NewGuid():N}.ndjson");

        Directory.CreateDirectory(appRoot);
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(logDirectory);
        File.WriteAllText(toolsFilePath, """{"tools":[]}""");
        File.WriteAllText(runtimeLogPath, """{"level":"Information"}""");
        File.WriteAllText(startupErrorPath, "startup error");

        try
        {
            var service = new DiagnosticBundleService(
                appRoot,
                toolsFilePath,
                NullLogger<DiagnosticBundleService>.Instance
            );

            var result = service.Export(outputDirectory);

            Assert.True(File.Exists(result.BundlePath));
            Assert.True(result.EntryCount >= 3);

            using var archive = ZipFile.OpenRead(result.BundlePath);
            var entries = archive.Entries.Select(entry => entry.FullName).ToList();

            Assert.Contains("manifest/host-info.json", entries);
            Assert.Contains("config/tools.json", entries);
            Assert.Contains("logs/startup-error.log", entries);
            Assert.Contains($"logs/runtime/{Path.GetFileName(runtimeLogPath)}", entries);
        }
        finally
        {
            if (File.Exists(runtimeLogPath))
            {
                File.Delete(runtimeLogPath);
            }

            if (File.Exists(startupErrorPath))
            {
                File.Delete(startupErrorPath);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
