using PhoneFork.Core.Models;

namespace PhoneFork.Core.Services;

/// <summary>
/// Which safety tiers each named debloat profile selects (F110).
///
/// Shared by the CLI (<c>phonefork debloat apply --profile</c>), the WPF Debloat tab, and the
/// tests that assert the Unsafe tier can never be reached implicitly. Keeping one definition
/// means a tier that upstream reclassifies as Unsafe drops out of every caller at once.
/// </summary>
public static class DebloatProfiles
{
    public const string Conservative = "Conservative";
    public const string Recommended = "Recommended";
    public const string Aggressive = "Aggressive";

    public static readonly IReadOnlyList<string> Names = new[] { Conservative, Recommended, Aggressive };

    /// <summary>
    /// Tiers selected by <paramref name="profile"/>. Unrecognised names fall back to the
    /// most cautious profile. <see cref="DebloatTier.Unsafe"/> is never included by a profile;
    /// it is only reachable when <paramref name="includeUnsafe"/> is explicitly set.
    /// </summary>
    public static HashSet<DebloatTier> TiersFor(string? profile, bool includeUnsafe = false)
    {
        var tiers = profile?.Trim().ToLowerInvariant() switch
        {
            "recommended" => new HashSet<DebloatTier> { DebloatTier.Delete, DebloatTier.Replace },
            "aggressive" => new HashSet<DebloatTier> { DebloatTier.Delete, DebloatTier.Replace, DebloatTier.Caution },
            _ => new HashSet<DebloatTier> { DebloatTier.Delete },
        };
        if (includeUnsafe) tiers.Add(DebloatTier.Unsafe);
        return tiers;
    }

    /// <summary>
    /// Of <paramref name="packageIds"/>, those the dataset classifies as
    /// <see cref="DebloatTier.Unsafe"/>. Used to gate an explicitly named package list, which
    /// bypasses profile tier filtering entirely and would otherwise disable an Unsafe package
    /// with no warning.
    /// </summary>
    public static IReadOnlyList<string> UnsafePackagesIn(
        DebloatDataset dataset,
        IEnumerable<string> packageIds)
    {
        return packageIds
            .Where(id => dataset.ByPackageId.TryGetValue(id, out var entry) && entry.Tier == DebloatTier.Unsafe)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
