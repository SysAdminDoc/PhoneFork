using PhoneFork.Core.Models;

namespace PhoneFork.Core.Services;

public enum SettingsDiffOutcome
{
    /// <summary>Key exists only on source.</summary>
    OnlyOnSource,
    /// <summary>Key exists only on destination.</summary>
    OnlyOnDest,
    /// <summary>Key exists on both, identical value.</summary>
    Same,
    /// <summary>Key exists on both, different value.</summary>
    Different,
}

public sealed record SettingsDiffEntry(
    SettingsNamespace Namespace,
    string Key,
    SettingsDiffOutcome Outcome,
    string? SourceValue,
    string? DestValue);

public sealed record SettingsDiffNamespace(
    SettingsNamespace Namespace,
    IReadOnlyList<SettingsDiffEntry> Entries)
{
    public int Count(SettingsDiffOutcome o) => Entries.Count(e => e.Outcome == o);
}

public sealed record SettingsPlan(
    string SourceSerial,
    string DestSerial,
    IReadOnlyList<SettingsDiffNamespace> Namespaces)
{
    public int TotalApplicable =>
        Namespaces.Sum(d => d.Count(SettingsDiffOutcome.Different) + d.Count(SettingsDiffOutcome.OnlyOnSource));
}

public static class SettingsDiffer
{
    public static SettingsPlan Build(SettingsSnapshot source, SettingsSnapshot dest)
    {
        var srcByNs = source.Namespaces
            .GroupBy(s => s.Namespace)
            .ToDictionary(g => g.Key, g => g.Last());
        var dstByNs = dest.Namespaces
            .GroupBy(s => s.Namespace)
            .ToDictionary(g => g.Key, g => g.Last());
        var allNs = srcByNs.Keys.Union(dstByNs.Keys).OrderBy(n => (int)n).ToList();
        var nsDiffs = new List<SettingsDiffNamespace>();

        foreach (var ns in allNs)
        {
            var src = srcByNs.TryGetValue(ns, out var s) ? s.Values : new Dictionary<string, string>();
            var dst = dstByNs.TryGetValue(ns, out var d) ? d.Values : new Dictionary<string, string>();
            var entries = new List<SettingsDiffEntry>(src.Count + dst.Count);

            foreach (var (key, sv) in src)
            {
                if (!dst.TryGetValue(key, out var dv))
                    entries.Add(new SettingsDiffEntry(ns, key, SettingsDiffOutcome.OnlyOnSource, sv, null));
                else if (string.Equals(sv, dv, StringComparison.Ordinal))
                    entries.Add(new SettingsDiffEntry(ns, key, SettingsDiffOutcome.Same, sv, dv));
                else
                    entries.Add(new SettingsDiffEntry(ns, key, SettingsDiffOutcome.Different, sv, dv));
            }
            foreach (var (key, dv) in dst)
            {
                if (!src.ContainsKey(key))
                    entries.Add(new SettingsDiffEntry(ns, key, SettingsDiffOutcome.OnlyOnDest, null, dv));
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            nsDiffs.Add(new SettingsDiffNamespace(ns, entries));
        }

        return new SettingsPlan(source.DeviceSerial, dest.DeviceSerial, nsDiffs);
    }
}
