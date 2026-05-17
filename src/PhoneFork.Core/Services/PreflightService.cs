using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Bundle of every pre-flight signal PhoneFork can offer before the user wipes the
/// source phone (F037). Aggregates the Samsung honesty probe, the CSC/locale diff,
/// the security posture (patch level, transport), Knox/bootloader checks (F044),
/// and the destination's bootloader-unlock posture (One UI 8.5 removed the toggle
/// entirely on S25/S26, so any "I'll just root and use Migrate-OSS later" plan
/// needs the warning before the user wipes).
/// </summary>
public sealed record PreflightReport(
    HonestyReport SamsungHonesty,
    MessageTransitionReport Messages,
    GalleryOneDriveReport GalleryOneDrive,
    SecurityPosture SourcePosture,
    SecurityPosture DestinationPosture,
    CscPosture? Csc,
    bool DestinationOemUnlockAvailable,
    string? KnoxState)
{
    public IEnumerable<HonestyFinding> AllFindings => SamsungHonesty.Findings
        .Concat(Messages.Findings)
        .Concat(GalleryOneDrive.Findings);
    public bool HasBlockers => AllFindings.Any(f => f.Level == HonestyLevel.Blocker);
    public int WarningCount => AllFindings.Count(f => f.Level == HonestyLevel.Warning);
    public int BlockerCount => AllFindings.Count(f => f.Level == HonestyLevel.Blocker);
}

public sealed record CscPosture(
    string SourceCsc,
    string DestinationCsc,
    string SourceCountry,
    string DestinationCountry,
    string SourceLocale,
    string DestinationLocale)
{
    public bool CountryMismatch => !string.Equals(SourceCountry, DestinationCountry, StringComparison.OrdinalIgnoreCase);
    public bool LocaleMismatch => !string.Equals(SourceLocale, DestinationLocale, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Runs every pre-flight probe in parallel and returns a single
/// <see cref="PreflightReport"/> for the UI / CLI to render.
/// </summary>
public sealed class PreflightService
{
    private readonly IAdbClient _client;
    private readonly SecurityPostureService _posture;
    private readonly SamsungHonestyService _honesty;
    private readonly ILogger _log;

    public PreflightService(IAdbClient client, SecurityPostureService posture, SamsungHonestyService honesty, ILogger log)
    {
        _client = client;
        _posture = posture;
        _honesty = honesty;
        _log = log.ForContext<PreflightService>();
    }

    public async Task<PreflightReport> RunAsync(DeviceData source, DeviceData destination, CancellationToken ct = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (destination is null) throw new ArgumentNullException(nameof(destination));

        var samsungHonesty = await _honesty.ProbeAsync(source, ct);
        var srcPosture = _posture.Probe(source);
        var dstPosture = _posture.Probe(destination);

        var csc = await ProbeCscAsync(source, destination, ct);
        var messages = await new MessageTransitionService(_client, _log)
            .ProbeAsync(source, csc?.SourceCountry, ct);
        var galleryOneDrive = await new GalleryOneDriveService(_client, _log)
            .ProbeAsync(source, ct);
        var oemUnlock = await ProbeOemUnlockAsync(destination, ct);
        var knox = await ProbeKnoxAsync(destination, ct);

        return new PreflightReport(samsungHonesty, messages, galleryOneDrive, srcPosture, dstPosture, csc,
            DestinationOemUnlockAvailable: oemUnlock, KnoxState: knox);
    }

    /// <summary>
    /// Pre-flight CSC diff (F043). Reads <c>persist.sys.sales_code</c>,
    /// <c>ro.csc.country_code</c>, and <c>persist.sys.locale</c> from both phones.
    /// </summary>
    private async Task<CscPosture?> ProbeCscAsync(DeviceData source, DeviceData destination, CancellationToken ct)
    {
        try
        {
            var srcCsc = (await _client.ShellAsync(source, "getprop persist.sys.sales_code", ct)).Trim();
            var dstCsc = (await _client.ShellAsync(destination, "getprop persist.sys.sales_code", ct)).Trim();
            var srcCountry = (await _client.ShellAsync(source, "getprop ro.csc.country_code", ct)).Trim();
            var dstCountry = (await _client.ShellAsync(destination, "getprop ro.csc.country_code", ct)).Trim();
            var srcLocale = (await _client.ShellAsync(source, "getprop persist.sys.locale", ct)).Trim();
            var dstLocale = (await _client.ShellAsync(destination, "getprop persist.sys.locale", ct)).Trim();
            return new CscPosture(srcCsc, dstCsc, srcCountry, dstCountry, srcLocale, dstLocale);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "CSC pre-flight probe failed");
            return null;
        }
    }

    /// <summary>
    /// Probe destination for OEM Unlock toggle availability (F044). One UI 8.5 on
    /// S25/S26 removed the Settings entry entirely; we surface this so the user
    /// doesn't go hunting for it post-migration.
    /// </summary>
    private async Task<bool> ProbeOemUnlockAsync(DeviceData destination, CancellationToken ct)
    {
        try
        {
            // settings get global oem_unlock_allowed returns "1" when the toggle is reachable.
            // On devices where the toggle was removed entirely, the property is absent
            // (the shell prints "null").
            var raw = (await _client.ShellAsync(destination, "settings get global oem_unlock_allowed", ct)).Trim();
            return raw == "1";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads <c>ro.boot.warranty_bit</c> and a couple Knox indicators so the UI can
    /// flag "this device has been rooted before" without scary technical jargon.
    /// </summary>
    private async Task<string?> ProbeKnoxAsync(DeviceData destination, CancellationToken ct)
    {
        try
        {
            var warranty = (await _client.ShellAsync(destination, "getprop ro.boot.warranty_bit", ct)).Trim();
            var knoxFlash = (await _client.ShellAsync(destination, "getprop ro.boot.flash.locked", ct)).Trim();
            return $"warranty={warranty} flashLocked={knoxFlash}";
        }
        catch
        {
            return null;
        }
    }
}
