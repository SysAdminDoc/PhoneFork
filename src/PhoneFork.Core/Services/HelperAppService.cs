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

    /// <summary>
    /// Dangerous runtime permissions the helper declares that the host must grant explicitly (F111).
    /// The helper ships no launcher activity, so it can never raise a runtime permission prompt;
    /// without these grants every SMS / call-log / contacts provider read fails with a
    /// <c>SecurityException</c> and returns a <c>permission-denied</c> envelope.
    /// </summary>
    public static readonly IReadOnlyList<string> RuntimePermissions = new[]
    {
        "android.permission.READ_SMS",
        "android.permission.READ_CALL_LOG",
        "android.permission.WRITE_CALL_LOG",
        "android.permission.READ_CONTACTS",
        "android.permission.WRITE_CONTACTS",
    };

    /// <summary>
    /// Permissions the manifest declares that <c>pm grant</c> can never satisfy: they are
    /// signature/privileged rather than runtime permissions. Listed so a failed grant is
    /// reported as expected rather than retried. The manifest already marks them with
    /// <c>tools:ignore="ProtectedPermissions"</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> PrivilegedPermissions = new[]
    {
        "android.permission.WRITE_SMS",
        "android.permission.READ_USER_DICTIONARY",
        "android.permission.WRITE_USER_DICTIONARY",
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
        // -g grants every runtime permission the manifest declares. Older/OEM pm builds ignore
        // it silently, so GrantRuntimePermissionsAsync re-grants each one individually below.
        var install = await _client.ShellAsync(device, $"pm install -r -g {AdbShell.Arg(remote)}", ct);
        await _client.ShellAsync(device, $"rm -f {AdbShell.Arg(remote)}", ct);

        var ok = (install ?? "").Contains("Success", StringComparison.OrdinalIgnoreCase);
        _log.Information("Helper install on {Device}: ok={Ok} out={Out}", device.Serial, ok, (install ?? "").Trim());
        if (ok)
            await GrantRuntimePermissionsAsync(device, ct);
        return ok;
    }

    /// <summary>
    /// Grants each dangerous runtime permission the helper needs (F111) and reports the result
    /// per permission. Idempotent: <c>pm grant</c> on an already-granted permission is a no-op.
    /// Privileged permissions in <see cref="PrivilegedPermissions"/> are not attempted.
    /// </summary>
    public async Task<HelperPermissionReport> GrantRuntimePermissionsAsync(
        DeviceData device,
        CancellationToken ct = default)
    {
        var granted = new List<string>(RuntimePermissions.Count);
        var failed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var permission in RuntimePermissions)
        {
            ct.ThrowIfCancellationRequested();
            var output = (await _client.ShellAsync(device,
                $"pm grant {AdbShell.PackageArg(PackageId)} {AdbShell.Arg(permission)}", ct) ?? "").Trim();

            // pm grant prints nothing on success and an Exception/Error line on failure.
            if (output.Length == 0)
            {
                granted.Add(permission);
            }
            else
            {
                failed[permission] = output;
                _log.Warning("Helper permission grant failed on {Device}: {Permission} -> {Output}",
                    device.Serial, permission, output);
            }
        }

        _log.Information("Helper runtime permissions on {Device}: {Granted}/{Total} granted",
            device.Serial, granted.Count, RuntimePermissions.Count);
        return new HelperPermissionReport(granted, failed);
    }

    /// <summary>
    /// Reads back which of <see cref="RuntimePermissions"/> the helper actually holds, by parsing
    /// <c>dumpsys package</c>'s runtime permission block. Used by the probe so the operator sees
    /// real state rather than the grant call's return value.
    /// </summary>
    public async Task<HelperPermissionReport> ProbeRuntimePermissionsAsync(
        DeviceData device,
        CancellationToken ct = default)
    {
        var dump = await _client.ShellAsync(device,
            $"dumpsys package {AdbShell.PackageArg(PackageId)}", ct) ?? "";
        return ParsePermissionDump(dump);
    }

    /// <summary>
    /// Parses <c>dumpsys package</c> output into a permission report. Split out from the ADB
    /// call so the parsing is testable without a device.
    /// </summary>
    public static HelperPermissionReport ParsePermissionDump(string? dumpsysOutput)
    {
        // Only the FIRST "runtime permissions:" block is read. On a device with a work profile or
        // secondary users, dumpsys emits one block per user; PhoneFork targets Android user 0,
        // which dumpsys always emits first, so scanning the whole dump could otherwise report
        // another user's grants as if they were user 0's.
        var dump = Section(dumpsysOutput ?? "");
        var granted = new List<string>(RuntimePermissions.Count);
        var missing = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var permission in RuntimePermissions)
        {
            // dumpsys renders one line per permission: "<name>: granted=true, flags=[...]".
            var index = dump.IndexOf(permission + ":", StringComparison.Ordinal);
            if (index < 0)
            {
                missing[permission] = "not-listed";
                continue;
            }

            var lineEnd = dump.IndexOf('\n', index);
            var line = lineEnd < 0 ? dump[index..] : dump[index..lineEnd];
            if (line.Contains("granted=true", StringComparison.Ordinal))
                granted.Add(permission);
            else
                missing[permission] = "granted=false";
        }

        return new HelperPermissionReport(granted, missing);

        // Narrows the dump to the first runtime-permission block, or returns it whole when the
        // device does not emit that header (older or OEM dumpsys layouts).
        static string Section(string text)
        {
            const string header = "runtime permissions:";
            var start = text.IndexOf(header, StringComparison.Ordinal);
            if (start < 0) return text;
            start += header.Length;

            var next = text.IndexOf(header, start, StringComparison.Ordinal);
            return next < 0 ? text[start..] : text[start..next];
        }
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

/// <summary>
/// Which of the helper's dangerous runtime permissions are held, and why the rest are not (F111).
/// </summary>
public sealed record HelperPermissionReport(
    IReadOnlyList<string> Granted,
    IReadOnlyDictionary<string, string> Failed)
{
    /// <summary>True when every permission the provider reads depend on is held.</summary>
    public bool AllGranted => Failed.Count == 0;

    /// <summary>
    /// True when the three provider authorities that need dangerous permissions can work.
    /// The wifi, wallpaper and ringtone authorities read without a runtime grant.
    /// </summary>
    public bool CanReadPrivilegedCategories =>
        Granted.Contains("android.permission.READ_SMS")
        && Granted.Contains("android.permission.READ_CALL_LOG")
        && Granted.Contains("android.permission.READ_CONTACTS");
}

/// <summary>Result of a helper residue check (F019).</summary>
public sealed record HelperResidueReport(bool HelperInstalled, IReadOnlyList<string> TempFilesLeft)
{
    public bool IsClean => !HelperInstalled && TempFilesLeft.Count == 0;
}
