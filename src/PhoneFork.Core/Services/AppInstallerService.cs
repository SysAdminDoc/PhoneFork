using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// One-shot migration outcome reported back to the UI/CLI layer per package.
/// </summary>
public sealed record InstallResult(string PackageName, bool Success, string? Error, TimeSpan Duration);

/// <summary>
/// Pulls split APKs from the source device to a local cache, then drives
/// <c>pm install-create / -write / -commit</c> on the destination via
/// <see cref="PackageManager.InstallMultiplePackageAsync"/>. Auto-attribution to Play Store.
/// </summary>
public sealed class AppInstallerService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;
    private readonly string _cacheRoot;

    public AppInstallerService(IAdbClient client, ILogger log, string? cacheRoot = null)
    {
        _client = client;
        _log = log.ForContext<AppInstallerService>();
        _cacheRoot = cacheRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhoneFork", "cache");
        Directory.CreateDirectory(_cacheRoot);
    }

    public string CacheRoot => _cacheRoot;

    public async Task<InstallResult> MigrateAsync(
        DeviceData source,
        DeviceData destination,
        AppInfo app,
        bool reinstall,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // 1) Pull every split APK to the local cache.
            var localFiles = await PullApksToCacheAsync(source, app, progress, ct);

            // 2) install-multiple on destination with Play-Store attribution.
            progress?.Report($"Installing {app.PackageName}…");
            var basePath = localFiles.FirstOrDefault(f => Path.GetFileName(f).Equals("base.apk", StringComparison.OrdinalIgnoreCase))
                          ?? localFiles[0];
            var splits = localFiles.Where(f => !f.Equals(basePath, StringComparison.OrdinalIgnoreCase)).ToList();

            var pm = new PackageManager(_client, destination);

            // Arguments mirror `pm install-create` flags:
            //   --user 0                 — primary user
            //   -i com.android.vending   — installer attribution: appears as Play-installed
            //   --install-reason 4       — DEVICE_RESTORE; surfaces a friendlier setup toast
            //   -g                       — auto-grant all runtime permissions declared in manifest
            //   -r                       — reinstall an existing app (only set when reinstall=true)
            var args = new List<string>
            {
                "--user", "0",
                "-i", "com.android.vending",
                "--install-reason", "4",
                "-g",
            };
            if (reinstall) args.Insert(0, "-r");

            await pm.InstallMultiplePackageAsync(
                basePackageFilePath: basePath,
                splitPackageFilePaths: splits,
                progress: null,
                cancellationToken: ct,
                arguments: args.ToArray());

            sw.Stop();
            _log.Information("Migrated {Pkg} ({Splits} splits, {Bytes} bytes) in {Ms} ms",
                app.PackageName, splits.Count + 1, app.TotalSizeBytes, sw.ElapsedMilliseconds);
            return new InstallResult(app.PackageName, true, null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.Error(ex, "Migrate {Pkg} failed", app.PackageName);
            return new InstallResult(app.PackageName, false, ex.Message, sw.Elapsed);
        }
    }

    public async Task<IReadOnlyList<string>> PullApksToCacheAsync(
        DeviceData source,
        AppInfo app,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (!AdbShell.IsPackageName(app.PackageName))
            throw new ArgumentException($"Invalid Android package identifier: {app.PackageName}", nameof(app));

        var pkgCache = Path.Combine(
            _cacheRoot,
            LocalPathNames.SafeFileName(source.Serial, "device"),
            LocalPathNames.SafeFileName(app.PackageName, "package"));
        Directory.CreateDirectory(pkgCache);

        var localFiles = new List<string>(app.RemoteApkPaths.Count);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < app.RemoteApkPaths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var remote = app.RemoteApkPaths[i];
            var rawName = remote.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            var name = LocalPathNames.SafeFileName(rawName, $"split-{i + 1}.apk");
            if (!name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                name += ".apk";

            var candidate = name;
            var suffix = i + 1;
            while (!usedNames.Add(candidate))
            {
                candidate = $"{Path.GetFileNameWithoutExtension(name)}-{suffix}{Path.GetExtension(name)}";
                suffix++;
            }
            name = candidate;

            var local = Path.Combine(pkgCache, name);
            progress?.Report($"Pulling {app.PackageName}/{name}...");
            _log.Information("Pull {Pkg} {Remote} -> {Local}", app.PackageName, remote, local);
            await PullAsync(source, remote, local, ct);
            localFiles.Add(local);
        }

        if (localFiles.Count == 0)
            throw new InvalidOperationException($"No APK paths were available for {app.PackageName}.");

        return localFiles;
    }

    private async Task PullAsync(DeviceData device, string remote, string local, CancellationToken ct)
    {
        using var sync = new SyncService(_client, device);
        var temp = local + ".tmp";
        try
        {
            await using (var fs = File.Create(temp))
                await sync.PullAsync(remote, fs, callback: null, useV2: false, cancellationToken: ct);
            File.Move(temp, local, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { }
            }
        }
    }
}
