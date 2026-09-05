using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F109 — the embedded dataset tracks UAD-NG, which renamed its removal vocabulary from
/// delete/replace/caution/unsafe to Recommended/Advanced/Expert/Unsafe during 2026.
/// Both spellings must parse, and the shipped dataset must be readable end to end.
/// </summary>
public class DebloatTaxonomyTests
{
    [Theory]
    // Current upstream vocabulary.
    [InlineData("Recommended", DebloatTier.Delete)]
    [InlineData("Advanced", DebloatTier.Replace)]
    [InlineData("Expert", DebloatTier.Caution)]
    [InlineData("Unsafe", DebloatTier.Unsafe)]
    // Legacy AppManagerNG vocabulary still used by older snapshots and override feeds.
    [InlineData("delete", DebloatTier.Delete)]
    [InlineData("replace", DebloatTier.Replace)]
    [InlineData("caution", DebloatTier.Caution)]
    [InlineData("unsafe", DebloatTier.Unsafe)]
    // Casing and stray whitespace must not change the classification.
    [InlineData("RECOMMENDED", DebloatTier.Delete)]
    [InlineData(" advanced ", DebloatTier.Replace)]
    public void ParseTierAcceptsBothVocabularies(string removal, DebloatTier expected)
    {
        Assert.Equal(expected, DebloatEntry.ParseTier(removal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("probably-fine")]
    public void ParseTierReturnsNullForUnknownValues(string? removal)
    {
        Assert.Null(DebloatEntry.ParseTier(removal));
    }

    [Fact]
    public void UnknownRemovalFallsBackToUnsafeOnAnEntry()
    {
        var entry = new DebloatEntry { PackageId = "com.example.mystery", Removal = "brand-new-tier" };
        Assert.Equal(DebloatTier.Unsafe, entry.Tier);
    }

    [Fact]
    public void OverrideParsedTierAcceptsCurrentUpstreamVocabulary()
    {
        var ov = new DebloatOverride { PackageId = "com.example.app", Action = "Advanced" };
        Assert.Equal(DebloatTier.Replace, ov.ParsedTier);
    }

    [Fact]
    public void EmbeddedDatasetUsesOnlyRecognisedRemovalValues()
    {
        var dataset = DebloatDataset.Load();
        Assert.NotEmpty(dataset.Entries);

        var unrecognised = dataset.Entries
            .Where(e => DebloatEntry.ParseTier(e.Removal) is null)
            .Select(e => $"{e.PackageId}={e.Removal}")
            .Distinct()
            .Take(10)
            .ToList();

        Assert.Empty(unrecognised);
    }

    [Fact]
    public void EmbeddedDatasetSpansEveryBucketAndTier()
    {
        var dataset = DebloatDataset.Load();

        foreach (var list in Enum.GetValues<DebloatList>())
            Assert.Contains(dataset.Entries, e => e.List == list);

        foreach (var tier in Enum.GetValues<DebloatTier>())
            Assert.Contains(dataset.Entries, e => e.Tier == tier);
    }

    /// <summary>
    /// Regression guard for the specific package that motivated F110: upstream moved
    /// com.samsung.oda.service off the Recommended tier because disabling it crashes
    /// SIM Manager on dual-SIM devices, so it must not land in the Conservative profile.
    /// </summary>
    [Fact]
    public void SamsungOdaServiceIsNotInTheConservativeTier()
    {
        var dataset = DebloatDataset.Load();
        Assert.True(dataset.ByPackageId.TryGetValue("com.samsung.oda.service", out var entry));
        Assert.NotEqual(DebloatTier.Delete, entry!.Tier);
    }

    [Fact]
    public void DatasetProvenanceIsRecordedAndPinnedToAnUpstreamCommit()
    {
        var path = DatasetSourcePath();
        Assert.True(File.Exists(path), $"dataset-source.json is missing at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        var commit = root.GetProperty("upstreamCommit").GetString();
        Assert.NotNull(commit);
        Assert.Equal(40, commit!.Length);
        Assert.True(commit.All(Uri.IsHexDigit), $"upstreamCommit is not a hex SHA: {commit}");

        var sha = root.GetProperty("upstreamSha256").GetString();
        Assert.NotNull(sha);
        Assert.Equal(64, sha!.Length);

        Assert.Equal(
            DebloatDataset.Load().Entries.Count,
            root.GetProperty("upstreamEntries").GetInt32());
    }

    private static string DatasetSourcePath()
    {
        // Walk up from the test binary to the repository root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PhoneFork.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "assets", "debloat", "dataset-source.json");
    }
}

/// <summary>
/// F110 — no named profile may reach the Unsafe tier implicitly, and the packages upstream
/// moved to Unsafe during 2026 must be excluded from every profile in the shipped dataset.
/// </summary>
public class DebloatProfileSafetyTests
{
    /// <summary>Packages upstream reclassified to Unsafe after PhoneFork's 2026-05-14 snapshot.</summary>
    private static readonly string[] ReclassifiedUnsafe =
    {
        "com.android.networkstack.tethering.inprocess",
        "jp.co.omronsoft.iwnnime.ml",
        "com.lenovo.ue.device",
        "com.google.android.overlay.gmsconfig.photos",
    };

    [Theory]
    [InlineData("Conservative")]
    [InlineData("Recommended")]
    [InlineData("Aggressive")]
    [InlineData("aggressive")]
    [InlineData("not-a-profile")]
    [InlineData(null)]
    public void NoProfileSelectsTheUnsafeTier(string? profile)
    {
        Assert.DoesNotContain(DebloatTier.Unsafe, DebloatProfiles.TiersFor(profile));
    }

    [Fact]
    public void IncludeUnsafeIsTheOnlyRouteToTheUnsafeTier()
    {
        foreach (var name in DebloatProfiles.Names)
            Assert.Contains(DebloatTier.Unsafe, DebloatProfiles.TiersFor(name, includeUnsafe: true));
    }

    [Fact]
    public void ProfilesAreOrderedByWideningTierCoverage()
    {
        var conservative = DebloatProfiles.TiersFor(DebloatProfiles.Conservative);
        var recommended = DebloatProfiles.TiersFor(DebloatProfiles.Recommended);
        var aggressive = DebloatProfiles.TiersFor(DebloatProfiles.Aggressive);

        Assert.ProperSubset(recommended, conservative);
        Assert.ProperSubset(aggressive, recommended);
    }

    [Fact]
    public void ReclassifiedPackagesAreUnsafeInTheShippedDataset()
    {
        var dataset = DebloatDataset.Load();
        foreach (var id in ReclassifiedUnsafe)
        {
            Assert.True(dataset.ByPackageId.TryGetValue(id, out var entry), $"{id} is missing from the dataset");
            Assert.Equal(DebloatTier.Unsafe, entry!.Tier);
        }
    }

    [Fact]
    public void ReclassifiedPackagesAreSelectedByNoProfile()
    {
        var dataset = DebloatDataset.Load();
        foreach (var name in DebloatProfiles.Names)
        {
            var tiers = DebloatProfiles.TiersFor(name);
            foreach (var id in ReclassifiedUnsafe)
                Assert.DoesNotContain(dataset.ByPackageId[id].Tier, tiers);
        }
    }

    [Fact]
    public void ExplicitlyNamedUnsafePackagesAreDetectedForGating()
    {
        var dataset = DebloatDataset.Load();
        var named = ReclassifiedUnsafe.Concat(new[] { "com.samsung.oda.service" });

        var flagged = DebloatProfiles.UnsafePackagesIn(dataset, named);

        Assert.Equal(ReclassifiedUnsafe.OrderBy(s => s), flagged.OrderBy(s => s));
        Assert.DoesNotContain("com.samsung.oda.service", flagged);
    }

    [Fact]
    public void UnsafeScanIgnoresPackagesTheDatasetDoesNotKnow()
    {
        var dataset = DebloatDataset.Load();
        Assert.Empty(DebloatProfiles.UnsafePackagesIn(dataset, new[] { "com.example.not.in.dataset" }));
    }
}
