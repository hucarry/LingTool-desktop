using System.Text;

namespace ToolHub.App;

internal static class ConsoleEncodingConfigurator
{
    internal static void Configure()
    {
        try
        {
            _ = Console.IsOutputRedirected;
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // WinExe started without a console can have invalid std handles.
        }
        catch (InvalidOperationException)
        {
            // Some hosts do not expose a writable console stream.
        }

        try
        {
            _ = Console.IsInputRedirected;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // WinExe started without a console can have invalid std handles.
        }
        catch (InvalidOperationException)
        {
            // Some hosts do not expose a readable console stream.
        }
    }
}
