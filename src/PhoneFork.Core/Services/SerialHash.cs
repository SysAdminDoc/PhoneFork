using System.Security.Cryptography;
using System.Text;

namespace PhoneFork.Core.Services;

/// <summary>
/// One-way short hash for ADB serial numbers (F006). Audit logs and the trusted-pair
/// registry write hashes instead of raw serials so an exported log is share-safe
/// (no hardware identifiers leaving the host). Hashes are 12 hex chars of SHA-256
/// over the serial — enough for cross-row correlation, not enough to brute the device.
/// </summary>
public static class SerialHash
{
    /// <summary>
    /// Returns a 12-hex-char SHA-256 prefix of <paramref name="serial"/>, or "" for empty input.
    /// Deterministic across runs on the same host (no salt) so audit rows are correlatable
    /// from one job to the next.
    /// </summary>
    public static string Of(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return "";
        var bytes = Encoding.UTF8.GetBytes(serial.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
    }
}
