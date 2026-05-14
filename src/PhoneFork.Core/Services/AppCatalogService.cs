using System.Text.RegularExpressions;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Enumerates third-party (<c>pm list packages -3 -f</c>) applications on a phone with their split-APK paths.
/// Label is best-effort from <c>dumpsys package</c>; falls back to the package name.
/// </summary>
public sealed class AppCatalogService
{
    private static readonly Regex PkgLine = new(
        @"^package:(?<path>.+?\.apk)=(?<pkg>[A-Za-z0-9_.]+)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex VersionName = new(
        @"versionName=(?<v>[^\s]+)",
        RegexOptions.Compiled);

    private static readonly Regex VersionCode = new(
        @"versionCode=(?<v>\d+)",
        RegexOptions.Compiled);

    private static readonly Regex AppLabel = new(
        @"^\s*nonLocalizedLabel=(?<label>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public AppCatalogService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<AppCatalogService>();
    }

    public async Task<IReadOnlyList<AppInfo>> EnumerateUserAppsAsync(DeviceData device, CancellationToken ct = default)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));

        var listOut = await _client.ShellAsync(device, "pm list packages -3 -f", ct);
        var entries = PkgLine.Matches(listOut ?? "")
            .Select(m => (Pkg: m.Groups["pkg"].Value, BasePath: m.Groups["path"].Value))
            .ToList();

        var apps = new List<AppInfo>(entries.Count);
        foreach (var (pkg, basePath) in entries)
        {
            ct.ThrowIfCancellationRequested();

            // pm path returns one or more package:/path/foo.apk lines (base + splits).
            var pkgArg = AdbShell.PackageArg(pkg);
            var pathsOut = await _client.ShellAsync(device, $"pm path {pkgArg}", ct);
            var remotePaths = (pathsOut ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.StartsWith("package:", StringComparison.Ordinal))
                .Select(s => s["package:".Length..].Trim())
                .ToList();

            if (remotePaths.Count == 0)
                remotePaths.Add(basePath);

            // Best-effort metadata + label via dumpsys.
            string label = pkg;
            string versionName = "";
            long versionCode = 0;
            long totalSize = 0;

            try
            {
                var dump = await _client.ShellAsync(device, $"dumpsys package {pkgArg}", ct);
                if (!string.IsNullOrEmpty(dump))
                {
                    var lm = AppLabel.Match(dump);
                    if (lm.Success)
                    {
                        var candidate = lm.Groups["label"].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(candidate) && !candidate.StartsWith("@"))
                            label = candidate;
                    }
                    var vm = VersionName.Match(dump);
                    if (vm.Success) versionName = vm.Groups["v"].Value;
                    var vc = VersionCode.Match(dump);
                    if (vc.Success && long.TryParse(vc.Groups["v"].Value, out var n)) versionCode = n;
                }

                // Best-effort total size via `stat -c %s` per split (cheap on shell).
                foreach (var p in remotePaths)
                {
                    var sz = await _client.ShellAsync(device, $"stat -c %s {AdbShell.Arg(p)}", ct);
                    if (long.TryParse((sz ?? "").Trim(), out var n)) totalSize += n;
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Metadata fetch failed for {Pkg}", pkg);
            }

            apps.Add(new AppInfo
            {
                PackageName = pkg,
                Label = label,
                VersionName = versionName,
                VersionCode = versionCode,
                RemoteApkPaths = remotePaths,
                TotalSizeBytes = totalSize,
                IsSystem = false,
            });
        }

        _log.Information("Enumerated {Count} user apps on {Serial}", apps.Count, device.Serial);
        return apps;
    }
}
