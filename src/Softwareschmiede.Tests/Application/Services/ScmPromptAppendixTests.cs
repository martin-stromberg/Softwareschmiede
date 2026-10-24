using FluentAssertions;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Unit-Tests für <see cref="ScmPromptAppendix"/>: Ereignisart-Mapping, Reihenfolge und Leer-Semantik.</summary>
public sealed class ScmPromptAppendixTests
{
    private static readonly ScmEventRepository Repository =
        new("test-scm", "github.com", "R_1", "owner", "repo", "U_1", "U_1", "owner", true);

    /// <summary>Referenztexte (Description) wählen den Ereignis-Anhang; Kommentare und jede Form von
    /// PR-Review (Zeilenkommentare und veröffentlichte Reviews) wählen den Kommentar-Anhang der Referenzart.</summary>
    [Theory]
    [InlineData(ScmReferenceKind.Issue, ScmContentKind.Description, AppEinstellungService.ScmPromptAppendIssueKey)]
    [InlineData(ScmReferenceKind.Issue, ScmContentKind.IssueComment, AppEinstellungService.ScmPromptAppendIssueCommentKey)]
    [InlineData(ScmReferenceKind.PullRequest, ScmContentKind.Description, AppEinstellungService.ScmPromptAppendPullRequestKey)]
    [InlineData(ScmReferenceKind.PullRequest, ScmContentKind.IssueComment, AppEinstellungService.ScmPromptAppendPullRequestCommentKey)]
    [InlineData(ScmReferenceKind.PullRequest, ScmContentKind.ReviewComment, AppEinstellungService.ScmPromptAppendPullRequestCommentKey)]
    [InlineData(ScmReferenceKind.PullRequest, ScmContentKind.PullRequestReview, AppEinstellungService.ScmPromptAppendPullRequestCommentKey)]
    public void SpecificKeyFor_MapsReferenceAndContentKind(ScmReferenceKind kind, ScmContentKind contentKind, string expected)
        => ScmPromptAppendix.SpecificKeyFor(Observation(kind, contentKind)).Should().Be(expected);

    /// <summary>Der allgemeine Anhang steht vor dem spezifischen, mit einer Leerzeile getrennt.</summary>
    [Fact]
    public void Combine_GeneralThenSpecific_SeparatedByBlankLine()
        => ScmPromptAppendix.Combine("allgemein", "speziell").Should().Be("allgemein\n\nspeziell");

    /// <summary>Ein leerer spezifischer Anhang ersetzt den allgemeinen nicht; er wird einfach weggelassen.</summary>
    [Theory]
    [InlineData(null, "speziell", "speziell")]
    [InlineData("allgemein", null, "allgemein")]
    [InlineData("allgemein", "   ", "allgemein")]
    [InlineData("  ", "speziell", "speziell")]
    public void Combine_OnlyConfiguredPartsAreAppended(string? general, string? specific, string expected)
        => ScmPromptAppendix.Combine(general, specific).Should().Be(expected);

    /// <summary>Ohne konfigurierte Anhänge wird nichts angehängt.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "  ")]
    public void Combine_AllEmpty_ReturnsNull(string? general, string? specific)
        => ScmPromptAppendix.Combine(general, specific).Should().BeNull();

    /// <summary>Gespeicherte Werte werden an den Rändern getrimmt; innere Zeilenumbrüche bleiben erhalten.</summary>
    [Fact]
    public void Combine_TrimsOuterWhitespace_KeepsInnerFormatting()
        => ScmPromptAppendix.Combine("  allgemein\nmehrzeilig  ", null).Should().Be("allgemein\nmehrzeilig");

    /// <summary>Resolve wählt aus den gelesenen Einstellungswerten allgemeinen plus ereignisartspezifischen Anhang.</summary>
    [Fact]
    public void Resolve_UsesGeneralPlusMatchingSpecificKey()
    {
        var values = new Dictionary<string, string?>
        {
            [AppEinstellungService.ScmPromptAppendGeneralKey] = "allgemein",
            [AppEinstellungService.ScmPromptAppendIssueKey] = "issue",
            [AppEinstellungService.ScmPromptAppendIssueCommentKey] = "issue-kommentar",
            [AppEinstellungService.ScmPromptAppendPullRequestKey] = "pr",
            [AppEinstellungService.ScmPromptAppendPullRequestCommentKey] = "pr-kommentar",
        };

        ScmPromptAppendix.Resolve(Observation(ScmReferenceKind.Issue, ScmContentKind.Description), values)
            .Should().Be("allgemein\n\nissue");
        ScmPromptAppendix.Resolve(Observation(ScmReferenceKind.Issue, ScmContentKind.IssueComment), values)
            .Should().Be("allgemein\n\nissue-kommentar");
        ScmPromptAppendix.Resolve(Observation(ScmReferenceKind.PullRequest, ScmContentKind.Description), values)
            .Should().Be("allgemein\n\npr");
        ScmPromptAppendix.Resolve(Observation(ScmReferenceKind.PullRequest, ScmContentKind.PullRequestReview), values)
            .Should().Be("allgemein\n\npr-kommentar");
    }

    /// <summary>Fehlende Einstellungsschlüssel liefern keinen Anhang.</summary>
    [Fact]
    public void Resolve_WithoutStoredValues_ReturnsNull()
        => ScmPromptAppendix.Resolve(Observation(ScmReferenceKind.Issue, ScmContentKind.IssueComment),
            new Dictionary<string, string?>()).Should().BeNull();

    /// <summary>Die Schlüsselliste enthält genau die fünf Anhang-Schlüssel, beginnend mit dem allgemeinen.</summary>
    [Fact]
    public void SettingKeys_ContainsAllFiveAppendixKeys()
        => ScmPromptAppendix.SettingKeys.Should().Equal(
            AppEinstellungService.ScmPromptAppendGeneralKey,
            AppEinstellungService.ScmPromptAppendIssueKey,
            AppEinstellungService.ScmPromptAppendIssueCommentKey,
            AppEinstellungService.ScmPromptAppendPullRequestKey,
            AppEinstellungService.ScmPromptAppendPullRequestCommentKey);

    private static ScmEventObservation Observation(ScmReferenceKind kind, ScmContentKind contentKind)
    {
        var time = DateTimeOffset.UtcNow;
        return new ScmEventObservation(new(Repository, kind, 7), contentKind, "node", "42",
            time, time, null, "U_1", "owner", null, null, "$Softwareschmiede test",
            "https://github.com/owner/repo/issues/7", time);
    }
}
