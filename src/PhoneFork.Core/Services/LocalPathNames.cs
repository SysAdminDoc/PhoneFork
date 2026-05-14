namespace PhoneFork.Core.Services;

/// <summary>
/// Converts device identifiers and Android relative paths into Windows-safe
/// local cache/stage paths. Wireless ADB serials contain ':' and Android file
/// names may contain characters that Windows treats as separators or invalid.
/// </summary>
public static class LocalPathNames
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static string SafeFileName(string? value, string fallback = "item", int maxLength = 120)
    {
        if (maxLength < 1)
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Maximum filename length must be positive.");

        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var chars = new char[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            chars[i] = IsSafeFileNameChar(c) ? c : '_';
        }

        var safe = new string(chars).Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(safe))
            safe = fallback;

        safe = PrefixReservedDeviceName(safe);

        if (safe.Length > maxLength)
            safe = safe[..maxLength].TrimEnd(' ', '.');

        if (string.IsNullOrWhiteSpace(safe))
            safe = fallback;

        return PrefixReservedDeviceName(safe);
    }

    public static string CombineSafeRelativePath(string root, string androidRelativePath)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("A root directory is required.", nameof(root));

        var segments = androidRelativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => SafePathSegment(segment))
            .Where(segment => segment.Length > 0)
            .ToArray();

        if (segments.Length == 0)
            throw new ArgumentException("A relative file path is required.", nameof(androidRelativePath));

        var combined = segments.Aggregate(root, Path.Combine);
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(combined);
        var rootPrefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Resolved path escaped the stage directory: {androidRelativePath}");

        return fullPath;
    }

    private static string SafePathSegment(string segment)
    {
        if (segment is "." or "..")
            return "_";

        return SafeFileName(segment, "segment");
    }

    private static string PrefixReservedDeviceName(string value)
    {
        var stem = Path.GetFileNameWithoutExtension(value).TrimEnd(' ', '.');
        return ReservedDeviceNames.Contains(stem) ? "_" + value : value;
    }

    private static bool IsSafeFileNameChar(char c) =>
        c >= 32
        && c != '/'
        && c != '\\'
        && !InvalidFileNameChars.Contains(c);
}
