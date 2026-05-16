namespace PhoneFork.Core.Models;

/// <summary>
/// Transport class for a connected device. Wireless ADB carries CVE-2026-0073 (zero-click
/// RCE in adbd on patch levels prior to 2026-05-01) so the App enforces a USB-first policy
/// (F003) and refuses wireless pairing on unpatched devices (F001, F105).
/// </summary>
public enum AdbTransport
{
    Unknown,
    Usb,
    Tcp,
}

/// <summary>
/// Android security-patch-level posture relative to the May 2026 bulletin (CVE-2026-0073).
/// </summary>
public enum PatchLevelStatus
{
    Unknown,
    /// <summary>Patch level &lt; 2026-05-01. Wireless ADB MUST be refused unless the user explicitly overrides.</summary>
    BelowCveFix,
    /// <summary>Patch level &gt;= 2026-05-01. Wireless ADB is allowed.</summary>
    MeetsCveFix,
}

/// <summary>
/// Per-device security/trust posture derived from <c>ro.build.version.security_patch</c>
/// (or sibling Samsung properties) and the active transport. Hydrated by
/// <see cref="PhoneFork.Core.Services.SecurityPostureService"/>.
/// </summary>
public sealed record SecurityPosture(
    string Serial,
    AdbTransport Transport,
    DateOnly? PatchDate,
    PatchLevelStatus PatchStatus,
    bool IsKnownVulnerable)
{
    /// <summary>The first Android security-patch level that addresses CVE-2026-0073.</summary>
    public static readonly DateOnly CveFixPatchLevel = new(2026, 5, 1);

    public bool IsWireless => Transport == AdbTransport.Tcp;

    /// <summary>True iff wireless ADB on this device must be refused by default policy.</summary>
    public bool BlockWirelessByDefault => IsWireless && PatchStatus == PatchLevelStatus.BelowCveFix;

    /// <summary>Short human-readable line for the device card.</summary>
    public string SummaryLine() =>
        (Transport, PatchStatus) switch
        {
            (AdbTransport.Usb, PatchLevelStatus.MeetsCveFix) => "USB · patch level current",
            (AdbTransport.Usb, PatchLevelStatus.BelowCveFix) => $"USB · patch level {Format(PatchDate)} (CVE-2026-0073 unpatched, USB is still safe)",
            (AdbTransport.Tcp, PatchLevelStatus.MeetsCveFix) => "Wireless · patch level current",
            (AdbTransport.Tcp, PatchLevelStatus.BelowCveFix) => $"Wireless · BLOCKED: patch level {Format(PatchDate)} – CVE-2026-0073 PoC exploit is public",
            (AdbTransport.Usb, _) => "USB",
            (AdbTransport.Tcp, _) => "Wireless · patch level unknown",
            _ => "Transport unknown",
        };

    private static string Format(DateOnly? d) => d?.ToString("yyyy-MM-dd") ?? "unknown";
}
