using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// One row of the overrides overlay (F102). Applied on top of the upstream dataset
/// when a device's One UI / Android version matches the override's predicate.
/// </summary>
public sealed record DebloatOverride
{
    [JsonPropertyName("packageId")] public required string PackageId { get; init; }
    [JsonPropertyName("oem")] public string? Oem { get; init; }
    [JsonPropertyName("oneUi")] public string? OneUi { get; init; }
    [JsonPropertyName("android")] public string? Android { get; init; }
    [JsonPropertyName("tier")] public string? Tier { get; init; }
    [JsonPropertyName("action")] public string? Action { get; init; }
    [JsonPropertyName("risk")] public string? Risk { get; init; }
    [JsonPropertyName("warning")] public string? Warning { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("expiresAt")] public string? ExpiresAt { get; init; }
    [JsonPropertyName("reviewAfter")] public string? ReviewAfter { get; init; }

    public DebloatTier? ParsedTier => (Tier ?? Action)?.ToLowerInvariant() switch
    {
        "delete" => DebloatTier.Delete,
        "replace" => DebloatTier.Replace,
        "caution" => DebloatTier.Caution,
        "unsafe" => DebloatTier.Unsafe,
        _ => null,
    };

    public bool IsExpired(DateOnly today) =>
        DateOnly.TryParse(ExpiresAt, out var expires) && expires < today;
}

/// <summary>
/// Container for the embedded overrides JSON. Top-level "overrides" array.
/// </summary>
public sealed record DebloatOverridesFile
{
    [JsonPropertyName("$schema")] public string? Schema { get; init; }
    [JsonPropertyName("generatedAt")] public string? GeneratedAt { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("overrides")] public IReadOnlyList<DebloatOverride> Overrides { get; init; } = Array.Empty<DebloatOverride>();
}

public sealed record DebloatOverrideFeed(
    string Path,
    string Sha256,
    IReadOnlyList<DebloatOverride> Overrides);

public static class DebloatOverrideFeedLoader
{
    public static async Task<DebloatOverrideFeed> LoadAsync(
        string path,
        string? expectedSha256 = null,
        bool requireChecksum = true,
        CancellationToken ct = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Debloat override feed not found.", path);

        var actual = await Sha256Async(path, ct);
        var expected = NormalizeSha256(expectedSha256) ?? await TryReadSidecarSha256Async(path, ct);
        if (requireChecksum && string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException("Debloat override feeds must be verified with --overlay-sha256 or a .sha256 sidecar file.");
        if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Debloat override feed checksum mismatch: expected {expected}, got {actual}.");

        await using var stream = File.OpenRead(path);
        var doc = await JsonSerializer.DeserializeAsync<DebloatOverridesFile>(stream, cancellationToken: ct)
                  ?? throw new InvalidDataException("Debloat override feed did not deserialize.");
        if (doc.Schema is not null && !doc.Schema.StartsWith("phonefork-debloat-overrides-", StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported debloat override feed schema '{doc.Schema}'.");

        return new DebloatOverrideFeed(path, actual, doc.Overrides);
    }

    private static async Task<string> Sha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string?> TryReadSidecarSha256Async(string path, CancellationToken ct)
    {
        var sidecar = path + ".sha256";
        if (!File.Exists(sidecar)) return null;
        var text = await File.ReadAllTextAsync(sidecar, ct);
        var token = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return NormalizeSha256(token);
    }

    private static string? NormalizeSha256(string? value)
    {
        var trimmed = value?.Trim();
        if (trimmed is null || trimmed.Length != 64) return null;
        return trimmed.All(Uri.IsHexDigit) ? trimmed.ToLowerInvariant() : null;
    }
}

/// <summary>
/// Loads the AppManagerNG / UAD-NG debloat dataset from embedded JSON (5 files: oem, google,
/// carrier, aosp, misc; ~5,481 entries at v0.4.0) and the per-OS override overlay (F102).
/// </summary>
public sealed class DebloatDataset
{
    private static readonly string[] FileNames = { "oem.json", "google.json", "carrier.json", "aosp.json", "misc.json" };
    private const string OverridesResource = "PhoneFork.Core.Assets.Debloat.overrides.json";

    public IReadOnlyDictionary<string, DebloatEntry> ByPackageId { get; }
    public IReadOnlyList<DebloatEntry> Entries { get; }
    public IReadOnlyList<DebloatOverride> Overrides { get; }

    private DebloatDataset(IReadOnlyList<DebloatEntry> entries, IReadOnlyList<DebloatOverride> overrides)
    {
        Entries = entries;
        Overrides = overrides;
        var dict = new Dictionary<string, DebloatEntry>(entries.Count, StringComparer.Ordinal);
        foreach (var e in entries) dict.TryAdd(e.PackageId, e);
        ByPackageId = dict;
    }

    public static DebloatDataset Load(ILogger? log = null)
    {
        var asm = Assembly.GetExecutingAssembly();
        var entries = new List<DebloatEntry>(6_000);
        foreach (var fname in FileNames)
        {
            var resource = $"PhoneFork.Core.Assets.Debloat.{fname}";
            using var stream = asm.GetManifestResourceStream(resource);
            if (stream is null)
            {
                log?.Warning("Debloat dataset resource missing: {Resource}", resource);
                continue;
            }
            var rows = JsonSerializer.Deserialize<List<DebloatEntry>>(stream)
                       ?? new List<DebloatEntry>();
            var list = fname.Replace(".json", "") switch
            {
                "oem"     => DebloatList.Oem,
                "google"  => DebloatList.Google,
                "carrier" => DebloatList.Carrier,
                "aosp"    => DebloatList.Aosp,
                "misc"    => DebloatList.Misc,
                _         => DebloatList.Misc,
            };
            foreach (var r in rows) entries.Add(r with { List = list });
            log?.Information("Loaded {Count} entries from {File}", rows.Count, fname);
        }

        // Load overrides overlay. Missing or malformed is non-fatal — the upstream dataset still wins.
        IReadOnlyList<DebloatOverride> overrides = Array.Empty<DebloatOverride>();
        try
        {
            using var overrideStream = asm.GetManifestResourceStream(OverridesResource);
            if (overrideStream is not null)
            {
                var file = JsonSerializer.Deserialize<DebloatOverridesFile>(overrideStream);
                overrides = file?.Overrides ?? Array.Empty<DebloatOverride>();
                log?.Information("Loaded {Count} debloat overrides", overrides.Count);
            }
        }
        catch (Exception ex)
        {
            log?.Warning(ex, "Failed to parse debloat overrides; falling back to upstream dataset only");
        }

        return new DebloatDataset(entries, overrides);
    }

    /// <summary>
    /// Build a dataset view with the overlay applied for the given device fingerprint
    /// (One UI version string from <c>ro.build.version.oneui</c>, Android version string
    /// from <c>ro.build.version.release</c>). Returns the same instance when nothing matches.
    /// </summary>
    public DebloatDataset WithOverridesFor(string oneUiVersionRaw, string androidVersionRaw)
        => WithOverridesFor(oneUiVersionRaw, androidVersionRaw, oemRaw: null);

    public DebloatDataset WithOverridesFor(string oneUiVersionRaw, string androidVersionRaw, string? oemRaw)
    {
        if (Overrides.Count == 0) return this;

        var oneUi = ParseOneUi(oneUiVersionRaw);
        var android = ParseAndroid(androidVersionRaw);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var modified = new Dictionary<string, DebloatEntry>(ByPackageId, StringComparer.Ordinal);
        var changed = 0;
        foreach (var ov in Overrides)
        {
            if (ov.IsExpired(today)) continue;
            if (!MatchesOem(ov.Oem, oemRaw)) continue;
            if (!MatchesOs(ov.OneUi, oneUi) || !MatchesOs(ov.Android, android)) continue;
            if (!modified.TryGetValue(ov.PackageId, out var entry))
            {
                // The override may reference a package the upstream dataset doesn't carry yet.
                // Synthesize a minimal entry so it is visible in the scanner.
                entry = new DebloatEntry
                {
                    PackageId = ov.PackageId,
                    Label = ov.PackageId,
                    Description = null,
                    Warning = BuildOverrideWarning(null, ov),
                    Removal = (ov.Tier ?? ov.Action ?? "unsafe").ToLowerInvariant(),
                    List = DebloatList.Misc,
                };
                modified[ov.PackageId] = entry;
                changed++;
                continue;
            }

            var tier = ov.ParsedTier ?? entry.Tier;
            modified[ov.PackageId] = entry with
            {
                Removal = tier.ToString().ToLowerInvariant(),
                Warning = BuildOverrideWarning(entry.Warning, ov),
            };
            changed++;
        }
        if (changed == 0) return this;
        return new DebloatDataset(modified.Values.ToArray(), Overrides);
    }

    public DebloatDataset WithOverrideFeed(DebloatOverrideFeed feed)
    {
        if (feed.Overrides.Count == 0) return this;
        return new DebloatDataset(Entries, Overrides.Concat(feed.Overrides).ToArray());
    }

    internal static bool MatchesOem(string? predicate, string? oemRaw)
    {
        if (string.IsNullOrWhiteSpace(predicate) || predicate.Trim() == "*") return true;
        if (string.IsNullOrWhiteSpace(oemRaw)) return false;

        var oem = oemRaw.Trim();
        return predicate.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, oem, StringComparison.OrdinalIgnoreCase));
    }

    private static string? BuildOverrideWarning(string? existingWarning, DebloatOverride ov)
    {
        var warning = string.IsNullOrWhiteSpace(ov.Warning) ? existingWarning : ov.Warning;
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(ov.Risk)) details.Add($"Risk: {ov.Risk}");
        if (!string.IsNullOrWhiteSpace(ov.Source)) details.Add($"Source: {ov.Source}");
        if (!string.IsNullOrWhiteSpace(ov.ReviewAfter)) details.Add($"Review after: {ov.ReviewAfter}");
        if (!string.IsNullOrWhiteSpace(ov.ExpiresAt)) details.Add($"Expires: {ov.ExpiresAt}");
        if (details.Count == 0) return warning;
        return string.IsNullOrWhiteSpace(warning)
            ? string.Join(" ", details)
            : warning + " " + string.Join(" ", details);
    }

    internal static Version? ParseOneUi(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // ro.build.version.oneui is encoded major*10000 + minor*100 + patch (e.g. 80500 = 8.5.0).
        if (int.TryParse(raw.Trim(), out var n))
        {
            return new Version(n / 10000, n / 100 % 100, n % 100);
        }
        if (Version.TryParse(raw.Trim(), out var v)) return v;
        return null;
    }

    internal static Version? ParseAndroid(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (int.TryParse(raw.Trim(), out var major)) return new Version(major, 0);
        if (Version.TryParse(raw.Trim(), out var v)) return v;
        return null;
    }

    /// <summary>
    /// Matches a version predicate like "&gt;=8.5", "&lt;9", "8.5", "*" against a device value.
    /// Predicate null/empty matches everything; device value null only matches "*".
    /// </summary>
    internal static bool MatchesOs(string? predicate, Version? deviceValue)
    {
        if (string.IsNullOrWhiteSpace(predicate) || predicate.Trim() == "*") return true;
        if (deviceValue is null) return false;

        var raw = predicate.Trim();
        string op;
        string rest;
        if (raw.StartsWith(">=", StringComparison.Ordinal)) { op = ">="; rest = raw[2..]; }
        else if (raw.StartsWith("<=", StringComparison.Ordinal)) { op = "<="; rest = raw[2..]; }
        else if (raw.StartsWith(">", StringComparison.Ordinal)) { op = ">"; rest = raw[1..]; }
        else if (raw.StartsWith("<", StringComparison.Ordinal)) { op = "<"; rest = raw[1..]; }
        else if (raw.StartsWith("==", StringComparison.Ordinal)) { op = "=="; rest = raw[2..]; }
        else { op = "=="; rest = raw; }

        if (!Version.TryParse(rest.Trim(), out var target))
        {
            // Allow bare "8" or "8.5" without three components.
            var parts = rest.Trim().Split('.');
            if (parts.Length == 1 && int.TryParse(parts[0], out var maj))
                target = new Version(maj, 0);
            else if (parts.Length == 2 && int.TryParse(parts[0], out maj) && int.TryParse(parts[1], out var min))
                target = new Version(maj, min);
            else
                return false;
        }

        // Normalize both to a 3-component form so 8.5 compares correctly to 8.5.0.
        var d = Normalize(deviceValue);
        var t = Normalize(target);
        var cmp = d.CompareTo(t);
        return op switch
        {
            ">="  => cmp >= 0,
            "<="  => cmp <= 0,
            ">"   => cmp > 0,
            "<"   => cmp < 0,
            "=="  => cmp == 0,
            _     => false,
        };
    }

    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor >= 0 ? v.Minor : 0, v.Build >= 0 ? v.Build : 0);
}

public static class DebloatDatasetResolver
{
    public static async Task<DebloatDataset> LoadForDeviceAsync(
        IAdbClient client,
        DeviceData device,
        ILogger log,
        string? overlayFeedPath = null,
        string? overlaySha256 = null,
        CancellationToken ct = default)
    {
        var dataset = DebloatDataset.Load(log);
        if (!string.IsNullOrWhiteSpace(overlayFeedPath))
        {
            var feed = await DebloatOverrideFeedLoader.LoadAsync(overlayFeedPath, overlaySha256, requireChecksum: true, ct);
            dataset = dataset.WithOverrideFeed(feed);
            log.Information("Loaded debloat overlay feed {Path} sha256={Sha256} overrides={Count}",
                feed.Path, feed.Sha256, feed.Overrides.Count);
        }

        var oneUi = await SafeGetpropAsync(client, device, "ro.build.version.oneui", ct);
        var android = await SafeGetpropAsync(client, device, "ro.build.version.release", ct);
        var oem = await SafeGetpropAsync(client, device, "ro.product.manufacturer", ct);
        return dataset.WithOverridesFor(oneUi, android, oem);
    }

    private static async Task<string> SafeGetpropAsync(IAdbClient client, DeviceData device, string prop, CancellationToken ct)
    {
        try
        {
            return (await client.ShellAsync(device, $"getprop {AdbShell.Arg(prop)}", ct)).Trim();
        }
        catch
        {
            return "";
        }
    }
}
