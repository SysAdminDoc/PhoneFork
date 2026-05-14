using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Intersection of <see cref="DebloatDataset"/> against a device's actually-installed packages.
/// </summary>
public sealed record DebloatCandidate(
    DebloatEntry Entry,
    bool IsEnabled);

public sealed class DebloatScanner
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;
    private readonly DebloatDataset _dataset;

    public DebloatScanner(IAdbClient client, ILogger log, DebloatDataset dataset)
    {
        _client = client;
        _log = log.ForContext<DebloatScanner>();
        _dataset = dataset;
    }

    public async Task<IReadOnlyList<DebloatCandidate>> ScanAsync(DeviceData device, CancellationToken ct = default)
    {
        // Enabled system packages.
        var enabledOut = await _client.ShellAsync(device, "pm list packages -s -e", ct);
        var enabled = ParsePmList(enabledOut);
        // Disabled (by user) system packages — we still want to surface so user can rollback.
        var disabledOut = await _client.ShellAsync(device, "pm list packages -s -d", ct);
        var disabled = ParsePmList(disabledOut);

        var installed = new HashSet<string>(enabled.Count + disabled.Count, StringComparer.Ordinal);
        installed.UnionWith(enabled);
        installed.UnionWith(disabled);

        var candidates = new List<DebloatCandidate>(installed.Count);
        foreach (var pkg in installed)
        {
            if (_dataset.ByPackageId.TryGetValue(pkg, out var entry))
                candidates.Add(new DebloatCandidate(entry, IsEnabled: enabled.Contains(pkg)));
        }
        _log.Information("Debloat scan {Serial}: {Installed} installed system packages, {Matched} matched dataset.",
            device.Serial, installed.Count, candidates.Count);
        return candidates;
    }

    private static HashSet<string> ParsePmList(string? output)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in (output ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimEnd('\r').Trim();
            if (trimmed.StartsWith("package:", StringComparison.Ordinal))
                set.Add(trimmed["package:".Length..]);
        }
        return set;
    }
}
