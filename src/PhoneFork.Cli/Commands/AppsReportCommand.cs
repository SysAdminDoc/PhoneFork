using System.ComponentModel;
using System.Text.Json;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class AppsReportCommand : AsyncCommand<AppsReportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")]
        [Description("Device serial to query. Defaults to the first connected device when only one is plugged in.")]
        public string? Serial { get; init; }

        [CommandOption("--package <PKG>")]
        [Description("Report only the specified package (may be passed multiple times).")]
        public string[] Packages { get; init; } = Array.Empty<string>();

        [CommandOption("--skip-backup-probes")]
        [Description("Skip dumpsys backup/package posture checks.")]
        public bool SkipBackupProbes { get; init; }

        [CommandOption("--skip-external-probes")]
        [Description("Skip /sdcard/Android/obb and /sdcard/Android/data probes.")]
        public bool SkipExternalProbes { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON instead of a table.")]
        public bool Json { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        if (devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No devices.[/]");
            return 1;
        }

        var picked = settings.Serial is { Length: > 0 } serial
            ? devices.FirstOrDefault(d => d.Serial == serial)
            : (devices.Count == 1 ? devices[0] : null);
        if (picked is null)
        {
            AnsiConsole.MarkupLine("[red]Specify --device <serial> (multiple devices connected).[/]");
            AnsiConsole.MarkupLine(string.Join(", ", devices.Select(d => d.Serial)));
            return 1;
        }

        var catalog = new AppCatalogService(host.Client, log);
        var apps = (await catalog.EnumerateUserAppsAsync(picked, ct)).OrderBy(a => a.PackageName).ToList();
        if (settings.Packages.Length > 0)
        {
            var wanted = settings.Packages.ToHashSet(StringComparer.Ordinal);
            apps = apps.Where(a => wanted.Contains(a.PackageName)).ToList();
        }

        var backup = new BackupCapabilityService(host.Client, log);
        var transfer = new AppTransferReportService(host.Client, log);
        var reports = new List<AppTransferReport>(apps.Count);
        foreach (var app in apps)
        {
            ct.ThrowIfCancellationRequested();
            BackupCapability? cap = null;
            AppExternalDataProbe? external = null;
            if (!settings.SkipBackupProbes)
                cap = await backup.ProbeAsync(picked, app.PackageName, ct);
            if (!settings.SkipExternalProbes)
                external = await transfer.ProbeExternalDataAsync(picked, app.PackageName, ct);
            reports.Add(AppTransferReportService.Assess(app, cap, external));
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        var table = new Table().RoundedBorder()
            .AddColumn("Package")
            .AddColumn("APK")
            .AddColumn("Private data")
            .AddColumn("OBB/external")
            .AddColumn("Warnings");
        foreach (var report in reports)
        {
            table.AddRow(
                Markup.Escape(report.PackageId),
                Summarize(report, "APK install"),
                Summarize(report, "Private app data"),
                Summarize(report, "OBB/external app data"),
                Markup.Escape(report.Warnings.Count == 0 ? "-" : string.Join(" | ", report.Warnings)));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{reports.Count} app transfer report(s) on {picked.Serial}.[/]");
        return 0;
    }

    private static string Summarize(AppTransferReport report, string area)
    {
        var facet = report.Facets.FirstOrDefault(f => f.Area == area);
        return facet is null ? "[grey]unknown[/]" : facet.Readiness switch
        {
            AppTransferReadiness.Supported => "[green]supported[/]",
            AppTransferReadiness.Partial => "[yellow]partial[/]",
            AppTransferReadiness.External => "[blue]external[/]",
            AppTransferReadiness.Unsupported => "[red]unsupported[/]",
            _ => "[grey]unknown[/]",
        };
    }
}
