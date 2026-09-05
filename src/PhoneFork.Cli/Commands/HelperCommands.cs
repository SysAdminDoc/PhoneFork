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
                "Build and sign the helper APK, then stage it with " +
                "`scripts/Stage-HelperApk.ps1 -ApkPath <signed-apk>`.");
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

        // Runtime permission state (F111). The helper has no launcher activity, so it cannot
        // prompt; without these grants the sms/calllog/contacts reads fail with permission-denied.
        var perms = await helper.ProbeRuntimePermissionsAsync(device, ct);
        var permTable = new Table().AddColumns("Runtime permission", "State");
        foreach (var p in HelperAppService.RuntimePermissions)
        {
            var state = perms.Granted.Contains(p)
                ? "[green]granted[/]"
                : $"[red]{Markup.Escape(perms.Failed.TryGetValue(p, out var why) ? why : "denied")}[/]";
            permTable.AddRow(Markup.Escape(p), state);
        }
        AnsiConsole.Write(permTable);
        if (!perms.CanReadPrivilegedCategories)
            AnsiConsole.MarkupLine("[yellow]SMS, call log and contacts reads will fail until those permissions are granted. Re-run `phonefork helper install` to grant them.[/]");

        return results.Values.All(v => v) && perms.CanReadPrivilegedCategories ? 0 : 1;
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

/// <summary>
/// <c>phonefork helper export</c> — page a helper ContentProvider to completion and write the
/// rows to disk (F112). Without this the helper's read path had no user-facing surface at all.
/// </summary>
public sealed class HelperExportCommand : AsyncCommand<HelperExportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Source device serial.")]
        public required string Serial { get; init; }

        [CommandOption("--category <NAME>")]
        [Description("Authority to export (repeatable): sms, calllog, contacts, dictionary, ringtone, wallpaper, wifi. Default: all of them.")]
        public string[] Categories { get; init; } = Array.Empty<string>();

        [CommandOption("--out <DIR>")] [Description("Directory to write <category>.json into. Default: ./helper-export.")]
        public string OutputDirectory { get; init; } = "helper-export";

        [CommandOption("--json")] [Description("Emit the per-category summary as JSON instead of a table.")]
        public bool Json { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        // Validate the requested categories before touching ADB so a typo is caught
        // without a device attached.
        var requested = s.Categories.Length > 0
            ? s.Categories.Select(c => c.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList()
            : HelperAppService.Authorities.ToList();

        var unknown = requested.Where(c => !HelperAppService.Authorities.Contains(c)).ToList();
        if (unknown.Count > 0)
        {
            AnsiConsole.MarkupLine(
                $"[red]Unknown categor(y/ies):[/] {Markup.Escape(string.Join(", ", unknown))}. " +
                $"Valid: {Markup.Escape(string.Join(", ", HelperAppService.Authorities))}.");
            return 2;
        }

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

        var permissions = await helper.ProbeRuntimePermissionsAsync(device, ct);
        if (!permissions.CanReadPrivilegedCategories)
            AnsiConsole.MarkupLine("[yellow]SMS, call log and contacts reads will fail: those runtime permissions are not granted. Re-run `phonefork helper install`.[/]");

        var exporter = new HelperExportService(helper, log);
        var results = new List<HelperExportResult>(requested.Count);
        foreach (var category in requested)
        {
            ct.ThrowIfCancellationRequested();
            var outPath = Path.Combine(s.OutputDirectory, HelperExportService.DefaultFileName(category));
            results.Add(await exporter.ExportAsync(device, category, outPath,
                s.Json ? null : new Progress<string>(m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]")),
                ct));
        }

        if (s.Json)
        {
            AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            var table = new Table().AddColumns("Category", "Rows", "Pages", "Output");
            foreach (var r in results)
            {
                table.AddRow(
                    Markup.Escape(r.Authority),
                    r.Success ? r.ItemCount.ToString() : "[red]—[/]",
                    r.Pages.ToString(),
                    Markup.Escape(r.Success ? r.OutputPath ?? "" : r.Error ?? "failed"));
            }
            AnsiConsole.Write(table);
            foreach (var warning in results.SelectMany(r => r.Warnings).Distinct(StringComparer.Ordinal))
                AnsiConsole.MarkupLine($"[yellow]warning:[/] {Markup.Escape(warning)}");
        }

        var receiptPath = await new MigrationReceiptService(log).WriteAsync(
            MigrationReceiptService.Create(
                operation: "helper-export",
                dryRun: false,
                devices: new[] { MigrationReceiptService.Device("source", device) },
                categories: results.Select(r => MigrationReceiptService.Category(
                    $"helper-{r.Authority}",
                    planned: 1,
                    succeeded: r.Success ? 1 : 0,
                    skipped: 0,
                    failed: r.Success ? 0 : 1,
                    failureDetails: r.Success ? null : new[] { r.Error ?? "failed" },
                    warnings: r.Warnings,
                    artifacts: r.OutputPath is null
                        ? null
                        : new[] { new MigrationReceiptArtifact("helper-export", r.OutputPath) })),
                warnings: permissions.CanReadPrivilegedCategories
                    ? null
                    : new[] { "Helper runtime permissions were incomplete; privileged categories could not be read." }),
            ct);
        AnsiConsole.MarkupLine($"[grey]Receipt:[/] {Markup.Escape(receiptPath)}");

        return results.All(r => r.Success) ? 0 : 1;
    }
}

/// <summary>
/// <c>phonefork helper agent</c> — push the app_process agent JAR, invoke one op, and remove it
/// again (F115). The agent runs as the shell user and installs nothing, so it reaches privileges
/// no helper APK can obtain and leaves only the JAR, which this command deletes.
/// </summary>
public sealed class HelperAgentCommand : AsyncCommand<HelperAgentCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Target device serial.")]
        public required string Serial { get; init; }

        [CommandOption("--op <NAME>")] [Description("Agent operation to invoke. Default: ping.")]
        public string Op { get; init; } = "ping";

        [CommandOption("--jar <PATH>")] [Description("Path to phonefork-agent.jar. Defaults to assets/helper/phonefork-agent.jar next to the CLI.")]
        public string? JarPath { get; init; }

        [CommandOption("--keep")] [Description("Leave the agent JAR on the device instead of removing it after the call.")]
        public bool Keep { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var jar = s.JarPath ?? ResolveAgentJar();
        if (!File.Exists(jar))
        {
            AnsiConsole.MarkupLine(
                $"[red]Agent JAR not found:[/] {Markup.Escape(jar)}. " +
                "Build it with `scripts/Build-AgentJar.ps1`, then stage it with `scripts/Stage-HelperApk.ps1`.");
            return 2;
        }

        var (host, _, log) = AdbBootstrap.Initialize();
        var device = host.GetDevices().FirstOrDefault(d => d.Serial == s.Serial);
        if (device is null)
        {
            AnsiConsole.MarkupLine($"[red]Device offline:[/] {Markup.Escape(s.Serial)}");
            return 2;
        }

        var agent = new AppProcessAgentService(host.Client, log);
        try
        {
            AnsiConsole.MarkupLine($"[grey]Pushing {Markup.Escape(Path.GetFileName(jar))} to {Markup.Escape(AppProcessAgentService.RemoteJarPath)}…[/]");
            await agent.PushAgentAsync(device, jar, ct);

            var request = $"{{\"op\":\"{s.Op}\"}}";
            var raw = await agent.InvokeAsync(device, request, ct);
            var json = raw.Trim();

            if (!HelperProviderContract.TryParseEnvelope(json, out var envelope))
            {
                AnsiConsole.MarkupLine("[red]Agent returned an unparseable payload:[/]");
                AnsiConsole.WriteLine(json);
                return 1;
            }

            AnsiConsole.WriteLine(json);
            if (!envelope!.IsOk)
            {
                AnsiConsole.MarkupLine($"[red]Agent op failed:[/] {Markup.Escape(envelope.Error?.Message ?? envelope.Status)}");
                return 1;
            }
            return 0;
        }
        finally
        {
            if (!s.Keep)
            {
                await agent.RemoveAgentAsync(device, ct);
                var residue = await new HelperAppService(host.Client, log).ResidueCheckAsync(device, ct);
                AnsiConsole.MarkupLine(residue.TempFilesLeft.Count == 0
                    ? "[grey]Agent removed; no /data/local/tmp leftovers.[/]"
                    : $"[yellow]Leftovers in /data/local/tmp:[/] {Markup.Escape(string.Join(", ", residue.TempFilesLeft))}");
            }
        }
    }

    internal static string ResolveAgentJar()
    {
        var here = Path.GetDirectoryName(AppContext.BaseDirectory) ?? Environment.CurrentDirectory;
        return Path.Combine(here, "assets", "helper", "phonefork-agent.jar");
    }
}
