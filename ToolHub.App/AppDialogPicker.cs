using Photino.NET;

namespace ToolHub.App;

internal sealed class AppDialogPicker(string appRoot) : IFileDialogService
{
    public string? ShowPythonPicker(PhotinoWindow window, string? defaultPath)
    {
        return ShowFilePicker(window, "Select Python Interpreter", defaultPath, null, null);
    }

    public string? ShowFilePicker(
        PhotinoWindow window,
        string title,
        string? defaultPath,
        string? filter,
        string? purpose
    )
    {
        var safeDefaultPath = ResolveDialogDefaultPath(defaultPath, purpose);
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

    private string? ResolveDialogDefaultPath(string? rawPath, string? purpose)
    {
        var fallbackPath = ResolveFallbackDialogPath(purpose);

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

    private string? ResolveFallbackDialogPath(string? purpose)
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
}
