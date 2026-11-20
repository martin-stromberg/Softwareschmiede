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
/// (<see cref="Softwareschmiede.Application.Services.AiExecutionStatusClassification"/>) für
/// <see cref="AiExecutionStatusConverter"/> (Volltext) und
/// <see cref="AiExecutionStatusSymbolConverter"/> (nur Glyph): mappt die Eingabetypen
/// <see cref="WorkItem"/> und <see cref="ActiveWorkItemPanelItem"/> auf
/// <see cref="AiExecutionStatusData"/> und delegiert die eigentliche Regellogik.
/// </summary>
internal static class AiExecutionStatusAdapter
{
    /// <summary>
    /// Klassifiziert <paramref name="value"/> (<see cref="WorkItem"/> oder <see cref="ActiveWorkItemPanelItem"/>)
    /// in einen <see cref="AiExecutionState"/>; liefert <c>null</c> für nicht unterstützte Eingaben.
    /// Im Zustand <see cref="AiExecutionState.Paused"/> enthält <paramref name="pauseRemaining"/>
    /// die verbleibende Pausenzeit.
    /// </summary>
    public static AiExecutionState? Klassifiziere(object value, out TimeSpan pauseRemaining)
    {
        pauseRemaining = default;

        var status = value switch
        {
            WorkItem workItem => AiExecutionStatusData.FromWorkItem(workItem, liveRunStatus: null, hasScheduledPrompt: false),
            ActiveWorkItemPanelItem item => new AiExecutionStatusData(item.Status, item.ExecutionStatus, item.ActiveRunId, item.LastHeartbeatUtc, item.RunStatus, item.LiveRunStatus, item.HasScheduledPrompt, item.PausedUntilUtc),
            _ => null
        };

        if (status is null)
        {
            return null;
        }

        return Softwareschmiede.Application.Services.AiExecutionStatusClassification
            .Klassifiziere(status, out pauseRemaining);
    }
}

/// <summary>Konvertiert eine Aufgabe oder ein Aufgabenpanel-Item in einen KI-Ausführungsstatus-String.</summary>
[ValueConversion(typeof(object), typeof(string))]
public sealed class AiExecutionStatusConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = AiExecutionStatusAdapter.Klassifiziere(value, out var pauseRemaining);
        return state switch
        {
            null => string.Empty,
            AiExecutionState.Paused => $"⏸ Pausiert (noch {FormatRemainingTime(pauseRemaining)})",
            AiExecutionState.PromptQueued => "⏳ Prompt in Wartestellung",
            AiExecutionState.Running => "▶ Läuft",
            AiExecutionState.Waiting => "⏸ Wartet",
            _ => "✓ Bereit"
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string FormatRemainingTime(TimeSpan remaining)
        => remaining >= TimeSpan.FromDays(1)
            ? remaining.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)
            : remaining.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// Konvertiert eine Aufgabe oder ein Aufgabenpanel-Item in den reinen Status-Glyph
/// (<c>⏸</c>/<c>⏳</c>/<c>▶</c>/<c>✓</c>) ohne Text oder Countdown — Symbolzeile der Aufgabenkachel.
/// </summary>
[ValueConversion(typeof(object), typeof(string))]
public sealed class AiExecutionStatusSymbolConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = AiExecutionStatusAdapter.Klassifiziere(value, out _);
        return state switch
        {
            null => string.Empty,
            AiExecutionState.Paused => "⏸",
            AiExecutionState.PromptQueued => "⏳",
            AiExecutionState.Running => "▶",
            AiExecutionState.Waiting => "⏸",
            _ => "✓"
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
