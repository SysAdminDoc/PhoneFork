using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class HelperAppServiceConstantsTests
{
    [Fact]
    public void AuthoritiesAreDistinct()
    {
        var set = new HashSet<string>(HelperAppService.Authorities, StringComparer.Ordinal);
        Assert.Equal(HelperAppService.Authorities.Count, set.Count);
    }

    [Fact]
    public void AuthoritiesCoverEveryMigrationCategory()
    {
        var expected = new[] { "sms", "calllog", "contacts", "wifi", "wallpaper", "ringtone", "dictionary" };
        Assert.Equal(expected.OrderBy(s => s), HelperAppService.Authorities.OrderBy(s => s));
    }

    [Fact]
    public void PackageIdMatchesAndroidManifest()
    {
        // If this constant drifts, the helper APK build's applicationId must change with it.
        Assert.Equal("com.sysadmindoc.phonefork.helper", HelperAppService.PackageId);
    }

    [Fact]
    public void HelperResidueReportIsCleanWhenEverythingGone()
    {
        var clean = new HelperResidueReport(HelperInstalled: false, TempFilesLeft: Array.Empty<string>());
        Assert.True(clean.IsClean);

        var dirty = new HelperResidueReport(HelperInstalled: true, TempFilesLeft: Array.Empty<string>());
        Assert.False(dirty.IsClean);

        var leftovers = new HelperResidueReport(HelperInstalled: false, TempFilesLeft: new[] { "phonefork-agent.jar" });
        Assert.False(leftovers.IsClean);
    }
}

public class ShizukuRunbookTests
{
    [Fact]
    public void RunbookCoversAllStates()
    {
        foreach (var state in Enum.GetValues<ShizukuState>())
        {
            var text = ShizukuService.Runbook(state);
            Assert.False(string.IsNullOrWhiteSpace(text), $"Runbook empty for {state}");
        }
    }

    [Fact]
    public void RunningRunbookExplainsThatNoActionIsNeeded()
    {
        var text = ShizukuService.Runbook(ShizukuState.Running);
        Assert.Contains("running", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotInstalledRunbookPointsAtOfficialDownload()
    {
        var text = ShizukuService.Runbook(ShizukuState.NotInstalled);
        Assert.Contains("shizuku.rikka.app", text);
    }
}

public class AppProcessAgentConstantsTests
{
    [Fact]
    public void RemoteJarPathLivesUnderDataLocalTmp()
    {
        // /data/local/tmp is the only path the shell UID can both write and execute from
        // without root or system permissions. Anything else breaks the scrcpy pattern.
        Assert.StartsWith("/data/local/tmp/", AppProcessAgentService.RemoteJarPath);
        Assert.EndsWith(".jar", AppProcessAgentService.RemoteJarPath);
    }

    [Fact]
    public void AgentClassUsesHelperPackageNamespace()
    {
        Assert.StartsWith("com.sysadmindoc.phonefork.helper.", AppProcessAgentService.AgentClass);
    }
}
