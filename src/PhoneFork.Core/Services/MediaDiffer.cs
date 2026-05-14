using PhoneFork.Core.Models;

namespace PhoneFork.Core.Services;

public enum MediaDiffOutcome
{
    /// <summary>File present only on source — should be pulled to dest.</summary>
    NewOnSource,
    /// <summary>File present only on dest — left alone unless <c>--delete</c> is requested.</summary>
    NewOnDest,
    /// <summary>Both sides have same RelPath + size + mtime; skip.</summary>
    Identical,
    /// <summary>Same RelPath, different size or mtime: source wins by default; <c>--update</c> swaps for "source newer only".</summary>
    Conflict,
}

public sealed record MediaDiffEntry(
    string RelPath,
    MediaDiffOutcome Outcome,
    MediaFile? Source,
    MediaFile? Dest);

public sealed record MediaCategoryDiff(
    MediaCategory Category,
    IReadOnlyList<MediaDiffEntry> Entries)
{
    public int Count(MediaDiffOutcome o) => Entries.Count(e => e.Outcome == o);
    public long BytesToTransfer => Entries
        .Where(e => e.Outcome is MediaDiffOutcome.NewOnSource or MediaDiffOutcome.Conflict)
        .Sum(e => e.Source?.SizeBytes ?? 0);
}

public sealed record MediaPlan(
    string SourceSerial,
    string DestSerial,
    IReadOnlyList<MediaCategoryDiff> CategoryDiffs)
{
    public long TotalBytesToTransfer => CategoryDiffs.Sum(d => d.BytesToTransfer);
    public int TotalFilesToTransfer => CategoryDiffs.Sum(d => d.Count(MediaDiffOutcome.NewOnSource) + d.Count(MediaDiffOutcome.Conflict));
    public int TotalConflicts => CategoryDiffs.Sum(d => d.Count(MediaDiffOutcome.Conflict));
}

public static class MediaDiffer
{
    public static MediaPlan Build(MediaManifest source, MediaManifest dest)
    {
        var bySource = source.Categories.ToDictionary(c => c.Category);
        var byDest = dest.Categories.ToDictionary(c => c.Category);
        var cats = bySource.Keys.Union(byDest.Keys).Distinct().OrderBy(c => (int)c).ToList();
        var diffs = new List<MediaCategoryDiff>();

        foreach (var cat in cats)
        {
            bySource.TryGetValue(cat, out var sm);
            byDest.TryGetValue(cat, out var dm);
            var srcMap = (sm?.Files ?? (IReadOnlyList<MediaFile>)Array.Empty<MediaFile>())
                .ToDictionary(f => f.RelPath, StringComparer.Ordinal);
            var dstMap = (dm?.Files ?? (IReadOnlyList<MediaFile>)Array.Empty<MediaFile>())
                .ToDictionary(f => f.RelPath, StringComparer.Ordinal);

            var entries = new List<MediaDiffEntry>(srcMap.Count + dstMap.Count);
            foreach (var (path, sf) in srcMap)
            {
                if (!dstMap.TryGetValue(path, out var df))
                {
                    entries.Add(new MediaDiffEntry(path, MediaDiffOutcome.NewOnSource, sf, null));
                }
                else if (sf.SizeBytes == df.SizeBytes && sf.Mtime == df.Mtime)
                {
                    entries.Add(new MediaDiffEntry(path, MediaDiffOutcome.Identical, sf, df));
                }
                else
                {
                    entries.Add(new MediaDiffEntry(path, MediaDiffOutcome.Conflict, sf, df));
                }
            }
            foreach (var (path, df) in dstMap)
            {
                if (!srcMap.ContainsKey(path))
                    entries.Add(new MediaDiffEntry(path, MediaDiffOutcome.NewOnDest, null, df));
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.RelPath, b.RelPath));
            diffs.Add(new MediaCategoryDiff(cat, entries));
        }

        return new MediaPlan(source.DeviceSerial, dest.DeviceSerial, diffs);
    }
}
