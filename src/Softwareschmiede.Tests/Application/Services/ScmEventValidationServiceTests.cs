using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Owner-/Editorprüfung ohne Provideraufrufe oder Seiteneffekte.</summary>
public sealed class ScmEventValidationServiceTests
{
    /// <summary>Nur der tatsächliche Bearbeiter und ein Trigger legitimieren die Textversion.</summary>
    [Theory]
    [InlineData("$softwareschmiede bitte", "42", null, false, true)]
    [InlineData("Kein Trigger", "42", null, false, false)]
    [InlineData("$Softwareschmiede", "7", null, false, false)]
    [InlineData("$Softwareschmiede", "42", "7", true, false)]
    [InlineData("$Softwareschmiede", "42", null, true, false)]
    [InlineData("$Softwareschmiede", "7", "42", true, true)]
    public void ActualAuthorOrEditorMustBeOwner(string body, string author, string? editor, bool edited, bool valid)
    {
        var observation = Observe(body, author, editor, edited);
        Assert.Equal(valid, ScmEventValidationService.GetRejection(observation) is null);
    }

    /// <summary>Der Trigger zählt nur frei stehend; von Zitatzeichen flankierte Vorkommen lösen nichts aus.</summary>
    [Theory]
    [InlineData("$Softwareschmiede bitte", true)]
    [InlineData("Bitte $softwareschmiede das übernehmen.", true)]
    [InlineData("($Softwareschmiede)", true)]
    [InlineData("Trigger: $SOFTWARESCHMIEDE!", true)]
    [InlineData("$Softwareschmiede\nZweite Zeile", true)]
    [InlineData("'$Softwareschmiede'", false)]
    [InlineData("\"$Softwareschmiede\"", false)]
    [InlineData("`$Softwareschmiede`", false)]
    [InlineData("´$Softwareschmiede", false)]
    [InlineData("‚$Softwareschmiede‘", false)]
    [InlineData("„$Softwareschmiede“", false)]
    [InlineData("«$Softwareschmiede»", false)]
    [InlineData("$Softwareschmiede's Trigger", false)]
    [InlineData("Der Auslöser ‚$Softwareschmiede‘ soll nur frei stehend gelten.", false)]
    [InlineData("Der Trigger „$Softwareschmiede“ legt keine Aufgabe an, wenn er zitiert wird.", false)]
    [InlineData("Zitiert ‚$Softwareschmiede‘, ruft aber frei $Softwareschmiede auf.", true)]
    public void TriggerMustBeFreeStanding(string body, bool valid)
    {
        var observation = Observe(body, "42", null, false);
        Assert.Equal(valid, ScmEventValidationService.GetRejection(observation) is null);
    }

    /// <summary>Ein konfiguriertes Keyword ersetzt den Standard-Trigger vollständig; die
    /// Frei-stehend-Regel und die Case-insensitive-Prüfung gelten unverändert.</summary>
    [Theory]
    [InlineData("!Bearbeite das bitte", "!Bearbeite", true)]
    [InlineData("Bitte !bearbeite das übernehmen.", "!Bearbeite", true)]
    [InlineData("$Softwareschmiede bitte", "!Bearbeite", false)]
    [InlineData("'!Bearbeite'", "!Bearbeite", false)]
    [InlineData("Zitiert ‚!Bearbeite‘, ruft aber frei !Bearbeite auf.", "!Bearbeite", true)]
    public void ConfiguredTriggerKeyword_ReplacesDefaultAndStaysFreeStanding(string body, string keyword, bool valid)
    {
        var observation = Observe(body, "42", null, false);
        Assert.Equal(valid, ScmEventValidationService.GetRejection(observation, keyword) is null);
    }

    /// <summary>Fehlende oder leere Keyword-Werte fallen auf den Standard-Trigger zurück.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingTriggerKeyword_FallsBackToDefault(string? keyword)
    {
        Assert.Null(ScmEventValidationService.GetRejection(Observe("$Softwareschmiede bitte", "42", null, false), keyword));
        Assert.NotNull(ScmEventValidationService.GetRejection(Observe("!Bearbeite bitte", "42", null, false), keyword));
    }

    /// <summary>Konfigurierte Keyword-Werte werden vor der Prüfung getrimmt.</summary>
    [Fact]
    public void TriggerKeyword_IsTrimmedBeforeMatching()
        => Assert.Null(ScmEventValidationService.GetRejection(Observe("!Bearbeite bitte", "42", null, false), "  !Bearbeite  "));

    /// <summary>Bekannte IDs haben Vorrang vor ähnlich geschriebenen Logins.</summary>
    [Fact]
    public void LoginCannotOverrideDifferentIds()
    {
        Assert.NotNull(ScmEventValidationService.GetRejection(Observe("$Softwareschmiede", "other", null, false)));
        var observation = Observe("$Softwareschmiede", "42", null, false);
        Assert.NotNull(ScmEventValidationService.GetRejection(observation with
        { Reference = observation.Reference with { Repository = observation.Reference.Repository with { OwnerId = "org" } } }));
    }

    /// <summary>Metadatenzeit ändert weder Version noch Ereignisschlüssel.</summary>
    [Fact]
    public void MetadataDoesNotCreateAnotherVersionAndContentNamespacesDo()
    {
        var first = Observe("$Softwareschmiede", "42", null, false);
        Assert.Equal(first.EventKey, (first with { UpdatedAt = first.UpdatedAt.AddHours(2) }).EventKey);
        Assert.NotEqual(first.EventKey, (first with { ContentKind = ScmContentKind.ReviewComment }).EventKey);
        Assert.NotEqual(first.EventKey, (first with { LastEditedAt = first.CreatedAt.AddSeconds(1), EditorId = "42" }).EventKey);
    }

    /// <summary>Im SeparatesKonto triggert die frei stehende @mention des separaten Kontos statt des Keywords.</summary>
    [Theory]
    [InlineData("@scm-bot bitte übernehmen", true)]
    [InlineData("Bitte @SCM-BOT das prüfen.", true)]
    [InlineData("@scm-bot", true)]
    [InlineData("$Softwareschmiede bitte", false)]
    [InlineData("'@scm-bot'", false)]
    [InlineData("Kein Aufruf", false)]
    public void SeparatesKonto_MentionReplacesKeywordTrigger(string body, bool valid)
    {
        var observation = ObserveSeparate(body, "42", null, false);
        Assert.Equal(valid, ScmEventValidationService.GetRejection(observation) is null);
        Assert.Equal(!valid, ScmEventValidationService.GetRejection(observation)
            is ScmEventValidationService.KeineErwaehnungRejection);
    }

    /// <summary>Im SeparatesKonto gilt die Autor-/Editor-Prüfung gegen den Repository-Owner, nicht gegen den Bot-Viewer.</summary>
    [Theory]
    [InlineData("42", null, false, true)]
    [InlineData("bot-id", null, false, false)]
    [InlineData("42", "bot-id", true, false)]
    [InlineData("bot-id", "42", true, true)]
    public void SeparatesKonto_AuthorOrEditorMustBeOwner_NotViewer(string author, string? editor, bool edited, bool valid)
    {
        var observation = ObserveSeparate("@scm-bot bitte", author, editor, edited);
        Assert.Equal(valid, ScmEventValidationService.GetRejection(observation) is null);
    }

    /// <summary>Im SeparatesKonto entfällt die Viewer-Owner-Regel: der Viewer ist das Bot-Konto, kein Owner.</summary>
    [Fact]
    public void SeparatesKonto_ViewerOwnerRuleDoesNotApply()
        => Assert.Null(ScmEventValidationService.GetRejection(ObserveSeparate("@scm-bot bitte", "42", null, false)));

    /// <summary>Die Marker-Regel „Eigene Antwort" gilt im SeparatesKonto weiter.</summary>
    [Fact]
    public void SeparatesKonto_OwnResponseMarkerStillRejected()
        => Assert.Equal("Eigene Antwort", ScmEventValidationService.GetRejection(
            ObserveSeparate("@scm-bot\n<!-- softwareschmiede:action:abc -->", "42", null, false)));

    /// <summary>Regression: Im Benutzerkonto bleibt der Keyword-Trigger unverändert; eine @mention triggert nicht.</summary>
    [Fact]
    public void Benutzerkonto_KeywordStaysTheOnlyTrigger()
    {
        Assert.NotNull(ScmEventValidationService.GetRejection(Observe("@scm-bot bitte", "42", null, false)));
        Assert.Null(ScmEventValidationService.GetRejection(Observe("$Softwareschmiede bitte", "42", null, false)));
    }

    private static ScmEventObservation Observe(string body, string author, string? editor, bool edited)
    {
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        return new(new(new("test", "host", "repo42", "OWNER", "repo", "42", "42", "owner", true), ScmReferenceKind.Issue, 1),
            ScmContentKind.IssueComment, "node", "1", date, date, edited ? date : null, author, "owner", editor, null, body, "https://host/OWNER/repo/issues/1", date);
    }

    private static ScmEventObservation ObserveSeparate(string body, string author, string? editor, bool edited)
    {
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var repository = new ScmEventRepository("test", "host", "repo42", "OWNER", "repo", "42", "bot-id", "scm-bot", true,
            Softwareschmiede.Domain.Enums.ScmKontoModus.SeparatesKonto);
        return new(new(repository, ScmReferenceKind.Issue, 1),
            ScmContentKind.IssueComment, "node", "1", date, date, edited ? date : null, author, "owner", editor, null, body, "https://host/OWNER/repo/issues/1", date);
    }
}
