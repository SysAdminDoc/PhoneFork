using PhoneFork.Core.Services;
using Serilog.Core;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F129 — the window reopens where the user left it, except when that position is no longer
/// reachable. Restoring onto a monitor that has been unplugged would hide the app entirely.
/// </summary>
public class WindowStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"phonefork-winstate-{Guid.NewGuid():N}");

    private WindowStateStore Store() => new(Logger.None, Path.Combine(_dir, "window-state.json"));

    private static WindowStateSnapshot Snapshot(double left, double top, double width = 1280, double height = 820) =>
        new() { Left = left, Top = top, Width = width, Height = height };

    /// <summary>A single 1920x1080 display at the origin.</summary>
    private static readonly (double, double, double, double)[] SinglePrimary = { (0, 0, 1920, 1080) };

    /// <summary>Primary plus a monitor to its left, the layout that produces negative coordinates.</summary>
    private static readonly (double, double, double, double)[] DualWithLeftMonitor =
    {
        (0, 0, 1920, 1080),
        (-1920, 0, 1920, 1080),
    };

    [Fact]
    public void RoundTripsEveryField()
    {
        var store = Store();
        var saved = new WindowStateSnapshot
        {
            Left = 120, Top = 64, Width = 1400, Height = 900, Maximized = true, SelectedTab = 4,
        };

        store.Save(saved);
        var loaded = store.Load();

        Assert.Equal(saved, loaded);
    }

    [Fact]
    public void LoadReturnsNullWhenNothingHasBeenSaved()
    {
        Assert.Null(Store().Load());
    }

    [Fact]
    public void ACorruptFileIsNotFatal()
    {
        var store = Store();
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{ this is not json");

        Assert.Null(store.Load());
    }

    [Fact]
    public void SaveCreatesTheDirectoryOnFirstRun()
    {
        var store = Store();
        Assert.False(Directory.Exists(_dir));

        store.Save(Snapshot(0, 0));

        Assert.True(File.Exists(store.FilePath));
    }

    [Fact]
    public void AWindowFullyOnTheDisplayIsRestored()
    {
        Assert.True(WindowStateStore.IsOnAnyDisplay(Snapshot(100, 100), SinglePrimary));
    }

    [Fact]
    public void AWindowOnAMonitorThatIsGoneIsRefused()
    {
        // Saved on a second monitor to the right; that monitor is no longer present.
        Assert.False(WindowStateStore.IsOnAnyDisplay(Snapshot(2600, 200), SinglePrimary));
    }

    [Fact]
    public void NegativeCoordinatesAreFineWhenThatMonitorIsStillAttached()
    {
        var saved = Snapshot(-1800, 100);

        Assert.True(WindowStateStore.IsOnAnyDisplay(saved, DualWithLeftMonitor));
        Assert.False(WindowStateStore.IsOnAnyDisplay(saved, SinglePrimary));
    }

    [Fact]
    public void APartiallyOffScreenWindowIsStillGrabbable()
    {
        // Most of the window hangs off the right edge, but enough title bar remains to drag.
        Assert.True(WindowStateStore.IsOnAnyDisplay(Snapshot(1800, 100), SinglePrimary));
    }

    [Fact]
    public void AWindowWithOnlyASliverShowingIsRefused()
    {
        // Ten pixels of overlap is not enough to grab; the user would have to fight the window.
        Assert.False(WindowStateStore.IsOnAnyDisplay(Snapshot(1910, 100), SinglePrimary));
    }

    [Fact]
    public void AWindowAboveTheDesktopIsRefused()
    {
        Assert.False(WindowStateStore.IsOnAnyDisplay(Snapshot(100, -900), SinglePrimary));
    }

    [Theory]
    [InlineData(0, 820)]
    [InlineData(1280, 0)]
    [InlineData(-1, -1)]
    public void ADegenerateSizeIsRefused(double width, double height)
    {
        // A zero or negative extent means the saved state is meaningless; fall back to defaults.
        Assert.False(WindowStateStore.IsOnAnyDisplay(Snapshot(100, 100, width, height), SinglePrimary));
    }

    [Fact]
    public void NoDisplaysMeansNoRestore()
    {
        Assert.False(WindowStateStore.IsOnAnyDisplay(Snapshot(100, 100), Array.Empty<(double, double, double, double)>()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
