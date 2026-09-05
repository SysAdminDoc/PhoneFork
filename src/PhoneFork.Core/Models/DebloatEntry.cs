using System.Text.Json.Serialization;

namespace PhoneFork.Core.Models;

/// <summary>
/// Safety classification PhoneFork's UI uses to colour and filter rows. Mirrors UAD-NG's
/// Recommended/Advanced/Expert/Unsafe levels and AppManagerNG's delete/replace/caution/unsafe.
/// </summary>
public enum DebloatTier
{
    /// <summary>Safe to disable; replaceable later if needed.</summary>
    Delete,
    /// <summary>Disable but consider installing a replacement (e.g. dialer, keyboard).</summary>
    Replace,
    /// <summary>May affect some behaviour. Disable with care.</summary>
    Caution,
    /// <summary>Disabling will break system functionality. Avoid unless you know exactly why.</summary>
    Unsafe,
}

/// <summary>
/// Which JSON file (~= vendor bucket) an entry originated from. Surfaced as a filter chip in the UI.
/// </summary>
public enum DebloatList
{
    Oem,
    Google,
    Carrier,
    Aosp,
    Misc,
}

/// <summary>
/// One package row from the AppManagerNG / UAD-NG debloat dataset.
/// </summary>
public sealed record DebloatEntry
{
    [JsonPropertyName("id")] public required string PackageId { get; init; }
    [JsonPropertyName("label")] public string? Label { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("warning")] public string? Warning { get; init; }
    [JsonPropertyName("removal")] public required string Removal { get; init; }
    [JsonPropertyName("web")] public IReadOnlyList<string>? Web { get; init; }
    [JsonPropertyName("tags")] public IReadOnlyList<string>? Tags { get; init; }
    [JsonPropertyName("dependencies")] public IReadOnlyList<string>? Dependencies { get; init; }
    [JsonPropertyName("required_by")] public IReadOnlyList<string>? RequiredBy { get; init; }

    [JsonIgnore] public DebloatList List { get; init; }
    [JsonIgnore] public DebloatTier Tier => ParseTier(Removal) ?? DebloatTier.Unsafe;

    /// <summary>
    /// Maps an upstream removal value onto a <see cref="DebloatTier"/>, accepting both the
    /// current UAD-NG vocabulary (Recommended / Advanced / Expert / Unsafe, renamed upstream
    /// during 2026) and the older AppManagerNG spelling (delete / replace / caution / unsafe)
    /// that earlier PhoneFork dataset snapshots and override feeds still use.
    /// Returns null when the value is not recognised so callers can decide how to fail.
    /// </summary>
    public static DebloatTier? ParseTier(string? removal) => removal?.Trim().ToLowerInvariant() switch
    {
        "recommended" or "delete"  => DebloatTier.Delete,
        "advanced" or "replace"    => DebloatTier.Replace,
        "expert" or "caution"      => DebloatTier.Caution,
        "unsafe"                   => DebloatTier.Unsafe,
        _                          => null,
    };

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? PackageId : Label;
}
