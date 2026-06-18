using System.Windows;
using System.Windows.Controls;

namespace Softwareschmiede.App.Controls;

/// <summary>Eine Ribbon-Gruppe mit abgerundetem Rahmen und Gruppenname am unteren Rand.</summary>
public sealed partial class RibbonGroup : UserControl
{
    /// <summary>DependencyProperty für den Gruppennamen.</summary>
    public static readonly DependencyProperty GruppenNameProperty =
        DependencyProperty.Register(
            nameof(GruppenName),
            typeof(string),
            typeof(RibbonGroup),
            new PropertyMetadata(string.Empty));

    /// <summary>DependencyProperty für den Gruppeninhalt.</summary>
    public static readonly DependencyProperty ItemsContentProperty =
        DependencyProperty.Register(
            nameof(ItemsContent),
            typeof(object),
            typeof(RibbonGroup),
            new PropertyMetadata(null));

    /// <summary>Name der Gruppe, der am unteren Rand des Rahmens angezeigt wird.</summary>
    public string GruppenName
    {
        get => (string)GetValue(GruppenNameProperty);
        set => SetValue(GruppenNameProperty, value);
    }

    /// <summary>Inhalt der Gruppe (Buttons).</summary>
    public object ItemsContent
    {
        get => GetValue(ItemsContentProperty);
        set => SetValue(ItemsContentProperty, value);
    }

    /// <inheritdoc cref="RibbonGroup"/>
    public RibbonGroup()
    {
        InitializeComponent();
    }
}
