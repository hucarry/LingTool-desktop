using System.Diagnostics;

namespace ToolHub.App.Utils;

public static class ProcessKiller
{
    public static async Task KillProcessTreeAsync(int pid, CancellationToken cancellationToken = default)
    {
        if (pid <= 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            await KillProcessTreeAsync(process, cancellationToken);
        }
        catch
        {
            // Process already exited or no permission; ignore.
        }
    }

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
            await WaitForExitSafeAsync(process, cancellationToken);
            return;
        }

        process.Kill(entireProcessTree: true);
        await WaitForExitSafeAsync(process, cancellationToken);
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

    private static async Task WaitForExitSafeAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            if (!process.HasExited)
            {
                await process.WaitForExitAsync(cancellationToken);
            }
        }
        catch
        {
            // Ignore wait errors.
        }
    }
}
