using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Whether Android's Advanced Protection mode is on for a device (F114).
/// Unknown is a first-class state: the probe never reports Off just because it could not read.
/// </summary>
public enum AdvancedProtectionState
{
    /// <summary>The device did not answer, or answered in a shape this build does not recognise.</summary>
    Unknown,
    Off,
    On,
}

/// <summary>
/// Advanced Protection posture for one device, plus the consequences for PhoneFork (F114).
/// </summary>
public sealed record AdvancedProtectionReport(
    AdvancedProtectionState State,
    string Evidence)
{
    /// <summary>Operations that stop working while Advanced Protection is on.</summary>
    public static readonly IReadOnlyList<string> AffectedOperations = new[]
    {
        "App migration: package installs can be refused (the sideloading permission is blocked).",
        "Helper APK install: the same install path is used, so helper-assisted reads become unavailable.",
        "USB transfers while the screen is locked: USB data signalling is disabled until the device is unlocked.",
        "Wireless debugging: Developer options can be disabled entirely, which removes the pairing surface.",
    };

    public HonestyFinding ToFinding(string role) => State switch
    {
        AdvancedProtectionState.On => new HonestyFinding(
            Id: $"advanced-protection-{role}",
            Title: $"Advanced Protection is ON for the {role} phone",
            Detail: "Android Advanced Protection blocks the sideloading permission, disables USB data while the device is locked, "
                    + "and can disable Developer options. Turn it off in Settings > Security and privacy > Advanced Protection "
                    + "for the duration of the migration, then turn it back on. Affected: "
                    + string.Join(" ", AffectedOperations),
            Level: HonestyLevel.Blocker),

        AdvancedProtectionState.Off => new HonestyFinding(
            Id: $"advanced-protection-{role}",
            Title: $"Advanced Protection is off for the {role} phone",
            Detail: "No Advanced Protection restrictions apply to package installs or USB data.",
            Level: HonestyLevel.Info),

        _ => new HonestyFinding(
            Id: $"advanced-protection-{role}",
            Title: $"Advanced Protection state is unknown for the {role} phone",
            Detail: "PhoneFork could not read the Advanced Protection setting on this device, so it cannot rule the "
                    + "restrictions out. If package installs fail with INSTALL_FAILED_USER_RESTRICTED, check "
                    + "Settings > Security and privacy > Advanced Protection. Evidence: " + Evidence,
            Level: HonestyLevel.Warning),
    };
}

/// <summary>
/// Probes Android's Advanced Protection mode (F114).
///
/// Advanced Protection landed in Android 16 and blocks the sideloading permission, disables USB
/// data signalling while the device is locked, and is rolling out an option that disables
/// Developer options outright. Any of those breaks PhoneFork, so pre-flight names it rather than
/// letting the user hit an opaque ADB failure.
///
/// The setting is not part of a documented stable API surface, so this reads several candidate
/// keys and reports <see cref="AdvancedProtectionState.Unknown"/> when none of them answer.
/// </summary>
public sealed class AdvancedProtectionService
{
    /// <summary>
    /// Global settings keys observed to carry the Advanced Protection flag. Read in order;
    /// the first key that returns a recognised value wins.
    /// </summary>
    public static readonly IReadOnlyList<string> CandidateKeys = new[]
    {
        "aapm_activated",
        "advanced_protection_mode",
    };

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public AdvancedProtectionService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<AdvancedProtectionService>();
    }

    public async Task<AdvancedProtectionReport> ProbeAsync(DeviceData device, CancellationToken ct = default)
    {
        var attempts = new List<string>(CandidateKeys.Count);

        foreach (var key in CandidateKeys)
        {
            ct.ThrowIfCancellationRequested();
            string raw;
            try
            {
                raw = (await _client.ShellAsync(device, $"settings get global {AdbShell.Arg(key)}", ct) ?? "").Trim();
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Advanced Protection probe failed reading {Key}", key);
                attempts.Add($"{key}=<error>");
                continue;
            }

            attempts.Add($"{key}={(raw.Length == 0 ? "<empty>" : raw)}");
            var state = Interpret(raw);
            if (state != AdvancedProtectionState.Unknown)
                return new AdvancedProtectionReport(state, string.Join(" ", attempts));
        }

        return new AdvancedProtectionReport(AdvancedProtectionState.Unknown, string.Join(" ", attempts));
    }

    /// <summary>
    /// Maps one <c>settings get global</c> response onto a state. "null", an empty string and
    /// anything unrecognised are Unknown, never Off: an absent key does not prove the feature
    /// is disabled, only that this key is not where the device stores it.
    /// </summary>
    public static AdvancedProtectionState Interpret(string? raw)
    {
        // Device shells vary in casing, so normalise before matching.
        var value = raw?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value)) return AdvancedProtectionState.Unknown;
        if (value == "null") return AdvancedProtectionState.Unknown;

        return value switch
        {
            "1" or "true" => AdvancedProtectionState.On,
            "0" or "false" => AdvancedProtectionState.Off,
            _ => AdvancedProtectionState.Unknown,
        };
    }
}
