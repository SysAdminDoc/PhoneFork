using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public sealed class LocalPathNamesTests
{
    [Fact]
    public void SafeFileName_RewritesWirelessAdbSerialForWindows()
    {
        Assert.Equal("192.168.1.10_5555", LocalPathNames.SafeFileName("192.168.1.10:5555"));
    }

    [Fact]
    public void SafeFileName_AvoidsWindowsReservedDeviceNames()
    {
        Assert.Equal("_CON", LocalPathNames.SafeFileName("CON"));
        Assert.Equal("_CON.jpg", LocalPathNames.SafeFileName("CON.jpg"));
    }

    [Fact]
    public void CombineSafeRelativePath_SanitizesAndroidSegmentsInsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "phonefork-tests");
        var path = LocalPathNames.CombineSafeRelativePath(root, @"Camera/IMG:001\raw?.jpg");

        Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Camera", "IMG_001", "raw_.jpg"), path);
    }

    [Fact]
    public void CombineSafeRelativePath_DoesNotResolveDotDotOutsideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "phonefork-tests");
        var path = LocalPathNames.CombineSafeRelativePath(root, "../secret.txt");

        Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.DirectorySeparatorChar + "_" + Path.DirectorySeparatorChar, path);
    }
}
