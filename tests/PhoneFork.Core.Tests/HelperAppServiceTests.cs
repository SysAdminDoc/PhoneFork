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

    [Fact]
    public void ProviderContractExtractsJsonFromContentQueryOutput()
    {
        var output = """Row: 0 json={"schema":"phonefork.helper.v1","authority":"sms","status":"ok","mode":"health","count":0,"items":[]}""";
        var json = HelperProviderContract.ExtractJsonFromContentQuery(output);
        Assert.NotNull(json);
        Assert.StartsWith("{\"schema\"", json);
    }

    [Fact]
    public void ProviderContractTreatsEmptyOutputAsDeniedOrMissing()
    {
        Assert.Null(HelperProviderContract.ExtractJsonFromContentQuery(""));
        Assert.False(HelperProviderContract.TryParseEnvelope(null, out var envelope));
        Assert.Null(envelope);
    }

    [Fact]
    public void ProviderContractRejectsMalformedJson()
    {
        Assert.Throws<FormatException>(() => HelperProviderContract.ParseEnvelope("{not-json"));
    }

    [Fact]
    public void ProviderContractParsesEmptyResultEnvelope()
    {
        var env = HelperProviderContract.ParseEnvelope(
            """{"schema":"phonefork.helper.v1","authority":"contacts","status":"ok","mode":"export","count":0,"items":[],"capabilities":{"canRead":true},"warnings":[]}""");

        Assert.True(env.IsOk);
        Assert.Equal("contacts", env.Authority);
        Assert.Equal(0, env.Count);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, env.Items.ValueKind);
        Assert.Equal(0, env.Items.GetArrayLength());
    }

    [Fact]
    public void ProviderContractParsesPaginationAndCapabilities()
    {
        var env = HelperProviderContract.ParseEnvelope(
            """{"schema":"phonefork.helper.v1","authority":"wifi","status":"ok","mode":"capability","count":1,"nextOffset":500,"items":[{"ssid":"lab"}],"capabilities":{"canReadPsk":false,"requiresShizukuOrPrivilegedApiForPsk":true},"warnings":["psks unavailable"]}""");

        Assert.Equal(500, env.NextOffset);
        Assert.False(env.Capabilities.GetProperty("canReadPsk").GetBoolean());
        Assert.True(env.Capabilities.GetProperty("requiresShizukuOrPrivilegedApiForPsk").GetBoolean());
        Assert.Equal("psks unavailable", Assert.Single(env.Warnings));
    }

    [Fact]
    public void ProviderContractBuildsKnownAuthorityUri()
    {
        var uri = HelperProviderContract.BuildQueryUri("sms", limit: 100, offset: 200);
        Assert.Equal("content://com.sysadmindoc.phonefork.helper.sms?limit=100&offset=200", uri);
    }

    [Fact]
    public void ProviderContractRejectsUnknownAuthority()
    {
        Assert.Throws<ArgumentException>(() => HelperProviderContract.BuildQueryUri("unknown"));
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
