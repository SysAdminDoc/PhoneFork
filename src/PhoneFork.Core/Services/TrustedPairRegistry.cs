using System.Text.Json;
using System.Text.Json.Serialization;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// A trusted-pair entry persisted across runs. Raw serials are not stored — only their
/// <see cref="SerialHash"/>. The display label is a user-friendly Manufacturer Model
/// suffix; the last-seen endpoint is kept for the mDNS reconnect surface (F005).
/// </summary>
public sealed record TrustedPair(
    [property: JsonPropertyName("hash")] string SerialHashValue,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("transport")] AdbTransport Transport,
    [property: JsonPropertyName("firstSeen")] DateTimeOffset FirstSeen,
    [property: JsonPropertyName("lastSeen")] DateTimeOffset LastSeen,
    [property: JsonPropertyName("lastEndpoint")] string? LastEndpoint);

/// <summary>
/// Local JSON registry of devices the user has previously authorized for PhoneFork
/// (F004). Lives at <c>%LOCALAPPDATA%\PhoneFork\trusted-pairs.json</c>. The file
/// contains only SHA-256-prefixed serials, never raw hardware IDs.
/// </summary>
public sealed class TrustedPairRegistry
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _sync = new();
    private readonly Dictionary<string, TrustedPair> _byHash = new();
    private readonly string _filePath;
    private readonly ILogger _log;

    public TrustedPairRegistry(string filePath, ILogger log)
    {
        _filePath = filePath;
        _log = log.ForContext<TrustedPairRegistry>();
        Load();
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhoneFork", "trusted-pairs.json");

    public IReadOnlyCollection<TrustedPair> All
    {
        get { lock (_sync) return _byHash.Values.OrderByDescending(p => p.LastSeen).ToArray(); }
    }

    public bool IsTrusted(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return false;
        lock (_sync) return _byHash.ContainsKey(SerialHash.Of(serial));
    }

    public TrustedPair? Get(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return null;
        lock (_sync)
            return _byHash.TryGetValue(SerialHash.Of(serial), out var p) ? p : null;
    }

    /// <summary>
    /// Insert or refresh a record for the given serial. Returns the canonical entry.
    /// </summary>
    public TrustedPair Touch(string serial, string label, AdbTransport transport, string? lastEndpoint = null)
    {
        if (string.IsNullOrWhiteSpace(serial))
            throw new ArgumentException("serial required", nameof(serial));

        var hash = SerialHash.Of(serial);
        TrustedPair entry;
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (_byHash.TryGetValue(hash, out var existing))
            {
                entry = existing with
                {
                    Label = string.IsNullOrWhiteSpace(label) ? existing.Label : label,
                    Transport = transport == AdbTransport.Unknown ? existing.Transport : transport,
                    LastSeen = now,
                    LastEndpoint = lastEndpoint ?? existing.LastEndpoint,
                };
            }
            else
            {
                entry = new TrustedPair(hash, label, transport, now, now, lastEndpoint);
            }
            _byHash[hash] = entry;
            Save();
        }
        return entry;
    }

    public bool Forget(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return false;
        return ForgetByHash(SerialHash.Of(serial));
    }

    /// <summary>
    /// Remove an entry by its hash directly. Used by the CLI's
    /// <c>phonefork trusted forget &lt;hash&gt;</c> when only the hash is known.
    /// </summary>
    public bool ForgetByHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return false;
        lock (_sync)
        {
            if (!_byHash.Remove(hash.Trim().ToLowerInvariant())) return false;
            Save();
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _byHash.Clear();
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var pairs = JsonSerializer.Deserialize<TrustedPair[]>(json, JsonOpts) ?? Array.Empty<TrustedPair>();
            lock (_sync)
            {
                _byHash.Clear();
                foreach (var p in pairs)
                    if (!string.IsNullOrWhiteSpace(p.SerialHashValue))
                        _byHash[p.SerialHashValue] = p;
            }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to load trusted-pair registry at {Path}", _filePath);
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_byHash.Values.OrderBy(p => p.SerialHashValue).ToArray(), JsonOpts);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to save trusted-pair registry to {Path}", _filePath);
        }
    }
}
