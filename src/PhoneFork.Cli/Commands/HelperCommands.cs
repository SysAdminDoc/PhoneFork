using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

/// <summary>
/// <c>phonefork helper install</c> — push and `pm install -r` the PhoneForkHelper.apk
/// onto a device. Path defaults to <c>assets/helper/PhoneForkHelper.apk</c> relative to the CLI.
/// </summary>
public sealed class HelperInstallCommand : AsyncCommand<HelperInstallCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Target device serial.")]
        public required string Serial { get; init; }

        [CommandOption("--apk <PATH>")] [Description("Path to the helper APK. Defaults to assets/helper/PhoneForkHelper.apk next to the CLI.")]
        public string? ApkPath { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var device = host.GetDevices().FirstOrDefault(d => d.Serial == s.Serial);
        if (device is null)
        {
            AnsiConsole.MarkupLine($"[red]Device offline:[/] {Markup.Escape(s.Serial)}");
            return 2;
        }

        var apk = s.ApkPath ?? ResolveHelperApk();
        if (!File.Exists(apk))
        {
            AnsiConsole.MarkupLine(
                $"[red]Helper APK not found:[/] {Markup.Escape(apk)}. " +
                "Build with `cd helper-apk; ./gradlew assembleRelease` and sign with apksigner, " +
                "then place at assets/helper/PhoneForkHelper.apk.");
            return 2;
        }

        var helper = new HelperAppService(host.Client, log);
        AnsiConsole.MarkupLine($"[grey]Installing {Markup.Escape(Path.GetFileName(apk))} on {Markup.Escape(s.Serial)}…[/]");
        var ok = await helper.InstallAsync(device, apk, ct);
        AnsiConsole.MarkupLine(ok ? "[green]Installed.[/]" : "[red]Install failed.[/]");
        return ok ? 0 : 2;
    }

    internal static string ResolveHelperApk()
    {
        var here = Path.GetDirectoryName(AppContext.BaseDirectory) ?? Environment.CurrentDirectory;
        return Path.Combine(here, "assets", "helper", "PhoneForkHelper.apk");
    }
}

/// <summary><c>phonefork helper uninstall</c> — `pm uninstall` the helper. Idempotent.</summary>
public sealed class HelperUninstallCommand : AsyncCommand<HelperUninstallCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Target device serial.")]
        public required string Serial { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var device = host.GetDevices().FirstOrDefault(d => d.Serial == s.Serial);
        if (device is null)
        {
            AnsiConsole.MarkupLine($"[red]Device offline:[/] {Markup.Escape(s.Serial)}");
            return 2;
        }
        var helper = new HelperAppService(host.Client, log);
        var ok = await helper.UninstallAsync(device, ct);
        AnsiConsole.MarkupLine(ok ? "[green]Uninstalled (or was not installed).[/]" : "[red]Uninstall failed.[/]");
        return ok ? 0 : 2;
    }
}

/// <summary>
/// <c>phonefork helper probe</c> — hit every helper authority's /health endpoint and report.
/// </summary>
public sealed class HelperProbeCommand : AsyncCommand<HelperProbeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Target device serial.")]
        public required string Serial { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var device = host.GetDevices().FirstOrDefault(d => d.Serial == s.Serial);
        if (device is null)
        {
            AnsiConsole.MarkupLine($"[red]Device offline:[/] {Markup.Escape(s.Serial)}");
            return 2;
        }

        var helper = new HelperAppService(host.Client, log);
        if (!await helper.IsInstalledAsync(device, ct))
        {
            AnsiConsole.MarkupLine("[yellow]Helper not installed.[/] Run `phonefork helper install -d <serial>` first.");
            return 1;
        }

        var results = await helper.ProbeAllAsync(device, ct);
        var table = new Table().AddColumns("Authority", "Healthy");
        foreach (var kv in results)
            table.AddRow(Markup.Escape(kv.Key), kv.Value ? "[green]ok[/]" : "[red]fail[/]");
        AnsiConsole.Write(table);
        return results.Values.All(v => v) ? 0 : 1;
    }
}

/// <summary>
/// <c>phonefork helper residue</c> — verify the helper APK is gone and no `phonefork*`
/// files linger in `/data/local/tmp` (F019).
/// </summary>
public sealed class HelperResidueCommand : AsyncCommand<HelperResidueCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Target device serial.")]
        public required string Serial { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var device = host.GetDevices().FirstOrDefault(d => d.Serial == s.Serial);
        if (device is null)
        {
            AnsiConsole.MarkupLine($"[red]Device offline:[/] {Markup.Escape(s.Serial)}");
            return 2;
        }
        var helper = new HelperAppService(host.Client, log);
        var report = await helper.ResidueCheckAsync(device, ct);

        if (report.IsClean)
        {
            AnsiConsole.MarkupLine("[green]Clean.[/] Helper not installed, no /data/local/tmp leftovers.");
            return 0;
        }
        if (report.HelperInstalled)
            AnsiConsole.MarkupLine("[yellow]Helper still installed.[/] Run `phonefork helper uninstall -d <serial>`.");
        if (report.TempFilesLeft.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Leftover /data/local/tmp files:[/]");
            foreach (var f in report.TempFilesLeft)
                AnsiConsole.MarkupLine($"  - {Markup.Escape(f)}");
        }
        return 1;
    }
}
