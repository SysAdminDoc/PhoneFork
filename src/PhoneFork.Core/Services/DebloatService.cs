using System.Text.Json;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record DebloatSnapshot(
    string DeviceSerial,
    DateTimeOffset CapturedAt,
    IReadOnlyList<string> EnabledSystemPackages);

public sealed record DebloatActionResult(string PackageId, bool Success, string? Output);

public sealed record DebloatApplyResult(
    int Disabled,
    int AlreadyDisabled,
    int Failed,
    IReadOnlyList<DebloatActionResult> Results,
    string SnapshotPath,
    TimeSpan Elapsed);

public sealed record DebloatRollbackResult(
    int ReEnabled,
    int AlreadyEnabled,
    int Failed,
    IReadOnlyList<DebloatActionResult> Results,
    TimeSpan Elapsed);

/// <summary>
/// Reversible debloat: <c>pm disable-user --user 0 &lt;pkg&gt;</c> only — never <c>pm uninstall</c>.
/// Snapshots the pre-debloat enabled set so a rollback (<c>cmd package install-existing &lt;pkg&gt;</c>
/// plus <c>pm enable</c>) can restore the state byte-for-byte.
/// </summary>
public sealed class DebloatService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;
    private readonly string _snapshotDir;

    public DebloatService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<DebloatService>();
        _snapshotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhoneFork", "debloat-snapshots");
        Directory.CreateDirectory(_snapshotDir);
    }

    public string SnapshotDirectory => _snapshotDir;

    public async Task<DebloatSnapshot> SnapshotAsync(DeviceData device, CancellationToken ct = default)
    {
        var output = await _client.ShellAsync(device, "pm list packages -s -e", ct);
        var set = (output ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r').Trim())
            .Where(l => l.StartsWith("package:", StringComparison.Ordinal))
            .Select(l => l["package:".Length..])
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        var snapshot = new DebloatSnapshot(device.Serial, DateTimeOffset.UtcNow, set);
        _log.Information("Snapshot {Serial}: {Count} enabled system packages.", device.Serial, set.Count);
        return snapshot;
    }

    public async Task<DebloatApplyResult> ApplyAsync(
        DeviceData device,
        IEnumerable<string> packageIds,
        bool dryRun,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var snapshot = await SnapshotAsync(device, ct);
        var snapshotPath = Path.Combine(_snapshotDir, $"{device.Serial}-{DateTime.UtcNow:yyyyMMddTHHmmss}.json");
        await using (var fs = File.Create(snapshotPath))
            await JsonSerializer.SerializeAsync(fs, snapshot, new JsonSerializerOptions { WriteIndented = true }, ct);

        int disabled = 0, alreadyDisabled = 0, failed = 0;
        var results = new List<DebloatActionResult>();
        var enabledSet = snapshot.EnabledSystemPackages.ToHashSet(StringComparer.Ordinal);

        foreach (var pkg in packageIds)
        {
            ct.ThrowIfCancellationRequested();
            if (!enabledSet.Contains(pkg))
            {
                alreadyDisabled++;
                results.Add(new DebloatActionResult(pkg, true, "already disabled"));
                continue;
            }
            progress?.Report($"pm disable-user --user 0 {pkg}");
            if (dryRun)
            {
                disabled++;
                results.Add(new DebloatActionResult(pkg, true, "(dry-run)"));
                continue;
            }
            try
            {
                var output = await _client.ShellAsync(device, $"pm disable-user --user 0 {pkg}", ct);
                if ((output ?? "").Contains("new state: disabled-user", StringComparison.Ordinal))
                {
                    disabled++;
                    results.Add(new DebloatActionResult(pkg, true, output?.Trim()));
                }
                else
                {
                    failed++;
                    results.Add(new DebloatActionResult(pkg, false, output?.Trim()));
                    _log.Warning("disable-user returned unexpected output for {Pkg}: {Output}", pkg, output?.Trim());
                }
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(new DebloatActionResult(pkg, false, ex.Message));
                _log.Warning(ex, "disable-user failed {Pkg}", pkg);
            }
        }
        sw.Stop();
        _log.Information("Debloat apply: disabled={Disabled} already={Already} failed={Failed}; snapshot={Snapshot}",
            disabled, alreadyDisabled, failed, snapshotPath);
        return new DebloatApplyResult(disabled, alreadyDisabled, failed, results, snapshotPath, sw.Elapsed);
    }

    public async Task<DebloatRollbackResult> RollbackAsync(
        DeviceData device,
        DebloatSnapshot snapshot,
        bool dryRun,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // Pull current enabled set; anything in snapshot.EnabledSystemPackages not currently enabled needs re-enabling.
        var current = await SnapshotAsync(device, ct);
        var currentSet = current.EnabledSystemPackages.ToHashSet(StringComparer.Ordinal);
        var snapshotSet = snapshot.EnabledSystemPackages.ToHashSet(StringComparer.Ordinal);
        var toReenable = snapshotSet.Except(currentSet).OrderBy(p => p, StringComparer.Ordinal).ToList();

        int reenabled = 0, alreadyEnabled = 0, failed = 0;
        var results = new List<DebloatActionResult>();

        foreach (var pkg in toReenable)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"cmd package install-existing {pkg}");
            if (dryRun)
            {
                reenabled++;
                results.Add(new DebloatActionResult(pkg, true, "(dry-run)"));
                continue;
            }
            try
            {
                // install-existing first (covers the pm uninstall-with-keep-data case), then enable.
                var ie = await _client.ShellAsync(device, $"cmd package install-existing {pkg}", ct);
                var en = await _client.ShellAsync(device, $"pm enable {pkg}", ct);
                if ((en ?? "").Contains("new state: enabled", StringComparison.Ordinal) ||
                    (ie ?? "").Contains("installed for user", StringComparison.Ordinal))
                {
                    reenabled++;
                    results.Add(new DebloatActionResult(pkg, true, en?.Trim()));
                }
                else
                {
                    failed++;
                    results.Add(new DebloatActionResult(pkg, false, $"install-existing: {ie?.Trim()} | enable: {en?.Trim()}"));
                }
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(new DebloatActionResult(pkg, false, ex.Message));
                _log.Warning(ex, "rollback failed {Pkg}", pkg);
            }
        }
        // Anything currently enabled that was in the snapshot too: count as already-enabled.
        alreadyEnabled = snapshotSet.Intersect(currentSet).Count();
        sw.Stop();
        _log.Information("Debloat rollback: reenabled={ReEnabled} already={Already} failed={Failed}",
            reenabled, alreadyEnabled, failed);
        return new DebloatRollbackResult(reenabled, alreadyEnabled, failed, results, sw.Elapsed);
    }

    public async Task<DebloatSnapshot?> LoadSnapshotAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return null;
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<DebloatSnapshot>(fs, cancellationToken: ct);
    }
}
