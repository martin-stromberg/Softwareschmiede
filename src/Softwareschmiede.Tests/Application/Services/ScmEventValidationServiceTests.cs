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
    public void MissingTriggerKeyword_IfBackToDefault(string? keyword)
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
    public void SeparatesAccount_MentionReplacesKeywordTrigger(string body, bool valid)
    {
        var observation = ObserveSeparate(body, "42", null, false);
        Assert.Equal(valid, ScmEventValidationService.GetRejection(observation) is null);
        Assert.Equal(!valid, ScmEventValidationService.GetRejection(observation)
            is ScmEventValidationService.NoMentionRejection);
    }

    /// <summary>Im SeparatesKonto gilt die Autor-/Editor-Prüfung gegen den Repository-Owner, nicht gegen den Bot-Viewer.</summary>
    [Theory]
    [InlineData("42", null, false, true)]
    [InlineData("bot-id", null, false, false)]
    [InlineData("42", "bot-id", true, false)]
    [InlineData("bot-id", "42", true, true)]
    public void SeparatesAccount_AuthorOrEditorMustBeOwner_NotViewer(string author, string? editor, bool edited, bool valid)
    {
        var observation = ObserveSeparate("@scm-bot bitte", author, editor, edited);
        Assert.Equal(valid, ScmEventValidationService.GetRejection(observation) is null);
    }

    /// <summary>Im SeparatesKonto entfällt die Viewer-Owner-Regel: der Viewer ist das Bot-Konto, kein Owner.</summary>
    [Fact]
    public void SeparatesAccount_ViewerOwnerRuleDoesNotApply()
        => Assert.Null(ScmEventValidationService.GetRejection(ObserveSeparate("@scm-bot bitte", "42", null, false)));

    /// <summary>Die Marker-Regel „Eigene Antwort" gilt im SeparatesKonto weiter.</summary>
    [Fact]
    public void SeparatesAccount_OwnResponseMarkerStillRejected()
        => Assert.Equal("Eigene Antwort", ScmEventValidationService.GetRejection(
            ObserveSeparate("@scm-bot\n<!-- softwareschmiede:action:abc -->", "42", null, false)));

    /// <summary>Regression: Im Benutzerkonto bleibt der Keyword-Trigger unverändert; eine @mention triggert nicht.</summary>
    [Fact]
    public void Useraccount_KeywordStaysTheOnlyTrigger()
    {
        Assert.NotNull(ScmEventValidationService.GetRejection(Observe("@scm-bot bitte", "42", null, false)));
        Assert.Null(ScmEventValidationService.GetRejection(Observe("$Softwareschmiede bitte", "42", null, false)));
    }

    /// <summary>Eine Reaktion des Ereignis-Kontos auf einem Kommentar gilt als bereits bearbeitet.</summary>
    [Theory]
    [InlineData(ScmContentKind.IssueComment)]
    [InlineData(ScmContentKind.ReviewComment)]
    [InlineData(ScmContentKind.PullRequestReview)]
    public void ViewerReaction_RejectsCommentKinds(ScmContentKind kind)
    {
        var observation = Observe("$Softwareschmiede bitte", "42", null, false, ["EYES"], kind);
        Assert.Equal(ScmEventValidationService.AlreadyReactedRejection, ScmEventValidationService.GetRejection(observation));
    }

    /// <summary>Im SeparatesKonto bindet die Reaktion an das Bot-Konto; auch eine manuell gesetzte
    /// Reaktion (nicht nur die Empfangsreaktion) unterdrückt den Kommentar.</summary>
    [Fact]
    public void SeparatesAccount_ViewerReaction_Rejects()
    {
        var observation = ObserveSeparate("@scm-bot bitte", "42", null, false, ["THUMBS_UP"]);
        Assert.Equal(ScmEventValidationService.AlreadyReactedRejection, ScmEventValidationService.GetRejection(observation));
    }

    /// <summary>Die Reaktionsprüfung läuft erst nach dem Trigger: ein reagierter Kommentar ohne
    /// Trigger behält die präzisere Trigger-Ablehnung.</summary>
    [Fact]
    public void ViewerReaction_CheckRunsAfterTrigger()
    {
        Assert.Equal("Kein Trigger", ScmEventValidationService.GetRejection(Observe("Nur Text", "42", null, false, ["EYES"])));
        Assert.Equal(ScmEventValidationService.NoMentionRejection,
            ScmEventValidationService.GetRejection(ObserveSeparate("Nur Text", "42", null, false, ["EYES"])));
    }

    /// <summary>Beschreibungen bleiben vom Reaktionskriterium ausgenommen: Auf Referenztexte wird
    /// keine Empfangsreaktion gesetzt, es gibt kein vergleichbares Remote-Merkmal.</summary>
    [Fact]
    public void ViewerReaction_DescriptionNotRejected()
    {
        var observation = Observe("$Softwareschmiede bitte", "42", null, false, ["EYES"], ScmContentKind.Description);
        Assert.Null(ScmEventValidationService.GetRejection(observation));
    }

    /// <summary>Ein Autor aus der Vertrauensliste legitimiert die Textversion auch ohne
    /// Owner-Identität; der Login-Vergleich ist case-insensitive.</summary>
    [Theory]
    [InlineData("github-actions[bot]")]
    [InlineData("app/github-actions")]
    [InlineData("GITHUB-ACTIONS[BOT]")]
    public void GitHubActionsAuthor_IsAccepted(string authorLogin)
    {
        var observation = Observe("$Softwareschmiede bitte", "bot-id", null, false, authorLogin: authorLogin);
        Assert.Null(ScmEventValidationService.GetRejection(observation));
    }

    /// <summary>Die Vertrauenslisten-Akzeptanz gilt im SeparatesKonto an derselben Stelle wie
    /// die Owner-Prüfung: Autor github-actions mit frei stehender Erwähnung wird angenommen.</summary>
    [Fact]
    public void SeparatesAccount_GitHubActionsAuthor_IsAccepted()
    {
        var observation = ObserveSeparate("@scm-bot bitte", "bot-id", null, false, authorLogin: "github-actions[bot]");
        Assert.Null(ScmEventValidationService.GetRejection(observation));
    }

    /// <summary>Auch als Bearbeiter gilt github-actions als vertrauenswürdiger Akteur —
    /// unabhängig davon, wer den ursprünglichen Autor stellt, in beiden Kontomodi.</summary>
    [Theory]
    [InlineData("42")]
    [InlineData("7")]
    public void GitHubActionsEditor_IsAccepted(string author)
    {
        Assert.Null(ScmEventValidationService.GetRejection(
            Observe("$Softwareschmiede bitte", author, "bot-id", true, editorLogin: "github-actions[bot]")));
        Assert.Null(ScmEventValidationService.GetRejection(
            ObserveSeparate("@scm-bot bitte", author, "bot-id", true, editorLogin: "github-actions[bot]")));
    }

    /// <summary>Fremde Bot- und Benutzer-Logins stehen nicht auf der Vertrauensliste und
    /// bleiben in beiden Kontomodi abgelehnt.</summary>
    [Theory]
    [InlineData("dependabot[bot]")]
    [InlineData("renovate[bot]")]
    [InlineData("fremder-benutzer")]
    public void ForeignBotLogins_StayRejected(string authorLogin)
    {
        Assert.Equal("Textversion stammt nicht vom Repository-Owner", ScmEventValidationService.GetRejection(
            Observe("$Softwareschmiede bitte", "bot-id", null, false, authorLogin: authorLogin)));
        Assert.Equal("Textversion stammt nicht vom Repository-Owner", ScmEventValidationService.GetRejection(
            ObserveSeparate("@scm-bot bitte", "bot-id", null, false, authorLogin: authorLogin)));
    }

    /// <summary>Der Repository-Owner ist berechtigter Zuweisender — ohne Texttrigger in der Beschreibung.</summary>
    [Fact]
    public void GetRejection_AssignOwnerAssigner_Accepts()
        => Assert.Null(ScmEventValidationService.GetRejection(ObserveAssign("Beschreibung ohne Trigger", "42", "owner")));

    /// <summary>Das überwachte Konto selbst und vertrauenswürdige Automatisierungskonten sind
    /// ebenfalls berechtigte Zuweisende; der Login-Vergleich ist case-insensitive.</summary>
    [Theory]
    [InlineData("bot-id", "scm-bot")]
    [InlineData(null, "scm-bot")]
    [InlineData(null, "github-actions[bot]")]
    [InlineData(null, "app/github-actions")]
    [InlineData(null, "GITHUB-ACTIONS[BOT]")]
    public void GetRejection_AssignViewerOrAutomation_Accepts(string? actorId, string? actorLogin)
        => Assert.Null(ScmEventValidationService.GetRejection(ObserveAssign("Beschreibung ohne Trigger", actorId, actorLogin)));

    /// <summary>Ein Zuweisender außerhalb von Owner, überwachtem Konto und Vertrauensliste
    /// wird abgelehnt — auch wenn die Beschreibung einen gültigen Auftrag enthält.</summary>
    [Fact]
    public void GetRejection_AssignForeignAssigner_Rejected()
        => Assert.Equal(ScmEventValidationService.AssignerNotAllowedRejection,
            ScmEventValidationService.GetRejection(ObserveAssign("Beschreibung mit Auftrag", "U_9", "fremd")));

    /// <summary>Enthält dieselbe Beschreibung zusätzlich eine gültige Erwähnung des separaten
    /// Kontos, legitimiert der reguläre Texttrigger die Aufnahme unabhängig vom Zuweisender —
    /// die abgelehnte Zuweisung darf den gleichzeitigen Mention-Auslöser nicht unterdrücken,
    /// nachdem sie die Notification-Deduplizierung gewonnen hat.</summary>
    [Fact]
    public void GetRejection_AssignForeignAssignerWithMention_AcceptsViaTextTrigger()
        => Assert.Null(ScmEventValidationService.GetRejection(ObserveAssign("@scm-bot bitte übernehmen", "U_9", "fremd")));

    /// <summary>Derselbe Texttrigger-Fallback gilt, wenn der Zuweisender gar nicht bestimmbar
    /// ist (z. B. assign → unassign vor der Verarbeitung): die Erwähnung triggert weiterhin.</summary>
    [Fact]
    public void GetRejection_AssignUndeterminedAssignerWithMention_AcceptsViaTextTrigger()
        => Assert.Null(ScmEventValidationService.GetRejection(ObserveAssign("@scm-bot bitte übernehmen", null, null)));

    /// <summary>Der Texttrigger-Fallback prüft vollständig: Eine Beschreibung mit Erwähnung,
    /// aber eigenem Antwort-Marker wird auch bei fremdem Zuweisender weiter abgelehnt.</summary>
    [Fact]
    public void GetRejection_AssignForeignAssignerWithMentionAndMarker_StaysRejected()
        => Assert.Equal(ScmEventValidationService.AssignerNotAllowedRejection, ScmEventValidationService.GetRejection(
            ObserveAssign("@scm-bot bitte\n<!-- softwareschmiede:action:abc -->", "U_9", "fremd")));

    /// <summary>Eine leere oder leerraumige Referenzbeschreibung trägt keinen Auftragsinhalt —
    /// abgelehnt unabhängig vom berechtigten Zuweisenden.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetRejection_AssignEmptyDescription_Rejected(string body)
        => Assert.Equal(ScmEventValidationService.AssignEmptyDescriptionRejection,
            ScmEventValidationService.GetRejection(ObserveAssign(body, "42", "owner")));

    /// <summary>Ohne bestimmbare Zuweisender-Identität (kein wirksames Zuweisungsereignis,
    /// keine Plugin-Fähigkeit) wird abgelehnt.</summary>
    [Fact]
    public void GetRejection_AssignUndeterminedAssigner_Rejected()
        => Assert.Equal(ScmEventValidationService.AssignerUnknownRejection,
            ScmEventValidationService.GetRejection(ObserveAssign("Beschreibung mit Auftrag", null, null)));

    /// <summary>Die eigene-Antwort-Markierung ist im Assign-Pfad kein Ablehnungsgrund —
    /// die berechtigte Zuweisung legitimiert auch eine maschinell gesetzte Beschreibung.</summary>
    [Fact]
    public void GetRejection_AssignOwnResponseMarker_Accepted()
        => Assert.Null(ScmEventValidationService.GetRejection(
            ObserveAssign("Auftrag\n<!-- softwareschmiede:action:abc -->", "42", "owner")));

    /// <summary>Eine Marker-Beschreibung legitimiert keinen fremden Zuweisender: ohne frei
    /// stehende Erwähnung scheitert der Texttrigger-Fallback bereits an `NoMentionRejection`,
    /// sodass die spezifischere Zuweisungsbegründung führend bleibt. Dass der Marker den
    /// Fallback zusätzlich selbst abwehrt, deckt `…_WithMentionAndMarker_StaysRejected` ab.</summary>
    [Fact]
    public void GetRejection_AssignForeignAssignerWithMarker_StaysRejected()
        => Assert.Equal(ScmEventValidationService.AssignerNotAllowedRejection, ScmEventValidationService.GetRejection(
            ObserveAssign("Auftrag\n<!-- softwareschmiede:action:abc -->", "U_9", "fremd")));

    private static ScmEventObservation ObserveAssign(string body, string? actorId, string? actorLogin)
    {
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var repository = new ScmEventRepository("test", "host", "repo42", "OWNER", "repo", "42", "bot-id", "scm-bot", true,
            Softwareschmiede.Domain.Enums.ScmAccountMode.SeparateAccount);
        return new(new(repository, ScmReferenceKind.Issue, 1), ScmContentKind.Description,
            "node", null, date, date, null, "42", "owner", null, null, body,
            "https://host/OWNER/repo/issues/1", date, null,
            new ScmEventTrigger(ScmNotification.AssignReason, actorId, actorLogin, "EV_1", date));
    }

    private static ScmEventObservation Observe(string body, string author, string? editor, bool edited,
        IReadOnlyList<string>? viewerReactions = null, ScmContentKind kind = ScmContentKind.IssueComment,
        string? authorLogin = "owner", string? editorLogin = null)
    {
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        return new(new(new("test", "host", "repo42", "OWNER", "repo", "42", "42", "owner", true), ScmReferenceKind.Issue, 1),
            kind, "node", "1", date, date, edited ? date : null, author, authorLogin, editor, editorLogin, body, "https://host/OWNER/repo/issues/1", date,
            viewerReactions);
    }

    private static ScmEventObservation ObserveSeparate(string body, string author, string? editor, bool edited,
        IReadOnlyList<string>? viewerReactions = null, ScmContentKind kind = ScmContentKind.IssueComment,
        string? authorLogin = "owner", string? editorLogin = null)
    {
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var repository = new ScmEventRepository("test", "host", "repo42", "OWNER", "repo", "42", "bot-id", "scm-bot", true,
            Softwareschmiede.Domain.Enums.ScmAccountMode.SeparateAccount);
        return new(new(repository, ScmReferenceKind.Issue, 1),
            kind, "node", "1", date, date, edited ? date : null, author, authorLogin, editor, editorLogin, body, "https://host/OWNER/repo/issues/1", date,
            viewerReactions);
    }
}
