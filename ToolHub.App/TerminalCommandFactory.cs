using System.Text;
using ToolHub.App.Models;
using ToolHub.App.Runtime;

namespace ToolHub.App;

internal static class TerminalCommandFactory
{
    private static readonly string TerminalScriptDirectory = Path.Combine(
        Path.GetTempPath(),
        "th"
    );

    internal static string BuildRunCommand(
        string shell,
        RunnableTool tool,
        IReadOnlyDictionary<string, string?> args,
        string? runtimePath)
    {
        return BuildRunCommand(shell, RunCommandBuilder.Build(tool, args, runtimePath));
    }

    internal static string BuildRunCommand(string shell, ResolvedRunCommand command)
    {
        var shellKind = TerminalShellUtilities.GetShellKind(shell);
        var sessionSetup = string.IsNullOrWhiteSpace(command.RuntimePath)
            ? null
            : BuildSessionSetupCommand(shellKind, command.ToolType, command.RuntimePath);

        return shellKind switch
        {
            ShellKind.PowerShell => BuildPowerShellCommand(command.WorkingDirectoryOverride, sessionSetup, command),
            ShellKind.Cmd => BuildCmdCommand(command.WorkingDirectoryOverride, sessionSetup, command),
            _ => BuildGenericCommand(command.WorkingDirectoryOverride, sessionSetup, command)
        };
    }

    internal static string? BuildTerminalBootstrapCommand(
        string shell,
        string? cwd,
        string? toolType,
        string? runtimePath)
    {
        var shellKind = TerminalShellUtilities.GetShellKind(shell);
        var sessionSetup = string.IsNullOrWhiteSpace(toolType) || string.IsNullOrWhiteSpace(runtimePath)
            ? null
            : BuildSessionSetupCommand(shellKind, toolType, runtimePath);

        if (string.IsNullOrWhiteSpace(cwd) && string.IsNullOrWhiteSpace(sessionSetup))
        {
            return null;
        }

        return shellKind switch
        {
            ShellKind.PowerShell => BuildPowerShellBootstrapCommand(cwd, sessionSetup),
            ShellKind.Cmd => BuildCmdBootstrapCommand(cwd, sessionSetup),
            _ => BuildGenericBootstrapCommand(cwd, sessionSetup)
        };
    }

    private static string BuildPowerShellCommand(
        string? cwd,
        string? sessionSetup,
        ResolvedRunCommand command)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            lines.Add($"Set-Location -LiteralPath {ToPowerShellLiteral(cwd!)} -ErrorAction Stop");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            lines.Add(sessionSetup);
        }

        lines.Add($"& {ToPowerShellLiteral(command.CommandPath)} {string.Join(" ", command.Arguments.Select(ToPowerShellLiteral))}".TrimEnd());
        return BuildPowerShellScriptInvocation(lines);
    }

    private static string BuildCmdCommand(
        string? cwd,
        string? sessionSetup,
        ResolvedRunCommand command)
    {
        var lines = new List<string> { "@echo off" };

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            lines.Add($"cd /d {ToCmdQuoted(cwd!)}");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            lines.Add(sessionSetup);
        }

        var parts = new List<string> { command.CommandPath };
        parts.AddRange(command.Arguments);
        lines.Add(string.Join(" ", parts.Select(ToCmdQuoted)));
        return BuildCmdScriptInvocation(lines);
    }

    private static string BuildPowerShellBootstrapCommand(string? cwd, string? sessionSetup)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            lines.Add($"Set-Location -LiteralPath {ToPowerShellLiteral(cwd!)} -ErrorAction Stop");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            lines.Add(sessionSetup);
        }

        return BuildPowerShellScriptInvocation(lines);
    }

    private static string BuildCmdBootstrapCommand(string? cwd, string? sessionSetup)
    {
        var lines = new List<string> { "@echo off" };

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            lines.Add($"cd /d {ToCmdQuoted(cwd!)}");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            lines.Add(sessionSetup);
        }

        return BuildCmdScriptInvocation(lines);
    }

    private static string BuildGenericCommand(
        string? cwd,
        string? sessionSetup,
        ResolvedRunCommand command)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            sb.Append("cd ");
            sb.Append(ToGenericQuoted(cwd!));
            sb.Append(" && ");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            sb.Append(sessionSetup);
            sb.Append(" && ");
        }

        var parts = new List<string> { command.CommandPath };
        parts.AddRange(command.Arguments);
        sb.Append(string.Join(" ", parts.Select(ToGenericQuoted)));
        sb.Append("\r");

        return sb.ToString();
    }

    private static string? BuildGenericBootstrapCommand(string? cwd, string? sessionSetup)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(cwd))
        {
            parts.Add($"cd {ToGenericQuoted(cwd!)}");
        }

        if (!string.IsNullOrWhiteSpace(sessionSetup))
        {
            parts.Add(sessionSetup);
        }

        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(" && ", parts) + "\r";
    }

    private static string? BuildSessionSetupCommand(ShellKind shellKind, string toolType, string runtimePath)
    {
        if (string.IsNullOrWhiteSpace(runtimePath) || !Path.IsPathRooted(runtimePath))
        {
            return null;
        }

        var normalizedRuntime = Path.GetFullPath(runtimePath);
        return shellKind switch
        {
            ShellKind.PowerShell => BuildPowerShellSessionSetup(toolType, normalizedRuntime),
            ShellKind.Cmd => BuildCmdSessionSetup(toolType, normalizedRuntime),
            _ => null
        };
    }

    private static string? BuildPowerShellSessionSetup(string toolType, string runtimePath)
    {
        var commands = new List<string>
        {
            "$env:VIRTUAL_ENV_DISABLE_PROMPT = '1'",
            "$env:CONDA_CHANGEPS1 = 'false'",
            "if (-not (Test-Path Function:_ToolHub_Old_Prompt)) { Rename-Item Function:prompt _ToolHub_Old_Prompt -ErrorAction SilentlyContinue }",
            $"function global:prompt {{ Write-Host -NoNewline -ForegroundColor Green {ToPowerShellLiteral($"({runtimePath}) ")}; if (Test-Path Function:_ToolHub_Old_Prompt) {{ & Function:_ToolHub_Old_Prompt }} else {{ \"PS $($executionContext.SessionState.Path.CurrentLocation)$('>' * ($nestedPromptLevel + 1)) \" }} }}"
        };

        if (string.Equals(toolType, "python", StringComparison.OrdinalIgnoreCase))
        {
            commands.Add(BuildPowerShellPythonResetCommand());

            if (TryGetCondaEnvironmentRoot(runtimePath, out var condaRoot))
            {
                commands.Add($"conda activate {ToPowerShellLiteral(condaRoot)} | Out-Null");
                return string.Join("; ", commands);
            }

            if (TryGetVirtualEnvActivateScript(runtimePath, out var activateScript))
            {
                commands.Add($". {ToPowerShellLiteral(activateScript)}");
                return string.Join("; ", commands);
            }

            if (TryGetRuntimeDirectory(runtimePath, out var runtimeDirectory))
            {
                commands.Add(BuildPowerShellPathPrependCommand(runtimeDirectory));
                commands.AddRange(BuildPowerShellBundledPythonEnvironmentCommands(runtimePath));
                commands.Add($"function global:python {{ & {ToPowerShellLiteral(runtimePath)} @args }}");
                commands.Add($"function global:pip {{ & {ToPowerShellLiteral(runtimePath)} -m pip @args }}");
                return string.Join("; ", commands);
            }

            return commands.Count > 4 ? string.Join("; ", commands) : null;
        }

        if (string.Equals(toolType, "node", StringComparison.OrdinalIgnoreCase)
            && TryGetRuntimeDirectory(runtimePath, out var nodeRuntimeDirectory))
        {
            commands.Add(BuildPowerShellRuntimeFunctionResetCommand());
            commands.Add(BuildPowerShellPathPrependCommand(nodeRuntimeDirectory));
            commands.Add($"function global:node {{ & {ToPowerShellLiteral(runtimePath)} @args }}");
            return string.Join("; ", commands);
        }

        return null;
    }

    private static string? BuildCmdSessionSetup(string toolType, string runtimePath)
    {
        var commands = new List<string>
        {
            "set \"VIRTUAL_ENV_DISABLE_PROMPT=1\"",
            "set \"CONDA_CHANGEPS1=false\"",
            $"set \"PROMPT=({runtimePath}) $P$G\""
        };

        if (string.Equals(toolType, "python", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetVirtualEnvActivateBatch(runtimePath, out var activateBatch))
            {
                commands.Add($"call {ToCmdQuoted(activateBatch)}");
                return string.Join(" && ", commands);
            }

            if (TryGetCondaEnvironmentRoot(runtimePath, out var condaRoot))
            {
                commands.Add($"call conda activate {ToCmdQuoted(condaRoot)}");
                return string.Join(" && ", commands);
            }

            if (TryGetRuntimeDirectory(runtimePath, out var runtimeDirectory))
            {
                commands.Add($"set \"PATH={runtimeDirectory};%PATH%\"");
                commands.AddRange(BuildCmdBundledPythonEnvironmentCommands(runtimePath));
                return string.Join(" && ", commands);
            }

            return commands.Count > 3 ? string.Join(" && ", commands) : null;
        }

        if (string.Equals(toolType, "node", StringComparison.OrdinalIgnoreCase)
            && TryGetRuntimeDirectory(runtimePath, out var nodeRuntimeDirectory))
        {
            commands.Add($"set \"PATH={nodeRuntimeDirectory};%PATH%\"");
            return string.Join(" && ", commands);
        }

        return null;
    }

    private static string BuildPowerShellPythonResetCommand()
    {
        return string.Join("; ",
        [
            BuildPowerShellRuntimeFunctionResetCommand(),
            "Remove-Item Env:PYTHONHOME -ErrorAction SilentlyContinue",
            "Remove-Item Env:TCL_LIBRARY -ErrorAction SilentlyContinue",
            "Remove-Item Env:TK_LIBRARY -ErrorAction SilentlyContinue",
            "if (Get-Command conda -ErrorAction SilentlyContinue) { while ($env:CONDA_SHLVL -and [int]$env:CONDA_SHLVL -gt 0) { conda deactivate | Out-Null } } elseif (Get-Command deactivate -ErrorAction SilentlyContinue) { try { deactivate | Out-Null } catch {} }"
        ]);
    }

    private static string BuildPowerShellRuntimeFunctionResetCommand()
    {
        return "foreach ($toolHubName in 'python', 'pip', 'node') { if (Test-Path (\"Function:\" + $toolHubName)) { Remove-Item (\"Function:\" + $toolHubName) -ErrorAction SilentlyContinue }; if (Test-Path (\"Alias:\" + $toolHubName)) { Remove-Item (\"Alias:\" + $toolHubName) -ErrorAction SilentlyContinue } }";
    }

    private static string BuildPowerShellPathPrependCommand(string runtimeDirectory)
    {
        var literal = ToPowerShellLiteral(runtimeDirectory);
        return $"$env:PATH = {literal} + ';' + ((($env:PATH -split ';') | Where-Object {{ $_ -and $_ -ne {literal} }}) -join ';')";
    }

    private static IEnumerable<string> BuildPowerShellBundledPythonEnvironmentCommands(string runtimePath)
    {
        if (!TryGetPythonRuntimeRoot(runtimePath, out var pythonRoot))
        {
            return [];
        }

        var commands = new List<string> { $"$env:PYTHONHOME = {ToPowerShellLiteral(pythonRoot)}" };

        if (TryGetTclLibraryPath(pythonRoot, out var tclLibrary))
        {
            commands.Add($"$env:TCL_LIBRARY = {ToPowerShellLiteral(tclLibrary)}");
        }

        if (TryGetTkLibraryPath(pythonRoot, out var tkLibrary))
        {
            commands.Add($"$env:TK_LIBRARY = {ToPowerShellLiteral(tkLibrary)}");
        }

        return commands;
    }

    private static IEnumerable<string> BuildCmdBundledPythonEnvironmentCommands(string runtimePath)
    {
        if (!TryGetPythonRuntimeRoot(runtimePath, out var pythonRoot))
        {
            return [];
        }

        var commands = new List<string> { $"set \"PYTHONHOME={pythonRoot}\"" };

        if (TryGetTclLibraryPath(pythonRoot, out var tclLibrary))
        {
            commands.Add($"set \"TCL_LIBRARY={tclLibrary}\"");
        }

        if (TryGetTkLibraryPath(pythonRoot, out var tkLibrary))
        {
            commands.Add($"set \"TK_LIBRARY={tkLibrary}\"");
        }

        return commands;
    }

    private static bool TryGetPythonRuntimeRoot(string runtimePath, out string pythonRoot)
    {
        pythonRoot = string.Empty;
        try
        {
            var runtimeDirectory = Path.GetDirectoryName(runtimePath);
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                return false;
            }

            pythonRoot = Path.GetFullPath(runtimeDirectory);
            return Directory.Exists(pythonRoot);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetTclLibraryPath(string pythonRoot, out string tclLibraryPath)
    {
        return TryGetFirstExistingDirectory(
            out tclLibraryPath,
            Path.Combine(pythonRoot, "tcl", "tcl8.6"),
            Path.Combine(pythonRoot, "Library", "lib", "tcl8.6"),
            Path.Combine(pythonRoot, "lib", "tcl8.6")
        );
    }

    private static bool TryGetTkLibraryPath(string pythonRoot, out string tkLibraryPath)
    {
        return TryGetFirstExistingDirectory(
            out tkLibraryPath,
            Path.Combine(pythonRoot, "tcl", "tk8.6"),
            Path.Combine(pythonRoot, "Library", "lib", "tk8.6"),
            Path.Combine(pythonRoot, "lib", "tk8.6")
        );
    }

    private static bool TryGetFirstExistingDirectory(out string resolvedPath, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        resolvedPath = string.Empty;
        return false;
    }

    private static bool TryGetRuntimeDirectory(string runtimePath, out string runtimeDirectory)
    {
        runtimeDirectory = Path.GetDirectoryName(runtimePath) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(runtimeDirectory);
    }

    private static bool TryGetVirtualEnvActivateScript(string runtimePath, out string activateScriptPath)
    {
        activateScriptPath = string.Empty;
        var runtimeDirectory = Path.GetDirectoryName(runtimePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(runtimeDirectory), "Scripts", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var environmentRoot = Directory.GetParent(runtimeDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(environmentRoot)
            || !File.Exists(Path.Combine(environmentRoot, "pyvenv.cfg")))
        {
            return false;
        }

        var candidate = Path.Combine(runtimeDirectory, "Activate.ps1");
        if (!File.Exists(candidate))
        {
            return false;
        }

        activateScriptPath = candidate;
        return true;
    }

    private static bool TryGetVirtualEnvActivateBatch(string runtimePath, out string activateBatchPath)
    {
        activateBatchPath = string.Empty;
        var runtimeDirectory = Path.GetDirectoryName(runtimePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(runtimeDirectory), "Scripts", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var environmentRoot = Directory.GetParent(runtimeDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(environmentRoot)
            || !File.Exists(Path.Combine(environmentRoot, "pyvenv.cfg")))
        {
            return false;
        }

        var candidate = Path.Combine(runtimeDirectory, "activate.bat");
        if (!File.Exists(candidate))
        {
            return false;
        }

        activateBatchPath = candidate;
        return true;
    }

    private static bool TryGetCondaEnvironmentRoot(string runtimePath, out string environmentRoot)
    {
        environmentRoot = string.Empty;
        var runtimeDirectory = Path.GetDirectoryName(runtimePath);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return false;
        }

        var candidate = Path.GetFullPath(runtimeDirectory);
        if (Directory.Exists(Path.Combine(candidate, "conda-meta")))
        {
            environmentRoot = candidate;
            return true;
        }

        return false;
    }

    private static string BuildPowerShellScriptInvocation(IReadOnlyList<string> lines)
    {
        var allLines = BuildPowerShellInvocationPrelude()
            .Concat(lines)
            .Concat(["Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue"])
            .ToArray();

        var scriptPath = CreateTerminalScriptFile(".ps1", allLines);
        return $". \"$env:TEMP\\th\\{Path.GetFileName(scriptPath)}\"\r";
    }

    private static string BuildCmdScriptInvocation(IReadOnlyList<string> lines)
    {
        var allLines = BuildCmdInvocationPrelude()
            .Concat(lines)
            .Concat(["del \"%~f0\" >nul 2>nul"])
            .ToArray();

        var scriptPath = CreateTerminalScriptFile(".cmd", allLines);
        return $"call \"%TEMP%\\th\\{Path.GetFileName(scriptPath)}\"\r";
    }

    private static string CreateTerminalScriptFile(string extension, IReadOnlyList<string> lines)
    {
        Directory.CreateDirectory(TerminalScriptDirectory);
        CleanupOldTerminalScripts();

        var scriptPath = Path.Combine(
            TerminalScriptDirectory,
            $"th-{Guid.NewGuid():N}".Substring(0, 11) + extension
        );

        File.WriteAllLines(scriptPath, lines, ResolveTerminalScriptEncoding(extension));
        return scriptPath;
    }

    private static IEnumerable<string> BuildPowerShellInvocationPrelude() => [];

    private static IEnumerable<string> BuildCmdInvocationPrelude() => [];

    private static Encoding ResolveTerminalScriptEncoding(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".ps1" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            ".cmd" or ".bat" => Encoding.Default,
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };
    }

    private static void CleanupOldTerminalScripts()
    {
        try
        {
            if (!Directory.Exists(TerminalScriptDirectory))
            {
                return;
            }

            var threshold = DateTime.UtcNow.AddHours(-12);
            foreach (var file in Directory.EnumerateFiles(TerminalScriptDirectory, "th-*.*"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < threshold)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                }
            }

            var legacyDirectory = Path.Combine(Path.GetTempPath(), "ToolHub", "terminal-scripts");
            if (Directory.Exists(legacyDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(legacyDirectory, "toolhub-*.*"))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(file) < threshold)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
    }

    private static string ToPowerShellLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static string ToCmdQuoted(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string ToGenericQuoted(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
