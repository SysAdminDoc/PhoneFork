using System.ComponentModel;
using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class MediaManifestCommand : AsyncCommand<MediaManifestCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")]
        [Description("Device serial. Defaults to the only connected device when there is one.")]
        public string? Serial { get; init; }

        [CommandOption("--categories <CSV>")]
        [Description("Comma-separated MediaCategory names (e.g. Dcim,Pictures,Music). Default: all.")]
        public string? Categories { get; init; }

        [CommandOption("-o|--out <PATH>")]
        [Description("Output path for the manifest JSON. Default: ./manifest-<serial>-<ts>.json.")]
        public string? Out { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = s.Serial is { Length: > 0 } serial
            ? devices.FirstOrDefault(d => d.Serial == serial)
            : (devices.Count == 1 ? devices[0] : null);
        if (picked is null)
        {
            AnsiConsole.MarkupLine("[red]Specify --device <serial>; multiple devices connected.[/]");
            return 1;
        }

        var cats = ParseCategories(s.Categories);
        var svc = new MediaManifestService(host.Client, log);
        var manifest = await svc.BuildAsync(picked, cats, new Progress<string>(msg => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(msg)}[/]")), ct);

        var outPath = s.Out ?? $"manifest-{picked.Serial}-{DateTime.UtcNow:yyyyMMddTHHmmss}.json";
        await using var fs = File.Create(outPath);
        await JsonSerializer.SerializeAsync(fs, manifest, MediaJson.Options, ct);

        AnsiConsole.MarkupLine($"[green]Wrote {Markup.Escape(outPath)}[/]");
        AnsiConsole.MarkupLine($"[grey]{manifest.TotalFileCount} files, {manifest.TotalSizeBytes / 1024.0 / 1024.0:F1} MiB across {manifest.Categories.Count} categories.[/]");
        return 0;
    }

    internal static List<MediaCategory> ParseCategories(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Enum.GetValues<MediaCategory>().ToList();
        var picked = new List<MediaCategory>();
        foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<MediaCategory>(token, ignoreCase: true, out var c))
                picked.Add(c);
            else
                AnsiConsole.MarkupLine($"[yellow]Unknown category '{Markup.Escape(token)}', skipped.[/]");
        }
        return picked;
    }
}
