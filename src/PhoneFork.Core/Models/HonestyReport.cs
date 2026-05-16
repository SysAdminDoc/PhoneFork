namespace PhoneFork.Core.Models;

/// <summary>
/// Severity of a single honesty-report finding. The UI uses this to render
/// the badge colour and decide whether migration can proceed unattended.
/// </summary>
public enum HonestyLevel
{
    /// <summary>Informational: data is migratable but the user should know about a quirk.</summary>
    Info,
    /// <summary>Warning: data will not transfer unaided; user must run an external flow.</summary>
    Warning,
    /// <summary>Blocker: data is permanently inaccessible (Knox, hardware-bound keystore, etc.).</summary>
    Blocker,
}

/// <summary>One pre-flight honesty finding (F037, F038, F039, F040, F108).</summary>
public sealed record HonestyFinding(
    string Id,
    string Title,
    string Detail,
    HonestyLevel Level,
    string? PackageId = null,
    string? ActionUrl = null);

/// <summary>
/// Aggregated honesty report. Caller iterates <see cref="Findings"/> for display
/// and consults <see cref="HasBlockers"/> before marking a destination wipe-ready.
/// </summary>
public sealed record HonestyReport(IReadOnlyList<HonestyFinding> Findings)
{
    public bool HasBlockers => Findings.Any(f => f.Level == HonestyLevel.Blocker);
    public int WarningCount => Findings.Count(f => f.Level == HonestyLevel.Warning);
    public int BlockerCount => Findings.Count(f => f.Level == HonestyLevel.Blocker);
    public int InfoCount => Findings.Count(f => f.Level == HonestyLevel.Info);
}
