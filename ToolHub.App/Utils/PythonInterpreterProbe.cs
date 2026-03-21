using System.Collections.Concurrent;
using System.Diagnostics;

namespace ToolHub.App.Utils;

public static class PythonInterpreterProbe
{
    /// <summary>TTL for cached <c>IsUsable</c> probe results.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>Cache of normalized interpreter path to probe result and timestamp.</summary>
    private static readonly ConcurrentDictionary<string, (bool IsUsable, DateTime CheckedAtUtc)> UsabilityCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] DefaultCandidates =
    [
        "python",
        "py",
        "python3"
    ];

    public static bool IsUsable(string? interpreter)
    {
        if (string.IsNullOrWhiteSpace(interpreter))
        {
            return false;
        }

        var normalized = Normalize(interpreter);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        // Return the cached result while it is still fresh.
        if (UsabilityCache.TryGetValue(normalized, out var cached)
            && (DateTime.UtcNow - cached.CheckedAtUtc) < CacheTtl)
        {
            return cached.IsUsable;
        }

        var result = CheckUsability(normalized);
        UsabilityCache[normalized] = (result, DateTime.UtcNow);
        return result;
    }

    /// <summary>Runs the actual usability probe without using the cache.</summary>
    private static bool CheckUsability(string normalized)
    {
        if (!Path.IsPathRooted(normalized))
        {
            return ExistsOnPath(normalized);
        }

        if (!File.Exists(normalized))
        {
            return false;
        }

        if (IsBrokenVirtualEnvInterpreter(normalized))
        {
            return false;
        }

        return CanStartInterpreter(normalized);
    }

    public static string? ResolvePreferred(string? preferred, string? fallback)
    {
        var candidates = new List<string?>(2 + DefaultCandidates.Length)
        {
            preferred,
            fallback
        };
        candidates.AddRange(GetBundledPythonCandidates());
        candidates.AddRange(DefaultCandidates);

        foreach (var candidate in candidates)
        {
            if (IsUsable(candidate))
            {
                return Normalize(candidate!);
            }
        }

        return null;
    }

    public static string? ResolveBundled()
    {
        foreach (var candidate in GetBundledPythonCandidates())
        {
            if (IsUsable(candidate))
            {
                return Normalize(candidate);
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        return value.Trim().Trim('"');
    }

    private static bool IsBrokenVirtualEnvInterpreter(string interpreterPath)
    {
        try
        {
            var interpreterDirectory = Path.GetDirectoryName(interpreterPath);
            if (string.IsNullOrWhiteSpace(interpreterDirectory))
            {
                return false;
            }

            var venvRoot = Directory.GetParent(interpreterDirectory)?.FullName;
            if (string.IsNullOrWhiteSpace(venvRoot))
            {
                return false;
            }

            var pyVenvConfigPath = Path.Combine(venvRoot, "pyvenv.cfg");
            if (!File.Exists(pyVenvConfigPath))
            {
                return false;
            }

            string? home = null;
            string? executable = null;

            foreach (var line in File.ReadLines(pyVenvConfigPath))
            {
                var split = line.Split('=', 2, StringSplitOptions.TrimEntries);
                if (split.Length != 2)
                {
                    continue;
                }

                var key = split[0].Trim().TrimStart('\uFEFF');
                var value = split[1].Trim().Trim('"');

                if (key.Equals("home", StringComparison.OrdinalIgnoreCase))
                {
                    home = value;
                    continue;
                }

                if (key.Equals("executable", StringComparison.OrdinalIgnoreCase))
                {
                    executable = value;
                }
            }

            if (!string.IsNullOrWhiteSpace(executable))
            {
                var normalizedExecutable = Path.GetFullPath(executable);
                return !File.Exists(normalizedExecutable);
            }

            if (!string.IsNullOrWhiteSpace(home))
            {
                var normalizedHome = Path.GetFullPath(home);
                if (!Directory.Exists(normalizedHome))
                {
                    return true;
                }

                var windowsPython = Path.Combine(normalizedHome, "python.exe");
                var unixPython = Path.Combine(normalizedHome, "python");
                return !File.Exists(windowsPython) && !File.Exists(unixPython);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanStartInterpreter(string interpreterPath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = interpreterPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                return false;
            }

            if (!process.WaitForExit(1500))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore kill failures.
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool ExistsOnPath(string command)
    {
        var trimmed = Normalize(command);
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.Contains('\\') || trimmed.Contains('/') || trimmed.Contains(':'))
        {
            return File.Exists(trimmed);
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (pathEnv.Length == 0)
        {
            return false;
        }

        var pathExtEnv = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD;.COM";
        var extensions = pathExtEnv
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasExtension = Path.HasExtension(trimmed);

        foreach (var directory in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            try
            {
                if (hasExtension)
                {
                    var fullPath = Path.Combine(directory, trimmed);
                    if (File.Exists(fullPath))
                    {
                        return true;
                    }
                }
                else
                {
                    foreach (var ext in extensions)
                    {
                        var fullPath = Path.Combine(directory, $"{trimmed}{ext}");
                        if (File.Exists(fullPath))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore invalid PATH entries.
            }
        }

        return false;
    }

    private static IEnumerable<string> GetBundledPythonCandidates()
    {
        string[] roots =
        [
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        ];

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            string[] candidates =
            [
                Path.Combine(root, "python", "python.exe"),
                Path.Combine(root, "python.exe")
            ];

            foreach (var candidate in candidates)
            {
                var normalized = Path.GetFullPath(candidate);
                if (emitted.Add(normalized))
                {
                    yield return normalized;
                }
            }
        }
    }
}
