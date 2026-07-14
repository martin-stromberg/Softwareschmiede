using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.Converters;

/// <summary>Konvertiert einen <see cref="DiffLineStatus"/> in einen themenabhängigen Hintergrund-Brush für den DiffViewer.</summary>
[ValueConversion(typeof(DiffLineStatus), typeof(Brush))]
public sealed class DiffLineStatusToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            DiffLineStatus.Added => FindThemeBrush("DiffAddedBrush"),
            DiffLineStatus.Removed => FindThemeBrush("DiffRemovedBrush"),
            DiffLineStatus.Modified => FindThemeBrush("DiffModifiedBrush"),
            _ => Brushes.Transparent
        };

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush FindThemeBrush(string resourceKey)
        => System.Windows.Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
}
