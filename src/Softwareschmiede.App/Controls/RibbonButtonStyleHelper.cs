using System.Windows;

namespace Softwareschmiede.App.Controls;

/// <summary>Stellt die anhängbare CornerRadius-Eigenschaft bereit, über die der gemeinsame Ribbon-Button-Style (<c>RibbonButtonStyle</c> und dessen Varianten in <c>RibbonButtonStyles.xaml</c>) pro Button-Instanz parametrisiert wird.</summary>
public static class RibbonButtonStyleHelper
{
    /// <summary>Anhängbare DependencyProperty für den im gemeinsamen Ribbon-Button-Template verwendeten CornerRadius.</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(RibbonButtonStyleHelper),
            new PropertyMetadata(new CornerRadius(4)));

    /// <summary>Liest den über <see cref="CornerRadiusProperty"/> gesetzten CornerRadius.</summary>
    /// <param name="element">Das Element, dessen CornerRadius gelesen wird.</param>
    /// <returns>Der gesetzte CornerRadius.</returns>
    public static CornerRadius GetCornerRadius(DependencyObject element)
        => (CornerRadius)element.GetValue(CornerRadiusProperty);

    /// <summary>Setzt den über <see cref="CornerRadiusProperty"/> gebundenen CornerRadius.</summary>
    /// <param name="element">Das Element, dessen CornerRadius gesetzt wird.</param>
    /// <param name="value">Der zu setzende CornerRadius.</param>
    public static void SetCornerRadius(DependencyObject element, CornerRadius value)
        => element.SetValue(CornerRadiusProperty, value);
}
