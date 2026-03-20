using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Photino.NET;
using ToolHub.App.Models;
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
            RunApp(args);
        }
        catch (Exception ex)
        {
            ReportStartupFailure(ex);
        }
    }

    private static void RunApp(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        TryConfigureConsoleEncoding();

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                ReportStartupFailure(exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            ReportStartupFailure(eventArgs.Exception);
            eventArgs.SetObserved();
        };

        var appRoot = PathUtils.ResolveProjectRoot();
        var toolsFilePath = Path.Combine(appRoot, "tools.json");
        var registry = new ToolRegistry(toolsFilePath, JsonOptions);
        var pythonPackageManager = new PythonPackageManager(appRoot);

        PhotinoWindow? window = null;
        var sendLock = new object();

        void SendMessage(object payload)
        {
            lock (sendLock)
            {
                if (window is null)
                {
                    return;
                }

                var json = JsonSerializer.Serialize(payload, JsonOptions);
                window.SendWebMessage(json);
            }
        }

        var processManager = new ProcessManager(SendMessage);
        var terminalManager = new TerminalManager(SendMessage);
        var shutdownTriggered = 0;

        void ShutdownManagers()
        {
            if (Interlocked.Exchange(ref shutdownTriggered, 1) == 1)
            {
                return;
            }

            try
            {
                terminalManager.Dispose();
            }
            catch
            {
                // Ignore shutdown errors.
            }

            try
            {
                processManager.Dispose();
            }
            catch
            {
                // Ignore shutdown errors.
            }
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => ShutdownManagers();

        var indexPath = ResolveFrontendIndexPath(appRoot);
        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException(
                $"Frontend not found: {indexPath}. Run `cd frontend && npm install && npm run build` first."
            );
        }

        window = new PhotinoWindow().SetTitle("ToolHub Local Tool Platform")
            .SetUseOsDefaultLocation(true)
            .SetSize(1380, 900)
            .Center()
            .RegisterWindowClosingHandler((sender, eventArgs) =>
            {
                ShutdownManagers();
                return false;
            })
            .RegisterWebMessageReceivedHandler((sender, rawMessage) =>
            {
                HandleMessage(
                    rawMessage,
                    registry,
                    processManager,
                    pythonPackageManager,
                    terminalManager,
                    SendMessage,
                    defaultPath => window is null ? null : ShowPythonPicker(window, appRoot, defaultPath),
                    (defaultPath, filter, purpose) => window is null ? null : ShowFilePicker(
                        window,
                        "Select File",
                        appRoot,
                        defaultPath,
                        filter,
                        purpose
                    )
                );
            });

        window.Load(indexPath);
        window.WaitForClose();
        ShutdownManagers();
        Environment.Exit(0);
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
                caption: "ToolHub 启动失败",
                type: 0x00000010
            );
        }
        catch
        {
            // Avoid secondary crashes while reporting startup errors.
        }
    }

    private static void TryConfigureConsoleEncoding()
    {
        try
        {
            _ = Console.IsOutputRedirected;
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // WinExe started without a console can have invalid std handles.
        }
        catch (InvalidOperationException)
        {
            // Some hosts do not expose a writable console stream.
        }

        try
        {
            _ = Console.IsInputRedirected;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // WinExe started without a console can have invalid std handles.
        }
        catch (InvalidOperationException)
        {
            // Some hosts do not expose a readable console stream.
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
            hints.Add("发布目录可能不完整，请确认 wwwroot 等文件已随程序一起分发。");
        }

        if (flattened.Contains("WebView2", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("当前系统可能缺少或损坏 Microsoft Edge WebView2 Runtime。");
        }

        if (flattened.Contains("Photino", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("桌面壳窗口初始化失败，通常与 WebView2 运行环境或本机系统组件有关。");
        }

        if (hints.Count == 0)
        {
            hints.Add("这是一个 .NET 启动期未处理异常，需要查看日志定位具体根因。");
        }

        return string.Join(
            Environment.NewLine,
            [
                "程序启动失败。",
                string.Empty,
                $"异常类型: {exception.GetType().FullName}",
                $"异常信息: {exception.Message}",
                string.Empty,
                "可能原因:",
                ..hints.Select(static hint => $"- {hint}"),
                string.Empty,
                $"详细日志: {logPath}"
            ]
        );
    }

    private static void HandleMessage(
        string rawMessage,
        ToolRegistry registry,
        ProcessManager processManager,
        PythonPackageManager pythonPackageManager,
        TerminalManager terminalManager,
        Action<object> sendMessage,
        Func<string?, string?> browsePython,
        Func<string?, string?, string?, string?> browseFile
    )
    {
        try
        {
            var context = new MessageContext(
                registry,
                processManager,
                pythonPackageManager,
                terminalManager,
                sendMessage,
                browsePython,
                browseFile,
                JsonOptions
            );

            MessageRouter.Route(context, rawMessage);
        }
        catch (Exception ex)
        {
            sendMessage(new ErrorMessage("Failed to handle message.", ex.Message));
        }
    }

    private static string ResolveFrontendIndexPath(string appRoot)
    {
        var fromOutputFolder = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(fromOutputFolder))
        {
            return fromOutputFolder;
        }

        var fromProjectFolder = Path.Combine(appRoot, "ToolHub.App", "wwwroot", "index.html");
        if (File.Exists(fromProjectFolder))
        {
            return fromProjectFolder;
        }

        return fromProjectFolder;
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

    private static string? ShowPythonPicker(PhotinoWindow window, string appRoot, string? defaultPath)
    {
        return ShowFilePicker(window, "Select Python Interpreter", appRoot, defaultPath, null, null);
    }

    private static string? ShowFilePicker(
        PhotinoWindow window,
        string title,
        string appRoot,
        string? defaultPath,
        string? filter,
        string? purpose
    )
    {
        var safeDefaultPath = ResolveDialogDefaultPath(appRoot, defaultPath, purpose);
        var filters = ResolveFileFilters(filter);

        var candidates = window.ShowOpenFile(
            title: title,
            defaultPath: safeDefaultPath,
            multiSelect: false,
            filters: filters
        );

        return candidates is { Length: > 0 }
            ? candidates[0]
            : null;
    }

    private static (string Name, string[] Extensions)[]? ResolveFileFilters(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var patterns = filter
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value =>
            {
                if (value is "*" or "*.*")
                {
                    return "*";
                }

                if (value.StartsWith("*."))
                {
                    return value;
                }

                if (value.StartsWith('.'))
                {
                    return $"*{value}";
                }

                return $"*.{value}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (patterns.Length == 0)
        {
            return null;
        }

        var normalized = patterns
            .Select(pattern => pattern.TrimStart('*', '.'))
            .ToArray();

        string name;
        if (normalized.Length == 1 && string.Equals(normalized[0], "py", StringComparison.OrdinalIgnoreCase))
        {
            name = "Python Files (*.py)";
        }
        else if (normalized.Length == 1 && string.Equals(normalized[0], "exe", StringComparison.OrdinalIgnoreCase))
        {
            name = "Executable Files (*.exe)";
        }
        else if (normalized.Length == 1)
        {
            name = $"{normalized[0].ToLowerInvariant()} files";
        }
        else
        {
            var display = string.Join(", ", patterns);
            name = $"Filtered Files ({display})";
        }

        return
        [
            (name, patterns),
            ("All Files (*.*)", new[] { "*" })
        ];
    }

    private static string? ResolveDialogDefaultPath(string appRoot, string? rawPath, string? purpose)
    {
        var fallbackPath = ResolveFallbackDialogPath(appRoot, purpose);

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return fallbackPath;
        }

        try
        {
            var fullPath = Path.IsPathRooted(rawPath)
                ? Path.GetFullPath(rawPath)
                : Path.GetFullPath(Path.Combine(appRoot, rawPath));

            return ResolveExistingDialogPath(fullPath) ?? fallbackPath;
        }
        catch
        {
            return fallbackPath;
        }
    }

    private static string? ResolveFallbackDialogPath(string appRoot, string? purpose)
    {
        if (IsToolPathBrowse(purpose))
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var resolvedDesktop = ResolveExistingDialogPath(desktop);
            if (!string.IsNullOrWhiteSpace(resolvedDesktop))
            {
                return resolvedDesktop;
            }
        }

        return ResolveExistingDialogPath(appRoot);
    }

    private static bool IsToolPathBrowse(string? purpose)
    {
        return string.Equals(purpose, "addToolPath", StringComparison.OrdinalIgnoreCase)
            || string.Equals(purpose, "editToolPath", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveExistingDialogPath(string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return null;
        }

        if (File.Exists(candidatePath))
        {
            var directory = Path.GetDirectoryName(candidatePath);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                ? directory
                : null;
        }

        if (Directory.Exists(candidatePath))
        {
            return candidatePath;
        }

        var parent = Path.GetDirectoryName(candidatePath);
        return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent)
            ? parent
            : null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
