using Serilog;
using Serilog.Formatting.Compact;

namespace PhoneFork.Core.Logging;

/// <summary>
/// NDJSON audit log at <c>%LOCALAPPDATA%\PhoneFork\logs\audit-YYYY-MM-DD.log</c>.
/// One JSON event per line; <c>jq</c>/Seq/Vector-friendly.
/// </summary>
public static class AuditLogger
{
    public const string LogDirEnv = "LOCALAPPDATA";

    public static ILogger Create()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhoneFork", "logs");
        Directory.CreateDirectory(logDir);

        return new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("app", "PhoneFork")
            .MinimumLevel.Verbose()
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: Path.Combine(logDir, "audit-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 30,
                flushToDiskInterval: TimeSpan.FromMilliseconds(250),
                shared: false)
            .CreateLogger();
    }

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhoneFork", "logs");
}
