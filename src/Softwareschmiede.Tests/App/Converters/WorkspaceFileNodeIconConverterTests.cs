using System.Globalization;
using FluentAssertions;
using Softwareschmiede.App.Converters;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.Converters;

/// <summary>Tests für WorkspaceFileNodeIconConverter.</summary>
public sealed class WorkspaceFileNodeIconConverterTests
{
    /// <summary>Für Verzeichnisknoten wird unabhängig vom Namen immer das Ordnersymbol geliefert.</summary>
    [Fact]
    public void Convert_Verzeichnis_LiefertOrdnerSymbol()
    {
        var converter = new WorkspaceFileNodeIconConverter();
        var node = new WorkspaceFileNode { Name = "src", IsDirectory = true };

        var result = converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("\U0001F4C1");
    }

    /// <summary>Für bekannte Dateitypen (hier: Quellcode) wird ein spezifisches statt des generischen Symbols geliefert.</summary>
    /// <param name="dateiname">Der zu prüfende Dateiname mit Quellcode-Endung.</param>
    [Theory]
    [InlineData("Program.cs")]
    [InlineData("View.xaml")]
    public void Convert_QuellcodeDatei_LiefertSpezifischesSymbol(string dateiname)
    {
        var converter = new WorkspaceFileNodeIconConverter();
        var node = new WorkspaceFileNode { Name = dateiname, IsDirectory = false };

        var result = converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("\U0001F4DC");
    }

    /// <summary>Für unbekannte Dateitypen wird das generische Dateisymbol geliefert.</summary>
    [Fact]
    public void Convert_UnbekannterDateityp_LiefertGenerischesDateiSymbol()
    {
        var converter = new WorkspaceFileNodeIconConverter();
        var node = new WorkspaceFileNode { Name = "unbekannt.xyz", IsDirectory = false };

        var result = converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("\U0001F4C4");
    }

    /// <summary>Für einen ungültigen Eingabewert wird ein leerer String statt einer Ausnahme geliefert.</summary>
    [Fact]
    public void Convert_KeinWorkspaceFileNode_LiefertLeerenString()
    {
        var converter = new WorkspaceFileNodeIconConverter();

        var result = converter.Convert("kein-node", typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be(string.Empty);
    }
}
