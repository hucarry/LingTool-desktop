namespace ToolHub.App;

internal static class TerminalShellUtilities
{
    internal static string ResolveWorkingDirectory(string? cwd)
    {
        var raw = string.IsNullOrWhiteSpace(cwd)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : cwd.Trim();

        return Path.GetFullPath(raw);
    }

    internal static string[] GetShellArgs(string shell)
    {
        if (GetShellKind(shell) == ShellKind.PowerShell)
        {
            return ["-ExecutionPolicy", "Bypass"];
        }

        return [];
    }

    internal static string ResolveShell(string? shell)
    {
        if (!string.IsNullOrWhiteSpace(shell))
        {
            return shell.Trim();
        }

        if (OperatingSystem.IsWindows())
        {
            return "powershell.exe";
        }

        return "/bin/bash";
    }

    internal static ShellKind GetShellKind(string shell)
    {
        var name = GetShellExecutableName(shell);
        return name switch
        {
            "powershell.exe" or "powershell" or "pwsh.exe" or "pwsh" => ShellKind.PowerShell,
            "cmd.exe" or "cmd" => ShellKind.Cmd,
            _ => ShellKind.Other
        };
    }

    internal static string GetShellExecutableName(string shell)
    {
        var trimmed = shell.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        string executable;
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            executable = endQuote > 1
                ? trimmed[1..endQuote]
                : trimmed.Trim('"');
        }
        else
        {
            var firstSpace = trimmed.IndexOf(' ');
            executable = firstSpace < 0 ? trimmed : trimmed[..firstSpace];
        }

        return Path.GetFileName(executable).ToLowerInvariant();
    }
}

internal enum ShellKind
{
    Cmd,
    PowerShell,
    Other
}
