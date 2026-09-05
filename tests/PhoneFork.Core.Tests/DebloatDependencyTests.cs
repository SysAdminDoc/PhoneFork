using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F121 — upstream records which packages depend on each entry. Disabling one while a dependent
/// stays enabled is the failure mode behind reports like "SIM Manager crashes after debloating",
/// so it has to be surfaced before anything is written.
/// </summary>
public class DebloatDependencyTests
{
    private static DebloatCandidate Candidate(string id, bool enabled, params string[] requiredBy) =>
        new(new DebloatEntry
        {
            PackageId = id,
            Removal = "Recommended",
            RequiredBy = requiredBy.Length == 0 ? null : requiredBy,
        }, enabled);

    private static DebloatDataset DatasetOf(params DebloatCandidate[] candidates) =>
        DebloatDataset.FromEntries(candidates.Select(c => c.Entry).ToList());

    [Fact]
    public void WarnsWhenAnEnabledDependentStaysBehind()
    {
        var oda = Candidate("com.samsung.oda.service", enabled: true, "com.samsung.android.app.telephonyui");
        var telephony = Candidate("com.samsung.android.app.telephonyui", enabled: true);
        var dataset = DatasetOf(oda, telephony);

        var warnings = DebloatDependencyCheck.Evaluate(
            dataset,
            new[] { "com.samsung.oda.service" },
            new[] { oda, telephony });

        var warning = Assert.Single(warnings);
        Assert.Equal("com.samsung.oda.service", warning.PackageId);
        Assert.Equal(new[] { "com.samsung.android.app.telephonyui" }, warning.NeededBy);
        Assert.Contains("telephonyui", warning.Describe());
    }

    [Fact]
    public void DoesNotWarnWhenTheDependentIsQueuedTooBecauseBothGoAway()
    {
        var oda = Candidate("com.samsung.oda.service", enabled: true, "com.samsung.android.app.telephonyui");
        var telephony = Candidate("com.samsung.android.app.telephonyui", enabled: true);
        var dataset = DatasetOf(oda, telephony);

        var warnings = DebloatDependencyCheck.Evaluate(
            dataset,
            new[] { "com.samsung.oda.service", "com.samsung.android.app.telephonyui" },
            new[] { oda, telephony });

        Assert.Empty(warnings);
    }

    [Fact]
    public void DoesNotWarnWhenTheDependentIsAlreadyDisabled()
    {
        var oda = Candidate("com.samsung.oda.service", enabled: true, "com.samsung.android.app.telephonyui");
        var telephony = Candidate("com.samsung.android.app.telephonyui", enabled: false);
        var dataset = DatasetOf(oda, telephony);

        var warnings = DebloatDependencyCheck.Evaluate(
            dataset, new[] { "com.samsung.oda.service" }, new[] { oda, telephony });

        Assert.Empty(warnings);
    }

    [Fact]
    public void DoesNotWarnWhenTheDependentIsNotOnThisDevice()
    {
        // The dataset is a cross-OEM list; most dependents simply are not installed.
        var oda = Candidate("com.samsung.oda.service", enabled: true, "com.samsung.android.app.telephonyui");
        var dataset = DatasetOf(oda);

        var warnings = DebloatDependencyCheck.Evaluate(dataset, new[] { "com.samsung.oda.service" }, new[] { oda });

        Assert.Empty(warnings);
    }

    [Fact]
    public void ReportsOnlyTheDependentsThatSurvive()
    {
        var target = Candidate("com.example.core", enabled: true, "com.example.a", "com.example.b", "com.example.c");
        var a = Candidate("com.example.a", enabled: true);
        var b = Candidate("com.example.b", enabled: false);
        var c = Candidate("com.example.c", enabled: true);
        var dataset = DatasetOf(target, a, b, c);

        var warnings = DebloatDependencyCheck.Evaluate(
            dataset,
            new[] { "com.example.core", "com.example.c" },
            new[] { target, a, b, c });

        // b is already disabled, c is queued; only a survives to be broken.
        var warning = Assert.Single(warnings);
        Assert.Equal(new[] { "com.example.a" }, warning.NeededBy);
    }

    [Fact]
    public void PackagesWithNoRecordedDependentsProduceNoWarning()
    {
        var plain = Candidate("com.example.plain", enabled: true);
        var dataset = DatasetOf(plain);

        Assert.Empty(DebloatDependencyCheck.Evaluate(dataset, new[] { "com.example.plain" }, new[] { plain }));
    }

    [Fact]
    public void APackageMissingFromTheDatasetIsSkipped()
    {
        var plain = Candidate("com.example.plain", enabled: true);
        var dataset = DatasetOf(plain);

        Assert.Empty(DebloatDependencyCheck.Evaluate(dataset, new[] { "com.example.unknown" }, new[] { plain }));
    }

    [Fact]
    public void WarningsAreOrderedAndDeduplicated()
    {
        var target = Candidate("com.example.core", enabled: true, "com.example.z", "com.example.a", "com.example.a");
        var z = Candidate("com.example.z", enabled: true);
        var a = Candidate("com.example.a", enabled: true);
        var dataset = DatasetOf(target, z, a);

        var warning = Assert.Single(
            DebloatDependencyCheck.Evaluate(dataset, new[] { "com.example.core" }, new[] { target, z, a }));

        Assert.Equal(new[] { "com.example.a", "com.example.z" }, warning.NeededBy);
    }

    [Fact]
    public void TheShippedDatasetActuallyCarriesADependencyGraph()
    {
        // Guards against a dataset regeneration that silently drops the neededBy field.
        var dataset = DebloatDataset.Load();

        Assert.True(dataset.ByPackageId.TryGetValue("com.samsung.oda.service", out var oda));
        Assert.Contains("com.samsung.android.app.telephonyui", oda!.RequiredBy ?? Array.Empty<string>());
        Assert.True(dataset.Entries.Count(e => e.RequiredBy is { Count: > 0 }) >= 10);
    }
}
