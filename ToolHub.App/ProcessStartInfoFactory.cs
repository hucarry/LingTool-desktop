using System.Diagnostics;
using System.Text;
using ToolHub.App.Runtime;

namespace ToolHub.App;

internal static class ProcessStartInfoFactory
{
    internal static ProcessStartInfo Build(ResolvedRunCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = command.WorkingDirectory
        };

        if (string.Equals(command.ToolType, "python", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = command.CommandPath;
            ApplyBundledPythonEnvironment(startInfo);
        }
        else if (string.Equals(command.ToolType, "node", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ToolType, "command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.ToolType, "executable", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = command.CommandPath;
        }
        else
        {
            throw new NotSupportedException(RuntimeErrorMessages.UnsupportedToolType(command.ToolType));
        }

        foreach (var arg in command.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
    }

    private static void ApplyBundledPythonEnvironment(ProcessStartInfo startInfo)
    {
        if (string.IsNullOrWhiteSpace(startInfo.FileName) || !Path.IsPathRooted(startInfo.FileName))
        {
            return;
        }

        var pythonRoot = Path.GetDirectoryName(startInfo.FileName);
        if (string.IsNullOrWhiteSpace(pythonRoot) || !Directory.Exists(pythonRoot))
        {
            return;
        }

        startInfo.Environment["PYTHONHOME"] = pythonRoot;

        var tclLibrary = ResolveFirstExistingDirectory(
            Path.Combine(pythonRoot, "tcl", "tcl8.6"),
            Path.Combine(pythonRoot, "Library", "lib", "tcl8.6"),
            Path.Combine(pythonRoot, "lib", "tcl8.6")
        );
        if (!string.IsNullOrWhiteSpace(tclLibrary))
        {
            startInfo.Environment["TCL_LIBRARY"] = tclLibrary;
        }

        var tkLibrary = ResolveFirstExistingDirectory(
            Path.Combine(pythonRoot, "tcl", "tk8.6"),
            Path.Combine(pythonRoot, "Library", "lib", "tk8.6"),
            Path.Combine(pythonRoot, "lib", "tk8.6")
        );
        if (!string.IsNullOrWhiteSpace(tkLibrary))
        {
            startInfo.Environment["TK_LIBRARY"] = tkLibrary;
        }
    }

    private static string? ResolveFirstExistingDirectory(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
