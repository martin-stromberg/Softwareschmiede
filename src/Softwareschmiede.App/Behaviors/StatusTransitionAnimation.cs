using System.Windows;
using System.Windows.Media.Animation;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.Domain.Entities;

namespace Softwareschmiede.App.Behaviors;

/// <summary>Attached Behavior, das bei einem echten Statuswechsel eine dezente Opacity-Fade-Animation auf dem Ziel-Element auslöst.</summary>
public static class StatusTransitionAnimation
{
    private const double StartOpacity = 0.3;
    private const double EndOpacity = 1.0;
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(250);

    private static readonly StatusChangeDetection ChangeDetection = new();

    private static readonly DependencyProperty UnloadedCleanupRegisteredProperty = DependencyProperty.RegisterAttached(
        "UnloadedCleanupRegistered",
        typeof(bool),
        typeof(StatusTransitionAnimation),
        new PropertyMetadata(false));

    /// <summary>Identifiziert das angehängte <c>Status</c>-Property.</summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.RegisterAttached(
        "Status",
        typeof(string),
        typeof(StatusTransitionAnimation),
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

        // Änderungsschlüssel ist die Aufgaben-Id - neben Aufgabe wird auch das
        // Panel-Item der aktiven Aufgabenliste akzeptiert (Kachel-Template).
        var workItemId = element.DataContext switch
        {
            WorkItem workItem => workItem.Id,
            ActiveWorkItemPanelItem item => item.Id,
            _ => (Guid?)null
        };
        if (workItemId is null)
            return;

        RegisterUnloadedCleanup(element, workItemId.Value);

        var newStatus = (string?)e.NewValue;
        if (!ChangeDetection.HasSichChanged(workItemId.Value, newStatus))
            return;

        var animation = new DoubleAnimation
        {
            From = StartOpacity,
            To = EndOpacity,
            Duration = new Duration(FadeDuration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static void RegisterUnloadedCleanup(FrameworkElement element, Guid workItemId)
    {
        if ((bool)element.GetValue(UnloadedCleanupRegisteredProperty))
            return;

        element.SetValue(UnloadedCleanupRegisteredProperty, true);
        element.Unloaded += (_, _) => ChangeDetection.Remove(workItemId);
    }
}
