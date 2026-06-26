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
            var minuten = (int)diff.TotalMinutes;
            return minuten == 1 ? "vor 1 Minute" : $"vor {minuten} Minuten";
        }

        if (diff.TotalHours < 24)
        {
            var stunden = (int)diff.TotalHours;
            return stunden == 1 ? "vor 1 Stunde" : $"vor {stunden} Stunden";
        }

        if (diff.TotalDays < 30)
        {
            var tage = (int)diff.TotalDays;
            return tage == 1 ? "vor 1 Tag" : $"vor {tage} Tagen";
        }

        if (diff.TotalDays < 365)
        {
            var monate = (int)(diff.TotalDays / 30);
            return monate == 1 ? "vor 1 Monat" : $"vor {monate} Monaten";
        }

        var jahre = (int)(diff.TotalDays / 365);
        return jahre == 1 ? "vor 1 Jahr" : $"vor {jahre} Jahren";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
