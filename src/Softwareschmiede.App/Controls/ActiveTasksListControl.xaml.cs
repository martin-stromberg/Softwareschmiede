using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Softwareschmiede.App.Controls;

/// <summary>Control für die Anzeige aktiver Aufgaben als Kachelliste (Seitenleiste und Dashboard).</summary>
public sealed partial class ActiveTasksListControl : UserControl
{
    /// <summary>Dependency Property für die anzuzeigenden aktiven Aufgaben.</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(ActiveTasksListControl),
            new PropertyMetadata(null));

    /// <summary>Dependency Property für den Navigations-Command.</summary>
    public static readonly DependencyProperty NavigateCommandProperty =
        DependencyProperty.Register(
            nameof(NavigateCommand),
            typeof(ICommand),
            typeof(ActiveTasksListControl),
            new PropertyMetadata(null));

    /// <summary>Dependency Property für die Anzeige des Navigations-Buttons.</summary>
    public static readonly DependencyProperty ShowNavigationButtonProperty =
        DependencyProperty.Register(
            nameof(ShowNavigationButton),
            typeof(bool),
            typeof(ActiveTasksListControl),
            new PropertyMetadata(true));

    /// <summary>Die anzuzeigenden aktiven Aufgaben.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Command für die Navigation zu einer Aufgabe.</summary>
    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }

    /// <summary>Gibt an, ob der separate Navigations-Button angezeigt wird. Wenn <c>false</c>, ist stattdessen die gesamte Kachel klickbar.</summary>
    public bool ShowNavigationButton
    {
        get => (bool)GetValue(ShowNavigationButtonProperty);
        set => SetValue(ShowNavigationButtonProperty, value);
    }

    /// <inheritdoc cref="ActiveTasksListControl"/>
    public ActiveTasksListControl()
    {
        InitializeComponent();
    }
}
