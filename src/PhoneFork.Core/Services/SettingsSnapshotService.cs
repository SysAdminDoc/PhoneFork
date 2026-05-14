using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Captures <c>settings list secure|system|global</c> from a device, parsing each <c>key=value</c>
/// line into a <see cref="SettingsSnapshot"/>.
/// </summary>
public sealed class SettingsSnapshotService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public SettingsSnapshotService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<SettingsSnapshotService>();
    }

    public async Task<SettingsSnapshot> CaptureAsync(
        DeviceData device,
        IEnumerable<SettingsNamespace>? namespaces = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var ns = namespaces?.ToArray() ?? new[] { SettingsNamespace.Secure, SettingsNamespace.System, SettingsNamespace.Global };
        var captured = new List<SettingsNamespaceSnapshot>();
        foreach (var n in ns)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Capturing {n}…");
            var values = await CaptureNamespaceAsync(device, n, ct);
            captured.Add(new SettingsNamespaceSnapshot { Namespace = n, Values = values });
            _log.Information("Settings {Ns} on {Serial}: {Count} keys", n, device.Serial, values.Count);
        }
        return new SettingsSnapshot
        {
            DeviceSerial = device.Serial,
            CapturedAt = DateTimeOffset.UtcNow,
            Namespaces = captured,
        };
    }

    public async Task<IReadOnlyDictionary<string, string>> CaptureNamespaceAsync(
        DeviceData device,
        SettingsNamespace ns,
        CancellationToken ct = default)
    {
        var nsName = ns.ToString().ToLowerInvariant();
        var output = await _client.ShellAsync(device, $"settings list {nsName}", ct);
        var dict = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in (output ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd('\r');
            // Values may legitimately contain '=' (e.g. JSON blobs). Split on first '=' only.
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq];
            var val = line[(eq + 1)..];
            // Drop the lone "null" sentinel that AOSP prints for unset keys.
            if (val == "null") val = "";
            dict[key] = val;
        }
        return dict;
    }
}
