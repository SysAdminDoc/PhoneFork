using System.Text.Json.Serialization;

namespace PhoneFork.Core.Services;

/// <summary>
/// Shape of a PhoneFork open-export archive (F023). One folder per migration,
/// human-readable on the host, scriptable via standard 7-Zip / tar tools.
/// </summary>
///
/// Layout under <c>%LOCALAPPDATA%\PhoneFork\exports\&lt;migrationId&gt;\</c>:
/// <list type="bullet">
///   <item><c>manifest.json</c> — this <see cref="OpenArchiveManifest"/> serialized.</item>
///   <item><c>contacts.vcf</c> — vCard 4.0, one record per contact.</item>
///   <item><c>sms.json</c> — NDJSON per thread (one thread per line; each line carries an
///         array of messages keyed by canonical address + epoch ms).</item>
///   <item><c>calllog.json</c> — NDJSON, one call per line.</item>
///   <item><c>wifi.json</c> — single JSON document, list of saved networks (PSK only when
///         the Shizuku-backed privileged path is available).</item>
///   <item><c>apps/&lt;packageId&gt;/base.apk</c> + <c>split_*.apk</c> — pulled APKs.</item>
///   <item><c>checksums.txt</c> — SHA-256 per file, AppManager-compatible format.</item>
/// </list>
/// The spec is intentionally close enough to Open Android Backup's 7-Zip layout that a
/// downstream consumer can drop a PhoneFork export into either ecosystem without rewriting.
public sealed record OpenArchiveManifest
{
    [JsonPropertyName("schema")] public string Schema { get; init; } = "phonefork-open-archive-v1";
    [JsonPropertyName("createdAt")] public required DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("toolVersion")] public required string ToolVersion { get; init; }
    [JsonPropertyName("migrationId")] public required string MigrationId { get; init; }

    [JsonPropertyName("source")] public ArchiveEndpointInfo Source { get; init; } = new();
    [JsonPropertyName("destination")] public ArchiveEndpointInfo? Destination { get; init; }

    [JsonPropertyName("categories")] public IReadOnlyList<CategoryEntry> Categories { get; init; } = Array.Empty<CategoryEntry>();
    [JsonPropertyName("notes")] public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Android 16 QPR2+ apps can opt into cross-platform transfer via
    /// <c>&lt;cross-platform-transfer platform="ios"&gt;</c> in their backup_rules.xml.
    /// PhoneFork echoes the same metadata in the archive manifest so a downstream
    /// consumer can answer "did the source app participate in iOS interop" without
    /// re-parsing per-app manifests (F035).
    /// </summary>
    [JsonPropertyName("crossPlatform")] public CrossPlatformMetadata? CrossPlatform { get; init; }
}

/// <summary>Per-archive iOS cross-platform-transfer posture (F035).</summary>
public sealed record CrossPlatformMetadata
{
    [JsonPropertyName("iosCompatibleApps")] public IReadOnlyList<string> IosCompatibleApps { get; init; } = Array.Empty<string>();
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; } = 1;
    [JsonPropertyName("notes")] public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record ArchiveEndpointInfo
{
    /// <summary>Hashed serial (12-hex SHA-256 prefix). Never raw.</summary>
    [JsonPropertyName("deviceHash")] public string DeviceHash { get; init; } = "";
    [JsonPropertyName("label")] public string Label { get; init; } = "";
    [JsonPropertyName("androidVersion")] public string AndroidVersion { get; init; } = "";
    [JsonPropertyName("oneUiVersion")] public string OneUiVersion { get; init; } = "";
}

public sealed record CategoryEntry
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("file")] public required string File { get; init; }
    [JsonPropertyName("sha256")] public required string Sha256 { get; init; }
    [JsonPropertyName("bytes")] public required long Bytes { get; init; }
    [JsonPropertyName("rows")] public long? Rows { get; init; }
}
