namespace PhoneFork.Core.Models;

/// <summary>
/// One installed third-party application discovered on a phone.
/// </summary>
public sealed record AppInfo
{
    public required string PackageName { get; init; }

    /// <summary>Human-readable label (from APK badging). Falls back to <see cref="PackageName"/> if unavailable.</summary>
    public string Label { get; init; } = "";

    public string VersionName { get; init; } = "";
    public long VersionCode { get; init; }

    /// <summary>All split APKs on the device for this package (base + density/abi/locale splits).</summary>
    public required IReadOnlyList<string> RemoteApkPaths { get; init; }

    /// <summary>Bytes — sum of all splits.</summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>True if the app is the user-visible "default launcher" home app, or system-bundled. Surface for UI filtering.</summary>
    public bool IsSystem { get; init; }

    public string SafeLabel => string.IsNullOrWhiteSpace(Label) ? PackageName : Label;
}
