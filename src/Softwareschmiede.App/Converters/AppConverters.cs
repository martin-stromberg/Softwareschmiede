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

/// <summary>Konvertiert eine Aufgabe oder ein Aufgabenpanel-Item in einen KI-Ausführungsstatus-String.</summary>
[ValueConversion(typeof(object), typeof(string))]
public sealed class KiAusfuehrungsStatusConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value switch
        {
            Aufgabe aufgabe => new StatusDaten(aufgabe.Status, aufgabe.AktiveRunId, aufgabe.LastHeartbeatUtc, aufgabe.LaufStatus, false),
            AktiveAufgabePanelItem item => new StatusDaten(item.Status, item.AktiveRunId, item.LastHeartbeatUtc, item.LaufStatus, item.HasScheduledPrompt),
            _ => null
        };

        if (status is null)
        {
            return string.Empty;
        }

        if (status.HasScheduledPrompt)
        {
            return "⏳ Prompt in Wartestellung";
        }

        if (status.AktiveRunId != null
            && status.LastHeartbeatUtc != null
            && DateTimeOffset.UtcNow - status.LastHeartbeatUtc.Value < TimeSpan.FromMinutes(AufgabeRecoveryService.HeartbeatTimeoutMinutes))
        {
            // LaufStatus bildet den Laufzeit-Substatus der CLI ab (siehe PseudoConsoleSession.RuntimeStatus,
            // persistiert über CliProcessManager/AufgabeService.AktualisiereLaufStatusAsync). Ohne diesen
            // Substatus (null, z. B. beim klassischen Start ohne ConPTY) bleibt es beim bisherigen "▶ Läuft".
            return status.LaufStatus == AufgabeLaufStatus.WartetAufEingabe
                ? "⏸ Wartet"
                : "▶ Läuft";
        }

        if (status.Status == AufgabeStatus.Wartend)
        {
            return "⏸ Wartet";
        }

        return "✓ Bereit";
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private sealed record StatusDaten(
        AufgabeStatus Status,
        string? AktiveRunId,
        DateTimeOffset? LastHeartbeatUtc,
        AufgabeLaufStatus? LaufStatus,
        bool HasScheduledPrompt);
}
