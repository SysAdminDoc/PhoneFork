using System.ComponentModel;
using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class MediaDiffCommand : AsyncCommand<MediaDiffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--src <PATH>")]
        [Description("Source manifest JSON path.")]
        public required string SrcManifest { get; init; }

        [CommandOption("--dst <PATH>")]
        [Description("Destination manifest JSON path.")]
        public required string DstManifest { get; init; }

        [CommandOption("-o|--out <PATH>")]
        [Description("Output path for the migration plan JSON.")]
        public string? Out { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var opts = MediaJson.Options;
        var src = await ReadAsync(s.SrcManifest, opts, ct);
        var dst = await ReadAsync(s.DstManifest, opts, ct);
        if (src is null || dst is null)
        {
            AnsiConsole.MarkupLine("[red]Could not read both manifests.[/]");
            return 1;
        }
        var plan = MediaDiffer.Build(src, dst);

        var table = new Table().RoundedBorder()
            .AddColumn("Category").AddColumn("New on src").AddColumn("Conflicts").AddColumn("Identical").AddColumn("Only on dst").AddColumn("MiB to xfer");
        foreach (var cd in plan.CategoryDiffs)
        {
            table.AddRow(
                cd.Category.Label(),
                cd.Count(MediaDiffOutcome.NewOnSource).ToString(),
                cd.Count(MediaDiffOutcome.Conflict).ToString(),
                cd.Count(MediaDiffOutcome.Identical).ToString(),
                cd.Count(MediaDiffOutcome.NewOnDest).ToString(),
                $"{cd.BytesToTransfer / 1024.0 / 1024.0:F1}");
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[bold]{plan.TotalFilesToTransfer}[/] files / [bold]{plan.TotalBytesToTransfer / 1024.0 / 1024.0:F1} MiB[/] would be transferred. [yellow]{plan.TotalConflicts} conflicts.[/]");

        if (s.Out is { Length: > 0 })
        {
            await using var fs = File.Create(s.Out);
            await JsonSerializer.SerializeAsync(fs, plan, opts, ct);
            AnsiConsole.MarkupLine($"[green]Plan saved -> {Markup.Escape(s.Out)}[/]");
        }
        return 0;
    }

    private static async Task<MediaManifest?> ReadAsync(string path, JsonSerializerOptions opts, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]Not found:[/] {Markup.Escape(path)}");
            return null;
        }
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<MediaManifest>(fs, opts, ct);
    }
}
