using System.Text.Json.Serialization;

namespace PhoneFork.Core.Services;

/// <summary>
/// On-disk shape of a PhoneFork per-package APK backup directory (F029/F030/F117).
/// </summary>
///
/// <remarks>
/// This is PhoneFork's own format, not App Manager's. Earlier revisions named the metadata file
/// <c>meta.am.v5</c> and described the layout as AppManager-compatible; that was never true.
/// App Manager 4.1.1 writes a plaintext <c>info_v5.am.json</c> beside an encrypted
/// <c>meta_v5.am.json</c>, with field names (<c>version</c>, <c>backup_name</c>,
/// <c>package_name</c>, <c>data_dirs</c>, <c>is_split_apk</c>, <c>split_configs</c>,
/// <c>apk_name</c>, <c>installer</c>) that do not match what PhoneFork emits, so neither tool can
/// read the other's backups. PhoneFork backups round-trip through PhoneFork only.
///
/// Directory layout per package, rooted under
/// <c>%LOCALAPPDATA%\PhoneFork\backups\&lt;deviceHash&gt;\&lt;packageId&gt;\&lt;backupTimestamp&gt;\</c>:
/// <list type="bullet">
///   <item><c>base.apk</c></item>
///   <item><c>split_*.apk</c> (one per ABI / density / locale split)</item>
///   <item><c>phonefork-backup.v1.json</c> (this <see cref="PhoneForkBackupMeta"/> serialized)</item>
///   <item><c>checksums.txt</c> — one SHA-256 per file, two-space separated.</item>
/// </list>
/// </remarks>
public sealed record PhoneForkBackupMeta
{
    /// <summary>Metadata file name written by this version.</summary>
    public const string FileName = "phonefork-backup.v1.json";

    /// <summary>
    /// Metadata file name used before v0.9.4-pre. Still read so backups taken by an earlier
    /// build keep working; never written.
    /// </summary>
    public const string LegacyFileName = "meta.am.v5";

    [JsonPropertyName("am_meta_version")] public int MetaVersion { get; init; } = 5;
    [JsonPropertyName("backup_name")] public required string BackupName { get; init; }
    [JsonPropertyName("backup_time")] public required long BackupTimeMs { get; init; }

    [JsonPropertyName("package_name")] public required string PackageName { get; init; }
    [JsonPropertyName("version_name")] public string? VersionName { get; init; }
    [JsonPropertyName("version_code")] public long? VersionCode { get; init; }
    [JsonPropertyName("min_sdk")] public int? MinSdk { get; init; }
    [JsonPropertyName("target_sdk")] public int? TargetSdk { get; init; }

    [JsonPropertyName("device_hash")] public string DeviceHash { get; init; } = "";
    [JsonPropertyName("tool")] public string Tool { get; init; } = "PhoneFork";
    [JsonPropertyName("tool_version")] public string ToolVersion { get; init; } = "";

    [JsonPropertyName("apks")] public IReadOnlyList<ApkFileEntry> Apks { get; init; } = Array.Empty<ApkFileEntry>();
    [JsonPropertyName("flags")] public BackupFlags Flags { get; init; } = new();
}

public sealed record ApkFileEntry
{
    [JsonPropertyName("name")] public required string FileName { get; init; }
    [JsonPropertyName("size")] public required long SizeBytes { get; init; }
    [JsonPropertyName("sha256")] public required string Sha256 { get; init; }
}

/// <summary>
/// Which payload kinds a backup directory contains. The field names follow App Manager's
/// "what's included" flags because that vocabulary is a good fit, but the archives are not
/// interchangeable: see <see cref="PhoneForkBackupMeta"/>.
/// </summary>
public sealed record BackupFlags
{
    [JsonPropertyName("apk")] public bool IncludesApk { get; init; }
    [JsonPropertyName("split_apks")] public bool IncludesSplits { get; init; }
    [JsonPropertyName("data")] public bool IncludesData { get; init; }
    [JsonPropertyName("ext_data")] public bool IncludesExtData { get; init; }
    [JsonPropertyName("obb")] public bool IncludesObb { get; init; }
    [JsonPropertyName("permissions")] public bool IncludesPermissions { get; init; }
    [JsonPropertyName("rules")] public bool IncludesRules { get; init; }
}
