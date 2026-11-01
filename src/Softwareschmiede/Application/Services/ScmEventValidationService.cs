using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Prüft Trigger und tatsächlichen Bearbeiter, bevor fachliche Aufträge entstehen.</summary>
public sealed class ScmEventValidationService
{
    /// <summary>Standard-Trigger-Keyword, wenn kein abweichender Wert konfiguriert ist
    /// (Einstellung <see cref="AppEinstellungService.ScmTriggerKeywordKey"/>).</summary>
    public const string DefaultTriggerKeyword = "$Softwareschmiede";

    /// <summary>Ablehnungsgrund, wenn im Modus <see cref="ScmKontoModus.SeparatesKonto"/> keine frei
    /// stehende Erwähnung des separaten Kontos vorliegt.</summary>
    public const string KeineErwaehnungRejection = "Keine Erwähnung des separaten Kontos";

    private const string QuoteChars = "'\"`´‚‘’„“”«»";

    /// <summary>Normalisiert ein konfiguriertes Trigger-Keyword: Leere oder nur aus Leerraum
    /// bestehende Werte fallen auf <see cref="DefaultTriggerKeyword"/> zurück, sonst wird der
    /// getrimmte Wert verwendet.</summary>
    public static string NormalizeTriggerKeyword(string? triggerKeyword)
        => string.IsNullOrWhiteSpace(triggerKeyword) ? DefaultTriggerKeyword : triggerKeyword.Trim();

    /// <summary>Prüft die vertrauenswürdige Provideridentität der beobachteten Textversion.</summary>
    /// <param name="observation">Die beobachtete Textversion.</param>
    /// <param name="triggerKeyword">Das zu prüfende Trigger-Keyword; leere Werte fallen auf
    /// <see cref="DefaultTriggerKeyword"/> zurück.</param>
    public static string? GetRejection(ScmEventObservation observation, string? triggerKeyword = null)
    {
        var repository = observation.Reference.Repository;
        var separat = repository.Konto == ScmKontoModus.SeparatesKonto;
        // Trigger je Modus: Benutzerkonto = konfiguriertes Keyword, SeparatesKonto = frei stehende
        // Erwähnung des separaten Kontos (Viewer = Bot-Identität).
        if (separat
                ? !HasFreeStandingTrigger(observation.Body, "@" + repository.ViewerLogin)
                : !HasFreeStandingTrigger(observation.Body, NormalizeTriggerKeyword(triggerKeyword)))
            return separat ? KeineErwaehnungRejection : "Kein Trigger";
        if (!separat && !SameIdentity(repository.OwnerId, repository.Owner, repository.ViewerId, repository.ViewerLogin))
            return "Angemeldeter Benutzer ist nicht Repository-Owner";
        var edited = observation.LastEditedAt is not null;
        var actorId = edited ? observation.EditorId : observation.AuthorId;
        var actorLogin = edited ? observation.EditorLogin : observation.AuthorLogin;
        if (string.IsNullOrWhiteSpace(actorId) && string.IsNullOrWhiteSpace(actorLogin))
            return "Bearbeiter nicht bestimmbar";
        if (!SameIdentity(actorId, actorLogin, repository.OwnerId, repository.Owner))
            return "Textversion stammt nicht vom Repository-Owner";
        if (observation.Body.Contains("<!-- softwareschmiede:", StringComparison.Ordinal))
            return "Eigene Antwort";
        return null;
    }

    /// <summary>Der Trigger zählt nur frei stehend; unmittelbar zitierte Vorkommen lösen nichts aus.</summary>
    private static bool HasFreeStandingTrigger(string body, string trigger)
    {
        var index = -1;
        while ((index = body.IndexOf(trigger, index + 1, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var before = index > 0 ? body[index - 1] : '\0';
            var afterIndex = index + trigger.Length;
            var after = afterIndex < body.Length ? body[afterIndex] : '\0';
            if (!QuoteChars.Contains(before) && !QuoteChars.Contains(after))
                return true;
        }
        return false;
    }

    private static bool SameIdentity(string? leftId, string? leftLogin, string? rightId, string? rightLogin)
        => !string.IsNullOrWhiteSpace(leftId) && !string.IsNullOrWhiteSpace(rightId)
            ? string.Equals(leftId, rightId, StringComparison.Ordinal)
            : !string.IsNullOrWhiteSpace(leftLogin) && !string.IsNullOrWhiteSpace(rightLogin)
              && string.Equals(leftLogin, rightLogin, StringComparison.OrdinalIgnoreCase);
}
