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
});

return await app.RunAsync(args);
