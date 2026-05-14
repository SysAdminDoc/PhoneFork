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
});

return await app.RunAsync(args);
