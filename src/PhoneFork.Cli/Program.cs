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
});

return await app.RunAsync(args);
