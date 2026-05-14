namespace PhoneFork.Core.Models;

/// <summary>
/// Snapshot of a connected phone's identifying info.
/// </summary>
public sealed record PhoneInfo(
    string Serial,
    string Manufacturer,
    string Model,
    string AndroidVersion,
    string OneUiVersion,
    string Codename,
    bool IsAuthorized)
{
    public string DisplayName =>
        string.IsNullOrEmpty(Manufacturer) || Manufacturer.Equals(Model, StringComparison.OrdinalIgnoreCase)
            ? Model
            : $"{Manufacturer} {Model}";

    public string FormattedOneUiVersion => FormatOneUi(OneUiVersion);

    public string ShortLabel => $"{DisplayName} ({(Serial.Length <= 4 ? Serial : Serial[^4..])})";

    private static string FormatOneUi(string raw)
    {
        // ro.build.version.oneui is encoded as <major><minor><patch>, e.g. 80000 = 8.0.0.
        if (raw.Length >= 5 && int.TryParse(raw, out var n))
            return $"{n / 10000}.{n / 100 % 100}.{n % 100}";

        return raw;
    }
}
