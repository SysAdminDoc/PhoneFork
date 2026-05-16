using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// One package's backup posture as Android sees it (F022). Surfaced as part of the
/// honesty report so the user knows which apps will benefit from Google's own
/// Auto Backup / D2D restore and which will lose data unless migrated manually.
/// </summary>
public sealed record BackupCapability(
    string PackageId,
    bool AllowBackup,
    bool HasDataExtractionRules,
    bool HasCrossPlatformTransfer,
    string? BackupAgentName,
    DateTimeOffset? LastBackupAt)
{
    /// <summary>True iff Google's Auto Backup will move this app's data on its own.</summary>
    public bool ParticipatesInAutoBackup => AllowBackup && string.IsNullOrEmpty(BackupAgentName) == false || HasDataExtractionRules;
}

/// <summary>
/// Queries each user app for its <c>allowBackup</c> / <c>dataExtractionRules</c>
/// posture via <c>dumpsys package</c>. Lightweight: no APK parsing, no helper APK
/// needed. Feeds the pre-flight honesty report (F037 / F041).
/// </summary>
public sealed class BackupCapabilityService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public BackupCapabilityService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<BackupCapabilityService>();
    }

    public async Task<BackupCapability> ProbeAsync(DeviceData device, string packageId, CancellationToken ct = default)
    {
        if (!AdbShell.IsPackageName(packageId))
            throw new ArgumentException($"Invalid package id: {packageId}", nameof(packageId));

        var output = await _client.ShellAsync(device,
            $"dumpsys package {AdbShell.PackageArg(packageId)}", ct);
        var text = output ?? "";

        // dumpsys package <pkg> includes a 'flags=[ ... ALLOW_BACKUP ... ]' line on modern Android.
        var allow = text.Contains("ALLOW_BACKUP", StringComparison.Ordinal);
        var hasExtraction = text.Contains("dataExtractionRules=", StringComparison.OrdinalIgnoreCase);
        var hasCpt = text.Contains("cross-platform-transfer", StringComparison.OrdinalIgnoreCase)
                     || text.Contains("crossPlatformTransfer", StringComparison.OrdinalIgnoreCase);

        string? backupAgent = null;
        var agentIdx = text.IndexOf("backupAgent=", StringComparison.Ordinal);
        if (agentIdx > 0)
        {
            var slice = text[(agentIdx + "backupAgent=".Length)..];
            var end = slice.IndexOfAny(new[] { '\n', ' ', '\r' });
            if (end > 0) backupAgent = slice[..end].Trim();
        }

        DateTimeOffset? lastBackup = null;
        // bmgr (Android backup manager) tracks last successful backup ms; absent on Samsung
        // by default but still worth probing.
        try
        {
            var bmgr = await _client.ShellAsync(device,
                $"dumpsys backup {AdbShell.PackageArg(packageId)}", ct);
            var idx = (bmgr ?? "").IndexOf("lastBackup=", StringComparison.Ordinal);
            if (idx > 0)
            {
                var slice = bmgr![(idx + "lastBackup=".Length)..];
                var end = slice.IndexOfAny(new[] { '\n', ' ', '\r' });
                if (end > 0 && long.TryParse(slice[..end].Trim(), out var ms) && ms > 0)
                    lastBackup = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            }
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "bmgr probe failed for {Pkg}", packageId);
        }

        return new BackupCapability(
            PackageId: packageId,
            AllowBackup: allow,
            HasDataExtractionRules: hasExtraction,
            HasCrossPlatformTransfer: hasCpt,
            BackupAgentName: backupAgent,
            LastBackupAt: lastBackup);
    }

    /// <summary>Bulk probe over a set of package ids. Sequential to avoid hammering shellsvc.</summary>
    public async Task<IReadOnlyList<BackupCapability>> ProbeManyAsync(DeviceData device, IEnumerable<string> packageIds, CancellationToken ct = default)
    {
        var list = new List<BackupCapability>();
        foreach (var pkg in packageIds)
        {
            ct.ThrowIfCancellationRequested();
            list.Add(await ProbeAsync(device, pkg, ct));
        }
        return list;
    }

    /// <summary>
    /// Translate a single capability into an honesty finding ready for inclusion in the
    /// pre-flight report. Returns null when the app is well-behaved (Auto Backup on, no caveat).
    /// </summary>
    public static HonestyFinding? ToFinding(BackupCapability cap)
    {
        if (cap.AllowBackup && cap.HasDataExtractionRules)
            return null;

        if (!cap.AllowBackup)
            return new HonestyFinding(
                Id: $"backup-disabled:{cap.PackageId}",
                Title: $"App disables Auto Backup ({cap.PackageId})",
                Detail: "The app declares android:allowBackup=\"false\". Its data will not transfer through Google's Auto Backup or any official D2D restore. Use the app's own export flow.",
                Level: HonestyLevel.Warning,
                PackageId: cap.PackageId);

        if (cap.AllowBackup && string.IsNullOrEmpty(cap.BackupAgentName) && !cap.HasDataExtractionRules)
            return new HonestyFinding(
                Id: $"backup-default:{cap.PackageId}",
                Title: $"App backs up via the legacy default agent ({cap.PackageId})",
                Detail: "android:allowBackup=\"true\" with no data-extraction rules means Google's Auto Backup will best-effort include this app, capped at 25 MB. Cloud-only.",
                Level: HonestyLevel.Info,
                PackageId: cap.PackageId);

        return null;
    }
}
