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
    [JsonIgnore] public DebloatTier Tier => Removal switch
    {
        "delete"  => DebloatTier.Delete,
        "replace" => DebloatTier.Replace,
        "caution" => DebloatTier.Caution,
        "unsafe"  => DebloatTier.Unsafe,
        _         => DebloatTier.Unsafe,
    };

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? PackageId : Label;
}
