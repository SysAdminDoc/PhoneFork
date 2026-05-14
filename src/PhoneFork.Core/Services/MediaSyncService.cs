using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record MediaSyncOptions
{
    /// <summary>If true, files that exist only on destination are deleted to mirror source exactly.</summary>
    public bool Delete { get; init; }
    /// <summary>If true, on conflict only overwrite when source mtime &gt; destination mtime (rsync <c>--update</c>).</summary>
    public bool UpdateOnly { get; init; }
    /// <summary>If true, conflicting destination files are renamed instead of overwritten (Syncthing pattern).</summary>
    public bool PreserveConflicts { get; init; }
    /// <summary>If true, no writes happen — manifest + plan emitted only.</summary>
    public bool DryRun { get; init; }
    /// <summary>Local stage directory for the pull leg. Defaults to <c>%LOCALAPPDATA%\PhoneFork\stage\&lt;sourceSerial&gt;\</c>.</summary>
    public string? StageDir { get; init; }
}

public sealed record MediaSyncProgress(
    string CurrentRelPath,
    long FilesDone,
    long FilesTotal,
    long BytesDone,
    long BytesTotal);

public sealed record MediaSyncResult(
    int FilesPulled,
    int FilesPushed,
    int FilesSkipped,
    int FilesDeleted,
    int FilesRenamedAsConflict,
    int Errors,
    TimeSpan Elapsed);

/// <summary>
/// Two-leg sync: pull source files to a local stage dir, then push the stage to the destination.
/// Honors <see cref="MediaSyncOptions"/> flags. Re-running with the same source+dest manifest is
/// effectively idempotent — identical files are skipped.
/// </summary>
public sealed class MediaSyncService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public MediaSyncService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<MediaSyncService>();
    }

    public async Task<MediaSyncResult> ApplyAsync(
        DeviceData source,
        DeviceData dest,
        MediaPlan plan,
        MediaSyncOptions options,
        IProgress<MediaSyncProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int pulled = 0, pushed = 0, skipped = 0, deleted = 0, renamed = 0, errors = 0;

        var stageRoot = options.StageDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhoneFork", "stage", source.Serial);
        Directory.CreateDirectory(stageRoot);

        // Pre-compute totals for progress reporting.
        var transferEntries = plan.CategoryDiffs
            .SelectMany(cd => cd.Entries
                .Where(e => e.Outcome is MediaDiffOutcome.NewOnSource or MediaDiffOutcome.Conflict)
                .Select(e => (cd.Category, Entry: e)))
            .ToList();
        long totalFiles = transferEntries.Count;
        long totalBytes = transferEntries.Sum(t => t.Entry.Source?.SizeBytes ?? 0);
        long filesDone = 0, bytesDone = 0;

        foreach (var catDiff in plan.CategoryDiffs)
        {
            ct.ThrowIfCancellationRequested();
            var remoteRoot = catDiff.Category.RemotePath();
            var stageCatDir = Path.Combine(stageRoot, catDiff.Category.ToString());

            // --update gate: only treat a conflict as "transfer" when source is strictly newer.
            // Otherwise treat as Identical-equivalent and skip.
            foreach (var entry in catDiff.Entries)
            {
                ct.ThrowIfCancellationRequested();

                switch (entry.Outcome)
                {
                    case MediaDiffOutcome.Identical:
                        skipped++;
                        continue;

                    case MediaDiffOutcome.NewOnDest:
                        if (options.Delete && !options.DryRun)
                        {
                            try
                            {
                                var p = $"{remoteRoot}/{entry.RelPath}";
                                await _client.ShellAsync(dest, $"rm -f \"{p}\"", ct);
                                deleted++;
                            }
                            catch (Exception ex)
                            {
                                errors++;
                                _log.Warning(ex, "Delete failed {Path}", entry.RelPath);
                            }
                        }
                        continue;

                    case MediaDiffOutcome.NewOnSource:
                    case MediaDiffOutcome.Conflict:
                        if (entry.Outcome == MediaDiffOutcome.Conflict
                            && options.UpdateOnly
                            && (entry.Source?.Mtime ?? 0) <= (entry.Dest?.Mtime ?? 0))
                        {
                            skipped++;
                            continue;
                        }

                        var srcRemote = $"{remoteRoot}/{entry.RelPath}";
                        var local = Path.Combine(stageCatDir, entry.RelPath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(local)!);

                        if (options.DryRun)
                        {
                            filesDone++;
                            bytesDone += entry.Source?.SizeBytes ?? 0;
                            progress?.Report(new MediaSyncProgress(entry.RelPath, filesDone, totalFiles, bytesDone, totalBytes));
                            continue;
                        }

                        try
                        {
                            await PullAsync(source, srcRemote, local, ct);
                            pulled++;

                            if (entry.Outcome == MediaDiffOutcome.Conflict && options.PreserveConflicts)
                            {
                                // Sync-conflict filename per Syncthing pattern:  foo.sync-conflict-<ts>-<sha8>.jpg
                                var sha8 = ShortSha(entry.Source?.SizeBytes ?? 0, entry.Source?.Mtime ?? 0);
                                var ts = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
                                var conflictRelPath = AppendBeforeExt(entry.RelPath, $".sync-conflict-{ts}-{sha8}");
                                var conflictRemote = $"{remoteRoot}/{conflictRelPath}";
                                await _client.ShellAsync(dest, $"mv \"{remoteRoot}/{entry.RelPath}\" \"{conflictRemote}\"", ct);
                                renamed++;
                            }

                            // Preserve source mtime so re-running the sync marks the file as Identical, not Conflict.
                            var srcMtimeUtc = entry.Source is { Mtime: var m } && m > 0
                                ? DateTimeOffset.FromUnixTimeSeconds(m)
                                : DateTimeOffset.UtcNow;
                            await PushAsync(dest, local, srcRemote, srcMtimeUtc, ct);
                            pushed++;

                            filesDone++;
                            bytesDone += entry.Source?.SizeBytes ?? 0;
                            progress?.Report(new MediaSyncProgress(entry.RelPath, filesDone, totalFiles, bytesDone, totalBytes));
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            _log.Warning(ex, "Sync entry failed {Path}", entry.RelPath);
                        }
                        continue;
                }
            }
        }

        sw.Stop();
        _log.Information("Media sync done: pulled={Pulled} pushed={Pushed} skipped={Skipped} deleted={Deleted} renamed={Renamed} errors={Errors} in {Ms} ms",
            pulled, pushed, skipped, deleted, renamed, errors, sw.ElapsedMilliseconds);
        return new MediaSyncResult(pulled, pushed, skipped, deleted, renamed, errors, sw.Elapsed);
    }

    private async Task PullAsync(DeviceData device, string remote, string local, CancellationToken ct)
    {
        using var sync = new SyncService(_client, device);
        using var fs = File.Create(local);
        await sync.PullAsync(remote, fs, callback: null, useV2: false, cancellationToken: ct);
    }

    private async Task PushAsync(DeviceData device, string local, string remote, DateTimeOffset mtimeUtc, CancellationToken ct)
    {
        using var sync = new SyncService(_client, device);
        using var fs = File.OpenRead(local);
        // Ensure remote parent exists.
        var parent = remote[..remote.LastIndexOf('/')];
        await _client.ShellAsync(device, $"mkdir -p \"{parent}\"", ct);
        await sync.PushAsync(fs, remote, AdvancedSharpAdbClient.Models.UnixFileStatus.DefaultFileMode, mtimeUtc, callback: null, useV2: false, cancellationToken: ct);
    }

    private static string ShortSha(long size, long mtime)
    {
        // Stable, short, content-independent stamp from manifest fields. Enough to disambiguate
        // simultaneous conflicts on the same file. We don't need cryptographic strength here.
        unchecked
        {
            ulong h = 14695981039346656037UL;
            h ^= (ulong)size; h *= 1099511628211UL;
            h ^= (ulong)mtime; h *= 1099511628211UL;
            return h.ToString("x")[..8];
        }
    }

    private static string AppendBeforeExt(string relPath, string suffix)
    {
        var ext = Path.GetExtension(relPath);
        var withoutExt = string.IsNullOrEmpty(ext) ? relPath : relPath[..^ext.Length];
        return withoutExt + suffix + ext;
    }
}
