using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class DebloatOverrideMatchingTests
{
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
        var patched = dataset.WithOverridesFor(oneUiVersionRaw: "80500", androidVersionRaw: "16");
        Assert.True(patched.ByPackageId.TryGetValue("com.samsung.android.smartsuggestions", out var entry));
        Assert.Equal(PhoneFork.Core.Models.DebloatTier.Unsafe, entry!.Tier);
        Assert.NotNull(entry.Warning);
        Assert.Contains("UAD-NG", entry.Warning!);
    }

    [Fact]
    public void WithOverridesForLeavesOlderOneUiUnchanged()
    {
        var dataset = DebloatDataset.Load();
        var dataset_v8 = dataset.WithOverridesFor(oneUiVersionRaw: "80000", androidVersionRaw: "16");
        if (dataset.ByPackageId.TryGetValue("com.samsung.android.smartsuggestions", out var original))
        {
            var entry = dataset_v8.ByPackageId["com.samsung.android.smartsuggestions"];
            // The override should not have applied for One UI 8.0.
            Assert.Equal(original.Tier, entry.Tier);
        }
    }
}
