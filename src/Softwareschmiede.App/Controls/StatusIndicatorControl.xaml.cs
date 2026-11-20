using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.App.Controls;

/// <summary>Visuelle Anzeige des Aufgaben-Status mit farbiger Statusmarkierung.</summary>
public sealed partial class StatusIndicatorControl : UserControl
{
    /// <summary>Dependency Property für den Aufgaben-Status.</summary>
    /// <returns>Registrierte <see cref="DependencyProperty"/>-Instanz.</returns>
    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(
            nameof(Status),
            typeof(WorkItemStatus),
            typeof(StatusIndicatorControl),
            new PropertyMetadata(WorkItemStatus.New, OnStatusChanged));

    /// <summary>Dependency Property für den Statustext.</summary>
    /// <returns>Registrierte <see cref="DependencyProperty"/>-Instanz.</returns>
    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(
            nameof(StatusText),
            typeof(string),
            typeof(StatusIndicatorControl),
            new PropertyMetadata(string.Empty));

    /// <summary>Dependency Property für die Statusfarbe.</summary>
    /// <returns>Registrierte <see cref="DependencyProperty"/>-Instanz.</returns>
    public static readonly DependencyProperty StatusColorProperty =
        DependencyProperty.Register(
            nameof(StatusColor),
            typeof(Brush),
            typeof(StatusIndicatorControl),
            new PropertyMetadata(Brushes.Gray));

    /// <summary>Dependency Property für den Branch-Namen.</summary>
    /// <returns>Registrierte <see cref="DependencyProperty"/>-Instanz.</returns>
    public static readonly DependencyProperty BranchNameProperty =
        DependencyProperty.Register(
            nameof(BranchName),
            typeof(string),
            typeof(StatusIndicatorControl),
            new PropertyMetadata(null));

    /// <summary>Aufgaben-Status.</summary>
    public WorkItemStatus Status
    {
        get => (WorkItemStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>Anzeigetext für den Status.</summary>
    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        private set => SetValue(StatusTextProperty, value);
    }

    /// <summary>Farbe des Status-Indikators.</summary>
    public Brush StatusColor
    {
        get => (Brush)GetValue(StatusColorProperty);
        private set => SetValue(StatusColorProperty, value);
    }

    /// <summary>Branch-Name der Aufgabe.</summary>
    public string? BranchName
    {
        get => (string?)GetValue(BranchNameProperty);
        set => SetValue(BranchNameProperty, value);
    }

    /// <inheritdoc cref="StatusIndicatorControl"/>
    public StatusIndicatorControl()
    {
        InitializeComponent();
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusIndicatorControl control && e.NewValue is WorkItemStatus status)
        {
            control.AktualisierenFuerStatus(status);
        }
    }

    private void AktualisierenFuerStatus(WorkItemStatus status)
    {
        (StatusText, StatusColor) = status switch
        {
            WorkItemStatus.New => ("Neu", Brushes.Gray),
            WorkItemStatus.Started => ("Gestartet", Brushes.DodgerBlue),
            WorkItemStatus.Waiting => ("Wartend", Brushes.Goldenrod),
            WorkItemStatus.Completed => ("Beendet", Brushes.SeaGreen),
            WorkItemStatus.Archived => ("Archiviert", Brushes.DimGray),
            _ => (status.ToString(), Brushes.Gray)
        };
    }
}
