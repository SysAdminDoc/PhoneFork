using System.Text.Json.Serialization;

namespace PhoneFork.Core.Models;

/// <summary>
/// Android settings namespace queryable via <c>settings list NS</c>.
/// </summary>
public enum SettingsNamespace
{
    Secure,
    System,
    Global,
}

/// <summary>
/// Snapshot of one namespace's key/value pairs at capture time.
/// </summary>
public sealed record SettingsNamespaceSnapshot
{
    public required SettingsNamespace Namespace { get; init; }
    public required IReadOnlyDictionary<string, string> Values { get; init; }
}

/// <summary>
/// Full settings snapshot for one device across all three AOSP namespaces.
/// </summary>
public sealed record SettingsSnapshot
{
    public required string DeviceSerial { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required IReadOnlyList<SettingsNamespaceSnapshot> Namespaces { get; init; }

    [JsonIgnore]
    public int TotalKeyCount => Namespaces.Sum(n => n.Values.Count);
}
