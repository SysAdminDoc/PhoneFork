using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record GalleryOneDriveReport(
    bool SamsungGalleryInstalled,
    bool OneDriveInstalled,
    bool SamsungAccountInstalled,
    IReadOnlyList<string> SamsungCloudPackagesInstalled,
    bool? OneDriveAccountVisible,
    bool? OneDriveMediaPermissionGranted,
    bool CameraBackupReady,
    IReadOnlyList<HonestyFinding> Findings)
{
    public string Summary
    {
        get
        {
            if (!SamsungGalleryInstalled && !OneDriveInstalled && !SamsungAccountInstalled && SamsungCloudPackagesInstalled.Count == 0)
                return "No Samsung Gallery, OneDrive, or Samsung Cloud indicators detected.";

            var ready = CameraBackupReady
                ? "OneDrive camera-backup prerequisites look ready from ADB-visible signals."
                : "Verify OneDrive camera backup account, media permission, and cloud storage before wiping the source.";
            return $"Gallery/OneDrive posture: Gallery={SamsungGalleryInstalled}, OneDrive={OneDriveInstalled}, Samsung account/cloud={SamsungAccountInstalled || SamsungCloudPackagesInstalled.Count > 0}. {ready}";
        }
    }
}

/// <summary>
/// Pre-flight probe for Samsung Gallery's direct OneDrive sync cutoff and the
/// replacement OneDrive camera-backup posture.
/// </summary>
public sealed class GalleryOneDriveService
{
    public const string SamsungGalleryPackage = "com.sec.android.gallery3d";
    public const string OneDrivePackage = "com.microsoft.skydrive";
    public const string SamsungAccountPackage = "com.osp.app.signin";
    public const string MicrosoftCutoffUrl = "https://support.microsoft.com/en-gb/office/changes-to-samsung-gallery-sync-and-onedrive-475ecc9c-c2fe-4d3c-ab9e-38e995123767";

    public static readonly IReadOnlyList<string> SamsungCloudPackages = new[]
    {
        "com.samsung.android.scloud",
        "com.samsung.android.scloud.sync",
    };

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public GalleryOneDriveService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<GalleryOneDriveService>();
    }

    public async Task<GalleryOneDriveReport> ProbeAsync(DeviceData source, CancellationToken ct = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var gallery = await PackageInstalledAsync(source, SamsungGalleryPackage, ct);
        var oneDrive = await PackageInstalledAsync(source, OneDrivePackage, ct);
        var samsungAccount = await PackageInstalledAsync(source, SamsungAccountPackage, ct);
        var cloudPackages = new List<string>();
        foreach (var packageId in SamsungCloudPackages)
        {
            if (await PackageInstalledAsync(source, packageId, ct))
                cloudPackages.Add(packageId);
        }

        bool? accountVisible = null;
        bool? mediaPermission = null;
        if (oneDrive)
        {
            accountVisible = await ProbeOneDriveAccountAsync(source, ct);
            mediaPermission = await ProbeOneDriveMediaPermissionAsync(source, ct);
        }

        return Assess(gallery, oneDrive, samsungAccount, cloudPackages, accountVisible, mediaPermission);
    }

    public static GalleryOneDriveReport Assess(
        bool samsungGalleryInstalled,
        bool oneDriveInstalled,
        bool samsungAccountInstalled,
        IReadOnlyList<string>? samsungCloudPackagesInstalled,
        bool? oneDriveAccountVisible,
        bool? oneDriveMediaPermissionGranted)
    {
        var cloudPackages = samsungCloudPackagesInstalled ?? Array.Empty<string>();
        var findings = new List<HonestyFinding>();

        if (samsungGalleryInstalled)
        {
            findings.Add(new HonestyFinding(
                Id: "samsung-gallery-onedrive-cutoff",
                Title: "Samsung Gallery direct OneDrive sync cutoff",
                Detail: "Microsoft says Samsung Gallery's direct sync with OneDrive ends on September 30, 2026. Files already in OneDrive remain accessible in the OneDrive app, OneDrive web, and File Explorer after the cutoff; use OneDrive camera backup for new photo/video backup instead of relying on Gallery sync.",
                Level: HonestyLevel.Info,
                PackageId: SamsungGalleryPackage,
                ActionUrl: MicrosoftCutoffUrl));
        }

        if (samsungGalleryInstalled && !oneDriveInstalled)
        {
            findings.Add(new HonestyFinding(
                Id: "onedrive-missing-for-camera-backup",
                Title: "OneDrive not detected",
                Detail: "Samsung Gallery is present but OneDrive is not installed for a camera-backup handoff. Install OneDrive or choose PhoneFork's local Media sync before wiping the source.",
                Level: HonestyLevel.Warning,
                PackageId: OneDrivePackage,
                ActionUrl: MicrosoftCutoffUrl));
        }

        if (samsungAccountInstalled || cloudPackages.Count > 0)
        {
            var packages = cloudPackages.Count == 0 ? "none detected" : string.Join(", ", cloudPackages);
            findings.Add(new HonestyFinding(
                Id: "samsung-account-cloud-present",
                Title: "Samsung account/cloud indicators present",
                Detail: $"Samsung account or cloud components are present. Account-bound Gallery/Cloud state should be verified on the source before wiping. Samsung Cloud package indicators: {packages}.",
                Level: HonestyLevel.Info,
                PackageId: samsungAccountInstalled ? SamsungAccountPackage : cloudPackages.FirstOrDefault(),
                ActionUrl: MicrosoftCutoffUrl));
        }

        if (oneDriveInstalled)
        {
            findings.Add(BuildNullableFinding(
                id: "onedrive-account-visible",
                title: "OneDrive account check",
                okDetail: "ADB-visible Android account state includes a OneDrive/Microsoft account indicator.",
                missingDetail: "ADB-visible Android account state did not show a OneDrive/Microsoft account indicator. Open OneDrive and verify the camera-backup account before wiping.",
                unknownDetail: "PhoneFork could not determine OneDrive account state from ADB. Open OneDrive and verify the camera-backup account before wiping.",
                ok: oneDriveAccountVisible,
                packageId: OneDrivePackage));

            findings.Add(BuildNullableFinding(
                id: "onedrive-media-permission",
                title: "OneDrive media permission check",
                okDetail: "OneDrive appears to have Android media-read permission for camera backup.",
                missingDetail: "OneDrive does not appear to have ADB-visible media-read permission. Re-enable Photos and videos permission before relying on camera backup.",
                unknownDetail: "PhoneFork could not determine OneDrive media permission state from ADB. Check Photos and videos permission in Android settings before relying on camera backup.",
                ok: oneDriveMediaPermissionGranted,
                packageId: OneDrivePackage));

            findings.Add(new HonestyFinding(
                Id: "onedrive-storage-manual-check",
                Title: "OneDrive storage quota check",
                Detail: "OneDrive cloud quota is not exposed to ADB. Open OneDrive camera-backup settings and confirm the account has enough storage for the source phone's camera roll.",
                Level: HonestyLevel.Info,
                PackageId: OneDrivePackage,
                ActionUrl: MicrosoftCutoffUrl));
        }

        var ready = oneDriveInstalled
                    && oneDriveAccountVisible == true
                    && oneDriveMediaPermissionGranted == true;

        return new GalleryOneDriveReport(
            SamsungGalleryInstalled: samsungGalleryInstalled,
            OneDriveInstalled: oneDriveInstalled,
            SamsungAccountInstalled: samsungAccountInstalled,
            SamsungCloudPackagesInstalled: cloudPackages.ToArray(),
            OneDriveAccountVisible: oneDriveAccountVisible,
            OneDriveMediaPermissionGranted: oneDriveMediaPermissionGranted,
            CameraBackupReady: ready,
            Findings: findings);
    }

    private static HonestyFinding BuildNullableFinding(
        string id,
        string title,
        string okDetail,
        string missingDetail,
        string unknownDetail,
        bool? ok,
        string packageId)
        => new(
            Id: id,
            Title: title,
            Detail: ok switch
            {
                true => okDetail,
                false => missingDetail,
                _ => unknownDetail,
            },
            Level: ok == false ? HonestyLevel.Warning : HonestyLevel.Info,
            PackageId: packageId,
            ActionUrl: MicrosoftCutoffUrl);

    private async Task<bool?> ProbeOneDriveAccountAsync(DeviceData source, CancellationToken ct)
    {
        try
        {
            var output = await _client.ShellAsync(source, "dumpsys account", ct);
            return (output ?? "").Contains("com.microsoft.skydrive", StringComparison.OrdinalIgnoreCase)
                   || (output ?? "").Contains("onedrive", StringComparison.OrdinalIgnoreCase)
                   || (output ?? "").Contains("microsoft", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "OneDrive account probe failed");
            return null;
        }
    }

    private async Task<bool?> ProbeOneDriveMediaPermissionAsync(DeviceData source, CancellationToken ct)
    {
        try
        {
            var output = await _client.ShellAsync(source,
                $"dumpsys package {AdbShell.PackageArg(OneDrivePackage)}",
                ct);
            return PermissionGranted(output, "android.permission.READ_MEDIA_IMAGES")
                   || PermissionGranted(output, "android.permission.READ_MEDIA_VIDEO")
                   || PermissionGranted(output, "android.permission.READ_EXTERNAL_STORAGE");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "OneDrive media permission probe failed");
            return null;
        }
    }

    private static bool PermissionGranted(string? output, string permission) =>
        (output ?? "").Contains($"{permission}: granted=true", StringComparison.OrdinalIgnoreCase);

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
            _log.Warning(ex, "Gallery/OneDrive package probe failed for {Pkg}", packageId);
            return false;
        }
    }
}
