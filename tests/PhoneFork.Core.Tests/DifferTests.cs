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
