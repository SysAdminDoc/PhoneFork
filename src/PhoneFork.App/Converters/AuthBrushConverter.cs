using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PhoneFork.App.Converters;

/// <summary>
/// Green Mocha when authorized, Red Mocha when unauthorized — drives the device-card status colour.
/// </summary>
public sealed class AuthBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ok = value is bool v && v;
        var key = ok ? "MochaGreenBrush" : "MochaRedBrush";
        if (Application.Current.Resources[key] is Brush b) return b;
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
