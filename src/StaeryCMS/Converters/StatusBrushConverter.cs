using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StaeryCMS.Core.Models;

namespace StaeryCMS.Converters;

/// <summary>Maps a <see cref="ContentStatus"/> to one of the configured brushes.</summary>
public sealed class StatusBrushConverter : IValueConverter
{
    public Brush Draft { get; set; } = Brushes.Goldenrod;

    public Brush Published { get; set; } = Brushes.SeaGreen;

    public Brush Archived { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ContentStatus.Published => Published,
        ContentStatus.Archived => Archived,
        _ => Draft,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
