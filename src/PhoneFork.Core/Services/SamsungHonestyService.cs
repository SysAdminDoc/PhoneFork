using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Probes a source device for Samsung packages whose data cannot transfer through the
/// PhoneFork no-root pipeline. Generates structured findings (F040 Secure Folder /
/// Pass / Wallet / Routines detector, F108 Samsung Pass → Wallet transition warning).
/// All probes are read-only and use shell-UID commands only.
/// </summary>
public sealed class SamsungHonestyService
{
    /// <summary>
    /// Known package IDs that map to "this category won't transfer" findings on Samsung One UI.
    /// Updated for the 2026 Pass -> Wallet transition.
    /// </summary>
    public static readonly IReadOnlyList<HonestyProbe> Probes = new[]
    {
        new HonestyProbe(
            Id: "samsung-pass",
            PackageId: "com.samsung.android.samsungpass",
            Title: "Samsung Pass present",
            Detail: "Samsung is folding Samsung Pass into Samsung Wallet in 2026. Open Samsung Pass on the source, run the in-app migration to Samsung Wallet, then re-install Wallet on the destination. PhoneFork cannot move Pass entries — they are sealed to Knox.",
            Level: HonestyLevel.Warning,
            ActionUrl: "https://www.samsung.com/us/support/answer/ANS10001582/"),
        new HonestyProbe(
            Id: "samsung-wallet",
            PackageId: "com.samsung.android.spay",
            Title: "Samsung Wallet present",
            Detail: "Wallet payment tokens are bound to the device's secure element. The card list re-syncs with your Samsung account on the new phone, but individual card tokens must be re-activated.",
            Level: HonestyLevel.Warning,
            ActionUrl: "https://www.samsung.com/us/support/answer/ANS10001582/"),
        new HonestyProbe(
            Id: "samsung-wallet-legacy",
            PackageId: "com.samsung.android.samsungpay",
            Title: "Legacy Samsung Pay present",
            Detail: "Legacy Samsung Pay is being merged into Samsung Wallet. Migrate on the source phone before wiping.",
            Level: HonestyLevel.Warning,
            ActionUrl: "https://www.samsung.com/us/support/answer/ANS10001582/"),
        new HonestyProbe(
            Id: "secure-folder",
            PackageId: "com.samsung.knox.securefolder",
            Title: "Secure Folder present",
            Detail: "Secure Folder content is encrypted under Knox and cannot leave the device through ADB. Run Smart Switch on the phone itself (with the Secure Folder PIN) to transfer this category, or re-create it on the destination.",
            Level: HonestyLevel.Blocker,
            ActionUrl: "https://docs.samsungknox.com/secure-folder/"),
        new HonestyProbe(
            Id: "samsung-account",
            PackageId: "com.osp.app.signin",
            Title: "Samsung account signed in",
            Detail: "Account-bound data (Bixby Routines, Samsung Notes sync, Samsung Cloud) will only repopulate after the destination phone signs back into the same Samsung account.",
            Level: HonestyLevel.Info),
        new HonestyProbe(
            Id: "bixby-routines",
            PackageId: "com.samsung.android.app.routines",
            Title: "Bixby Routines present",
            Detail: "Routines back up to the Samsung account; signing in on the destination will restore them. Take a screenshot of any custom routine for safety.",
            Level: HonestyLevel.Info),
        new HonestyProbe(
            Id: "samsung-notes",
            PackageId: "com.samsung.android.app.notes",
            Title: "Samsung Notes present",
            Detail: "Local-only notes do not transfer through ADB. Sync to Samsung Cloud or export to PDF/Markdown before wiping.",
            Level: HonestyLevel.Warning),
    };

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public SamsungHonestyService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<SamsungHonestyService>();
    }

    public async Task<HonestyReport> ProbeAsync(DeviceData device, CancellationToken ct = default)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));

        var findings = new List<HonestyFinding>();
        foreach (var probe in Probes)
        {
            ct.ThrowIfCancellationRequested();
            if (!await PackageInstalledAsync(device, probe.PackageId, ct)) continue;

            findings.Add(new HonestyFinding(
                Id: probe.Id,
                Title: probe.Title,
                Detail: probe.Detail,
                Level: probe.Level,
                PackageId: probe.PackageId,
                ActionUrl: probe.ActionUrl));
        }

        _log.Information("Samsung honesty probe on {Serial}: {Count} findings ({Blockers} blockers)",
            device.Serial, findings.Count, findings.Count(f => f.Level == HonestyLevel.Blocker));
        return new HonestyReport(findings);
    }

    private async Task<bool> PackageInstalledAsync(DeviceData device, string packageId, CancellationToken ct)
    {
        try
        {
            var output = await _client.ShellAsync(device,
                $"pm list packages {AdbShell.PackageArg(packageId)}",
                ct);
            // pm list packages prints "package:<id>" only if the package is installed for the current user.
            return (output ?? "").Contains($"package:{packageId}", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Honesty probe failed for {Pkg}", packageId);
            return false;
        }
    }
}

/// <summary>One detector entry: matches by package ID and emits a finding when present.</summary>
public sealed record HonestyProbe(
    string Id,
    string PackageId,
    string Title,
    string Detail,
    HonestyLevel Level,
    string? ActionUrl = null);
