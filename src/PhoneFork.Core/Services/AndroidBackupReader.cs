using System.IO.Compression;
using System.Text;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Detection-only reader for the legacy <c>adb backup</c> archive format (.ab, F031).
/// Real extraction follows nelenkov/android-backup-extractor's reference:
/// <list type="bullet">
///   <item>Header magic: "ANDROID BACKUP\n" (15 bytes).</item>
///   <item>Version line (1, 2, 3, 4, 5).</item>
///   <item>Compression flag (0 = none, 1 = zlib).</item>
///   <item>Encryption tag ("none" or "AES-256").</item>
///   <item>(optional) Key-derivation block: 5 hex-encoded lines (salt, chk salt,
///         iterations, user-key blob, master-key blob).</item>
///   <item>Body: optionally zlib-deflated tar.</item>
/// </list>
/// The actual decryption + tar walk is non-trivial and ships as v0.9.1; this class
/// only does header sniffing so the UI can correctly classify an input file.
/// </summary>
public sealed class AndroidBackupReader
{
    public const string ExpectedMagic = "ANDROID BACKUP";

    public sealed record AbHeader(
        int FormatVersion,
        bool Compressed,
        string EncryptionTag,
        bool HasEncryptionKeyBlock);

    private readonly ILogger _log;

    public AndroidBackupReader(ILogger log)
    {
        _log = log.ForContext<AndroidBackupReader>();
    }

    /// <summary>
    /// Return the parsed header if <paramref name="path"/> is an <c>adb backup</c> archive,
    /// null otherwise. Does not attempt decryption or tar walking.
    /// </summary>
    public AbHeader? Sniff(string path)
    {
        if (!File.Exists(path)) return null;

        using var fs = File.OpenRead(path);
        var headerLines = ReadHeaderLines(fs, maxLines: 9, maxBytes: 1024);
        if (headerLines.Count < 4) return null;
        if (!headerLines[0].StartsWith(ExpectedMagic, StringComparison.Ordinal)) return null;
        if (!int.TryParse(headerLines[1], out var version) || version < 1 || version > 5) return null;
        if (!int.TryParse(headerLines[2], out var compressed) || compressed is < 0 or > 1) return null;

        var encryption = headerLines[3].Trim();
        var hasKeyBlock = headerLines.Count >= 9;
        _log.Information("AB header: version={Ver} compressed={Comp} enc={Enc} key={Key}",
            version, compressed == 1, encryption, hasKeyBlock);
        return new AbHeader(version, compressed == 1, encryption, hasKeyBlock);
    }

    /// <summary>True iff <paramref name="path"/> looks like a legacy ADB backup archive.</summary>
    public bool IsAdbBackup(string path) => Sniff(path) is not null;

    private static List<string> ReadHeaderLines(Stream stream, int maxLines, int maxBytes)
    {
        var lines = new List<string>();
        var sb = new StringBuilder();
        var totalBytes = 0;
        while (lines.Count < maxLines && totalBytes < maxBytes)
        {
            var b = stream.ReadByte();
            if (b < 0) break;
            totalBytes++;
            if (b == '\n')
            {
                lines.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append((char)b);
        }
        if (sb.Length > 0) lines.Add(sb.ToString());
        return lines;
    }
}

/// <summary>
/// Detection-only reader for Open Android Backup 7-Zip archives (F032). Open
/// Android Backup ships .7z bundles plus a sidecar metadata file; both are
/// observable on disk before any decryption. Real extraction follows in v0.9.1.
/// </summary>
public sealed class OpenAndroidBackupReader
{
    public sealed record OabHeader(
        string ArchivePath,
        long ArchiveSizeBytes,
        bool HasSidecar);

    private readonly ILogger _log;

    public OpenAndroidBackupReader(ILogger log)
    {
        _log = log.ForContext<OpenAndroidBackupReader>();
    }

    /// <summary>
    /// Inspect a directory for an Open Android Backup-style archive set. Returns the
    /// archive descriptor when one is found, null otherwise.
    /// </summary>
    public OabHeader? Sniff(string directory)
    {
        if (!Directory.Exists(directory)) return null;

        var sevenZips = Directory.EnumerateFiles(directory, "*.7z", SearchOption.TopDirectoryOnly).ToArray();
        if (sevenZips.Length == 0) return null;
        var archive = sevenZips[0];

        // Open Android Backup names sidecar metadata "backup-metadata.json" or
        // "<archive>.txt" — either is enough to confirm.
        var sidecar = File.Exists(Path.Combine(directory, "backup-metadata.json"))
                      || File.Exists(Path.ChangeExtension(archive, ".txt"));

        var len = new FileInfo(archive).Length;
        _log.Information("OAB header: archive={Archive} bytes={Bytes} sidecar={Sidecar}",
            archive, len, sidecar);
        return new OabHeader(archive, len, sidecar);
    }
}
