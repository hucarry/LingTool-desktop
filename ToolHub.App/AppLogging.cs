using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace ToolHub.App;

internal static class AppLogging
{
    private const string LogDirectoryName = "logs";
    private const string LogFileNamePrefix = "toolhub-.ndjson";

    internal static string ResolveLogDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, LogDirectoryName);
    }

    internal static Serilog.ILogger CreateLogger()
    {
        var logDirectory = ResolveLogDirectory();
        Directory.CreateDirectory(logDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "ToolHub.App")
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: Path.Combine(logDirectory, LogFileNamePrefix),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true
            )
            .CreateLogger();
    }
}
