using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.Converters;

/// <summary>Konvertiert einen Enum-Wert auf <see cref="bool"/> für RadioButton-Bindungen.</summary>
/// <remarks>ConverterParameter enthält den Enum-Wert, gegen den verglichen wird.</remarks>
[ValueConversion(typeof(object), typeof(bool))]
public sealed class EnumToBoolConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true && parameter is not null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}

/// <summary>Konvertiert einen Boolean-Wert in <see cref="Visibility"/>.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>Konvertiert einen Boolean-Wert in eine Breite (ExpandedWidth wenn true, CollapsedWidth wenn false).</summary>
[ValueConversion(typeof(bool), typeof(double))]
public sealed class BoolToWidthConverter : IValueConverter
{
    /// <summary>Breite im aufgeklappten Zustand (true). Standard: 240.</summary>
    public double ExpandedWidth { get; set; } = 240.0;

    /// <summary>Breite im eingeklappten Zustand (false). Standard: 48.</summary>
    public double CollapsedWidth { get; set; } = 48.0;

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? ExpandedWidth : CollapsedWidth;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d && d > CollapsedWidth;
}

/// <summary>Kehrt einen Boolean-Wert um.</summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBoolConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is false;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is false;
}

/// <summary>Konvertiert einen inversen Boolean-Wert in <see cref="Visibility"/>.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is false ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}

/// <summary>Konvertiert einen Wert in <see cref="Visibility"/> (Collapsed wenn null, leer oder Leerstring).</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return Visibility.Collapsed;
        if (value is string s) return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Konvertiert einen Integer-Wert in <see cref="Visibility"/> (Visible wenn größer als 0).</summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public sealed class GreaterThanZeroToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// App-seitiger Adapter der gemeinsamen Statusklassifikation
/// (<see cref="Softwareschmiede.Application.Services.KiAusfuehrungsStatusKlassifikation"/>) für
/// <see cref="KiAusfuehrungsStatusConverter"/> (Volltext) und
/// <see cref="KiAusfuehrungsStatusSymbolConverter"/> (nur Glyph): mappt die Eingabetypen
/// <see cref="Aufgabe"/> und <see cref="AktiveAufgabePanelItem"/> auf
/// <see cref="KiAusfuehrungsStatusDaten"/> und delegiert die eigentliche Regellogik.
/// </summary>
internal static class KiAusfuehrungsStatusAdapter
{
    /// <summary>
    /// Klassifiziert <paramref name="value"/> (<see cref="Aufgabe"/> oder <see cref="AktiveAufgabePanelItem"/>)
    /// in einen <see cref="KiAusfuehrungsZustand"/>; liefert <c>null</c> für nicht unterstützte Eingaben.
    /// Im Zustand <see cref="KiAusfuehrungsZustand.Pausiert"/> enthält <paramref name="pausiertRestzeit"/>
    /// die verbleibende Pausenzeit.
    /// </summary>
    public static KiAusfuehrungsZustand? Klassifiziere(object value, out TimeSpan pausiertRestzeit)
    {
        pausiertRestzeit = default;

        var status = value switch
        {
            Aufgabe aufgabe => KiAusfuehrungsStatusDaten.VonAufgabe(aufgabe, liveLaufStatus: null, hasScheduledPrompt: false),
            AktiveAufgabePanelItem item => new KiAusfuehrungsStatusDaten(item.Status, item.AusfuehrungsStatus, item.AktiveRunId, item.LastHeartbeatUtc, item.LaufStatus, item.LiveLaufStatus, item.HasScheduledPrompt, item.PausiertBisUtc),
            _ => null
        };

        if (status is null)
        {
            return null;
        }

        return Softwareschmiede.Application.Services.KiAusfuehrungsStatusKlassifikation
            .Klassifiziere(status, out pausiertRestzeit);
    }
}

/// <summary>Konvertiert eine Aufgabe oder ein Aufgabenpanel-Item in einen KI-Ausführungsstatus-String.</summary>
[ValueConversion(typeof(object), typeof(string))]
public sealed class KiAusfuehrungsStatusConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var zustand = KiAusfuehrungsStatusAdapter.Klassifiziere(value, out var pausiertRestzeit);
        return zustand switch
        {
            null => string.Empty,
            KiAusfuehrungsZustand.Pausiert => $"⏸ Pausiert (noch {FormatiereRestzeit(pausiertRestzeit)})",
            KiAusfuehrungsZustand.PromptInWartestellung => "⏳ Prompt in Wartestellung",
            KiAusfuehrungsZustand.Laeuft => "▶ Läuft",
            KiAusfuehrungsZustand.Wartet => "⏸ Wartet",
            _ => "✓ Bereit"
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string FormatiereRestzeit(TimeSpan restzeit)
        => restzeit >= TimeSpan.FromDays(1)
            ? restzeit.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)
            : restzeit.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// Konvertiert eine Aufgabe oder ein Aufgabenpanel-Item in den reinen Status-Glyph
/// (<c>⏸</c>/<c>⏳</c>/<c>▶</c>/<c>✓</c>) ohne Text oder Countdown — Symbolzeile der Aufgabenkachel.
/// </summary>
[ValueConversion(typeof(object), typeof(string))]
public sealed class KiAusfuehrungsStatusSymbolConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var zustand = KiAusfuehrungsStatusAdapter.Klassifiziere(value, out _);
        return zustand switch
        {
            null => string.Empty,
            KiAusfuehrungsZustand.Pausiert => "⏸",
            KiAusfuehrungsZustand.PromptInWartestellung => "⏳",
            KiAusfuehrungsZustand.Laeuft => "▶",
            KiAusfuehrungsZustand.Wartet => "⏸",
            _ => "✓"
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
