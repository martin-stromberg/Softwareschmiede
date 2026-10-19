using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Softwareschmiede.App.ViewModels;

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
            new PropertyMetadata(null, OnItemsSourceChanged));

    /// <summary>Dependency Property für den Navigations-Command.</summary>
    public static readonly DependencyProperty NavigateCommandProperty =
        DependencyProperty.Register(
            nameof(NavigateCommand),
            typeof(ICommand),
            typeof(ActiveTasksListControl),
            new PropertyMetadata(null));

    /// <summary>Dependency Property für den Anzeigemodus der Liste (flach oder gruppiert).</summary>
    public static readonly DependencyProperty ListenModusProperty =
        DependencyProperty.Register(
            nameof(ListenModus),
            typeof(AufgabenListenModus),
            typeof(ActiveTasksListControl),
            new PropertyMetadata(AufgabenListenModus.Datum, OnListenModusChanged));

    // Instanz-eigene Collection-View: Die ItemsSource wird zwischen Seitenleiste und Dashboard
    // geteilt - CollectionViewSource.GetDefaultView wäre eine gemeinsame Default-View und würde
    // die Gruppierung einer Ansicht in die andere leaken.
    private ListCollectionView? _view;

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

    /// <summary>Der Anzeigemodus der Liste (flach nach Datum oder gruppiert nach Repository/KI).</summary>
    public AufgabenListenModus ListenModus
    {
        get => (AufgabenListenModus)GetValue(ListenModusProperty);
        set => SetValue(ListenModusProperty, value);
    }

    /// <inheritdoc cref="ActiveTasksListControl"/>
    public ActiveTasksListControl()
    {
        InitializeComponent();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ActiveTasksListControl)d;
        control.ErstelleAnsicht(e.NewValue);
    }

    private static void OnListenModusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ActiveTasksListControl)d;
        if (control._view is not null)
        {
            AufgabenListenAnsicht.Konfiguriere(control._view, (AufgabenListenModus)e.NewValue);
        }
    }

    private void ErstelleAnsicht(object? itemsSource)
    {
        if (itemsSource is IList liste)
        {
            _view = new ListCollectionView(liste);
            AufgabenListenAnsicht.Konfiguriere(_view, ListenModus);
        }
        else
        {
            _view = null;
        }

        // Die View wird direkt zugewiesen (nicht über ein ItemsSource-Binding am ItemsControl):
        // Ein Binding auf die gemeinsame Auflistung würde die Default-View verwenden, auf der
        // die Sort-/Gruppendeskriptoren dieser Instanz nicht liegen.
        AufgabenItemsControl.ItemsSource = _view ?? (IEnumerable?)itemsSource;
    }

    private void OffeneTodosButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }
}
