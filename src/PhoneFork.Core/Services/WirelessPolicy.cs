using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Outcome of an attempted wireless-ADB operation against the trust policy.
/// </summary>
public enum WirelessDecision
{
    Allowed,
    BlockedByPatchLevel,
    BlockedByPolicy,
}

public sealed record WirelessDecisionResult(WirelessDecision Decision, string Reason)
{
    public bool IsAllowed => Decision == WirelessDecision.Allowed;
}

/// <summary>
/// Trust-posture gate for wireless ADB (F001, F003, F007, F105). Default policy is
/// "USB first; wireless only if the user has explicitly opted in AND the destination
/// device's Android security patch level is &gt;= 2026-05-01 (the CVE-2026-0073 fix)".
/// Public PoC code exists for CVE-2026-0073, so unpatched wireless is treated as
/// hostile by default and only an explicit user override can unblock it.
/// </summary>
public sealed class WirelessPolicy
{
    private readonly ILogger _log;
    private readonly object _sync = new();
    private bool _wirelessOptedIn;
    private DateTimeOffset _sessionStartedAt;
    private TimeSpan _sessionWindow = TimeSpan.FromMinutes(30);
    private bool _allowUnpatchedOverride;

    public WirelessPolicy(ILogger log)
    {
        _log = log.ForContext<WirelessPolicy>();
    }

    /// <summary>Whether the user has explicitly turned on wireless mode for this app session.</summary>
    public bool WirelessOptedIn
    {
        get { lock (_sync) return _wirelessOptedIn; }
    }

    /// <summary>
    /// When the active wireless session started. <see cref="SessionExpiresAt"/> elapses
    /// <see cref="SessionWindow"/> later.
    /// </summary>
    public DateTimeOffset SessionStartedAt
    {
        get { lock (_sync) return _sessionStartedAt; }
    }

    public TimeSpan SessionWindow
    {
        get { lock (_sync) return _sessionWindow; }
        set { lock (_sync) _sessionWindow = value <= TimeSpan.Zero ? TimeSpan.FromMinutes(30) : value; }
    }

    public DateTimeOffset SessionExpiresAt
    {
        get { lock (_sync) return _sessionStartedAt + _sessionWindow; }
    }

    /// <summary>True iff the optional override that lets unpatched devices pair is on (default off).</summary>
    public bool AllowUnpatchedOverride
    {
        get { lock (_sync) return _allowUnpatchedOverride; }
        set
        {
            lock (_sync) _allowUnpatchedOverride = value;
            _log.Warning("Wireless ADB unpatched-override set to {Value}. CVE-2026-0073 PoC is public.", value);
        }
    }

    public void OptInWireless(TimeSpan? window = null)
    {
        lock (_sync)
        {
            _wirelessOptedIn = true;
            _sessionStartedAt = DateTimeOffset.UtcNow;
            if (window is { } w && w > TimeSpan.Zero) _sessionWindow = w;
        }
        _log.Information("Wireless ADB session opened (window={Window}).", _sessionWindow);
    }

    public void KillWireless()
    {
        lock (_sync)
        {
            _wirelessOptedIn = false;
            _sessionStartedAt = default;
        }
        _log.Information("Wireless ADB session closed (kill switch).");
    }

    /// <summary>True iff the opted-in wireless session has elapsed past its timeout.</summary>
    public bool IsSessionExpired()
    {
        lock (_sync)
        {
            if (!_wirelessOptedIn) return false;
            return DateTimeOffset.UtcNow >= _sessionStartedAt + _sessionWindow;
        }
    }

    /// <summary>
    /// Decide whether an operation against a device should be allowed under the current policy.
    /// </summary>
    public WirelessDecisionResult Evaluate(SecurityPosture posture)
    {
        if (posture.Transport != AdbTransport.Tcp)
            return new WirelessDecisionResult(WirelessDecision.Allowed, "USB transport.");

        lock (_sync)
        {
            if (!_wirelessOptedIn)
                return new WirelessDecisionResult(
                    WirelessDecision.BlockedByPolicy,
                    "USB-first policy: enable Wireless mode in the device bar before pairing or connecting.");

            if (IsSessionExpired())
                return new WirelessDecisionResult(
                    WirelessDecision.BlockedByPolicy,
                    $"Wireless session expired at {SessionExpiresAt:yyyy-MM-dd HH:mm} UTC. Re-enable Wireless mode.");

            if (posture.PatchStatus == PatchLevelStatus.BelowCveFix && !_allowUnpatchedOverride)
                return new WirelessDecisionResult(
                    WirelessDecision.BlockedByPatchLevel,
                    $"Device patch level {posture.PatchDate:yyyy-MM-dd} is below {SecurityPosture.CveFixPatchLevel:yyyy-MM-dd}. "
                  + "CVE-2026-0073 is a critical zero-click RCE in wireless adbd with working PoC code published. "
                  + "Update the destination phone, switch to USB, or set 'Allow unpatched wireless' explicitly.");

            if (posture.PatchStatus == PatchLevelStatus.Unknown && !_allowUnpatchedOverride)
                return new WirelessDecisionResult(
                    WirelessDecision.BlockedByPatchLevel,
                    "Could not read this phone's Android security patch level. Refusing wireless ADB by default. "
                  + "Connect over USB, verify patch level >= 2026-05-01, or set 'Allow unpatched wireless' explicitly.");
        }

        return new WirelessDecisionResult(WirelessDecision.Allowed, "Wireless ADB session active and device patched.");
    }

    /// <summary>
    /// Convenience: given a raw host:port, build a synthetic posture with Unknown patch level
    /// and evaluate. Used when pairing before the device shows up on the bus.
    /// </summary>
    public WirelessDecisionResult EvaluateHostPort(string hostPort) =>
        Evaluate(new SecurityPosture(
            Serial: hostPort,
            Transport: AdbTransport.Tcp,
            PatchDate: null,
            PatchStatus: PatchLevelStatus.Unknown,
            IsKnownVulnerable: false));
}
