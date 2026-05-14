using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class DevicesCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var (_, devices, _) = AdbBootstrap.Initialize();
        var phones = devices.Phones;

        if (phones.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No ADB devices detected.[/]");
            AnsiConsole.MarkupLine("Plug your phones in and ensure USB debugging is authorized.");
            return 0;
        }

        var table = new Table().RoundedBorder()
            .AddColumn("Serial").AddColumn("Device").AddColumn("Android").AddColumn("One UI").AddColumn("Status");

        foreach (var p in phones)
        {
            table.AddRow(
                Markup.Escape(p.Serial),
                Markup.Escape(p.DisplayName),
                Markup.Escape(p.AndroidVersion),
                Markup.Escape(p.FormattedOneUiVersion),
                p.IsAuthorized ? "[green]ready[/]" : "[red]unauthorized[/]");
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
