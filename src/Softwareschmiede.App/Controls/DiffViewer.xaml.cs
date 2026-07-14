using System.Windows;
using System.Windows.Controls;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Controls;

/// <summary>Wiederverwendbarer, zeilenweiser Diff-Renderer für <see cref="TextDiffLine"/>-Items.</summary>
public sealed partial class DiffViewer : UserControl
{
    /// <summary>DependencyProperty für die anzuzeigenden Diff-Zeilen.</summary>
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(
        nameof(Lines),
        typeof(IEnumerable<TextDiffLine>),
        typeof(DiffViewer),
        new PropertyMetadata(null));

    /// <summary>Die anzuzeigenden Diff-Zeilen.</summary>
    public IEnumerable<TextDiffLine>? Lines
    {
        get => (IEnumerable<TextDiffLine>?)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <inheritdoc cref="DiffViewer"/>
    public DiffViewer()
    {
        InitializeComponent();
    }
}
