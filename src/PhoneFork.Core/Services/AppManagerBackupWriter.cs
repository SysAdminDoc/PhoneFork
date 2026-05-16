using System.Security.Cryptography;
using System.Text.Json;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Writes a PhoneFork backup directory in AppManager-compatible v5 layout (F029).
/// Takes a list of local APK file paths (base + splits) already pulled from the
/// device, a package id, and the per-device metadata that the host already knows.
/// </summary>
public sealed class AppManagerBackupWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly ILogger _log;

    public AppManagerBackupWriter(ILogger log)
    {
        _log = log.ForContext<AppManagerBackupWriter>();
    }

    /// <summary>
    /// Materialize one package's backup. Returns the absolute path of the per-package
    /// backup directory that was written.
    /// </summary>
    public async Task<string> WriteAsync(
        string backupRoot,
        string deviceSerial,
        AppInfo app,
        IReadOnlyList<string> localApkPaths,
        string toolVersion,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(backupRoot)) throw new ArgumentException("required", nameof(backupRoot));
        if (string.IsNullOrWhiteSpace(deviceSerial)) throw new ArgumentException("required", nameof(deviceSerial));
        if (app is null) throw new ArgumentNullException(nameof(app));
        if (localApkPaths is null || localApkPaths.Count == 0)
            throw new ArgumentException("at least one APK required", nameof(localApkPaths));

        var deviceHash = SerialHash.Of(deviceSerial);
        var timestamp = DateTimeOffset.UtcNow;
        var backupTimeMs = timestamp.ToUnixTimeMilliseconds();
        var dir = Path.Combine(
            backupRoot,
            deviceHash,
            LocalPathNames.SafeFileName(app.PackageName, fallback: "unknown.package"),
            backupTimeMs.ToString());
        Directory.CreateDirectory(dir);

        var apkEntries = new List<ApkFileEntry>();
        var checksumLines = new List<string>();
        foreach (var src in localApkPaths)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(src))
            {
                _log.Warning("Skipping missing APK source {Src}", src);
                continue;
            }
            var name = Path.GetFileName(src);
            var dst = Path.Combine(dir, name);
            File.Copy(src, dst, overwrite: true);
            var sha = await Sha256OfFileAsync(dst, ct);
            var size = new FileInfo(dst).Length;
            apkEntries.Add(new ApkFileEntry { FileName = name, SizeBytes = size, Sha256 = sha });
            checksumLines.Add($"{sha}  {name}");
        }

        var meta = new AppManagerBackupMeta
        {
            BackupName = $"{app.PackageName}_{backupTimeMs}",
            BackupTimeMs = backupTimeMs,
            PackageName = app.PackageName,
            VersionName = app.VersionName,
            VersionCode = app.VersionCode,
            DeviceHash = deviceHash,
            ToolVersion = toolVersion,
            Apks = apkEntries,
            Flags = new BackupFlags
            {
                IncludesApk = apkEntries.Count > 0,
                IncludesSplits = apkEntries.Count > 1,
                IncludesData = false,
                IncludesExtData = false,
                IncludesObb = false,
                IncludesPermissions = false,
                IncludesRules = false,
            },
        };

        await File.WriteAllTextAsync(Path.Combine(dir, "meta.am.v5"),
            JsonSerializer.Serialize(meta, JsonOpts), ct);
        await File.WriteAllLinesAsync(Path.Combine(dir, "checksums.txt"),
            checksumLines, ct);

        _log.Information("AppManager-format backup written: pkg={Pkg} dir={Dir} apks={Apks}",
            app.PackageName, dir, apkEntries.Count);
        return dir;
    }

    private static async Task<string> Sha256OfFileAsync(string path, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var stream = File.OpenRead(path);
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
