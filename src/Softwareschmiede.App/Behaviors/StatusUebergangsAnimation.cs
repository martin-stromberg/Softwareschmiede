using System.Windows;
using System.Windows.Media.Animation;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.Behaviors;

/// <summary>Attached Behavior, das bei einem echten Statuswechsel eine dezente Opacity-Fade-Animation auf dem Ziel-Element auslöst.</summary>
public static class StatusUebergangsAnimation
{
    private const double StartOpacity = 0.3;
    private const double EndOpacity = 1.0;
    private static readonly TimeSpan FadeDauer = TimeSpan.FromMilliseconds(250);

    private static readonly StatusAenderungsErkennung AenderungsErkennung = new();

    private static readonly DependencyProperty UnloadedBereinigungRegistriertProperty = DependencyProperty.RegisterAttached(
        "UnloadedBereinigungRegistriert",
        typeof(bool),
        typeof(StatusUebergangsAnimation),
        new PropertyMetadata(false));

    /// <summary>Identifiziert das angehängte <c>Status</c>-Property.</summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.RegisterAttached(
        "Status",
        typeof(string),
        typeof(StatusUebergangsAnimation),
        new PropertyMetadata(null, OnStatusChanged));

    /// <summary>Ruft den Wert des angehängten <c>Status</c>-Property ab.</summary>
    /// <param name="element">Das Element, dessen Status abgerufen wird.</param>
    /// <returns>Der aktuell gesetzte Status.</returns>
    public static string? GetStatus(UIElement element) => (string?)element.GetValue(StatusProperty);

    /// <summary>Setzt den Wert des angehängten <c>Status</c>-Property.</summary>
    /// <param name="element">Das Element, dessen Status gesetzt wird.</param>
    /// <param name="value">Der neue Status.</param>
    public static void SetStatus(UIElement element, string? value) => element.SetValue(StatusProperty, value);

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        if (element.DataContext is not Aufgabe aufgabe)
            return;

        RegistriereUnloadedBereinigung(element, aufgabe.Id);

        var neuerStatus = (string?)e.NewValue;
        if (!AenderungsErkennung.HatSichGeaendert(aufgabe.Id, neuerStatus))
            return;

        var animation = new DoubleAnimation
        {
            From = StartOpacity,
            To = EndOpacity,
            Duration = new Duration(FadeDauer),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static void RegistriereUnloadedBereinigung(FrameworkElement element, Guid aufgabeId)
    {
        if ((bool)element.GetValue(UnloadedBereinigungRegistriertProperty))
            return;

        element.SetValue(UnloadedBereinigungRegistriertProperty, true);
        element.Unloaded += (_, _) => AenderungsErkennung.Entfernen(aufgabeId);
    }
}
