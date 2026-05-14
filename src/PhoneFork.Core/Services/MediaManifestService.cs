using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Builds a <see cref="MediaManifest"/> by enumerating each requested category subtree on the device
/// with a single <c>find -printf</c> shell call, then parsing path/size/mtime tab-separated lines.
/// </summary>
public sealed class MediaManifestService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public MediaManifestService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<MediaManifestService>();
    }

    public async Task<MediaManifest> BuildAsync(
        DeviceData device,
        IEnumerable<MediaCategory> categories,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var cats = new List<MediaCategoryManifest>();
        foreach (var cat in categories)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Manifesting {cat.Label()}…");
            var manifest = await BuildCategoryAsync(device, cat, ct);
            cats.Add(manifest);
            _log.Information("Manifest {Cat} on {Serial}: {Files} files, {Bytes} bytes",
                cat, device.Serial, manifest.FileCount, manifest.TotalSizeBytes);
        }
        return new MediaManifest
        {
            DeviceSerial = device.Serial,
            CapturedAt = DateTimeOffset.UtcNow,
            Categories = cats,
        };
    }

    public async Task<MediaCategoryManifest> BuildCategoryAsync(
        DeviceData device,
        MediaCategory cat,
        CancellationToken ct = default)
    {
        var root = cat.RemotePath();
        // First confirm the category root exists; missing roots are normal (e.g. WhatsApp on phones
        // without it installed). Skip cleanly with an empty manifest.
        var probe = await _client.ShellAsync(device, $"[ -d \"{root}\" ] && echo Y || echo N", ct);
        if (!probe.Trim().StartsWith("Y", StringComparison.Ordinal))
        {
            return new MediaCategoryManifest
            {
                Category = cat,
                RemoteRoot = root,
                Files = Array.Empty<MediaFile>(),
            };
        }

        // find -printf is available on Toybox (Android 6+). %P is path-relative-to-arg, %s is bytes,
        // %T@ is mtime as epoch.fractional. Tab-separated for trivial split.
        var cmd = $"find \"{root}\" -type f -printf '%P\\t%s\\t%T@\\n'";
        var output = await _client.ShellAsync(device, cmd, ct);

        var files = new List<MediaFile>();
        foreach (var line in (output ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.TrimEnd('\r').Split('\t');
            if (parts.Length < 3) continue;
            if (!long.TryParse(parts[1], out var size)) continue;
            // Mtime: take integer seconds only.
            var dotIdx = parts[2].IndexOf('.');
            var mtimeStr = dotIdx >= 0 ? parts[2][..dotIdx] : parts[2];
            if (!long.TryParse(mtimeStr, out var mtime)) continue;
            files.Add(new MediaFile
            {
                RelPath = parts[0].Replace('\\', '/'),
                SizeBytes = size,
                Mtime = mtime,
            });
        }
        files.Sort((a, b) => string.CompareOrdinal(a.RelPath, b.RelPath));
        return new MediaCategoryManifest
        {
            Category = cat,
            RemoteRoot = root,
            Files = files,
        };
    }
}
