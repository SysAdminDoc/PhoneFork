using PhoneFork.Core.Models;

namespace PhoneFork.Core.Services;

/// <summary>
/// One reason a queued package might break something still on the device (F121).
/// </summary>
/// <param name="PackageId">The package the user selected for disabling.</param>
/// <param name="NeededBy">
/// Packages the dataset says depend on it that are still enabled and are NOT themselves queued.
/// A dependent that is being disabled in the same run is not a conflict.
/// </param>
public sealed record DebloatDependencyWarning(
    string PackageId,
    IReadOnlyList<string> NeededBy)
{
    public string Describe() =>
        $"{PackageId} is needed by {string.Join(", ", NeededBy)}, which will stay enabled.";
}

/// <summary>
/// Cross-checks a debloat queue against the dataset's dependency graph (F121).
///
/// Upstream ships a <c>neededBy</c> list per package, which PhoneFork stores as
/// <see cref="DebloatEntry.RequiredBy"/>. Turning that into a pre-apply warning is the difference
/// between a description the user skims and a check they see: disabling
/// <c>com.samsung.oda.service</c>, for example, crashes SIM Manager on dual-SIM devices because
/// <c>com.samsung.android.app.telephonyui</c> needs it.
/// </summary>
public static class DebloatDependencyCheck
{
    /// <summary>
    /// Warnings for <paramref name="queuedPackageIds"/>, given what the scan found on the device.
    /// A dependent only counts when it is still enabled and is not itself in the queue.
    /// </summary>
    public static IReadOnlyList<DebloatDependencyWarning> Evaluate(
        DebloatDataset dataset,
        IEnumerable<string> queuedPackageIds,
        IEnumerable<DebloatCandidate> candidates)
    {
        var queued = new HashSet<string>(queuedPackageIds, StringComparer.Ordinal);
        var enabledOnDevice = new HashSet<string>(
            candidates.Where(c => c.IsEnabled).Select(c => c.Entry.PackageId),
            StringComparer.Ordinal);

        var warnings = new List<DebloatDependencyWarning>();
        foreach (var packageId in queued.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!dataset.ByPackageId.TryGetValue(packageId, out var entry)) continue;
            if (entry.RequiredBy is not { Count: > 0 } requiredBy) continue;

            var survivors = requiredBy
                .Where(dependent => enabledOnDevice.Contains(dependent) && !queued.Contains(dependent))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToList();

            if (survivors.Count > 0)
                warnings.Add(new DebloatDependencyWarning(packageId, survivors));
        }

        return warnings;
    }
}
