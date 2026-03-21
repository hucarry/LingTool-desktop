using ToolHub.App.Models;
using ToolHub.App.Runtime;

namespace ToolHub.App;

public interface IProcessManager : IDisposable
{
    RunInfo StartRun(RunnableTool tool, ResolvedRunCommand resolvedCommand);

    Task<bool> StopRunAsync(string runId);

    IReadOnlyList<RunInfo> GetRuns();
}
