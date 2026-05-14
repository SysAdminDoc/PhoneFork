using System.Text.RegularExpressions;

namespace PhoneFork.Core.Services;

/// <summary>
/// Small helpers for building Android shell commands from host-side code.
/// ADB shell strings are interpreted by Android's shell, so every value that
/// originates from a user, device, manifest, or dataset must be quoted here.
/// </summary>
public static partial class AdbShell
{
    /// <summary>
    /// Quotes one shell argument with POSIX single-quote escaping.
    /// </summary>
    public static string Arg(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "''";

        return "'" + value.Replace("'", "'\\''") + "'";
    }

    /// <summary>
    /// Returns a quoted package argument after rejecting values that are not
    /// Android package identifiers. This prevents user-provided CLI values and
    /// dataset/snapshot drift from becoming executable shell syntax.
    /// </summary>
    public static string PackageArg(string value, string paramName = "package")
    {
        if (!IsPackageName(value))
            throw new ArgumentException($"Invalid Android package identifier: {value}", paramName);

        return Arg(value);
    }

    public static bool IsPackageName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && PackageNameRegex().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9_.]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageNameRegex();
}
