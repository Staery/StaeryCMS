using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StaeryCMS.Converters;

/// <summary>Turns a resource key coming from a view model (e.g. "Icon.Draft") into the application resource.</summary>
public sealed class ResourceLookupConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? Application.Current?.TryFindResource(key) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
