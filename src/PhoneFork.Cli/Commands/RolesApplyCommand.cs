using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class RolesApplyCommand : AsyncCommand<RolesApplyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--from <SERIAL>")] [Description("Source device serial; its current role holders are read.")]
        public required string From { get; init; }

        [CommandOption("--to <SERIAL>")] [Description("Destination device serial.")]
        public required string To { get; init; }

        [CommandOption("--dry-run")] [Description("Print what would be assigned; don't write.")]
        public bool DryRun { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var src = devices.FirstOrDefault(d => d.Serial == s.From);
        var dst = devices.FirstOrDefault(d => d.Serial == s.To);
        if (src is null || dst is null) { AnsiConsole.MarkupLine("[red]One or both devices not connected.[/]"); return 1; }

        var svc = new RoleService(host.Client, log);
        var srcSnap = await svc.SnapshotAsync(src, ct: ct);
        var dstSnap = await svc.SnapshotAsync(dst, ct: ct);

        // Build the assignment list: for every role with a holder on source AND a different
        // holder (or none) on dest, queue an apply.
        var dstByRole = dstSnap.Holders.ToDictionary(h => h.Role);
        var queued = new List<(string Role, string Pkg)>();
        var table = new Table().RoundedBorder().AddColumn("Role").AddColumn("Source").AddColumn("Dest").AddColumn("Action");
        foreach (var sh in srcSnap.Holders)
        {
            if (sh.HolderPackage is null) continue;
            dstByRole.TryGetValue(sh.Role, out var dh);
            var dstPkg = dh?.HolderPackage ?? "";
            if (string.Equals(sh.HolderPackage, dstPkg, StringComparison.Ordinal))
            {
                table.AddRow(PhoneFork.Core.Models.DefaultRoles.ShortLabel(sh.Role), Markup.Escape(sh.HolderPackage), Markup.Escape(dstPkg), "[grey]match[/]");
                continue;
            }
            queued.Add((sh.Role, sh.HolderPackage));
            table.AddRow(PhoneFork.Core.Models.DefaultRoles.ShortLabel(sh.Role), Markup.Escape(sh.HolderPackage), Markup.Escape(dstPkg), "[yellow]assign[/]");
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[bold]{queued.Count}[/] assignment(s) to apply.");
        if (s.DryRun) AnsiConsole.MarkupLine("[yellow]Dry-run — no writes.[/]");

        var result = await svc.ApplyAsync(dst, queued, s.DryRun,
            new Progress<string>(m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]")), ct);

        AnsiConsole.MarkupLine($"[green]applied[/] {result.Applied}, [red]failed[/] {result.Failed}.");
        if (result.Failed > 0)
            foreach (var f in result.Failures)
                AnsiConsole.MarkupLine($"  [red]{Markup.Escape(f.Role)}[/] -> {Markup.Escape(f.Pkg)}: {Markup.Escape(f.Error)}");
        return result.Failed == 0 ? 0 : 2;
    }
}
