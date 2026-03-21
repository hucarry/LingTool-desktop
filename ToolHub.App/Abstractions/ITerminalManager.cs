using ToolHub.App.Models;
using ToolHub.App.Runtime;

namespace ToolHub.App;

public interface ITerminalManager : IDisposable
{
    Task<TerminalInfo> StartTerminalAsync(
        string? title = null,
        string? shell = null,
        string? cwd = null,
        string? toolType = null,
        string? runtimePath = null
    );

    Task<bool> SendInputAsync(string terminalId, string data);

    void ResizeTerminal(string terminalId, int cols, int rows);

    Task RunToolInTerminalAsync(
        string? terminalId,
        RunnableTool tool,
        ResolvedRunCommand resolvedCommand
    );

    void StopTerminal(string terminalId);

    IEnumerable<TerminalInfo> GetTerminals();
}
