using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public sealed class PlatformMigrationWatcherTests
{
    [Fact]
    public void ReportIncludesOfficialPlatformAndSeedvaultWatchItems()
    {
        var report = PlatformMigrationWatcherService.Build(DateTimeOffset.UnixEpoch);

        Assert.Contains(report.Sources, s => s.Id == "android-cross-platform-transfer" && s.SourceId == "S04");
        Assert.Contains(report.Sources, s => s.Id == "apple-ios-to-android" && s.SourceId == "S14");
        Assert.Contains(report.Sources, s => s.Id == "seedvault" && s.SourceId == "G14");
        Assert.True(report.WatchCount >= 2);
        Assert.Contains(report.RecommendedActions, a => a.Contains("Refresh S04, S14, and G14", StringComparison.Ordinal));
    }

    [Fact]
    public void SeedvaultIsReferenceNotStockSamsungInstallPlan()
    {
        var seedvault = PlatformMigrationWatcherService.Build(DateTimeOffset.UnixEpoch)
            .Sources.Single(s => s.Id == "seedvault");

        Assert.Equal(PlatformWatcherSeverity.Info, seedvault.Severity);
        Assert.Contains("cannot be installed as a regular app", seedvault.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("interoperability reference", seedvault.PhoneForkImplication, StringComparison.OrdinalIgnoreCase);
    }
}
