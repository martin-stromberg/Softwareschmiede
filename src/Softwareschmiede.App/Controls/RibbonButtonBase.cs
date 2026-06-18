using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Softwareschmiede.App.Controls;

/// <summary>Basisklasse für Ribbon-Buttons mit gemeinsamen DependencyProperties für Command, Icon, Text und Automation-Namen.</summary>
public class RibbonButtonBase : UserControl
{
    /// <summary>DependencyProperty für den Command.</summary>
    public static readonly DependencyProperty ButtonCommandProperty =
        DependencyProperty.Register(
            nameof(ButtonCommand),
            typeof(ICommand),
            typeof(RibbonButtonBase),
            new PropertyMetadata(null));

    /// <summary>DependencyProperty für das Icon-Symbol.</summary>
    public static readonly DependencyProperty ButtonIconProperty =
        DependencyProperty.Register(
            nameof(ButtonIcon),
            typeof(string),
            typeof(RibbonButtonBase),
            new PropertyMetadata(string.Empty));

    /// <summary>DependencyProperty für den Beschriftungstext.</summary>
    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register(
            nameof(ButtonText),
            typeof(string),
            typeof(RibbonButtonBase),
            new PropertyMetadata(string.Empty));

    /// <summary>DependencyProperty für den Automation-Namen.</summary>
    public static readonly DependencyProperty AutomationNameProperty =
        DependencyProperty.Register(
            nameof(AutomationName),
            typeof(string),
            typeof(RibbonButtonBase),
            new PropertyMetadata(string.Empty));

    /// <summary>Command, der beim Klick ausgeführt wird.</summary>
    public ICommand ButtonCommand
    {
        get => (ICommand)GetValue(ButtonCommandProperty);
        set => SetValue(ButtonCommandProperty, value);
    }

    /// <summary>Unicode-Symbol oder Emoji als Icon.</summary>
    public string ButtonIcon
    {
        get => (string)GetValue(ButtonIconProperty);
        set => SetValue(ButtonIconProperty, value);
    }

    /// <summary>Beschriftungstext.</summary>
    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    /// <summary>Name für UI-Automatisierung.</summary>
    public string AutomationName
    {
        get => (string)GetValue(AutomationNameProperty);
        set => SetValue(AutomationNameProperty, value);
    }
}
