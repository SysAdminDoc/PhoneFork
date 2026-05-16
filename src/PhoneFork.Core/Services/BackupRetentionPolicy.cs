using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Backup retention rules per-package (F033, F034). Pruning runs locally after each
/// backup write or as a periodic sweep. All three limits compose with logical AND:
/// a backup must satisfy every active rule to be kept.
/// </summary>
public sealed record RetentionPolicy(
    int? KeepMostRecentCount = null,
    TimeSpan? KeepWithin = null,
    long? KeepUnderTotalBytes = null)
{
    /// <summary>Sensible defaults: keep 5 most recent, no time limit, no size limit.</summary>
    public static RetentionPolicy Default { get; } = new(KeepMostRecentCount: 5);

    public bool HasAnyLimit =>
        KeepMostRecentCount is > 0
        || KeepWithin is { Ticks: > 0 }
        || KeepUnderTotalBytes is > 0;
}

/// <summary>
/// Applies a <see cref="RetentionPolicy"/> across a set of dated backup directories.
/// Order of evaluation: newest first by name (timestamp-derived) or by mtime; the
/// first backup that violates any rule is deleted, sweep restarts.
/// </summary>
public sealed class BackupRetentionSweeper
{
    private readonly ILogger _log;

    public BackupRetentionSweeper(ILogger log)
    {
        _log = log.ForContext<BackupRetentionSweeper>();
    }

    /// <summary>
    /// Returns the directories that <em>would</em> be deleted under the policy.
    /// Idempotent and read-only — call <see cref="Apply"/> to actually delete.
    /// </summary>
    public IReadOnlyList<string> Plan(IEnumerable<string> backupDirs, RetentionPolicy policy)
    {
        if (!policy.HasAnyLimit) return Array.Empty<string>();

        var ordered = backupDirs
            .Where(Directory.Exists)
            .Select(d => new BackupCandidate(d, Directory.GetCreationTimeUtc(d), MeasureSize(d)))
            .OrderByDescending(c => c.Created)
            .ToArray();

        var keep = new List<BackupCandidate>(ordered.Length);
        var drop = new List<string>();
        long cumulative = 0;
        var cutoff = policy.KeepWithin is { } window
            ? DateTime.UtcNow - window
            : (DateTime?)null;

        foreach (var c in ordered)
        {
            if (policy.KeepMostRecentCount is { } maxCount && keep.Count >= maxCount)
            {
                drop.Add(c.Directory);
                continue;
            }
            if (cutoff is { } co && c.Created < co)
            {
                drop.Add(c.Directory);
                continue;
            }
            if (policy.KeepUnderTotalBytes is { } maxBytes && cumulative + c.SizeBytes > maxBytes)
            {
                drop.Add(c.Directory);
                continue;
            }
            cumulative += c.SizeBytes;
            keep.Add(c);
        }

        _log.Debug("Retention plan: keep={Keep} drop={Drop} totalBytes={Bytes}",
            keep.Count, drop.Count, cumulative);
        return drop;
    }

    /// <summary>
    /// Plan + delete. Returns the directories actually removed.
    /// </summary>
    public IReadOnlyList<string> Apply(IEnumerable<string> backupDirs, RetentionPolicy policy)
    {
        var dropped = new List<string>();
        foreach (var dir in Plan(backupDirs, policy))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                dropped.Add(dir);
                _log.Information("Retention sweep removed {Dir}", dir);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Retention sweep failed to remove {Dir}", dir);
            }
        }
        return dropped;
    }

    private static long MeasureSize(string dir)
    {
        try
        {
            return new DirectoryInfo(dir)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    private sealed record BackupCandidate(string Directory, DateTime Created, long SizeBytes);
}
