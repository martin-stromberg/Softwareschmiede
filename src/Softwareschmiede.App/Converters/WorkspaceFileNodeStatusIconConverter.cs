using System.Globalization;
using System.Windows.Data;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.App.Converters;

/// <summary>Konvertiert einen <see cref="WorkspaceFileNode"/> in ein Symbol, das anzeigt, ob die Datei neu, gelöscht oder geändert ist.</summary>
[ValueConversion(typeof(WorkspaceFileNode), typeof(string))]
public sealed class WorkspaceFileNodeStatusIconConverter : IValueConverter
{
    private const string LoeschSymbol = "\U0001F5D1";
    private const string NeuSymbol = "\U0001F195";
    private const string AenderungsSymbol = "✏";

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not WorkspaceFileNode { IsDirectory: false } node)
            return string.Empty;

        if (node.IsDeleted)
            return LoeschSymbol;

        var status = node.Status;
        if (status is null)
            return string.Empty;

        if (status.IsDeleted)
            return LoeschSymbol;

        if (status.IsUntracked || status.IndexStatus == 'A')
            return NeuSymbol;

        if (status.IsDirty || status.IsStaged)
            return AenderungsSymbol;

        return string.Empty;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
