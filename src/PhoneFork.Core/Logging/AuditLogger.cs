using PhoneFork.Core.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace PhoneFork.Core.Logging;

/// <summary>
/// Replaces any string property whose name ends in <c>Serial</c> (or is exactly <c>device</c>)
/// with its <see cref="SerialHash"/>. Implements F006 — raw ADB serials never reach disk.
/// </summary>
internal sealed class SerialHashingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var key in logEvent.Properties.Keys.ToArray())
        {
            if (!ShouldHash(key)) continue;
            if (logEvent.Properties[key] is not ScalarValue sv || sv.Value is not string s) continue;
            if (string.IsNullOrWhiteSpace(s)) continue;

            // Defensive: don't double-hash a value that already looks like a 12-hex prefix.
            if (s.Length == 12 && System.Text.RegularExpressions.Regex.IsMatch(s, "^[0-9a-f]{12}$"))
                continue;

            var hashed = propertyFactory.CreateProperty(key, SerialHash.Of(s));
            logEvent.AddOrUpdateProperty(hashed);
        }
    }

    private static bool ShouldHash(string key) =>
        key.Equals("device", StringComparison.OrdinalIgnoreCase)
        || key.Equals("serial", StringComparison.OrdinalIgnoreCase)
        || key.EndsWith("Serial", StringComparison.Ordinal);
}

/// <summary>
/// NDJSON audit log at <c>%LOCALAPPDATA%\PhoneFork\logs\audit-YYYY-MM-DD.log</c>.
/// One JSON event per line; <c>jq</c>/Seq/Vector-friendly. ADB serials are SHA-256-hashed
/// at write time (F006) so an exported log carries no raw hardware identifiers.
/// </summary>
public static class AuditLogger
{
    public const string LogDirEnv = "LOCALAPPDATA";

    public static ILogger Create(string? logDirectory = null)
    {
        var logDir = logDirectory ?? LogDirectory;
        Directory.CreateDirectory(logDir);

        return new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("app", "PhoneFork")
            .Enrich.With(new SerialHashingEnricher())
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
