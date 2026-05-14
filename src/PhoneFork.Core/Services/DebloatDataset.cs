using System.Reflection;
using System.Text.Json;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Loads the AppManagerNG / UAD-NG debloat dataset from embedded JSON. 5 files (oem, google,
/// carrier, aosp, misc); ~5,481 entries total at v0.4.0.
/// </summary>
public sealed class DebloatDataset
{
    private static readonly string[] FileNames = { "oem.json", "google.json", "carrier.json", "aosp.json", "misc.json" };

    public IReadOnlyDictionary<string, DebloatEntry> ByPackageId { get; }
    public IReadOnlyList<DebloatEntry> Entries { get; }

    private DebloatDataset(IReadOnlyList<DebloatEntry> entries)
    {
        Entries = entries;
        var dict = new Dictionary<string, DebloatEntry>(entries.Count, StringComparer.Ordinal);
        foreach (var e in entries) dict.TryAdd(e.PackageId, e);
        ByPackageId = dict;
    }

    public static DebloatDataset Load(ILogger? log = null)
    {
        var asm = Assembly.GetExecutingAssembly();
        var entries = new List<DebloatEntry>(6_000);
        foreach (var fname in FileNames)
        {
            var resource = $"PhoneFork.Core.Assets.Debloat.{fname}";
            using var stream = asm.GetManifestResourceStream(resource);
            if (stream is null)
            {
                log?.Warning("Debloat dataset resource missing: {Resource}", resource);
                continue;
            }
            var rows = JsonSerializer.Deserialize<List<DebloatEntry>>(stream)
                       ?? new List<DebloatEntry>();
            var list = fname.Replace(".json", "") switch
            {
                "oem"     => DebloatList.Oem,
                "google"  => DebloatList.Google,
                "carrier" => DebloatList.Carrier,
                "aosp"    => DebloatList.Aosp,
                "misc"    => DebloatList.Misc,
                _         => DebloatList.Misc,
            };
            foreach (var r in rows) entries.Add(r with { List = list });
            log?.Information("Loaded {Count} entries from {File}", rows.Count, fname);
        }
        return new DebloatDataset(entries);
    }
}
