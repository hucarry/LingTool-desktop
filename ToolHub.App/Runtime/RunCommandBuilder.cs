using ToolHub.App.Models;
using ToolHub.App.Utils;

namespace ToolHub.App.Runtime;

public static class RunCommandBuilder
{
    public static ResolvedRunCommand Build(
        RunnableTool tool,
        IReadOnlyDictionary<string, string?> args,
        string? runtimeOverride
    )
    {
        var resolvedArgs = ArgsSpecCompiler.BuildArguments(tool.ArgsSpec, tool.ArgsTemplate, args);
        var workingDirectoryOverride = string.IsNullOrWhiteSpace(tool.Cwd) ? null : tool.Cwd;
        var workingDirectory = ResolveWorkingDirectory(workingDirectoryOverride);

        if (string.Equals(tool.Type, "python", StringComparison.OrdinalIgnoreCase))
        {
            var interpreter = PythonInterpreterProbe.ResolvePreferred(runtimeOverride, tool.RuntimePath);
            if (string.IsNullOrWhiteSpace(interpreter))
            {
                throw new InvalidOperationException(RuntimeErrorMessages.NoUsablePythonInterpreter);
            }

            return new ResolvedRunCommand
            {
                ToolType = tool.Type,
                CommandPath = interpreter,
                WorkingDirectory = workingDirectory,
                WorkingDirectoryOverride = workingDirectoryOverride,
                RuntimePath = interpreter,
                Arguments = PrependArgument(tool.Path, resolvedArgs)
            };
        }

        if (string.Equals(tool.Type, "node", StringComparison.OrdinalIgnoreCase))
        {
            var runtime = NodeRuntimeProbe.ResolvePreferred(runtimeOverride, tool.RuntimePath);
            if (string.IsNullOrWhiteSpace(runtime))
            {
                throw new InvalidOperationException(RuntimeErrorMessages.NoUsableNodeRuntime);
            }

            return new ResolvedRunCommand
            {
                ToolType = tool.Type,
                CommandPath = runtime,
                WorkingDirectory = workingDirectory,
                WorkingDirectoryOverride = workingDirectoryOverride,
                RuntimePath = runtime,
                Arguments = PrependArgument(tool.Path, resolvedArgs)
            };
        }

        if (string.Equals(tool.Type, "command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tool.Type, "executable", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedRunCommand
            {
                ToolType = tool.Type,
                CommandPath = tool.Path,
                WorkingDirectory = workingDirectory,
                WorkingDirectoryOverride = workingDirectoryOverride,
                RuntimePath = null,
                Arguments = resolvedArgs
            };
        }

        throw new NotSupportedException(RuntimeErrorMessages.UnsupportedToolType(tool.Type));
    }

    private static string ResolveWorkingDirectory(string? cwd)
    {
        return string.IsNullOrWhiteSpace(cwd)
            ? Directory.GetCurrentDirectory()
            : cwd;
    }

    private static IReadOnlyList<string> PrependArgument(string first, IReadOnlyList<string> remaining)
    {
        var values = new List<string>(remaining.Count + 1)
        {
            first
        };
        values.AddRange(remaining);
        return values;
    }
}
