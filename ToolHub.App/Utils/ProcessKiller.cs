using System.Diagnostics;

namespace ToolHub.App.Utils;

public static class ProcessKiller
{
    public static async Task KillProcessTreeAsync(Process process, CancellationToken cancellationToken = default)
    {
        if (process is null)
        {
            return;
        }

        if (!IsAlive(process))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            using var killer = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            killer.StartInfo.ArgumentList.Add("/PID");
            killer.StartInfo.ArgumentList.Add(process.Id.ToString());
            killer.StartInfo.ArgumentList.Add("/T");
            killer.StartInfo.ArgumentList.Add("/F");

            killer.Start();
            await killer.WaitForExitAsync(cancellationToken);
            return;
        }

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(cancellationToken);
    }

    private static bool IsAlive(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
