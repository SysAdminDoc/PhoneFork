using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class PermsGrantCommand : AsyncCommand<PermsGrantCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Device serial.")]
        public required string Serial { get; init; }

        [CommandOption("-p|--package <PKG>")] [Description("Target package.")]
        public required string Package { get; init; }

        [CommandOption("--permission <PERM>")] [Description("Runtime permission to grant (e.g. android.permission.READ_CONTACTS). Repeatable.")]
        public string[] Permissions { get; init; } = Array.Empty<string>();

        [CommandOption("--appop <OP>")] [Description("Format: OP_NAME=mode (e.g. SYSTEM_ALERT_WINDOW=allow). Repeatable.")]
        public string[] AppOps { get; init; } = Array.Empty<string>();
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = devices.FirstOrDefault(d => d.Serial == s.Serial);
        if (picked is null) { AnsiConsole.MarkupLine($"[red]Device {Markup.Escape(s.Serial)} not connected.[/]"); return 1; }
        if (!AdbShell.IsPackageName(s.Package))
        {
            AnsiConsole.MarkupLine($"[red]Invalid Android package id:[/] {Markup.Escape(s.Package)}");
            return 1;
        }
        if (s.Permissions.Length == 0 && s.AppOps.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nothing to grant. Provide --permission or --appop.[/]");
            return 1;
        }

        var svc = new RoleService(host.Client, log);
        int ok = 0, fail = 0;
        foreach (var perm in s.Permissions)
        {
            var output = await svc.GrantAsync(picked, s.Package, perm, ct);
            if (output.Contains("Exception", StringComparison.Ordinal)) { fail++; AnsiConsole.MarkupLine($"[red]grant fail[/] {Markup.Escape(perm)}: {Markup.Escape(output.Trim())}"); }
            else { ok++; AnsiConsole.MarkupLine($"[green]grant ok[/] {Markup.Escape(perm)}"); }
        }
        foreach (var spec in s.AppOps)
        {
            var eq = spec.IndexOf('=');
            if (eq <= 0) { AnsiConsole.MarkupLine($"[yellow]Invalid --appop {Markup.Escape(spec)}, expected OP=mode.[/]"); continue; }
            var op = spec[..eq];
            var mode = spec[(eq + 1)..];
            var output = await svc.SetAppOpAsync(picked, s.Package, op, mode, ct);
            if (output.Contains("Exception", StringComparison.Ordinal)) { fail++; AnsiConsole.MarkupLine($"[red]appop fail[/] {Markup.Escape(spec)}: {Markup.Escape(output.Trim())}"); }
            else { ok++; AnsiConsole.MarkupLine($"[green]appop ok[/] {Markup.Escape(spec)}"); }
        }
        AnsiConsole.MarkupLine($"[bold]{ok}[/] succeeded, [red]{fail}[/] failed.");
        return fail == 0 ? 0 : 2;
    }
}
