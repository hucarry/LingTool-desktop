using System.Collections.Concurrent;
using System.Diagnostics;

namespace ToolHub.App.Utils;

public static class NodeRuntimeProbe
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static readonly ConcurrentDictionary<string, (bool IsUsable, DateTime CheckedAtUtc)> UsabilityCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] DefaultCandidates =
    [
        "node",
        "node.exe"
    ];

    public static bool IsUsable(string? runtimePath)
    {
        if (string.IsNullOrWhiteSpace(runtimePath))
        {
            return false;
        }

        var normalized = Normalize(runtimePath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (UsabilityCache.TryGetValue(normalized, out var cached)
            && (DateTime.UtcNow - cached.CheckedAtUtc) < CacheTtl)
        {
            return cached.IsUsable;
        }

        var result = CheckUsability(normalized);
        UsabilityCache[normalized] = (result, DateTime.UtcNow);
        return result;
    }

    public static string? ResolvePreferred(string? preferred, string? fallback)
    {
        foreach (var candidate in new[] { preferred, fallback }.Concat(DefaultCandidates))
        {
            if (IsUsable(candidate))
            {
                return Normalize(candidate!);
            }
        }

        return null;
    }

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

        return CanStartRuntime(normalized);
    }

    private static string Normalize(string value)
    {
        return value.Trim().Trim('"');
    }

    private static bool CanStartRuntime(string runtimePath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = runtimePath,
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
                    if (File.Exists(Path.Combine(directory, trimmed)))
                    {
                        return true;
                    }
                }
                else
                {
                    foreach (var ext in extensions)
                    {
                        if (File.Exists(Path.Combine(directory, $"{trimmed}{ext}")))
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
}
