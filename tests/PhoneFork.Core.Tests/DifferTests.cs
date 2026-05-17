using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public sealed class DifferTests
{
    [Fact]
    public void SettingsDiffer_ToleratesDuplicateNamespacesInImportedSnapshots()
    {
        var source = new SettingsSnapshot
        {
            DeviceSerial = "src",
            CapturedAt = DateTimeOffset.UnixEpoch,
            Namespaces = new[]
            {
                new SettingsNamespaceSnapshot
                {
                    Namespace = SettingsNamespace.System,
                    Values = new Dictionary<string, string> { ["old"] = "ignored" },
                },
                new SettingsNamespaceSnapshot
                {
                    Namespace = SettingsNamespace.System,
                    Values = new Dictionary<string, string> { ["font_scale"] = "1.1" },
                },
            },
        };
        var dest = new SettingsSnapshot
        {
            DeviceSerial = "dst",
            CapturedAt = DateTimeOffset.UnixEpoch,
            Namespaces = new[]
            {
                new SettingsNamespaceSnapshot
                {
                    Namespace = SettingsNamespace.System,
                    Values = new Dictionary<string, string> { ["font_scale"] = "1.0" },
                },
            },
        };

        var plan = SettingsDiffer.Build(source, dest);

        var entry = Assert.Single(plan.Namespaces.Single().Entries);
        Assert.Equal("font_scale", entry.Key);
        Assert.Equal(SettingsDiffOutcome.Different, entry.Outcome);
    }

    [Fact]
    public void SamsungSettingsCorpus_MarksKnownDisplayKeySafe()
    {
        var entry = new SettingsDiffEntry(
            SettingsNamespace.System,
            "font_scale",
            SettingsDiffOutcome.Different,
            "1.1",
            "1.0");

        var assessment = SamsungSettingsCorpus.Assess(entry);

        Assert.Equal(SettingsSafetyStatus.Safe, assessment.Status);
        Assert.True(SamsungSettingsCorpus.CanApplyByDefault(entry));
    }

    [Fact]
    public void SamsungSettingsCorpus_BlocksDangerousDeviceSpecificKey()
    {
        var entry = new SettingsDiffEntry(
            SettingsNamespace.Secure,
            "android_id",
            SettingsDiffOutcome.Different,
            "source-id",
            "dest-id");

        var assessment = SamsungSettingsCorpus.Assess(entry);

        Assert.Equal(SettingsSafetyStatus.Blocked, assessment.Status);
        Assert.False(SamsungSettingsCorpus.CanApplyByDefault(entry));
        Assert.False(SamsungSettingsCorpus.CanApplyWithExplicitOverride(entry));
    }

    [Fact]
    public void SamsungSettingsCorpus_UnknownKeysRequireExplicitOverride()
    {
        var entry = new SettingsDiffEntry(
            SettingsNamespace.System,
            "com.samsung.unknown_experimental_setting",
            SettingsDiffOutcome.Different,
            "1",
            "0");

        var assessment = SamsungSettingsCorpus.Assess(entry);

        Assert.Equal(SettingsSafetyStatus.Unknown, assessment.Status);
        Assert.False(SamsungSettingsCorpus.CanApplyByDefault(entry));
        Assert.True(SamsungSettingsCorpus.CanApplyWithExplicitOverride(entry));
    }

    [Fact]
    public void SamsungSettingsCorpus_SummarizesReadOnlyDiffSafety()
    {
        var plan = new SettingsPlan(
            "src",
            "dst",
            new[]
            {
                new SettingsDiffNamespace(
                    SettingsNamespace.System,
                    new[]
                    {
                        new SettingsDiffEntry(SettingsNamespace.System, "font_scale", SettingsDiffOutcome.Different, "1.1", "1.0"),
                        new SettingsDiffEntry(SettingsNamespace.System, "android_id", SettingsDiffOutcome.Different, "a", "b"),
                        new SettingsDiffEntry(SettingsNamespace.System, "uncatalogued", SettingsDiffOutcome.OnlyOnSource, "x", null),
                        new SettingsDiffEntry(SettingsNamespace.System, "screen_brightness", SettingsDiffOutcome.Same, "10", "10"),
                    }),
            });

        var summary = SamsungSettingsCorpus.Summarize(SamsungSettingsCorpus.Assess(plan));

        Assert.Equal(1, summary.Safe);
        Assert.Equal(1, summary.Blocked);
        Assert.Equal(1, summary.Unknown);
        Assert.Equal(3, summary.Total);
    }

    [Fact]
    public void MediaDiffer_ToleratesDuplicateCategoriesAndPathsInImportedManifests()
    {
        var source = new MediaManifest
        {
            DeviceSerial = "src",
            CapturedAt = DateTimeOffset.UnixEpoch,
            Categories = new[]
            {
                Manifest(MediaCategory.Dcim, new MediaFile { RelPath = "IMG.jpg", SizeBytes = 1, Mtime = 1 }),
                Manifest(MediaCategory.Dcim, new MediaFile { RelPath = "IMG.jpg", SizeBytes = 2, Mtime = 2 }),
            },
        };
        var dest = new MediaManifest
        {
            DeviceSerial = "dst",
            CapturedAt = DateTimeOffset.UnixEpoch,
            Categories = new[]
            {
                Manifest(MediaCategory.Dcim, new MediaFile { RelPath = "IMG.jpg", SizeBytes = 1, Mtime = 1 }),
            },
        };

        var plan = MediaDiffer.Build(source, dest);

        var entry = Assert.Single(plan.CategoryDiffs.Single().Entries);
        Assert.Equal(MediaDiffOutcome.Conflict, entry.Outcome);
        Assert.Equal(2, entry.Source?.SizeBytes);
    }

    private static MediaCategoryManifest Manifest(MediaCategory category, params MediaFile[] files) =>
        new()
        {
            Category = category,
            RemoteRoot = category.RemotePath(),
            Files = files,
        };
}
