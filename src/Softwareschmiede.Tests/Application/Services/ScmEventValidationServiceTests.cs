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

    private static ScmEventObservation Observe(string body, string author, string? editor, bool edited)
    {
        var date = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        return new(new(new("test", "host", "repo42", "OWNER", "repo", "42", "42", "owner", true), ScmReferenceKind.Issue, 1),
            ScmContentKind.IssueComment, "node", "1", date, date, edited ? date : null, author, "owner", editor, null, body, "https://host/OWNER/repo/issues/1", date);
    }
}
