using ToolHub.App.Models;
using ToolHub.App.Runtime;
using ToolHub.App.Utils;

namespace ToolHub.App;

internal sealed class ToolExecutionSupport(IToolRegistry registry)
{
    internal bool TryResolveRunnableTool(
        MessageContext context,
        string toolId,
        out RunnableTool tool)
    {
        tool = null!;

        var view = registry.GetToolById(toolId);
        if (view is null)
        {
            context.SendMessage(new ErrorMessage(ToolErrorMessages.ToolNotFound(toolId)));
            return false;
        }

        if (!view.Valid)
        {
            context.SendMessage(new ErrorMessage(ToolErrorMessages.ToolInvalid(view.Name), view.ValidationMessage));
            return false;
        }

        tool = CreateRunnableTool(view);
        return true;
    }

    internal RunnableTool CreateRunnableTool(ToolItem source)
    {
        return new RunnableTool
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            Path = source.Path,
            RuntimePath = source.RuntimePath,
            Cwd = source.Cwd,
            ArgsTemplate = source.ArgsTemplate,
            ArgsSpec = ArgsSpecCompiler.Normalize(source.ArgsSpec)
        };
    }

    internal bool TryBuildResolvedRunCommand(
        MessageContext context,
        RunnableTool tool,
        IReadOnlyDictionary<string, string?> args,
        string? rawRuntimePath,
        out ResolvedRunCommand resolvedCommand)
    {
        resolvedCommand = null!;
        var runtimeOverride = Program.ResolveRuntimeOverride(rawRuntimePath, tool.Cwd);

        try
        {
            resolvedCommand = RunCommandBuilder.Build(tool, args, runtimeOverride);
            return true;
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, RuntimeErrorMessages.NoUsablePythonInterpreter, StringComparison.Ordinal))
        {
            context.SendMessage(new ErrorMessage(
                ex.Message,
                "Install Python in the sandbox or choose a valid python.exe in the tool settings."
            ));
            return false;
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, RuntimeErrorMessages.NoUsableNodeRuntime, StringComparison.Ordinal))
        {
            context.SendMessage(new ErrorMessage(
                ex.Message,
                "Install Node.js or choose a valid node.exe in the tool settings."
            ));
            return false;
        }
        catch (NotSupportedException ex)
        {
            context.SendMessage(new ErrorMessage(ex.Message));
            return false;
        }
    }
}
