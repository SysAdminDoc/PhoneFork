using System.ComponentModel;
using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class BackupInspectCommand : AsyncCommand<BackupInspectCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")]
        [Description("AppManager backup directory, backup root, Open Android Backup directory, or legacy .ab file.")]
        public required string Path { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON instead of tables.")]
        public bool Json { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var (_, _, log) = AdbBootstrap.Initialize();
        var findings = new List<object>();
        var path = settings.Path;

        if (File.Exists(path))
        {
            var ab = new AndroidBackupReader(log).Sniff(path);
            if (ab is null)
            {
                AnsiConsole.MarkupLine("[yellow]No known backup format detected.[/]");
                return 1;
            }

            findings.Add(new
            {
                Format = "android-ab",
                File = System.IO.Path.GetFullPath(path),
                ab.FormatVersion,
                ab.Compressed,
                ab.EncryptionTag,
                ab.HasEncryptionKeyBlock,
            });
        }
        else if (Directory.Exists(path))
        {
            var reader = new AppManagerBackupReader(log);
            foreach (var dir in reader.EnumerateBackupDirs(path).DefaultIfEmpty(path).Distinct())
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(System.IO.Path.Combine(dir, "meta.am.v5"))) continue;
                try
                {
                    var handle = await reader.ReadAsync(dir, ct);
                    findings.Add(new
                    {
                        Format = "appmanager-v5",
                        Directory = handle.Directory,
                        handle.Meta.PackageName,
                        handle.Meta.VersionName,
                        handle.Meta.VersionCode,
                        ApkCount = handle.Meta.Apks.Count,
                        ChecksumCount = handle.ChecksumsByFileName.Count,
                        BackupTime = handle.BackupTime,
                        handle.Meta.Flags,
                    });
                }
                catch (Exception ex)
                {
                    findings.Add(new { Format = "appmanager-v5", Directory = dir, Error = ex.Message });
                }
            }

            var oab = new OpenAndroidBackupReader(log).Sniff(path);
            if (oab is not null)
            {
                findings.Add(new
                {
                    Format = "open-android-backup",
                    oab.ArchivePath,
                    oab.ArchiveSizeBytes,
                    oab.HasSidecar,
                });
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Path not found:[/] {Markup.Escape(path)}");
            return 1;
        }

        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(findings, new JsonSerializerOptions { WriteIndented = true }));
            return findings.Count == 0 ? 1 : 0;
        }

        if (findings.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No known backup format detected.[/]");
            return 1;
        }

        var table = new Table().RoundedBorder().AddColumn("Format").AddColumn("Item").AddColumn("Details");
        foreach (var item in findings)
        {
            var json = JsonSerializer.Serialize(item);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var format = root.GetProperty("Format").GetString() ?? "unknown";
            var name = root.TryGetProperty("PackageName", out var pkg)
                ? pkg.GetString() ?? ""
                : root.TryGetProperty("ArchivePath", out var archive)
                    ? System.IO.Path.GetFileName(archive.GetString()) ?? ""
                    : root.TryGetProperty("File", out var file)
                        ? System.IO.Path.GetFileName(file.GetString()) ?? ""
                        : root.TryGetProperty("Directory", out var dir)
                            ? dir.GetString() ?? ""
                            : "";
            table.AddRow(Markup.Escape(format), Markup.Escape(name), Markup.Escape(json ?? ""));
        }
        AnsiConsole.Write(table);
        return 0;
    }
}

public sealed class BackupExportAppManagerCommand : AsyncCommand<BackupExportAppManagerCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")]
        [Description("Source device serial.")]
        public required string Serial { get; init; }

        [CommandOption("-o|--out <PATH>")]
        [Description("Backup root directory.")]
        public required string Out { get; init; }

        [CommandOption("--package <PKG>")]
        [Description("Export only specific packages. Repeatable.")]
        public string[] Packages { get; init; } = Array.Empty<string>();
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var device = host.GetDevices().FirstOrDefault(d => d.Serial == settings.Serial);
        if (device is null)
        {
            AnsiConsole.MarkupLine($"[red]Device not connected:[/] {Markup.Escape(settings.Serial)}");
            return 1;
        }

        var catalog = new AppCatalogService(host.Client, log);
        var installer = new AppInstallerService(host.Client, log);
        var writer = new AppManagerBackupWriter(log);
        var apps = await catalog.EnumerateUserAppsAsync(device, ct);
        if (settings.Packages.Length > 0)
        {
            var wanted = settings.Packages.ToHashSet(StringComparer.Ordinal);
            apps = apps.Where(a => wanted.Contains(a.PackageName)).ToArray();
        }

        Directory.CreateDirectory(settings.Out);
        var written = new List<string>();
        foreach (var app in apps.OrderBy(a => a.PackageName))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                AnsiConsole.MarkupLine($"[grey]Pulling APKs for[/] {Markup.Escape(app.PackageName)}");
                var files = await installer.PullApksToCacheAsync(device, app, progress: null, ct);
                var dir = await writer.WriteAsync(settings.Out, device.Serial, app, files, ToolVersion(), ct);
                written.Add(dir);
                AnsiConsole.MarkupLine($"[green]exported[/] {Markup.Escape(app.PackageName)} -> {Markup.Escape(dir)}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]export failed[/] {Markup.Escape(app.PackageName)}: {Markup.Escape(ex.Message)}");
            }
        }

        AnsiConsole.MarkupLine($"[bold]{written.Count} AppManager-compatible backup(s) written.[/]");
        return written.Count == apps.Count ? 0 : 2;
    }

    private static string ToolVersion()
    {
        var assemblyVersion = typeof(BackupExportAppManagerCommand).Assembly.GetName().Version?.ToString();
        return string.IsNullOrWhiteSpace(assemblyVersion) ? "phonefork" : $"phonefork-cli/{assemblyVersion}";
    }
}

public sealed class BackupInstallAppManagerCommand : AsyncCommand<BackupInstallAppManagerCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--to <SERIAL>")]
        [Description("Destination device serial.")]
        public required string To { get; init; }

        [CommandOption("--backup <PATH>")]
        [Description("Single AppManager-compatible backup directory containing meta.am.v5.")]
        public required string Backup { get; init; }

        [CommandOption("--reinstall")]
        [Description("Pass -r when installing the APK set.")]
        public bool Reinstall { get; init; }

        [CommandOption("--dry-run")]
        [Description("Verify checksums and print the install plan without installing.")]
        public bool DryRun { get; init; }

        [CommandOption("--allow-multi-user")]
        [Description("Proceed even when the destination has work profiles or secondary users. PhoneFork still targets Android user 0 only.")]
        public bool AllowMultiUser { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var destination = host.GetDevices().FirstOrDefault(d => d.Serial == settings.To);
        if (destination is null)
        {
            AnsiConsole.MarkupLine($"[red]Destination not connected:[/] {Markup.Escape(settings.To)}");
            return 1;
        }

        var reader = new AppManagerBackupReader(log);
        var handle = await reader.ReadAsync(settings.Backup, ct);
        var localApks = handle.ResolveApkPaths();
        var table = new Table().RoundedBorder().AddColumn("Package").AddColumn("Version").AddColumn("APKs").AddColumn("Mode");
        table.AddRow(
            Markup.Escape(handle.Meta.PackageName),
            Markup.Escape(handle.Meta.VersionName ?? ""),
            localApks.Count.ToString(),
            settings.DryRun ? "verify only" : "install");
        AnsiConsole.Write(table);

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[green]Backup checksums verified. Install skipped.[/]");
            return 0;
        }

        var app = new AppInfo
        {
            PackageName = handle.Meta.PackageName,
            Label = handle.Meta.PackageName,
            VersionName = handle.Meta.VersionName ?? "",
            VersionCode = handle.Meta.VersionCode ?? 0,
            RemoteApkPaths = Array.Empty<string>(),
            TotalSizeBytes = handle.Meta.Apks.Sum(a => a.SizeBytes),
            IsSystem = false,
        };

        var installer = new AppInstallerService(host.Client, log);
        var result = await installer.InstallLocalBackupAsync(destination, app, localApks, settings.Reinstall, progress: null, ct,
            allowMultiUser: settings.AllowMultiUser);
        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]installed[/] {Markup.Escape(result.PackageName)} ({result.Duration.TotalSeconds:F1}s)");
            return 0;
        }

        AnsiConsole.MarkupLine($"[red]install failed[/] {Markup.Escape(result.PackageName)}: {Markup.Escape(result.Error ?? "(unknown)")}");
        return 2;
    }
}
