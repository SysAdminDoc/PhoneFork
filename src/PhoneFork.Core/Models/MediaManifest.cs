using System.Text.Json.Serialization;

namespace PhoneFork.Core.Models;

/// <summary>Shared JSON options for manifest read/write so the format is stable and human-readable.</summary>
public static class MediaJson
{
    public static System.Text.Json.JsonSerializerOptions Options { get; } = Build();
    private static System.Text.Json.JsonSerializerOptions Build()
    {
        var o = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}

/// <summary>
/// One file inside a category. Path is RELATIVE to the category root so the same manifest can be
/// applied to a destination whose category root differs (rare, but cheap to support).
/// </summary>
public sealed record MediaFile
{
    /// <summary>Relative path from the category root, using forward slashes. E.g. <c>Camera/IMG_20260514_123456.jpg</c>.</summary>
    public required string RelPath { get; init; }
    public required long SizeBytes { get; init; }
    /// <summary>Unix-epoch seconds. Android <c>find -printf '%T@'</c> output is fractional seconds; we truncate to long.</summary>
    public required long Mtime { get; init; }
}

public sealed record MediaCategoryManifest
{
    public required MediaCategory Category { get; init; }
    public required string RemoteRoot { get; init; }
    /// <summary>Sorted ascending by RelPath for deterministic diff output.</summary>
    public required IReadOnlyList<MediaFile> Files { get; init; }

    [JsonIgnore]
    public long TotalSizeBytes => Files.Sum(f => f.SizeBytes);

    [JsonIgnore]
    public int FileCount => Files.Count;
}

public sealed record MediaManifest
{
    public required string DeviceSerial { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required IReadOnlyList<MediaCategoryManifest> Categories { get; init; }

    [JsonIgnore]
    public long TotalSizeBytes => Categories.Sum(c => c.TotalSizeBytes);

    [JsonIgnore]
    public int TotalFileCount => Categories.Sum(c => c.FileCount);
}
