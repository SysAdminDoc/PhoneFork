using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Persisted window geometry and the tab the user was last on (F129).
/// </summary>
public sealed record WindowStateSnapshot
{
    [JsonPropertyName("left")] public double Left { get; init; }
    [JsonPropertyName("top")] public double Top { get; init; }
    [JsonPropertyName("width")] public double Width { get; init; }
    [JsonPropertyName("height")] public double Height { get; init; }
    [JsonPropertyName("maximized")] public bool Maximized { get; init; }
    [JsonPropertyName("selectedTab")] public int SelectedTab { get; init; }
}

/// <summary>
/// Reads and writes the window's size, position, maximised state and selected tab (F129).
///
/// The interesting part is not the file: it is refusing to restore a position that would put the
/// window somewhere the user cannot reach. A saved position is only honoured when it still lands
/// on a connected display, so unplugging a second monitor between runs cannot open PhoneFork
/// off-screen.
/// </summary>
public sealed class WindowStateStore
{
    /// <summary>Smallest visible sliver, in device-independent pixels, that counts as on-screen.</summary>
    private const double MinimumVisibleExtent = 80;

    private readonly string _path;
    private readonly ILogger _log;

    public WindowStateStore(ILogger log, string? path = null)
    {
        _log = log.ForContext<WindowStateStore>();
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhoneFork",
            "window-state.json");
    }

    /// <summary>Where the state is stored. Named FilePath so it does not shadow System.IO.Path.</summary>
    public string FilePath => _path;

    /// <summary>
    /// Loads the saved state, or null when there is none or it cannot be read. A corrupt file is
    /// never fatal: the window just opens at its default size.
    /// </summary>
    public WindowStateSnapshot? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<WindowStateSnapshot>(File.ReadAllText(_path));
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not read window state from {Path}; using defaults", _path);
            return null;
        }
    }

    /// <summary>Writes the state. Failure is logged and swallowed: it must never break shutdown.</summary>
    public void Save(WindowStateSnapshot snapshot)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not write window state to {Path}", _path);
        }
    }

    /// <summary>
    /// Whether a saved rectangle still overlaps one of <paramref name="displays"/> by enough to be
    /// grabbable. Guards the case where the window was last closed on a monitor that is now gone.
    /// </summary>
    public static bool IsOnAnyDisplay(
        WindowStateSnapshot snapshot,
        IEnumerable<(double Left, double Top, double Width, double Height)> displays)
    {
        if (snapshot.Width <= 0 || snapshot.Height <= 0) return false;

        foreach (var display in displays)
        {
            var overlapWidth = Math.Min(snapshot.Left + snapshot.Width, display.Left + display.Width)
                               - Math.Max(snapshot.Left, display.Left);
            var overlapHeight = Math.Min(snapshot.Top + snapshot.Height, display.Top + display.Height)
                                - Math.Max(snapshot.Top, display.Top);

            if (overlapWidth >= MinimumVisibleExtent && overlapHeight >= MinimumVisibleExtent)
                return true;
        }

        return false;
    }
}
