using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Lifecycle + provider-call orchestration for <c>PhoneForkHelper.apk</c> (F010 / F019 / F021).
/// The helper APK ships under <c>assets/helper/PhoneForkHelper.apk</c>; the host installs it
/// before a privileged read, drives content-provider queries via <c>adb shell content query</c>,
/// and uninstalls when the migration completes.
/// </summary>
public sealed class HelperAppService
{
    public const string PackageId = "com.sysadmindoc.phonefork.helper";
    public const string AuthorityPrefix = PackageId;

    public static readonly IReadOnlyList<string> Authorities = new[]
    {
        "sms", "calllog", "contacts", "wifi", "wallpaper", "ringtone", "dictionary",
    };

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public HelperAppService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<HelperAppService>();
    }

    /// <summary>True iff the helper is currently installed on this device for user 0.</summary>
    public async Task<bool> IsInstalledAsync(DeviceData device, CancellationToken ct = default)
    {
        var output = await _client.ShellAsync(device,
            $"pm list packages {AdbShell.PackageArg(PackageId)}", ct);
        return (output ?? "").Contains($"package:{PackageId}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Push and install the helper APK from a local path.
    /// </summary>
    public async Task<bool> InstallAsync(DeviceData device, string localApkPath, CancellationToken ct = default)
    {
        if (!File.Exists(localApkPath))
            throw new FileNotFoundException("Helper APK not found", localApkPath);

        var remote = $"/data/local/tmp/{Path.GetFileName(localApkPath)}";
        await using (var stream = File.OpenRead(localApkPath))
        {
            using var sync = new SyncService(_client, device);
            await sync.PushAsync(stream, remote, UnixFileStatus.DefaultFileMode, DateTimeOffset.UtcNow,
                callback: null, useV2: false, cancellationToken: ct);
        }
        var install = await _client.ShellAsync(device, $"pm install -r {AdbShell.Arg(remote)}", ct);
        await _client.ShellAsync(device, $"rm -f {AdbShell.Arg(remote)}", ct);

        var ok = (install ?? "").Contains("Success", StringComparison.OrdinalIgnoreCase);
        _log.Information("Helper install on {Device}: ok={Ok} out={Out}", device.Serial, ok, (install ?? "").Trim());
        return ok;
    }

    /// <summary>Uninstalls the helper APK (F019). Idempotent — missing package returns true.</summary>
    public async Task<bool> UninstallAsync(DeviceData device, CancellationToken ct = default)
    {
        if (!await IsInstalledAsync(device, ct)) return true;
        var output = await _client.ShellAsync(device, $"pm uninstall {AdbShell.PackageArg(PackageId)}", ct);
        var ok = (output ?? "").Contains("Success", StringComparison.OrdinalIgnoreCase);
        _log.Information("Helper uninstall on {Device}: ok={Ok}", device.Serial, ok);
        return ok;
    }

    /// <summary>
    /// Hits the helper's <c>&lt;authority&gt;/health</c> endpoint. Returns the raw JSON
    /// payload if reachable, or null when the helper is not installed / not responding.
    /// </summary>
    public async Task<string?> HealthCheckAsync(DeviceData device, string authority, CancellationToken ct = default)
    {
        if (!Authorities.Contains(authority))
            throw new ArgumentException($"Unknown helper authority: {authority}", nameof(authority));

        var uri = HelperProviderContract.BuildQueryUri(authority, path: "health");
        var output = await _client.ShellAsync(device,
            $"content query --uri {AdbShell.Arg(uri)} --projection json", ct);

        return HelperProviderContract.ExtractJsonFromContentQuery(output);
    }

    /// <summary>
    /// Query a helper authority and parse the v1 JSON envelope into a typed host DTO.
    /// </summary>
    public async Task<HelperProviderEnvelope?> QueryAsync(
        DeviceData device,
        string authority,
        int? limit = null,
        int? offset = null,
        CancellationToken ct = default)
    {
        var uri = HelperProviderContract.BuildQueryUri(authority, limit: limit, offset: offset);
        using var audit = ProviderCallAudit.Begin($"{authority}.query", device.Serial, null, null, _log);
        var output = await _client.ShellAsync(device,
            $"content query --uri {AdbShell.Arg(uri)} --projection json", ct);
        var json = HelperProviderContract.ExtractJsonFromContentQuery(output);
        if (!HelperProviderContract.TryParseEnvelope(json, out var envelope))
        {
            audit.End(ok: false, note: "invalid-or-empty-provider-envelope");
            return null;
        }

        audit.End(ok: envelope!.IsOk, rowsTouched: envelope.Count, note: envelope.Status);
        return envelope;
    }

    /// <summary>
    /// Verify that the host can talk to every advertised helper authority on this device.
    /// Returns a per-authority pass/fail map.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, bool>> ProbeAllAsync(DeviceData device, CancellationToken ct = default)
    {
        var results = new Dictionary<string, bool>(Authorities.Count, StringComparer.Ordinal);
        foreach (var auth in Authorities)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var resp = await HealthCheckAsync(device, auth, ct);
                results[auth] = HelperProviderContract.TryParseEnvelope(resp, out var envelope) && envelope!.IsOk;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Helper authority {Auth} probe failed", auth);
                results[auth] = false;
            }
        }
        return results;
    }

    /// <summary>
    /// Best-effort residue check (F019): confirms the helper package is gone and no helper
    /// artifacts remain in <c>/data/local/tmp</c>.
    /// </summary>
    public async Task<HelperResidueReport> ResidueCheckAsync(DeviceData device, CancellationToken ct = default)
    {
        var stillInstalled = await IsInstalledAsync(device, ct);
        var tmpScan = await _client.ShellAsync(device, "ls -1 /data/local/tmp 2>/dev/null", ct);
        var leftovers = (tmpScan ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Contains("phonefork", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new HelperResidueReport(stillInstalled, leftovers);
    }
}

/// <summary>Result of a helper residue check (F019).</summary>
public sealed record HelperResidueReport(bool HelperInstalled, IReadOnlyList<string> TempFilesLeft)
{
    public bool IsClean => !HelperInstalled && TempFilesLeft.Count == 0;
}
