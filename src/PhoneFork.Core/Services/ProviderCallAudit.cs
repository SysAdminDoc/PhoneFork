using Serilog;
using Serilog.Context;

namespace PhoneFork.Core.Services;

/// <summary>
/// Audit-event scope for one helper provider call (F021). Each invocation against the
/// PhoneForkHelper APK or the push-and-run JAR opens a scope that adds the migration
/// id, the source/destination correlation IDs, the helper authority, and an outcome.
/// All entries are written through the existing Serilog NDJSON pipeline, so they pick
/// up the <c>SerialHashingEnricher</c> automatically (no raw serials on disk).
///
/// Usage:
/// <code>
/// using (ProviderCallAudit.Begin("sms.export", srcSerial, dstSerial, migrationId, _log))
/// {
///     // ... run the helper call
///     ProviderCallAudit.RecordOutcome(_log, ok: true, rowsTouched: 1234);
/// }
/// </code>
/// </summary>
public static class ProviderCallAudit
{
    public sealed class Scope : IDisposable
    {
        private readonly List<IDisposable> _scopes;
        private readonly ILogger _log;
        private readonly string _operation;
        private readonly DateTimeOffset _startedAt;
        private bool _ended;

        internal Scope(string operation, ILogger log, List<IDisposable> scopes)
        {
            _operation = operation;
            _log = log;
            _scopes = scopes;
            _startedAt = DateTimeOffset.UtcNow;
            _log.Information("provider.call.begin op={Op}", _operation);
        }

        public void End(bool ok, long? rowsTouched = null, string? note = null)
        {
            if (_ended) return;
            _ended = true;
            var elapsed = DateTimeOffset.UtcNow - _startedAt;
            _log.Information(
                "provider.call.end op={Op} ok={Ok} ms={Ms} rows={Rows} note={Note}",
                _operation, ok, (long)elapsed.TotalMilliseconds, rowsTouched, note ?? "");
        }

        public void Dispose()
        {
            if (!_ended) End(ok: false, note: "scope-disposed-without-explicit-end");
            for (var i = _scopes.Count - 1; i >= 0; i--) _scopes[i].Dispose();
        }
    }

    /// <summary>
    /// Open a provider-call audit scope. <paramref name="sourceSerial"/> and
    /// <paramref name="destSerial"/> are pushed as <c>device</c>-prefixed properties
    /// which the SerialHashingEnricher rewrites to 12-hex hashes before disk.
    /// </summary>
    public static Scope Begin(string operation, string? sourceSerial, string? destSerial, string? migrationId, ILogger log)
    {
        var scopes = new List<IDisposable>
        {
            LogContext.PushProperty("op", operation),
        };
        if (!string.IsNullOrWhiteSpace(sourceSerial))
            scopes.Add(LogContext.PushProperty("sourceSerial", sourceSerial));
        if (!string.IsNullOrWhiteSpace(destSerial))
            scopes.Add(LogContext.PushProperty("destSerial", destSerial));
        if (!string.IsNullOrWhiteSpace(migrationId))
            scopes.Add(LogContext.PushProperty("migrationId", migrationId));

        return new Scope(operation, log, scopes);
    }
}
