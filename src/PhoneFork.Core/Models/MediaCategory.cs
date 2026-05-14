namespace PhoneFork.Core.Models;

/// <summary>
/// User-storage subtrees PhoneFork's Media tab knows how to manifest, diff, and sync.
/// Order is the UI presentation order.
/// </summary>
public enum MediaCategory
{
    Dcim,
    Pictures,
    Movies,
    Music,
    Download,
    Documents,
    Ringtones,
    Notifications,
    Alarms,
    Recordings,
    WhatsAppMedia,
}

public static class MediaCategoryExtensions
{
    /// <summary>Remote (on-device) path under <c>/sdcard/</c> that backs each category.</summary>
    public static string RemotePath(this MediaCategory cat) => cat switch
    {
        MediaCategory.Dcim          => "/sdcard/DCIM",
        MediaCategory.Pictures      => "/sdcard/Pictures",
        MediaCategory.Movies        => "/sdcard/Movies",
        MediaCategory.Music         => "/sdcard/Music",
        MediaCategory.Download      => "/sdcard/Download",
        MediaCategory.Documents     => "/sdcard/Documents",
        MediaCategory.Ringtones     => "/sdcard/Ringtones",
        MediaCategory.Notifications => "/sdcard/Notifications",
        MediaCategory.Alarms        => "/sdcard/Alarms",
        MediaCategory.Recordings    => "/sdcard/Recordings",
        MediaCategory.WhatsAppMedia => "/sdcard/Android/media/com.whatsapp",
        _ => throw new ArgumentOutOfRangeException(nameof(cat), cat, null),
    };

    /// <summary>Short human label used in the UI checkbox list.</summary>
    public static string Label(this MediaCategory cat) => cat switch
    {
        MediaCategory.Dcim          => "DCIM (Camera)",
        MediaCategory.Pictures      => "Pictures",
        MediaCategory.Movies        => "Movies",
        MediaCategory.Music         => "Music",
        MediaCategory.Download      => "Downloads",
        MediaCategory.Documents     => "Documents",
        MediaCategory.Ringtones     => "Ringtones",
        MediaCategory.Notifications => "Notifications",
        MediaCategory.Alarms        => "Alarms",
        MediaCategory.Recordings    => "Recordings",
        MediaCategory.WhatsAppMedia => "WhatsApp media",
        _ => cat.ToString(),
    };
}
