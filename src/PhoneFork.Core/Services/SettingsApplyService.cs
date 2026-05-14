using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record SettingsApplyResult(
    int Applied,
    int Skipped,
    int Failed,
    IReadOnlyList<(SettingsNamespace Ns, string Key, string Error)> Failures,
    TimeSpan Elapsed);

/// <summary>
/// Applies a subset of <see cref="SettingsDiffEntry"/> rows to the destination via
/// <c>settings put NS KEY VAL</c>. Hard-blocks a small allowlist of known-locked keys
/// where the call would <c>SecurityException</c> from shell UID.
/// </summary>
public sealed class SettingsApplyService
{
    /// <summary>
    /// Keys that <c>settings put</c> rejects from shell UID on Android 14/15/16, or that are
    /// dangerous to clone across devices (e.g. <c>android_id</c>, GUIDs). Refuse to push them.
    /// </summary>
    public static readonly IReadOnlySet<string> KnownLockedOrDangerous = new HashSet<string>(StringComparer.Ordinal)
    {
        // AOSP system-managed.
        "device_provisioned",
        "adb_enabled",
        "development_settings_enabled",
        "android_id",
        "bluetooth_address",
        "wifi_p2p_pending_factory_reset",
        // Per-device GUIDs that should not be cloned.
        "android.security.fingerprint",
        // Carrier / SIM-provisioned values.
        "preferred_network_mode",
        "preferred_network_mode1",
        "preferred_network_mode2",
        "mobile_data",
        "mobile_data1",
        "mobile_data2",
        // Backup transport bookkeeping.
        "backup_provisioned",
    };

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public SettingsApplyService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<SettingsApplyService>();
    }

    public async Task<SettingsApplyResult> ApplyAsync(
        DeviceData destination,
        IEnumerable<SettingsDiffEntry> entriesToApply,
        bool dryRun,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int applied = 0, skipped = 0, failed = 0;
        var failures = new List<(SettingsNamespace, string, string)>();

        foreach (var entry in entriesToApply)
        {
            ct.ThrowIfCancellationRequested();

            if (KnownLockedOrDangerous.Contains(entry.Key))
            {
                skipped++;
                _log.Information("Skip locked/dangerous {Ns}/{Key}", entry.Namespace, entry.Key);
                continue;
            }

            var value = entry.SourceValue ?? "";
            progress?.Report($"{entry.Namespace.ToString().ToLowerInvariant()} {entry.Key} = {Truncate(value, 60)}");

            if (dryRun)
            {
                applied++;
                continue;
            }

            try
            {
                // settings put NAMESPACE KEY VALUE
                // Quote the value to survive whitespace and shell metas. The settings shim accepts
                // a single quoted argument as the entire value.
                var nsName = entry.Namespace.ToString().ToLowerInvariant();
                var quoted = QuoteForShell(value);
                var cmd = $"settings put {nsName} {entry.Key} {quoted}";
                var output = await _client.ShellAsync(destination, cmd, ct);
                if (!string.IsNullOrWhiteSpace(output) && output.Contains("Exception", StringComparison.Ordinal))
                {
                    failed++;
                    failures.Add((entry.Namespace, entry.Key, output.Trim()));
                    _log.Warning("settings put {Ns}/{Key} returned exception: {Output}", entry.Namespace, entry.Key, output.Trim());
                }
                else
                {
                    applied++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                failures.Add((entry.Namespace, entry.Key, ex.Message));
                _log.Warning(ex, "settings put failed {Ns}/{Key}", entry.Namespace, entry.Key);
            }
        }

        sw.Stop();
        _log.Information("Settings apply: applied={Applied} skipped={Skipped} failed={Failed} in {Ms} ms",
            applied, skipped, failed, sw.ElapsedMilliseconds);
        return new SettingsApplyResult(applied, skipped, failed, failures, sw.Elapsed);
    }

    /// <summary>
    /// Sets the system default ringtone / notification sound / alarm sound URI given a path under
    /// <c>/sdcard/Ringtones</c> etc. Pairs with the Media tab's ringtone push step. Each URI is set
    /// individually because <c>settings put</c> doesn't accept batched args.
    /// </summary>
    public async Task<int> SetDefaultSoundUrisAsync(
        DeviceData destination,
        string? ringtoneRemotePath,
        string? notificationRemotePath,
        string? alarmRemotePath,
        CancellationToken ct = default)
    {
        int applied = 0;
        async Task ApplyOne(string key, string? remote)
        {
            if (string.IsNullOrEmpty(remote)) return;
            var uri = $"file://{remote}";
            var cmd = $"settings put system {key} {QuoteForShell(uri)}";
            try
            {
                var output = await _client.ShellAsync(destination, cmd, ct);
                if (string.IsNullOrWhiteSpace(output) || !output.Contains("Exception", StringComparison.Ordinal))
                    applied++;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Default-sound apply failed {Key}", key);
            }
        }
        await ApplyOne("ringtone", ringtoneRemotePath);
        await ApplyOne("notification_sound", notificationRemotePath);
        await ApplyOne("alarm_alert", alarmRemotePath);
        return applied;
    }

    private static string QuoteForShell(string value)
    {
        // Wrap in single quotes; escape any inner single-quotes with the standard `'\''` dance.
        var escaped = value.Replace("'", "'\\''");
        return $"'{escaped}'";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
