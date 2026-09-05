using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F115 — the app_process agent speaks the same v1 envelope as the ContentProviders, so the host
/// must be able to parse what it prints. The agent is not a provider: it has no content:// URI.
/// </summary>
public class AgentContractTests
{
    /// <summary>Exactly what Agent.java's ping() prints, so the host parse path is exercised.</summary>
    private const string AgentPingEnvelope =
        """
        {"schema":"phonefork.helper.v1","authority":"agent","status":"ok","mode":"health","count":1,"items":[{"uid":2000,"pid":12345,"sdkInt":36,"isShellUid":true}],"capabilities":{},"warnings":[]}
        """;

    private const string AgentErrorEnvelope =
        """
        {"schema":"phonefork.helper.v1","authority":"agent","status":"error","mode":"health","count":0,"items":[],"capabilities":{},"warnings":[],"error":{"code":"unsupported-op","message":"Unsupported agent op: banana"}}
        """;

    [Fact]
    public void TheHostParsesAnAgentPingEnvelope()
    {
        Assert.True(HelperProviderContract.TryParseEnvelope(AgentPingEnvelope, out var envelope));
        Assert.True(envelope!.IsOk);
        Assert.Equal(HelperProviderContract.AgentAuthority, envelope.Authority);
        Assert.Equal(1, envelope.Count);
        Assert.Equal(1, envelope.Items.GetArrayLength());
    }

    [Fact]
    public void TheHostParsesAnAgentErrorEnvelopeAndKeepsTheReason()
    {
        Assert.True(HelperProviderContract.TryParseEnvelope(AgentErrorEnvelope, out var envelope));
        Assert.False(envelope!.IsOk);
        Assert.Equal("unsupported-op", envelope.Error?.Code);
        Assert.Contains("banana", envelope.Error?.Message);
    }

    [Fact]
    public void TheAgentAuthorityIsNotAProviderAuthority()
    {
        // Adding it to Authorities would let BuildQueryUri mint a content:// URI for something
        // that is not a ContentProvider.
        Assert.DoesNotContain(HelperProviderContract.AgentAuthority, HelperAppService.Authorities);
        Assert.Throws<ArgumentException>(() =>
            HelperProviderContract.BuildQueryUri(HelperProviderContract.AgentAuthority));
    }

    [Fact]
    public void EveryProviderAuthorityAndTheAgentAreAcceptedInAnEnvelope()
    {
        foreach (var authority in HelperAppService.Authorities)
            Assert.True(HelperProviderContract.IsKnownAuthority(authority), authority);

        Assert.True(HelperProviderContract.IsKnownAuthority(HelperProviderContract.AgentAuthority));
    }

    [Theory]
    [InlineData("banking")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownAuthoritiesAreStillRejected(string? authority)
    {
        Assert.False(HelperProviderContract.IsKnownAuthority(authority));
    }

    [Fact]
    public void AnEnvelopeFromAnUnknownAuthorityIsRefused()
    {
        var hostile =
            """
            {"schema":"phonefork.helper.v1","authority":"banking","status":"ok","mode":"export","count":0,"items":[]}
            """;

        Assert.False(HelperProviderContract.TryParseEnvelope(hostile, out _));
    }

    [Fact]
    public void TheAgentJarPathTargetsTheHostsExpectedRemoteLocation()
    {
        Assert.StartsWith("/data/local/tmp/", AppProcessAgentService.RemoteJarPath);
        Assert.EndsWith("phonefork-agent.jar", AppProcessAgentService.RemoteJarPath);
    }

    [Fact]
    public void TheAgentMainClassMatchesTheShippedJavaSource()
    {
        // The host invokes this class name by string; a rename on either side breaks the JAR
        // silently with a ClassNotFoundException that only shows up on a real device.
        var source = File.ReadAllText(RepoFile(
            "helper-apk/agent/src/main/java/com/sysadmindoc/phonefork/helper/Agent.java"));

        Assert.Contains("package com.sysadmindoc.phonefork.helper;", source);
        Assert.Contains("public final class Agent", source);
        Assert.Contains("public static void main(String[] args)", source);
        Assert.Equal("com.sysadmindoc.phonefork.helper.Agent", AppProcessAgentService.AgentClass);
    }

    [Fact]
    public void TheAgentSourceEmitsTheSchemaTheHostValidates()
    {
        var source = File.ReadAllText(RepoFile(
            "helper-apk/agent/src/main/java/com/sysadmindoc/phonefork/helper/Agent.java"));

        Assert.Contains($"\"{HelperProviderContract.Schema}\"", source);
        Assert.Contains($"AUTHORITY = \"{HelperProviderContract.AgentAuthority}\"", source);
    }

    private static string RepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PhoneFork.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
