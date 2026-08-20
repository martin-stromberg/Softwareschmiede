using System.Windows;
using System.Windows.Input;

namespace Softwareschmiede.App.Controls;

/// <summary>Ribbon-Split-Button: Der Haupt-Button öffnet direkt, der Dropdown-Button (nur bei mehreren Einstiegspunkten sichtbar) zeigt eine Auswahl.</summary>
public sealed partial class RibbonSplitButton : RibbonButtonBase
{
    /// <summary>DependencyProperty für den Dropdown-Command.</summary>
    public static readonly DependencyProperty DropdownCommandProperty =
        DependencyProperty.Register(
            nameof(DropdownCommand),
            typeof(ICommand),
            typeof(RibbonSplitButton),
            new PropertyMetadata(null));

    /// <summary>DependencyProperty für die Sichtbarkeitssteuerung des Dropdown-Buttons.</summary>
    public static readonly DependencyProperty CanShowDropdownProperty =
        DependencyProperty.Register(
            nameof(CanShowDropdown),
            typeof(bool),
            typeof(RibbonSplitButton),
            new PropertyMetadata(false));

    /// <summary>Command, der beim Klick auf den Dropdown-Button ausgeführt wird.</summary>
    public ICommand DropdownCommand
    {
        get => (ICommand)GetValue(DropdownCommandProperty);
        set => SetValue(DropdownCommandProperty, value);
    }

    /// <summary>Steuert die Sichtbarkeit des Dropdown-Buttons.</summary>
    public bool CanShowDropdown
    {
        get => (bool)GetValue(CanShowDropdownProperty);
        set => SetValue(CanShowDropdownProperty, value);
    }

    /// <inheritdoc cref="RibbonSplitButton"/>
    public RibbonSplitButton()
    {
        InitializeComponent();
    }
}
