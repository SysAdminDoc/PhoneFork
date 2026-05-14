using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace PhoneFork.Cli.Commands;

public sealed class AppsMigrateCommand : AsyncCommand<AppsMigrateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--from <SERIAL>")]
        [Description("Source device serial.")]
        public required string From { get; init; }

        [CommandOption("--to <SERIAL>")]
        [Description("Destination device serial.")]
        public required string To { get; init; }

        [CommandOption("--package <PKG>")]
        [Description("Migrate only the specified package (may be passed multiple times).")]
        public string[] Packages { get; init; } = Array.Empty<string>();

        [CommandOption("--dry-run")]
        [Description("Enumerate and pull APKs but skip the install on destination.")]
        public bool DryRun { get; init; }

        [CommandOption("--reinstall")]
        [Description("Reinstall (pass -r) if the package is already present on destination.")]
        public bool Reinstall { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var deviceData = host.GetDevices().ToList();

        var src = deviceData.FirstOrDefault(d => d.Serial == settings.From);
        var dst = deviceData.FirstOrDefault(d => d.Serial == settings.To);
        if (src is null) { AnsiConsole.MarkupLine($"[red]Source {settings.From} not connected.[/]"); return 1; }
        if (dst is null) { AnsiConsole.MarkupLine($"[red]Destination {settings.To} not connected.[/]"); return 1; }
        if (src.Serial == dst.Serial) { AnsiConsole.MarkupLine("[red]Source and destination cannot be the same device.[/]"); return 1; }

        var catalog = new AppCatalogService(host.Client, log);
        var installer = new AppInstallerService(host.Client, log);

        AnsiConsole.MarkupLine($"[grey]Enumerating user apps on {src.Serial}…[/]");
        var apps = await catalog.EnumerateUserAppsAsync(src);
        if (settings.Packages.Length > 0)
        {
            var allowed = settings.Packages.ToHashSet();
            apps = apps.Where(a => allowed.Contains(a.PackageName)).ToList();
        }
        AnsiConsole.MarkupLine($"[grey]Selected {apps.Count} app(s) to migrate.[/]");

        int ok = 0, fail = 0;
        await AnsiConsole.Progress()
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Migrating apps", maxValue: apps.Count);
                foreach (var app in apps)
                {
                    var progress = new Progress<string>(_ => { });
                    if (settings.DryRun)
                    {
                        // Pull only — install skipped.
                        try
                        {
                            await installer.PullApksToCacheAsync(src, app, progress, cancellationToken);
                            ok++;
                            AnsiConsole.MarkupLine($"[green]dry-run pulled[/] {Markup.Escape(app.PackageName)}");
                        }
                        catch (Exception ex)
                        {
                            fail++;
                            AnsiConsole.MarkupLine($"[red]pull failed[/] {Markup.Escape(app.PackageName)}: {Markup.Escape(ex.Message)}");
                        }
                    }
                    else
                    {
                        var r = await installer.MigrateAsync(src, dst, app, settings.Reinstall, progress, cancellationToken);
                        if (r.Success)
                        {
                            ok++;
                            AnsiConsole.MarkupLine($"[green]ok[/] {Markup.Escape(r.PackageName)} ({r.Duration.TotalSeconds:F1}s)");
                        }
                        else
                        {
                            fail++;
                            AnsiConsole.MarkupLine($"[red]fail[/] {Markup.Escape(r.PackageName)}: {Markup.Escape(r.Error ?? "(unknown)")}");
                        }
                    }
                    task.Increment(1);
                }
            });

        AnsiConsole.MarkupLine($"\n[bold]{ok} migrated[/], [red]{fail} failed[/].");
        AnsiConsole.MarkupLine($"[grey]Audit log: {Markup.Escape(PhoneFork.Core.Logging.AuditLogger.LogDirectory)}[/]");
        return fail == 0 ? 0 : 2;
    }
}
