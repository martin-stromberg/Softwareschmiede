using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using static Softwareschmiede.Tests.Helpers.WpfUnitTestHelpers;

namespace Softwareschmiede.Tests.App.Views;

/// <summary>Rendered queue cells remain readable when the application theme changes.</summary>
public sealed class ScmEventQueueViewTests_Theme
{
    /// <summary>Filled rows, headers and selected cells have readable text in both themes.</summary>
    [Fact]
    public void Queue_ThemeChanges_RowsHeadersAndSelectionKeepReadableContrast()
    {
        RunOnSta(() =>
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Softwareschmiede.slnx"))) root = root.Parent;
            var document = XDocument.Load(Path.Combine(root!.FullName, "src", "Softwareschmiede.App", "Views", "ScmEventQueueView.xaml"));
            var markup = document.Descendants().Single(x => x.Name.LocalName == "DataGrid");
            markup.Attribute("MouseDoubleClick")!.Remove();
            markup.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
            var grid = (DataGrid)XamlReader.Parse(markup.ToString());
            grid.ItemsSource = new[] { new { TaskTitle = "SCM Aufgabe", Repository = "owner/repo", Reference = "Issue #7",
                Kind = "Kommentar", Received = "14.10.2026", StatusText = "Wartend" } };
            foreach (var theme in new[] { "Dark", "Light", "Dark" })
            {
                grid.Resources.MergedDictionaries.Clear();
                grid.Resources.MergedDictionaries.Add(new ResourceDictionary
                { Source = new Uri($"/Softwareschmiede.App;component/Themes/{theme}Theme.xaml", UriKind.Relative) });
                grid.SelectedItem = null;
                grid.Measure(new Size(1100, 240));
                grid.Arrange(new Rect(0, 0, 1100, 240));
                grid.UpdateLayout();
                var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
                AssertReadable(row.Foreground, row.Background, theme + " row");
                var cells = Descendants<DataGridCell>(row).ToArray();
                Assert.Equal(6, cells.Length);
                Assert.All(cells, cell => AssertReadable(cell.Foreground, EffectiveBackground(cell), theme + " cell"));
                Assert.All(Descendants<DataGridColumnHeader>(grid).Where(x => x.Content is not null),
                    header => AssertReadable(header.Foreground, header.Background, theme + " header"));
                grid.SelectedIndex = 0;
                grid.UpdateLayout();
                Assert.True(row.IsSelected);
                Assert.False(Selector.GetIsSelectionActive(row));
                AssertReadable(row.Foreground, row.Background, theme + " inactive selected row");
                Assert.All(cells, cell => AssertReadable(cell.Foreground, EffectiveBackground(cell), theme + " inactive selected cell"));
            }
        });
    }

    private static Brush EffectiveBackground(Control control)
    {
        DependencyObject? current = control;
        while (current is not null)
        {
            if (current is Control { Background: SolidColorBrush brush } && brush.Color.A == 255) return brush;
            current = VisualTreeHelper.GetParent(current);
        }
        throw new InvalidOperationException("No opaque background found.");
    }

    private static void AssertReadable(Brush foreground, Brush background, string state)
    {
        var text = ((SolidColorBrush)foreground).Color;
        var surface = ((SolidColorBrush)background).Color;
        var a = Luminance(text);
        var b = Luminance(surface);
        var contrast = (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        Assert.True(contrast >= 4.5, $"{state}: {text} on {surface}, contrast {contrast:F2}:1");
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value) => value / 255d <= 0.04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
        {
            var child = VisualTreeHelper.GetChild(node, index);
            if (child is T item) yield return item;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
