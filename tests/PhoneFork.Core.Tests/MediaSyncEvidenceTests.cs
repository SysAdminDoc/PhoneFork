using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class MediaSyncEvidenceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"phonefork-media-evidence-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void QuickShareAdvisoryOnlyAppearsForSingleLargeTransfer()
    {
        var plan = Plan(("DCIM/huge.mp4", 800L * 1024 * 1024));

        var advisories = MediaSyncEvidence.BuildAdvisories(
            plan,
            hugeFileWarningBytes: 4L * 1024 * 1024 * 1024,
            quickShareSingleFileBytes: 512L * 1024 * 1024);

        Assert.Contains(advisories, a => a.Kind == MediaTransferAdvisoryKind.QuickShareBetterSingleFile);
    }

    [Fact]
    public void QuickShareAdvisoryIsSuppressedForMultiFilePlans()
    {
        var plan = Plan(
            ("DCIM/a.mp4", 800L * 1024 * 1024),
            ("DCIM/b.mp4", 800L * 1024 * 1024));

        var advisories = MediaSyncEvidence.BuildAdvisories(
            plan,
            hugeFileWarningBytes: 4L * 1024 * 1024 * 1024,
            quickShareSingleFileBytes: 512L * 1024 * 1024);

        Assert.DoesNotContain(advisories, a => a.Kind == MediaTransferAdvisoryKind.QuickShareBetterSingleFile);
    }

    [Fact]
    public void HugeFileAdvisoryUsesConfiguredThreshold()
    {
        var plan = Plan(("Movies/long.mkv", 5L * 1024 * 1024 * 1024));

        var advisories = MediaSyncEvidence.BuildAdvisories(
            plan,
            hugeFileWarningBytes: 4L * 1024 * 1024 * 1024,
            quickShareSingleFileBytes: 8L * 1024 * 1024 * 1024);

        Assert.Contains(advisories, a => a.Kind == MediaTransferAdvisoryKind.HugeFile
                                      && a.RelPath == "Movies/long.mkv");
    }

    [Fact]
    public async Task CheckpointRoundTripsCompletedKeys()
    {
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, "checkpoint.json");
        var checkpoint = new MediaSyncCheckpoint(
            MediaSyncEvidence.CheckpointSchema,
            "SRC",
            "DST",
            DateTimeOffset.UtcNow,
            new[] { "DCIM|Camera/a.jpg|12|1234" });

        await MediaSyncCheckpointStore.SaveAsync(path, checkpoint);
        var loaded = await MediaSyncCheckpointStore.LoadOrCreateAsync(path, "SRC", "DST");

        Assert.Equal(checkpoint.CompletedKeys, loaded.CompletedKeys);
    }

    private static MediaPlan Plan(params (string RelPath, long SizeBytes)[] files)
    {
        var entries = files.Select((f, i) => new MediaDiffEntry(
            f.RelPath,
            MediaDiffOutcome.NewOnSource,
            new MediaFile { RelPath = f.RelPath, SizeBytes = f.SizeBytes, Mtime = 1_700_000_000 + i },
            null)).ToArray();

        return new MediaPlan(
            "SRC",
            "DST",
            new[] { new MediaCategoryDiff(MediaCategory.Dcim, entries) });
    }
}
