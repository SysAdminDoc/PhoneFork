using System.Text.RegularExpressions;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public enum AppTransferReadiness
{
    Supported,
    Partial,
    External,
    Unsupported,
    Unknown,
}

public sealed record AppTransferFacet(
    string Area,
    AppTransferReadiness Readiness,
    string Detail);

public sealed record AppExternalDataLocation(
    string Kind,
    string Path,
    bool Exists,
    long? Bytes,
    long? FileCount,
    string? Error);

public sealed record AppExternalDataProbe(
    string PackageId,
    AppExternalDataLocation Obb,
    AppExternalDataLocation ExternalData)
{
    public bool HasPayload => Obb.Exists || ExternalData.Exists;
}

public sealed record AppTransferReport(
    string PackageId,
    string Label,
    string VersionName,
    long VersionCode,
    int ApkCount,
    long ApkBytes,
    BackupCapability? BackupCapability,
    AppExternalDataProbe? ExternalData,
    IReadOnlyList<AppTransferFacet> Facets,
    IReadOnlyList<string> Warnings);

public sealed record PackageFileArtifact(
    string RemotePath,
    string LocalPath,
    long Bytes,
    string Sha256);

public static class PackageFileIntegrity
{
    public static async Task<PackageFileArtifact> FromFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(localPath);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);
        return new PackageFileArtifact(
            RemotePath: remotePath,
            LocalPath: localPath,
            Bytes: stream.Length,
            Sha256: Convert.ToHexString(hash).ToLowerInvariant());
    }
}

public sealed partial class AppTransferReportService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public AppTransferReportService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<AppTransferReportService>();
    }

    public async Task<AppExternalDataProbe> ProbeExternalDataAsync(DeviceData device, string packageId, CancellationToken ct = default)
    {
        if (!AdbShell.IsPackageName(packageId))
            throw new ArgumentException($"Invalid package id: {packageId}", nameof(packageId));

        var obbPath = $"/sdcard/Android/obb/{packageId}";
        var dataPath = $"/sdcard/Android/data/{packageId}";
        var obb = await ProbeLocationAsync(device, packageId, "obb", obbPath, ct);
        var data = await ProbeLocationAsync(device, packageId, "external-data", dataPath, ct);
        return new AppExternalDataProbe(packageId, obb, data);
    }

    public static AppTransferReport Assess(AppInfo app, BackupCapability? backupCapability, AppExternalDataProbe? externalData)
    {
        var facets = new List<AppTransferFacet>();
        var warnings = new List<string>();

        var apkReadiness = app.RemoteApkPaths.Count > 0 ? AppTransferReadiness.Supported : AppTransferReadiness.Unsupported;
        facets.Add(new AppTransferFacet(
            "APK install",
            apkReadiness,
            app.RemoteApkPaths.Count > 0
                ? $"PhoneFork can pull {app.RemoteApkPaths.Count} APK/split file(s) from pm path and install them with Android's package installer session API."
                : "No APK path was discovered, so PhoneFork cannot install this package from the current device snapshot."));
        if (app.RemoteApkPaths.Count == 0)
            warnings.Add("No APK source path was discovered.");

        facets.Add(new AppTransferFacet(
            "Integrity and provenance",
            AppTransferReadiness.Partial,
            "APK source paths come from ADB pm path/dumpsys package. AppManager exports write SHA-256 checksums; direct migration logs local APK SHA-256 after each pull but cannot prove a remote APK hash before pulling."));

        if (backupCapability is null)
        {
            facets.Add(new AppTransferFacet(
                "Private app data",
                AppTransferReadiness.Unknown,
                "Backup posture was not probed. PhoneFork still cannot read /data/data without root; use the app's own export, Google restore, Smart Switch, or a helper-backed category flow where available."));
        }
        else if (backupCapability.ParticipatesInAutoBackup)
        {
            facets.Add(new AppTransferFacet(
                "Private app data",
                AppTransferReadiness.External,
                "PhoneFork cannot copy /data/data locally, but Android backup metadata suggests the OS may restore this app through Google/official backup flows."));
        }
        else if (!backupCapability.AllowBackup)
        {
            facets.Add(new AppTransferFacet(
                "Private app data",
                AppTransferReadiness.Unsupported,
                "The app disables Android Auto Backup. PhoneFork cannot copy its private data without root; use the app's own export/login flow."));
            warnings.Add("App private data will not transfer through PhoneFork or Android Auto Backup.");
        }
        else
        {
            facets.Add(new AppTransferFacet(
                "Private app data",
                AppTransferReadiness.External,
                "The app allows Android backup but does not expose explicit data-extraction rules. Treat official restore as best-effort and cloud/account dependent."));
        }

        if (externalData is null)
        {
            facets.Add(new AppTransferFacet(
                "OBB/external app data",
                AppTransferReadiness.Unknown,
                "External app folders were not probed. Run the report with external probes enabled before wiping the source."));
        }
        else if (externalData.HasPayload)
        {
            var locations = new[]
                {
                    externalData.Obb.Exists ? $"{externalData.Obb.Kind} {FormatBytes(externalData.Obb.Bytes)}" : null,
                    externalData.ExternalData.Exists ? $"{externalData.ExternalData.Kind} {FormatBytes(externalData.ExternalData.Bytes)}" : null,
                }
                .Where(x => x is not null);
            facets.Add(new AppTransferFacet(
                "OBB/external app data",
                AppTransferReadiness.Partial,
                $"ADB-visible external payload exists ({string.Join(", ", locations)}). APK migration does not include these folders; include them in media/file planning or let the app redownload them."));
            warnings.Add("OBB or /sdcard/Android/data payload detected outside the APK set.");
        }
        else
        {
            facets.Add(new AppTransferFacet(
                "OBB/external app data",
                AppTransferReadiness.Supported,
                "No ADB-visible OBB or /sdcard/Android/data payload was detected for this package."));
        }

        return new AppTransferReport(
            PackageId: app.PackageName,
            Label: app.SafeLabel,
            VersionName: app.VersionName,
            VersionCode: app.VersionCode,
            ApkCount: app.RemoteApkPaths.Count,
            ApkBytes: app.TotalSizeBytes,
            BackupCapability: backupCapability,
            ExternalData: externalData,
            Facets: facets,
            Warnings: warnings);
    }

    internal static AppExternalDataLocation ParseProbeOutput(string kind, string path, string output)
    {
        var text = (output ?? "").Trim();
        if (text.Equals("missing", StringComparison.OrdinalIgnoreCase))
            return new AppExternalDataLocation(kind, path, Exists: false, Bytes: null, FileCount: null, Error: null);

        var match = LocationProbeRegex().Match(text);
        if (match.Success)
        {
            long? bytes = null;
            if (long.TryParse(match.Groups["kb"].Value, out var kb))
                bytes = kb * 1024;

            long? files = null;
            if (long.TryParse(match.Groups["files"].Value, out var count))
                files = count;

            return new AppExternalDataLocation(kind, path, Exists: true, Bytes: bytes, FileCount: files, Error: null);
        }

        return new AppExternalDataLocation(kind, path, Exists: false, Bytes: null, FileCount: null, Error: text.Length == 0 ? "empty probe output" : text);
    }

    private async Task<AppExternalDataLocation> ProbeLocationAsync(DeviceData device, string packageId, string kind, string path, CancellationToken ct)
    {
        try
        {
            var quoted = AdbShell.Arg(path);
            var output = await _client.ShellAsync(device,
                $"if [ -d {quoted} ]; then files=$(find {quoted} -type f 2>/dev/null | wc -l); kb=$(du -k -s {quoted} 2>/dev/null | awk '{{print $1}}'); echo present files=$files kb=$kb; else echo missing; fi",
                ct);
            return ParseProbeOutput(kind, path, output ?? "");
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "External data probe failed for {Package} {Kind}", packageId, kind);
            return new AppExternalDataLocation(kind, path, Exists: false, Bytes: null, FileCount: null, Error: ex.Message);
        }
    }

    private static string FormatBytes(long? bytes)
        => bytes is null ? "unknown size" : $"{bytes.Value / 1024.0 / 1024.0:F1} MiB";

    [GeneratedRegex(@"present\s+files=(?<files>\d+)\s+kb=(?<kb>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LocationProbeRegex();
}
