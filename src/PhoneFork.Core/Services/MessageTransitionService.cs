using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record MessageTransitionReport(
    string? DefaultSmsPackage,
    bool SamsungMessagesInstalled,
    bool GoogleMessagesInstalled,
    string SourceCountry,
    bool IsUsMarket,
    bool CanUseHelperSms,
    IReadOnlyList<HonestyFinding> Findings)
{
    public string Summary => CanUseHelperSms
        ? $"SMS default role is clear ({DefaultSmsPackage}); helper-assisted SMS work can move to the next explicit category confirmation step."
        : "Review SMS default-app state before helper-assisted SMS work. Samsung Messages US transition guidance may require switching to Google Messages first.";
}

/// <summary>
/// Detects Samsung Messages / Google Messages posture before any SMS helper flow.
/// Samsung's US transition to Google Messages is user-mediated, so PhoneFork
/// should warn before touching SMS backup or restore state.
/// </summary>
public sealed class MessageTransitionService
{
    public const string SamsungMessagesPackage = "com.samsung.android.messaging";
    public const string GoogleMessagesPackage = "com.google.android.apps.messaging";
    public const string SamsungMessagesTransitionUrl = "https://www.samsung.com/us/apps/samsung-messages/";

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public MessageTransitionService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<MessageTransitionService>();
    }

    public async Task<MessageTransitionReport> ProbeAsync(
        DeviceData source,
        string? sourceCountry = null,
        CancellationToken ct = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        string? defaultSms = null;
        try
        {
            var roles = new RoleService(_client, _log);
            var smsRole = await roles.SnapshotAsync(source, new[] { DefaultRoles.Sms }, ct);
            defaultSms = smsRole.Holders.FirstOrDefault(h => h.Role == DefaultRoles.Sms)?.HolderPackage;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Message transition default SMS role probe failed");
        }

        var country = sourceCountry;
        if (string.IsNullOrWhiteSpace(country))
            country = await ProbeCountryAsync(source, ct);

        var samsung = await PackageInstalledAsync(source, SamsungMessagesPackage, ct);
        var google = await PackageInstalledAsync(source, GoogleMessagesPackage, ct);

        return Assess(defaultSms, samsung, google, country);
    }

    public static MessageTransitionReport Assess(
        string? defaultSmsPackage,
        bool samsungMessagesInstalled,
        bool googleMessagesInstalled,
        string? sourceCountry)
    {
        var country = string.IsNullOrWhiteSpace(sourceCountry) ? "unknown" : sourceCountry.Trim();
        var isUs = string.Equals(country, "US", StringComparison.OrdinalIgnoreCase);
        var defaultIsSamsung = string.Equals(defaultSmsPackage, SamsungMessagesPackage, StringComparison.Ordinal);
        var defaultIsGoogle = string.Equals(defaultSmsPackage, GoogleMessagesPackage, StringComparison.Ordinal);

        var findings = new List<HonestyFinding>();

        if (isUs && samsungMessagesInstalled)
        {
            findings.Add(new HonestyFinding(
                Id: "samsung-messages-us-transition",
                Title: defaultIsSamsung
                    ? "Switch Samsung Messages before SMS migration"
                    : "Samsung Messages US transition",
                Detail: "Samsung says Samsung Messages is being discontinued in the US market in July 2026 and directs users to Google Messages. Switch on the source phone first and verify conversations in Google Messages before using PhoneFork's helper-assisted SMS flow; Samsung says the transfer can take up to about 24 hours.",
                Level: defaultIsSamsung ? HonestyLevel.Warning : HonestyLevel.Info,
                PackageId: SamsungMessagesPackage,
                ActionUrl: SamsungMessagesTransitionUrl));
        }
        else if (samsungMessagesInstalled)
        {
            findings.Add(new HonestyFinding(
                Id: "samsung-messages-present",
                Title: "Samsung Messages present",
                Detail: "Samsung Messages is installed. Verify the default SMS app before helper-assisted SMS work so PhoneFork does not operate while the user is mid-transition.",
                Level: HonestyLevel.Info,
                PackageId: SamsungMessagesPackage,
                ActionUrl: SamsungMessagesTransitionUrl));
        }

        if (isUs && defaultIsSamsung && !googleMessagesInstalled)
        {
            findings.Add(new HonestyFinding(
                Id: "google-messages-missing",
                Title: "Google Messages not detected",
                Detail: "Install Google Messages and make it the default SMS app before helper-assisted SMS work. This keeps PhoneFork aligned with Samsung's US transition path.",
                Level: HonestyLevel.Warning,
                PackageId: GoogleMessagesPackage,
                ActionUrl: SamsungMessagesTransitionUrl));
        }

        if (string.IsNullOrWhiteSpace(defaultSmsPackage))
        {
            findings.Add(new HonestyFinding(
                Id: "sms-default-missing",
                Title: "Default SMS role not detected",
                Detail: "Android did not report a default SMS role holder. Open Settings and choose the intended SMS app before PhoneFork uses helper-assisted SMS work.",
                Level: HonestyLevel.Warning));
        }
        else if (defaultIsGoogle)
        {
            findings.Add(new HonestyFinding(
                Id: "sms-default-google",
                Title: "Google Messages is default SMS",
                Detail: "Default SMS role is already Google Messages. Helper-assisted SMS work can move to explicit SMS category confirmation.",
                Level: HonestyLevel.Info,
                PackageId: GoogleMessagesPackage));
        }
        else if (!defaultIsSamsung)
        {
            findings.Add(new HonestyFinding(
                Id: "sms-default-other",
                Title: "SMS default role is explicit",
                Detail: $"Default SMS role holder is {defaultSmsPackage}. Helper-assisted SMS work can move to explicit SMS category confirmation after the user confirms that this app is intentional.",
                Level: HonestyLevel.Info,
                PackageId: defaultSmsPackage));
        }

        var canUseHelperSms = !string.IsNullOrWhiteSpace(defaultSmsPackage)
                              && !(isUs && defaultIsSamsung);

        return new MessageTransitionReport(
            DefaultSmsPackage: defaultSmsPackage,
            SamsungMessagesInstalled: samsungMessagesInstalled,
            GoogleMessagesInstalled: googleMessagesInstalled,
            SourceCountry: country,
            IsUsMarket: isUs,
            CanUseHelperSms: canUseHelperSms,
            Findings: findings);
    }

    private async Task<string?> ProbeCountryAsync(DeviceData source, CancellationToken ct)
    {
        try
        {
            return (await _client.ShellAsync(source, "getprop ro.csc.country_code", ct)).Trim();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Message transition country probe failed");
            return null;
        }
    }

    private async Task<bool> PackageInstalledAsync(DeviceData device, string packageId, CancellationToken ct)
    {
        try
        {
            var output = await _client.ShellAsync(device,
                $"pm list packages {AdbShell.PackageArg(packageId)}",
                ct);
            return (output ?? "").Contains($"package:{packageId}", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Message package probe failed for {Pkg}", packageId);
            return false;
        }
    }
}
