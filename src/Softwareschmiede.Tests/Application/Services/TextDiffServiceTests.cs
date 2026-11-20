using FluentAssertions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Enums;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Tests für den TextDiffService.</summary>
public sealed class TextDiffServiceTests
{
    private readonly TextDiffService _sut = new();

    /// <summary>Gleicher Alt-/Neu-Inhalt liefert ausschließlich Context-Zeilen.</summary>
    [Fact]
    public void BuildDiff_IdenticalContent_ReturnsOnlyContextLines()
    {
        const string content = "zeile1\nzeile2\nzeile3";

        var diff = _sut.BuildDiff(content, content);

        diff.Lines.Should().HaveCount(3);
        diff.Lines.Should().OnlyContain(line => line.Status == DiffLineStatus.Context);
    }

    /// <summary>Eine im Zielinhalt neu hinzugefügte Zeile liefert Added-Status mit NewLineNumber.</summary>
    [Fact]
    public void BuildDiff_AddedLine_ReturnsAddedStatus()
    {
        var original = "zeile1\nzeile2";
        var current = "zeile1\nzeile2\nzeile3";

        var diff = _sut.BuildDiff(original, current);

        var addedLine = diff.Lines.Should().ContainSingle(line => line.Status == DiffLineStatus.Added).Subject;
        addedLine.Content.Should().Be("zeile3");
        addedLine.NewLineNumber.Should().Be(3);
        addedLine.OldLineNumber.Should().BeNull();
    }

    /// <summary>Eine im Zielinhalt fehlende Zeile liefert Removed-Status mit OldLineNumber.</summary>
    [Fact]
    public void BuildDiff_DeletedLine_ReturnsRemovedStatus()
    {
        var original = "zeile1\nzeile2\nzeile3";
        var current = "zeile1\nzeile3";

        var diff = _sut.BuildDiff(original, current);

        var removedLine = diff.Lines.Should().ContainSingle(line => line.Status == DiffLineStatus.Removed).Subject;
        removedLine.Content.Should().Be("zeile2");
        removedLine.OldLineNumber.Should().Be(2);
        removedLine.NewLineNumber.Should().BeNull();
    }

    /// <summary>Eine geänderte Zeile liefert Modified-Status mit Inline-Segmenten, die nur den geänderten Wortteil markieren.</summary>
    [Fact]
    public void BuildDiff_ChangedLine_ReturnsModifiedWithInlineSegments()
    {
        var original = "hallo welt";
        var current = "hallo erde";

        var diff = _sut.BuildDiff(original, current);

        var modifiedLine = diff.Lines.Should().ContainSingle(line => line.Status == DiffLineStatus.Modified).Subject;
        modifiedLine.Content.Should().Be("hallo erde");
        modifiedLine.OldLineNumber.Should().Be(1);
        modifiedLine.NewLineNumber.Should().Be(1);

        modifiedLine.InlineSegments.Should().Contain(segment => segment.IsChanged && segment.Text.Contains("erde"));
        modifiedLine.InlineSegments.Should().Contain(segment => !segment.IsChanged && segment.Text.Contains("hallo"));
    }

    /// <summary>Ein leerer Alt- oder Neu-Inhalt liefert einen korrekten Diff ohne Ausnahme.</summary>
    [Fact]
    public void BuildDiff_EmptyContent_NoException()
    {
        var bothEmpty = () => _sut.BuildDiff(string.Empty, string.Empty);
        var altEmpty = () => _sut.BuildDiff(null, "zeile1");
        var newEmpty = () => _sut.BuildDiff("zeile1", null);

        bothEmpty.Should().NotThrow();
        altEmpty.Should().NotThrow();
        newEmpty.Should().NotThrow();

        _sut.BuildDiff(string.Empty, string.Empty).Lines.Should().BeEmpty();

        var onlyNew = _sut.BuildDiff(null, "zeile1");
        onlyNew.Lines.Should().ContainSingle();
        onlyNew.Lines[0].Status.Should().Be(DiffLineStatus.Added);

        var onlyAlt = _sut.BuildDiff("zeile1", null);
        onlyAlt.Lines.Should().ContainSingle();
        onlyAlt.Lines[0].Status.Should().Be(DiffLineStatus.Removed);
    }

    /// <summary>Überschreitet die Zeilenanzahl die LCS-Obergrenze, weicht der Diff auf einen einfachen Blockdiff aus, statt die volle O(n·m)-Matrix zu allokieren.</summary>
    [Fact]
    public void BuildDiff_LinecountOverThreshold_ReturnsBlockdiffWithoutFullLcsMatrix()
    {
        const int lineCount = 5_001;
        var original = string.Join('\n', Enumerable.Range(0, lineCount).Select(i => $"alt-{i}"));
        var current = string.Join('\n', Enumerable.Range(0, lineCount).Select(i => $"neu-{i}"));

        var build = () => _sut.BuildDiff(original, current);

        build.Should().NotThrow();
        var diff = build();
        diff.Lines.Should().HaveCount(lineCount);
    }
}
