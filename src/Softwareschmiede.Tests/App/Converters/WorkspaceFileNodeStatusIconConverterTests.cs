using System.Globalization;
using FluentAssertions;
using Softwareschmiede.App.Converters;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.App.Converters;

/// <summary>Tests für WorkspaceFileNodeStatusIconConverter.</summary>
public sealed class WorkspaceFileNodeStatusIconConverterTests
{
    private static readonly WorkspaceFileNodeStatusIconConverter Converter = new();

    /// <summary>Für gelöschte Dateien (IsDeleted-Flag) wird das Löschsymbol geliefert.</summary>
    [Fact]
    public void Convert_IsDeletedFlag_LiefertLoeschSymbol()
    {
        var node = new WorkspaceFileNode { Name = "a.txt", IsDeleted = true };

        var result = Converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("\U0001F5D1");
    }

    /// <summary>Für Git-Status "gelöscht" wird das Löschsymbol geliefert.</summary>
    [Fact]
    public void Convert_GitStatusGeloescht_LiefertLoeschSymbol()
    {
        var node = new WorkspaceFileNode { Name = "a.txt", Status = WorkspaceFileStatus.Parse(" D") };

        var result = Converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("\U0001F5D1");
    }

    /// <summary>Für nicht versionierte (neue) Dateien wird das "Neu"-Symbol geliefert.</summary>
    [Fact]
    public void Convert_Untracked_LiefertNeuSymbol()
    {
        var node = new WorkspaceFileNode { Name = "a.txt", Status = WorkspaceFileStatus.Parse("??") };

        var result = Converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("\U0001F195");
    }

    /// <summary>Für im Index hinzugefügte Dateien wird das "Neu"-Symbol geliefert.</summary>
    [Fact]
    public void Convert_IndexAdded_LiefertNeuSymbol()
    {
        var node = new WorkspaceFileNode { Name = "a.txt", Status = WorkspaceFileStatus.Parse("A ") };

        var result = Converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("\U0001F195");
    }

    /// <summary>Für geänderte Dateien wird das Änderungssymbol geliefert.</summary>
    [Fact]
    public void Convert_Geaendert_LiefertAenderungsSymbol()
    {
        var node = new WorkspaceFileNode { Name = "a.txt", Status = WorkspaceFileStatus.Parse(" M") };

        var result = Converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("✏");
    }

    /// <summary>Für unveränderte Dateien (kein Status) wird kein Symbol geliefert.</summary>
    [Fact]
    public void Convert_KeinStatus_LiefertLeerenString()
    {
        var node = new WorkspaceFileNode { Name = "a.txt" };

        var result = Converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be(string.Empty);
    }

    /// <summary>Für Verzeichnisknoten wird nie ein Status-Symbol geliefert.</summary>
    [Fact]
    public void Convert_Verzeichnis_LiefertLeerenString()
    {
        var node = new WorkspaceFileNode { Name = "src", IsDirectory = true, Status = WorkspaceFileStatus.Parse(" M") };

        var result = Converter.Convert(node, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be(string.Empty);
    }
}
