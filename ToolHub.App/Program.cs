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
                    defaultPath => window is null ? null : ShowPythonPicker(window, defaultPath),
                    (defaultPath, filter) => window is null ? null : ShowFilePicker(
                        window,
                        "Select File",
                        defaultPath,
                        filter
                    )
                );
            });

        window.Load(indexPath);
        window.WaitForClose();
        ShutdownManagers();
    }

    private static void HandleMessage(
        string rawMessage,
        ToolRegistry registry,
        ProcessManager processManager,
        PythonPackageManager pythonPackageManager,
        TerminalManager terminalManager,
        Action<object> sendMessage,
        Func<string?, string?> browsePython,
        Func<string?, string?, string?> browseFile
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

    internal static string? ResolveRuntimeOverride(string? runtimePath, ToolItem tool)
    {
        if (string.IsNullOrWhiteSpace(runtimePath))
        {
            return null;
        }

        var baseDirectory = !string.IsNullOrWhiteSpace(tool.Cwd)
            ? tool.Cwd
            : Directory.GetCurrentDirectory();

        return PathUtils.ResolvePathOrCommand(runtimePath, baseDirectory);
    }

    private static string? ShowPythonPicker(PhotinoWindow window, string? defaultPath)
    {
        return ShowFilePicker(window, "Select Python Interpreter", defaultPath, null);
    }

    private static string? ShowFilePicker(
        PhotinoWindow window,
        string title,
        string? defaultPath,
        string? filter
    )
    {
        var safeDefaultPath = ResolveDialogDefaultPath(defaultPath);
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

    private static string? ResolveDialogDefaultPath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(rawPath);

            if (File.Exists(fullPath))
            {
                var directory = Path.GetDirectoryName(fullPath);
                return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                    ? directory
                    : null;
            }

            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }

            var parent = Path.GetDirectoryName(fullPath);
            return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent)
                ? parent
                : null;
        }
        catch
        {
            return null;
        }
    }
}
