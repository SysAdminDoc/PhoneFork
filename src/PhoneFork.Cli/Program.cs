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
});

return await app.RunAsync(args);
