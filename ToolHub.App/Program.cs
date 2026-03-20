using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            AppBootstrap.Run(JsonOptions, ReportStartupFailure);
        }
        catch (Exception ex)
        {
            ReportStartupFailure(ex);
        }
    }

    private static void ReportStartupFailure(Exception exception)
    {
        try
        {
            var logPath = WriteStartupErrorLog(exception);
            var detail = BuildFriendlyStartupMessage(exception, logPath);
            MessageBoxW(
                hWnd: 0,
                text: detail,
                caption: "ToolHub Startup Error",
                type: 0x00000010
            );
        }
        catch
        {
            // Avoid secondary crashes while reporting startup errors.
        }
    }

    private static string WriteStartupErrorLog(Exception exception)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
        var lines = new[]
        {
            $"Time: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            $"BaseDirectory: {AppContext.BaseDirectory}",
            $"CurrentDirectory: {Directory.GetCurrentDirectory()}",
            $"OSVersion: {Environment.OSVersion}",
            $"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}",
            $"Framework: {RuntimeInformation.FrameworkDescription}",
            "Exception:",
            exception.ToString(),
            string.Empty
        };

        File.WriteAllLines(logPath, lines, Encoding.UTF8);
        return logPath;
    }

    private static string BuildFriendlyStartupMessage(Exception exception, string logPath)
    {
        var hints = new List<string>();
        var flattened = exception.ToString();

        if (exception is FileNotFoundException)
        {
            hints.Add("The frontend bundle may be missing. Make sure ToolHub.App/wwwroot/index.html exists.");
        }

        if (flattened.Contains("WebView2", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("Install or repair Microsoft Edge WebView2 Runtime.");
        }

        if (flattened.Contains("Photino", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("Verify the desktop host can initialize WebView2 and access the bundled frontend files.");
        }

        if (hints.Count == 0)
        {
            hints.Add("Check the runtime environment and review the generated startup log for details.");
        }

        return string.Join(
            Environment.NewLine,
            [
                "ToolHub failed to start.",
                string.Empty,
                $"Exception type: {exception.GetType().FullName}",
                $"Exception message: {exception.Message}",
                string.Empty,
                "Hints:",
                ..hints.Select(static hint => $"- {hint}"),
                string.Empty,
                $"Startup log: {logPath}"
            ]
        );
    }

    internal static string? ResolveRuntimeOverride(string? runtimePath, string? cwd)
    {
        if (string.IsNullOrWhiteSpace(runtimePath))
        {
            return null;
        }

        var baseDirectory = !string.IsNullOrWhiteSpace(cwd)
            ? cwd
            : Directory.GetCurrentDirectory();

        return PathUtils.ResolvePathOrCommand(runtimePath, baseDirectory);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
