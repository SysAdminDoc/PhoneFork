using PhoneFork.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("phonefork");
    config.AddCommand<DevicesCommand>("devices")
        .WithDescription("List connected ADB devices and their Source/Destination role candidates.");

    config.AddBranch("apps", apps =>
    {
        apps.SetDescription("App enumeration and migration.");
        apps.AddCommand<AppsListCommand>("list")
            .WithDescription("Enumerate -3 user apps on a device.");
        apps.AddCommand<AppsMigrateCommand>("migrate")
            .WithDescription("Pull APKs+splits from a source device and install on a destination device.");
    });

    config.AddBranch("media", media =>
    {
        media.SetDescription("/sdcard media manifesting, diffing, and incremental sync.");
        media.AddCommand<MediaManifestCommand>("manifest")
            .WithDescription("Build a JSON manifest of /sdcard media on a device.");
        media.AddCommand<MediaDiffCommand>("diff")
            .WithDescription("Diff two manifests; emit a migration plan.");
        media.AddCommand<MediaSyncCommand>("sync")
            .WithDescription("Incremental sync of /sdcard media between two devices.");
    });

    config.AddBranch("settings", settings =>
    {
        settings.SetDescription("System settings snapshot, diff, and selective apply.");
        settings.AddCommand<SettingsDumpCommand>("dump")
            .WithDescription("Dump secure/system/global namespaces to a JSON snapshot.");
        settings.AddCommand<SettingsDiffCommand>("diff")
            .WithDescription("Diff two snapshots; show buckets by namespace.");
        settings.AddCommand<SettingsApplyCommand>("apply")
            .WithDescription("Capture both devices live and apply source -> destination via settings put.");
    });

    config.AddBranch("debloat", debloat =>
    {
        debloat.SetDescription("Apply AppManagerNG/UAD-NG curated debloat list. Reversible via snapshot rollback.");
        debloat.AddCommand<DebloatListCommand>("list")
            .WithDescription("List packages on the device that intersect the dataset.");
        debloat.AddCommand<DebloatApplyCommand>("apply")
            .WithDescription("Disable matched packages by profile or explicit allowlist. Snapshots pre-state for rollback.");
        debloat.AddCommand<DebloatRollbackCommand>("rollback")
            .WithDescription("Re-enable packages that were disabled by a prior apply, using its snapshot JSON.");
    });

    config.AddBranch("wifi", wifi =>
    {
        wifi.SetDescription("Wi-Fi SSID enumeration + QR-bridge generation. PSK export requires v0.7 helper APK / Shizuku.");
        wifi.AddCommand<WifiListCommand>("list")
            .WithDescription("List SSIDs on a device (PSKs are not recoverable without Shizuku/helper).");
        wifi.AddCommand<WifiQrCommand>("qr")
            .WithDescription("Render a scannable WIFI: QR code (PNG or SVG) from a user-supplied SSID + PSK.");
    });

    config.AddBranch("csc", csc =>
    {
        csc.SetDescription("Region / locale / CSC diff between two devices (pre-flight banner).");
        csc.AddCommand<CscDiffCommand>("diff")
            .WithDescription("Capture both devices and print the CSC / country / locale / timezone diff.");
    });

    config.AddBranch("roles", roles =>
    {
        roles.SetDescription("Default-app role snapshot + apply (cmd role).");
        roles.AddCommand<RolesGetCommand>("get")
            .WithDescription("Snapshot current default-app role holders on a device.");
        roles.AddCommand<RolesApplyCommand>("apply")
            .WithDescription("Read source role holders and add them as role holders on destination.");
    });

    config.AddBranch("perms", perms =>
    {
        perms.SetDescription("Per-package runtime permission grants + appops.");
        perms.AddCommand<PermsGrantCommand>("grant")
            .WithDescription("Grant a runtime permission and/or set an appop mode for a package.");
    });

    config.AddCommand<PairCommand>("pair")
        .WithDescription("Pair with a phone over Wireless debugging (TLS pairing on Android 11+).");
    config.AddCommand<ConnectCommand>("connect")
        .WithDescription("Connect to a paired wireless ADB endpoint.");
    config.AddCommand<DisconnectCommand>("disconnect")
        .WithDescription("Disconnect a wireless ADB endpoint (or all when omitted).");

    config.AddBranch("mdns", mdns =>
    {
        mdns.SetDescription("Wireless ADB mDNS-SD service discovery and trust-aware reconnect.");
        mdns.AddCommand<MdnsServicesCommand>("services")
            .WithDescription("List wireless ADB services discovered on the LAN. Marks trusted endpoints.");
    });

    config.AddCommand<HonestyCommand>("honesty")
        .WithDescription("Pre-flight scan of a source device for Samsung categories that won't transfer (Pass, Wallet, Secure Folder, Routines, etc.).");

    config.AddBranch("helper", helper =>
    {
        helper.SetDescription("Lifecycle for the PhoneForkHelper companion APK (SMS, call log, contacts, Wi-Fi, wallpaper, ringtone, dictionary).");
        helper.AddCommand<HelperInstallCommand>("install")
            .WithDescription("Push and install PhoneForkHelper.apk onto a device.");
        helper.AddCommand<HelperUninstallCommand>("uninstall")
            .WithDescription("Uninstall PhoneForkHelper (idempotent).");
        helper.AddCommand<HelperProbeCommand>("probe")
            .WithDescription("Health-check every helper provider authority.");
        helper.AddCommand<HelperResidueCommand>("residue")
            .WithDescription("Verify the helper is gone after migration (F019).");
    });

    config.AddBranch("shizuku", shz =>
    {
        shz.SetDescription("Shizuku detection and runbook (F012).");
        shz.AddCommand<ShizukuStatusCommand>("status")
            .WithDescription("Detect Shizuku state on a device and print the runbook.");
    });

    config.AddBranch("smartswitch", ss =>
    {
        ss.SetDescription("Samsung Smart Switch detection and handoff (F024 / F025).");
        ss.AddCommand<SmartSwitchDetectCommand>("detect")
            .WithDescription("Detect Smart Switch (legacy MSI + Microsoft Store) and backup folder.");
    });

    config.AddBranch("trusted", t =>
    {
        t.SetDescription("Trusted-pair registry (F004). Hashed serials only — no raw IDs on disk.");
        t.AddCommand<TrustedListCommand>("list")
            .WithDescription("List trusted pairs.");
        t.AddCommand<TrustedForgetCommand>("forget")
            .WithDescription("Forget one trusted pair by hash (copy from `trusted list`).");
    });

    config.AddCommand<BurstModeCommand>("burst-mode")
        .WithDescription("Toggle ADB Burst Mode (F104). Affects newly-started ADB servers; restart required.");
});

return await app.RunAsync(args);
