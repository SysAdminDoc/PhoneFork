using System.Text.Json.Serialization;

namespace PhoneFork.Core.Services;

/// <summary>
/// On-disk shape of an AppManager-compatible per-package backup directory (F029/F030).
/// Mirrors <c>MuntashirAkon/AppManager</c>'s <c>meta.am.v5</c> schema closely enough that
/// PhoneFork backups can be read by AppManager and vice-versa. License note: the
/// AppManager codebase itself is GPL-3.0; PhoneFork copies the on-disk layout (data,
/// not code) only.
/// </summary>
///
/// Directory layout per package, rooted under
/// <c>%LOCALAPPDATA%\PhoneFork\backups\&lt;deviceHash&gt;\&lt;packageId&gt;\&lt;backupTimestamp&gt;\</c>:
/// <list type="bullet">
///   <item><c>base.apk</c></item>
///   <item><c>split_*.apk</c> (one per ABI / density / locale split)</item>
///   <item><c>meta.am.v5</c> (this <see cref="AppManagerBackupMeta"/> serialized)</item>
///   <item><c>checksums.txt</c> — one SHA-256 per file, AppManager-format.</item>
///   <item><c>permissions.am.tsv</c> — declared runtime permissions, tab-separated.</item>
///   <item><c>rules.am.tsv</c> — battery optimization, net policy, SSAID etc.</item>
///   <item><c>(future) data.tar.gz.0, ext_data.tar.gz.0, obb.tar.gz.0</c></item>
/// </list>
public sealed record AppManagerBackupMeta
{
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
/// Subset of AppManager's "what's included" flags. PhoneFork emits the same shape so
/// AppManager doesn't complain when it reads our directories.
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
