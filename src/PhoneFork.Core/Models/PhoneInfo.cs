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

    public string ShortLabel => $"{DisplayName} ({Serial[^4..]})";
}
