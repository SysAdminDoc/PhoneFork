using System.Security.Cryptography;
using System.Text.Json;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>Result of reading one backup directory.</summary>
public sealed record PhoneForkBackupHandle(
    string Directory,
    PhoneForkBackupMeta Meta,
    IReadOnlyDictionary<string, string> ChecksumsByFileName)
{
    public DateTimeOffset BackupTime => DateTimeOffset.FromUnixTimeMilliseconds(Meta.BackupTimeMs);

    public IReadOnlyList<string> ResolveApkPaths() =>
        Meta.Apks.Select(a => Path.Combine(Directory, a.FileName)).ToArray();
}

/// <summary>
/// Reads PhoneFork backup directories (F030). Validates the on-disk
/// SHA-256 checksums against the checksums.txt manifest before returning a handle.
/// </summary>
public sealed class PhoneForkBackupReader
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    private readonly ILogger _log;

    public PhoneForkBackupReader(ILogger log)
    {
        _log = log.ForContext<PhoneForkBackupReader>();
    }

    /// <summary>
    /// Load a single backup directory and verify checksums.
    /// </summary>
    public async Task<PhoneForkBackupHandle> ReadAsync(string backupDir, CancellationToken ct = default)
    {
        if (!Directory.Exists(backupDir))
            throw new DirectoryNotFoundException(backupDir);

        var metaPath = ResolveMetaPath(backupDir)
            ?? throw new FileNotFoundException(
                $"{PhoneForkBackupMeta.FileName} missing",
                Path.Combine(backupDir, PhoneForkBackupMeta.FileName));

        await using var metaStream = File.OpenRead(metaPath);
        var meta = await JsonSerializer.DeserializeAsync<PhoneForkBackupMeta>(metaStream, JsonOpts, ct)
            ?? throw new InvalidOperationException($"{Path.GetFileName(metaPath)} in {backupDir} did not deserialize.");

        var checksums = await LoadChecksumsAsync(Path.Combine(backupDir, "checksums.txt"), ct);
        await VerifyChecksumsAsync(backupDir, checksums, ct);

        return new PhoneForkBackupHandle(backupDir, meta, checksums);
    }

    /// <summary>
    /// Metadata file for a backup directory, preferring the current name and falling back to the
    /// pre-v0.9.4-pre <c>meta.am.v5</c> so older local backups keep loading. Null when neither
    /// is present.
    /// </summary>
    public static string? ResolveMetaPath(string backupDir)
    {
        foreach (var name in new[] { PhoneForkBackupMeta.FileName, PhoneForkBackupMeta.LegacyFileName })
        {
            var candidate = Path.Combine(backupDir, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Enumerate every backup directory beneath <paramref name="root"/> without verifying
    /// checksums, matching both the current and legacy metadata file names. Cheap; useful for
    /// retention sweeps.
    /// </summary>
    public IReadOnlyList<string> EnumerateBackupDirs(string root)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        return new[] { PhoneForkBackupMeta.FileName, PhoneForkBackupMeta.LegacyFileName }
            .SelectMany(name => Directory.EnumerateFiles(root, name, SearchOption.AllDirectories))
            .Select(Path.GetDirectoryName)
            .Where(d => d is not null)
            .Select(d => d!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, string>> LoadChecksumsAsync(string path, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return map;

        foreach (var rawLine in await File.ReadAllLinesAsync(path, ct))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            // checksums.txt format: "<sha256>  <filename>" (two spaces, AppManager-format).
            var space = line.IndexOf(' ');
            if (space <= 0) continue;
            var sha = line[..space].Trim();
            var name = line[space..].Trim();
            if (sha.Length == 64 && name.Length > 0)
                map[name] = sha.ToLowerInvariant();
        }
        return map;
    }

    private async Task VerifyChecksumsAsync(string dir, IReadOnlyDictionary<string, string> expected, CancellationToken ct)
    {
        foreach (var (name, expectedSha) in expected)
        {
            ct.ThrowIfCancellationRequested();
            var path = Path.Combine(dir, name);
            if (!File.Exists(path))
                throw new InvalidDataException($"Backup missing referenced file {name} in {dir}");
            var actual = await Sha256OfFileAsync(path, ct);
            if (!string.Equals(actual, expectedSha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Checksum mismatch on {name}: expected {expectedSha}, got {actual}");
        }
    }

    private static async Task<string> Sha256OfFileAsync(string path, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var stream = File.OpenRead(path);
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
