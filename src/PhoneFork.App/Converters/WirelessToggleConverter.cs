using System.Globalization;
using System.Windows.Data;

namespace PhoneFork.App.Converters;

/// <summary>
/// Boolean → button-label converter for the wireless session toggle.
/// True (session open) renders "Close wireless session"; false renders "Start wireless session".
/// </summary>
public sealed class WirelessToggleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "Close wireless session" : "Start wireless session";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
