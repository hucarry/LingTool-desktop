namespace ToolHub.App;

internal sealed class AppShutdownCoordinator(TerminalManager terminalManager, ProcessManager processManager)
{
    private int _shutdownTriggered;

    internal void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownTriggered, 1) == 1)
        {
            return;
        }

        try
        {
            terminalManager.Dispose();
        }
        catch
        {
            // Ignore shutdown errors.
        }

        try
        {
            processManager.Dispose();
        }
        catch
        {
            // Ignore shutdown errors.
        }
    }
}
