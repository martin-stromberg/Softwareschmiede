using System.Globalization;
using System.IO;
using System.Windows.Data;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Converters;

/// <summary>Konvertiert einen <see cref="WorkspaceFileNode"/> in ein generisches, für einige Dateitypen spezifisches Symbol.</summary>
[ValueConversion(typeof(WorkspaceFileNode), typeof(string))]
public sealed class WorkspaceFileNodeIconConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not WorkspaceFileNode node)
            return string.Empty;

        if (node.IsDirectory)
            return "\U0001F4C1";

        return Path.GetExtension(node.Name).ToLowerInvariant() switch
        {
            ".md" or ".markdown" => "\U0001F4DD",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".bmp" or ".ico" or ".webp" => "\U0001F5BC",
            ".json" or ".xml" or ".yaml" or ".yml" or ".config" or ".toml" or ".ini" => "⚙",
            ".cs" or ".xaml" or ".ts" or ".tsx" or ".js" or ".jsx" or ".py" or ".java"
                or ".cpp" or ".c" or ".h" or ".go" or ".rs" or ".html" or ".htm" or ".css"
                or ".razor" or ".cshtml" or ".sql" or ".ps1" or ".sh" => "\U0001F4DC",
            _ => "\U0001F4C4"
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
