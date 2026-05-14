using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace PhoneFork.Cli.Commands;

public sealed class AppsListCommand : AsyncCommand<AppsListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")]
        [Description("Device serial to query. Defaults to the first connected device when only one is plugged in.")]
        public string? Serial { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON instead of a table.")]
        public bool Json { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var (host, devices, log) = AdbBootstrap.Initialize();
        var deviceData = host.GetDevices().ToList();

        if (deviceData.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No devices.[/]");
            return 1;
        }

        var picked = settings.Serial is { Length: > 0 } s
            ? deviceData.FirstOrDefault(d => d.Serial == s)
            : (deviceData.Count == 1 ? deviceData[0] : null);

        if (picked is null)
        {
            AnsiConsole.MarkupLine("[red]Specify --device <serial> (multiple devices connected).[/]");
            AnsiConsole.MarkupLine(string.Join(", ", deviceData.Select(d => d.Serial)));
            return 1;
        }

        var catalog = new AppCatalogService(host.Client, log);
        var apps = await catalog.EnumerateUserAppsAsync(picked);

        if (settings.Json)
        {
            AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(apps, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        var table = new Table().RoundedBorder()
            .AddColumn("Package").AddColumn("Label").AddColumn("Version").AddColumn("Splits").AddColumn("Size (MiB)");
        foreach (var a in apps.OrderBy(x => x.PackageName))
        {
            table.AddRow(
                Markup.Escape(a.PackageName),
                Markup.Escape(a.SafeLabel),
                Markup.Escape(a.VersionName),
                a.RemoteApkPaths.Count.ToString(),
                $"{a.TotalSizeBytes / 1024.0 / 1024.0:F1}");
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{apps.Count} user apps on {picked.Serial}[/]");
        return 0;
    }
}
