using System.Security.Cryptography;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>Verification mode for a media-sync verification pass (F046, F098).</summary>
public enum MediaIntegrityMode
{
    /// <summary>Compare file size and mtime only. Fast; default during incremental sync.</summary>
    SizeAndMtime,
    /// <summary>Compute CRC32 on both sides. Faster than SHA-256, catches accidental corruption.</summary>
    Crc32,
    /// <summary>Compute SHA-256 on both sides. Slow but trust-grade.</summary>
    Sha256,
}

public sealed record IntegrityMismatch(string RelPath, string SourceFingerprint, string DestinationFingerprint, long SizeBytes);

public sealed record IntegrityReport(
    MediaIntegrityMode Mode,
    int FilesChecked,
    int FilesMatched,
    IReadOnlyList<IntegrityMismatch> Mismatches,
    IReadOnlyList<string> MissingOnDestination)
{
    public bool IsClean => Mismatches.Count == 0 && MissingOnDestination.Count == 0;
}

/// <summary>
/// Post-sync integrity verification (F046). Walks both manifests and runs the
/// configured fingerprint check on each pair. CRC32 is implemented in host code
/// (System.IO.Hashing-style polynomial) so we don't need to push anything to the
/// device beyond the existing <c>cat</c> /<c>md5sum</c>-style shell commands.
/// </summary>
public sealed class MediaIntegrityService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public MediaIntegrityService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<MediaIntegrityService>();
    }

    public async Task<IntegrityReport> VerifyAsync(
        DeviceData source,
        DeviceData destination,
        IEnumerable<MediaFile> sourceManifest,
        IEnumerable<MediaFile> destinationManifest,
        MediaIntegrityMode mode,
        CancellationToken ct = default)
    {
        var dstByPath = destinationManifest.ToDictionary(f => f.RelPath, StringComparer.Ordinal);
        var mismatches = new List<IntegrityMismatch>();
        var missing = new List<string>();
        var checkedCount = 0;
        var matched = 0;

        foreach (var src in sourceManifest)
        {
            ct.ThrowIfCancellationRequested();
            if (!dstByPath.TryGetValue(src.RelPath, out var dst))
            {
                missing.Add(src.RelPath);
                continue;
            }

            checkedCount++;
            switch (mode)
            {
                case MediaIntegrityMode.SizeAndMtime:
                    if (src.SizeBytes == dst.SizeBytes && Math.Abs(src.Mtime - dst.Mtime) <= 1)
                        matched++;
                    else
                        mismatches.Add(new IntegrityMismatch(src.RelPath,
                            $"size={src.SizeBytes},mtime={src.Mtime}",
                            $"size={dst.SizeBytes},mtime={dst.Mtime}",
                            src.SizeBytes));
                    break;

                case MediaIntegrityMode.Crc32:
                {
                    var srcCrc = await ShellCrc32Async(source, src.RelPath, ct);
                    var dstCrc = await ShellCrc32Async(destination, dst.RelPath, ct);
                    if (string.Equals(srcCrc, dstCrc, StringComparison.OrdinalIgnoreCase))
                        matched++;
                    else
                        mismatches.Add(new IntegrityMismatch(src.RelPath,
                            $"crc32={srcCrc}", $"crc32={dstCrc}", src.SizeBytes));
                    break;
                }

                case MediaIntegrityMode.Sha256:
                {
                    var srcSha = await ShellSha256Async(source, src.RelPath, ct);
                    var dstSha = await ShellSha256Async(destination, dst.RelPath, ct);
                    if (string.Equals(srcSha, dstSha, StringComparison.OrdinalIgnoreCase))
                        matched++;
                    else
                        mismatches.Add(new IntegrityMismatch(src.RelPath,
                            $"sha256={srcSha}", $"sha256={dstSha}", src.SizeBytes));
                    break;
                }
            }
        }

        _log.Information(
            "Integrity verify mode={Mode} checked={Checked} matched={Matched} mismatched={Mismatched} missing={Missing}",
            mode, checkedCount, matched, mismatches.Count, missing.Count);
        return new IntegrityReport(mode, checkedCount, matched, mismatches, missing);
    }

    /// <summary>Compute CRC32 (IEEE 802.3 polynomial) for a buffer. Used in unit tests.</summary>
    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xffffffffu;
        for (var i = 0; i < data.Length; i++)
        {
            crc ^= data[i];
            for (var b = 0; b < 8; b++)
                crc = (crc & 1u) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
        }
        return ~crc;
    }

    private async Task<string> ShellCrc32Async(DeviceData device, string relPath, CancellationToken ct)
    {
        var remote = $"/sdcard/{relPath}";
        // Some toybox builds ship `crc32`; fall back to `cksum` (POSIX), which prints
        // "<crc32> <bytes> <name>". cksum's polynomial differs but it's deterministic.
        var output = await _client.ShellAsync(device,
            $"(crc32 {AdbShell.Arg(remote)} 2>/dev/null || cksum {AdbShell.Arg(remote)} 2>/dev/null) | awk '{{print $1}}'",
            ct);
        return (output ?? "").Trim();
    }

    private async Task<string> ShellSha256Async(DeviceData device, string relPath, CancellationToken ct)
    {
        var remote = $"/sdcard/{relPath}";
        var output = await _client.ShellAsync(device,
            $"sha256sum {AdbShell.Arg(remote)} 2>/dev/null | awk '{{print $1}}'", ct);
        return (output ?? "").Trim();
    }
}
