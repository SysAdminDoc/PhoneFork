using System.Security.Cryptography;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class DebloatOverrideMatchingTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"phonefork-debloat-feed-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(">=8.5", "80500", true)]
    [InlineData(">=8.5", "80400", false)]
    [InlineData(">=8.5", "80600", true)]
    [InlineData(">=8.5", "90000", true)]
    [InlineData(">=8.5", "70000", false)]
    [InlineData(">=8", "80500", true)]
    [InlineData(">8.5", "80500", false)]
    [InlineData(">8.5", "80501", true)]
    [InlineData("<=8.5", "80500", true)]
    [InlineData("<8.5", "80500", false)]
    [InlineData("==8.5", "80500", true)]
    [InlineData("8.5", "80500", true)]
    [InlineData("8.5", "80501", false)]
    [InlineData("*", "70000", true)]
    [InlineData("", "70000", true)]
    public void MatchesOneUi(string predicate, string raw, bool expected)
    {
        var v = DebloatDataset.ParseOneUi(raw);
        Assert.Equal(expected, DebloatDataset.MatchesOs(predicate, v));
    }

    [Theory]
    [InlineData("80500", 8, 5, 0)]
    [InlineData("80400", 8, 4, 0)]
    [InlineData("90203", 9, 2, 3)]
    public void ParseOneUiFromSamsungInteger(string raw, int major, int minor, int build)
    {
        var v = DebloatDataset.ParseOneUi(raw);
        Assert.NotNull(v);
        Assert.Equal(major, v!.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(build, v.Build);
    }

    [Theory]
    [InlineData("16", 16, 0)]
    [InlineData("16.1", 16, 1)]
    [InlineData("", null, null)]
    public void ParseAndroidMajorOnly(string raw, int? expectedMajor, int? expectedMinor)
    {
        var v = DebloatDataset.ParseAndroid(raw);
        if (expectedMajor is null)
        {
            Assert.Null(v);
            return;
        }
        Assert.Equal(expectedMajor, v!.Major);
        Assert.Equal(expectedMinor ?? 0, v.Minor);
    }

    [Fact]
    public void OverridesLoadFromAssembly()
    {
        var dataset = DebloatDataset.Load();
        Assert.NotEmpty(dataset.Overrides);
        Assert.Contains(dataset.Overrides, o => o.PackageId == "com.samsung.android.smartsuggestions");
    }

    [Fact]
    public void WithOverridesForFlagsSmartSuggestionsOnOneUi85()
    {
        var dataset = DebloatDataset.Load();
        var patched = dataset.WithOverridesFor(oneUiVersionRaw: "80500", androidVersionRaw: "16", oemRaw: "samsung");
        Assert.True(patched.ByPackageId.TryGetValue("com.samsung.android.smartsuggestions", out var entry));
        Assert.Equal(PhoneFork.Core.Models.DebloatTier.Unsafe, entry!.Tier);
        Assert.NotNull(entry.Warning);
        Assert.Contains("UAD-NG", entry.Warning!);
    }

    [Fact]
    public void WithOverridesForLeavesOlderOneUiUnchanged()
    {
        var dataset = DebloatDataset.Load();
        var dataset_v8 = dataset.WithOverridesFor(oneUiVersionRaw: "80000", androidVersionRaw: "16", oemRaw: "samsung");
        if (dataset.ByPackageId.TryGetValue("com.samsung.android.smartsuggestions", out var original))
        {
            var entry = dataset_v8.ByPackageId["com.samsung.android.smartsuggestions"];
            // The override should not have applied for One UI 8.0.
            Assert.Equal(original.Tier, entry.Tier);
        }
    }

    [Fact]
    public void OemPredicateIsCaseInsensitiveAndCommaDelimited()
    {
        Assert.True(DebloatDataset.MatchesOem("samsung,google", "Samsung"));
        Assert.False(DebloatDataset.MatchesOem("google", "Samsung"));
    }

    [Fact]
    public async Task ExternalFeedRequiresChecksum()
    {
        Directory.CreateDirectory(_tempDir);
        var path = await WriteFeedAsync("com.example.feed", action: "Unsafe");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DebloatOverrideFeedLoader.LoadAsync(path));
    }

    [Fact]
    public async Task ExternalFeedLoadsWithSidecarChecksum()
    {
        Directory.CreateDirectory(_tempDir);
        var path = await WriteFeedAsync("com.example.feed", action: "Unsafe");
        var sha = await Sha256Async(path);
        await File.WriteAllTextAsync(path + ".sha256", sha + "  feed.json");

        var feed = await DebloatOverrideFeedLoader.LoadAsync(path);

        Assert.Equal(sha, feed.Sha256);
        Assert.Single(feed.Overrides);
    }

    [Fact]
    public async Task ExternalFeedChecksumMismatchFailsClosed()
    {
        Directory.CreateDirectory(_tempDir);
        var path = await WriteFeedAsync("com.example.feed", action: "Unsafe");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DebloatOverrideFeedLoader.LoadAsync(path, new string('0', 64)));
    }

    [Fact]
    public async Task ExternalFeedCanAddPackageAndExpiredOverridesAreIgnored()
    {
        Directory.CreateDirectory(_tempDir);
        var path = await WriteFeedAsync("com.example.feed", action: "Caution", expiresAt: "2026-12-31");
        var sha = await Sha256Async(path);
        var feed = await DebloatOverrideFeedLoader.LoadAsync(path, sha);

        var patched = DebloatDataset.Load()
            .WithOverrideFeed(feed)
            .WithOverridesFor("80500", "16", "samsung");

        Assert.True(patched.ByPackageId.TryGetValue("com.example.feed", out var added));
        Assert.Equal(PhoneFork.Core.Models.DebloatTier.Caution, added!.Tier);

        var expiredPath = await WriteFeedAsync("com.example.expired", action: "Unsafe", expiresAt: "2026-01-01");
        var expiredSha = await Sha256Async(expiredPath);
        var expiredFeed = await DebloatOverrideFeedLoader.LoadAsync(expiredPath, expiredSha);
        var unchanged = DebloatDataset.Load()
            .WithOverrideFeed(expiredFeed)
            .WithOverridesFor("80500", "16", "samsung");

        Assert.False(unchanged.ByPackageId.ContainsKey("com.example.expired"));
    }

    private async Task<string> WriteFeedAsync(string packageId, string action, string expiresAt = "2026-12-31")
    {
        var path = Path.Combine(_tempDir, $"feed-{Guid.NewGuid():N}.json");
        var json = $$"""
        {
          "$schema": "phonefork-debloat-overrides-v1",
          "generatedAt": "2026-05-17",
          "source": "https://github.com/SysAdminDoc/PhoneFork",
          "overrides": [
            {
              "packageId": "{{packageId}}",
              "oem": "samsung",
              "oneUi": ">=8.5",
              "android": ">=16",
              "action": "{{action}}",
              "risk": "test risk",
              "warning": "test warning",
              "source": "https://github.com/SysAdminDoc/PhoneFork/issues/1",
              "reviewAfter": "2026-07-01",
              "expiresAt": "{{expiresAt}}"
            }
          ]
        }
        """;
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
