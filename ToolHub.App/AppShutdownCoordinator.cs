using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ToolHub.App;

internal sealed class AppShutdownCoordinator(
    ITerminalManager terminalManager,
    IProcessManager processManager,
    ILogger<AppShutdownCoordinator>? logger = null)
{
    private readonly ILogger<AppShutdownCoordinator> _logger = logger ?? NullLogger<AppShutdownCoordinator>.Instance;
    private int _shutdownTriggered;

    internal void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownTriggered, 1) == 1)
        {
            return;
        }

        _logger.LogInformation("Starting coordinated shutdown.");

        try
        {
            terminalManager.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terminal manager shutdown failed.");
        }

        try
        {
            processManager.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Process manager shutdown failed.");
        }

        _logger.LogInformation("Coordinated shutdown completed.");
    }
}
