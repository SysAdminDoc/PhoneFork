using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    [JsonPropertyName("oneUi")] public string? OneUi { get; init; }
    [JsonPropertyName("android")] public string? Android { get; init; }
    [JsonPropertyName("tier")] public string? Tier { get; init; }
    [JsonPropertyName("warning")] public string? Warning { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }

    public DebloatTier? ParsedTier => Tier?.ToLowerInvariant() switch
    {
        "delete" => DebloatTier.Delete,
        "replace" => DebloatTier.Replace,
        "caution" => DebloatTier.Caution,
        "unsafe" => DebloatTier.Unsafe,
        _ => null,
    };
}

/// <summary>
/// Container for the embedded overrides JSON. Top-level "overrides" array.
/// </summary>
public sealed record DebloatOverridesFile
{
    [JsonPropertyName("overrides")] public IReadOnlyList<DebloatOverride> Overrides { get; init; } = Array.Empty<DebloatOverride>();
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
    {
        if (Overrides.Count == 0) return this;

        var oneUi = ParseOneUi(oneUiVersionRaw);
        var android = ParseAndroid(androidVersionRaw);

        var modified = new Dictionary<string, DebloatEntry>(ByPackageId, StringComparer.Ordinal);
        var changed = 0;
        foreach (var ov in Overrides)
        {
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
                    Warning = ov.Warning,
                    Removal = (ov.Tier ?? "unsafe").ToLowerInvariant(),
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
                Warning = string.IsNullOrWhiteSpace(ov.Warning) ? entry.Warning : ov.Warning,
            };
            changed++;
        }
        if (changed == 0) return this;
        return new DebloatDataset(modified.Values.ToArray(), Overrides);
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
