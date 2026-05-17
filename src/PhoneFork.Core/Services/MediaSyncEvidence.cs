using System.Text.Json;
using PhoneFork.Core.Models;

namespace PhoneFork.Core.Services;

public enum MediaSyncEvidenceStatus
{
    SkippedIdentical,
    SkippedCheckpoint,
    DryRun,
    Transferred,
    Deleted,
    ConflictRenamed,
    Failed,
    DeferredQuickShare,
}

public enum MediaTransferAdvisoryKind
{
    HugeFile,
    QuickShareBetterSingleFile,
}

public sealed record MediaTransferAdvisory(
    MediaTransferAdvisoryKind Kind,
    MediaCategory Category,
    string RelPath,
    long SizeBytes,
    string Detail);

public sealed record MediaSyncEvidenceEntry(
    MediaCategory Category,
    string RelPath,
    MediaDiffOutcome Outcome,
    MediaSyncEvidenceStatus Status,
    long SizeBytes,
    int Attempts,
    long DurationMs,
    double ThroughputBytesPerSecond,
    string? Error = null);

public sealed record MediaSyncEvidenceReport(
    string Schema,
    string SourceSerial,
    string DestinationSerial,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<MediaSyncEvidenceEntry> Entries,
    IReadOnlyList<MediaTransferAdvisory> Advisories);

public sealed record MediaSyncCheckpoint(
    string Schema,
    string SourceSerial,
    string DestinationSerial,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> CompletedKeys);

public static class MediaSyncEvidence
{
    public const string ReportSchema = "phonefork.media-sync-report.v1";
    public const string CheckpointSchema = "phonefork.media-sync-checkpoint.v1";
    public const long DefaultHugeFileWarningBytes = 4L * 1024 * 1024 * 1024;
    public const long DefaultQuickShareSingleFileBytes = 512L * 1024 * 1024;

    public static IReadOnlyList<MediaTransferAdvisory> BuildAdvisories(
        MediaPlan plan,
        long hugeFileWarningBytes = DefaultHugeFileWarningBytes,
        long quickShareSingleFileBytes = DefaultQuickShareSingleFileBytes)
    {
        var transfers = TransferEntries(plan).ToArray();
        var advisories = new List<MediaTransferAdvisory>();

        foreach (var (category, entry) in transfers)
        {
            var size = entry.Source?.SizeBytes ?? 0;
            if (size >= hugeFileWarningBytes)
            {
                advisories.Add(new MediaTransferAdvisory(
                    MediaTransferAdvisoryKind.HugeFile,
                    category,
                    entry.RelPath,
                    size,
                    "Large media file. Keep the USB cable stable and expect this transfer to dominate ETA."));
            }
        }

        if (transfers.Length == 1)
        {
            var (category, entry) = transfers[0];
            var size = entry.Source?.SizeBytes ?? 0;
            if (size >= quickShareSingleFileBytes)
            {
                advisories.Add(new MediaTransferAdvisory(
                    MediaTransferAdvisoryKind.QuickShareBetterSingleFile,
                    category,
                    entry.RelPath,
                    size,
                    "Single large ad hoc file. Quick Share may be faster and more ergonomic than a full ADB sync run."));
            }
        }

        return advisories;
    }

    public static IEnumerable<(MediaCategory Category, MediaDiffEntry Entry)> TransferEntries(MediaPlan plan) =>
        plan.CategoryDiffs.SelectMany(cd => cd.Entries
            .Where(e => e.Outcome is MediaDiffOutcome.NewOnSource or MediaDiffOutcome.Conflict)
            .Select(e => (cd.Category, e)));

    public static string CheckpointKey(MediaCategory category, MediaDiffEntry entry)
    {
        var source = entry.Source;
        return $"{category}|{entry.RelPath}|{source?.SizeBytes ?? 0}|{source?.Mtime ?? 0}";
    }
}

public static class MediaSyncCheckpointStore
{
    public static async Task<MediaSyncCheckpoint> LoadOrCreateAsync(
        string path,
        string sourceSerial,
        string destinationSerial,
        CancellationToken ct = default)
    {
        if (File.Exists(path))
        {
            await using var read = File.OpenRead(path);
            var existing = await JsonSerializer.DeserializeAsync<MediaSyncCheckpoint>(read, MediaJson.Options, ct);
            if (existing is not null
                && existing.Schema == MediaSyncEvidence.CheckpointSchema
                && existing.SourceSerial == sourceSerial
                && existing.DestinationSerial == destinationSerial)
            {
                return existing;
            }
        }

        return new MediaSyncCheckpoint(
            MediaSyncEvidence.CheckpointSchema,
            sourceSerial,
            destinationSerial,
            DateTimeOffset.UtcNow,
            Array.Empty<string>());
    }

    public static async Task SaveAsync(string path, MediaSyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        var updated = checkpoint with { UpdatedAt = DateTimeOffset.UtcNow };
        await using var write = File.Create(path);
        await JsonSerializer.SerializeAsync(write, updated, MediaJson.Options, ct);
    }
}
