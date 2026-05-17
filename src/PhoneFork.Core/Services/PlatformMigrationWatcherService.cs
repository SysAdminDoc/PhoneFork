namespace PhoneFork.Core.Services;

public enum PlatformWatcherSeverity
{
    Info,
    Watch,
    Action,
}

public sealed record PlatformMigrationSource(
    string Id,
    string Name,
    string Url,
    string Status,
    string PhoneForkImplication,
    PlatformWatcherSeverity Severity,
    string SourceId);

public sealed record PlatformMigrationWatcherReport(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PlatformMigrationSource> Sources,
    IReadOnlyList<string> RecommendedActions)
{
    public int ActionCount => Sources.Count(s => s.Severity == PlatformWatcherSeverity.Action);
    public int WatchCount => Sources.Count(s => s.Severity == PlatformWatcherSeverity.Watch);
}

public static class PlatformMigrationWatcherService
{
    public static PlatformMigrationWatcherReport Build(DateTimeOffset? generatedAt = null)
    {
        var sources = new[]
        {
            new PlatformMigrationSource(
                Id: "android-cross-platform-transfer",
                Name: "Android Auto Backup cross-platform transfer",
                Url: "https://developer.android.com/identity/data/autobackup",
                Status: "Android 16 QPR2 / API 36.1 adds <cross-platform-transfer platform=\"ios\"> inside data-extraction-rules, with include/exclude rules and iOS as the currently documented target platform.",
                PhoneForkImplication: "Keep echoing app cross-platform-transfer posture in open archives and app reports; do not implement a private clone of the platform D2D channel.",
                Severity: PlatformWatcherSeverity.Watch,
                SourceId: "S04"),
            new PlatformMigrationSource(
                Id: "apple-ios-to-android",
                Name: "Apple Transfer to Android",
                Url: "https://support.apple.com/en-au/126058",
                Status: "Apple documents iOS/iPadOS 26.3 transfer to compatible Android 17 devices for eSIM, photos, contacts, calendars, call history, messages, accessibility settings, home screen layout, wallpaper, and developer-enabled third-party app data.",
                PhoneForkImplication: "Treat iOS-source work as v2 watchlist. PhoneFork should guide users to the official QR/session flow until a local gap is proven.",
                Severity: PlatformWatcherSeverity.Watch,
                SourceId: "S14"),
            new PlatformMigrationSource(
                Id: "seedvault",
                Name: "Seedvault",
                Url: "https://github.com/seedvault-app/seedvault",
                Status: "Seedvault is an AOSP backup application that must be integrated into a ROM and cannot be installed as a regular app.",
                PhoneForkImplication: "Track Seedvault as an interoperability reference for backup semantics, not a feature PhoneFork can deploy onto stock Samsung phones.",
                Severity: PlatformWatcherSeverity.Info,
                SourceId: "G14"),
            new PlatformMigrationSource(
                Id: "phonefork-open-archive",
                Name: "PhoneFork open archive metadata",
                Url: "PROJECT_CONTEXT.md",
                Status: "Open archives already carry crossPlatform.iosCompatibleApps metadata and hashed endpoint IDs.",
                PhoneForkImplication: "Keep the archive schema stable; add new official platform fields only behind optional manifest properties.",
                Severity: PlatformWatcherSeverity.Info,
                SourceId: "L13"),
        };

        var actions = new[]
        {
            "Refresh S04, S14, and G14 before each public release because Android 17+ and iOS transfer behavior is still moving.",
            "Keep third-party app-data claims conditional on developer-enabled Android cross-platform-transfer metadata.",
            "Use official Android/iOS/Seedvault flows as handoffs unless PhoneFork has a tested local capability advantage.",
        };

        return new PlatformMigrationWatcherReport(generatedAt ?? DateTimeOffset.UtcNow, sources, actions);
    }
}
