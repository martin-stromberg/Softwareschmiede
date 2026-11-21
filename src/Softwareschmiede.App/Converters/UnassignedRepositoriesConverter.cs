using System.Globalization;
using System.Windows.Data;

namespace Softwareschmiede.App.Converters;

/// <summary>Konvertiert ein <see cref="DateTime"/> in eine relative Zeitangabe (z. B. "vor 2 Stunden").</summary>
[ValueConversion(typeof(DateTime), typeof(string))]
public sealed class UnassignedRepositoriesConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime || dateTime == DateTime.MinValue)
            return "unbekannt";

        var diff = DateTime.UtcNow - dateTime.ToUniversalTime();

        if (diff.TotalMinutes < 1)
            return "gerade eben";

        if (diff.TotalMinutes < 60)
        {
            var minutes = (int)diff.TotalMinutes;
            return minutes == 1 ? "vor 1 Minute" : $"vor {minutes} Minuten";
        }

        if (diff.TotalHours < 24)
        {
            var hours = (int)diff.TotalHours;
            return hours == 1 ? "vor 1 Stunde" : $"vor {hours} Stunden";
        }

        if (diff.TotalDays < 30)
        {
            var day = (int)diff.TotalDays;
            return day == 1 ? "vor 1 Tag" : $"vor {day} Tagen";
        }

        if (diff.TotalDays < 365)
        {
            var month = (int)(diff.TotalDays / 30);
            return month == 1 ? "vor 1 Monat" : $"vor {month} Monaten";
        }

        var year = (int)(diff.TotalDays / 365);
        return year == 1 ? "vor 1 Jahr" : $"vor {year} Jahren";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
