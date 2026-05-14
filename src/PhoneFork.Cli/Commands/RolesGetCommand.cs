using System.ComponentModel;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class RolesGetCommand : AsyncCommand<RolesGetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Device serial.")]
        public string? Serial { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = s.Serial is { Length: > 0 } x
            ? devices.FirstOrDefault(d => d.Serial == x)
            : (devices.Count == 1 ? devices[0] : null);
        if (picked is null) { AnsiConsole.MarkupLine("[red]Specify --device <serial>.[/]"); return 1; }

        var svc = new RoleService(host.Client, log);
        var snap = await svc.SnapshotAsync(picked, ct: ct);

        var table = new Table().RoundedBorder().AddColumn("Role").AddColumn("Holder package");
        foreach (var h in snap.Holders)
            table.AddRow(DefaultRoles.ShortLabel(h.Role), h.HolderPackage is null ? "[grey]<none>[/]" : Markup.Escape(h.HolderPackage));
        AnsiConsole.Write(table);
        return 0;
    }
}
