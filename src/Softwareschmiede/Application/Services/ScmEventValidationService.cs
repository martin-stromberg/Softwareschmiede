using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Prüft Trigger und tatsächlichen Bearbeiter, bevor fachliche Aufträge entstehen.</summary>
public sealed class ScmEventValidationService
{
    /// <summary>Standard-Trigger-Keyword, wenn kein abweichender Wert konfiguriert ist
    /// (Einstellung <see cref="AppSettingService.ScmTriggerKeywordKey"/>).</summary>
    public const string DefaultTriggerKeyword = "$Softwareschmiede";

    /// <summary>Ablehnungsgrund, wenn im Modus <see cref="ScmAccountMode.SeparateAccount"/> keine frei
    /// stehende Erwähnung des separaten Kontos vorliegt.</summary>
    public const string NoMentionRejection = "Keine Erwähnung des separaten Kontos";

    /// <summary>Ablehnungsgrund, wenn das aktive Ereignis-Konto auf den beobachteten Kommentar
    /// bereits remote reagiert hat (instanzübergreifende Duplikaterkennung).</summary>
    public const string AlreadyReactedRejection = "Bereits mit dem Konto reagiert";

    /// <summary>Ablehnungsgrund, wenn eine durch eine Assignee-Zuweisung ausgelöste Beobachtung
    /// keinen Auftragsinhalt trägt (leere Referenzbeschreibung).</summary>
    public const string AssignEmptyDescriptionRejection = "Referenzbeschreibung ist leer";

    /// <summary>Ablehnungsgrund, wenn zu einer Assignee-Zuweisung kein wirksames
    /// Zuweisungsereignis und damit kein Zuweisender ermittelt werden kann.</summary>
    public const string AssignerUnknownRejection = "Zuweisender nicht bestimmbar";

    /// <summary>Ablehnungsgrund, wenn die Zuweisung weder durch den Repository-Owner noch durch
    /// das überwachte Konto selbst noch durch ein vertrauenswürdiges Automatisierungskonto erfolgte.</summary>
    public const string AssignerNotAllowedRejection = "Zuweisung nicht durch berechtigtes Konto";

    private const string QuoteChars = "'\"`´‚‘’„“”«»";

    /// <summary>Vertrauenswürdige Automatisierungs-Logins, deren Textversionen dem Repository
    /// selbst zugerechnet werden (beide Schreibweisen des GitHub-Kontos github-actions).</summary>
    private static readonly IReadOnlyList<string> TrustedAutomationLogins = ["github-actions[bot]", "app/github-actions"];

    /// <summary>Normalisiert ein konfiguriertes Trigger-Keyword: Leere oder nur aus Leerraum
    /// bestehende Werte fallen auf <see cref="DefaultTriggerKeyword"/> zurück, sonst wird der
    /// getrimmte Wert verwendet.</summary>
    public static string NormalizeTriggerKeyword(string? triggerKeyword)
        => string.IsNullOrWhiteSpace(triggerKeyword) ? DefaultTriggerKeyword : triggerKeyword.Trim();

    /// <summary>Prüft die vertrauenswürdige Provideridentität der beobachteten Textversion.
    /// Als Akteur gilt neben dem Repository-Owner auch ein Eintrag aus
    /// <see cref="TrustedAutomationLogins"/> (z. B. das GitHub-Konto github-actions).</summary>
    /// <param name="observation">Die beobachtete Textversion.</param>
    /// <param name="triggerKeyword">Das zu prüfende Trigger-Keyword; leere Werte fallen auf
    /// <see cref="DefaultTriggerKeyword"/> zurück.</param>
    public static string? GetRejection(ScmEventObservation observation, string? triggerKeyword = null)
    {
        // Zuweisungsauslöser: die Referenzbeschreibung trägt den Auftrag ohne Texttrigger;
        // geprüft werden Inhalt und Zuweisender-Kreis statt Erwähnung/Keyword und Akteur.
        if (observation.Trigger is { Reason: ScmNotification.AssignReason } assignTrigger)
            return GetAssignRejection(observation, assignTrigger, triggerKeyword);
        var repository = observation.Reference.Repository;
        var separate = repository.Account == ScmAccountMode.SeparateAccount;
        // Trigger je Modus: Benutzerkonto = konfiguriertes Keyword, SeparatesKonto = frei stehende
        // Erwähnung des separaten Kontos (Viewer = Bot-Identität).
        if (separate
                ? !HasFreeStandingTrigger(observation.Body, "@" + repository.ViewerLogin)
                : !HasFreeStandingTrigger(observation.Body, NormalizeTriggerKeyword(triggerKeyword)))
            return separate ? NoMentionRejection : "Kein Trigger";
        // Eine Kontoreaktion am Kommentar bedeutet: eine andere Instanz (oder dieser Kommentar
        // zuvor) hat die Verarbeitung bereits quittiert — keine erneute Aufnahme. Beschränkt auf
        // Kommentar-Arten, weil nur diese remote eine Empfangsreaktion tragen können.
        if (observation.ContentKind.IsCommentKind() && observation.ViewerHasReacted)
            return AlreadyReactedRejection;
        if (!separate && !SameIdentity(repository.OwnerId, repository.Owner, repository.ViewerId, repository.ViewerLogin))
            return "Angemeldeter Benutzer ist nicht Repository-Owner";
        var edited = observation.LastEditedAt is not null;
        var actorId = edited ? observation.EditorId : observation.AuthorId;
        var actorLogin = edited ? observation.EditorLogin : observation.AuthorLogin;
        if (string.IsNullOrWhiteSpace(actorId) && string.IsNullOrWhiteSpace(actorLogin))
            return "Bearbeiter nicht bestimmbar";
        if (!SameIdentity(actorId, actorLogin, repository.OwnerId, repository.Owner)
                && !IsTrustedAutomationLogin(actorLogin))
            return "Textversion stammt nicht vom Repository-Owner";
        if (observation.Body.Contains("<!-- softwareschmiede:", StringComparison.Ordinal))
            return "Eigene Antwort";
        return null;
    }

    /// <summary>Prüfschritt des Assignee-Auslösers: Auftragsinhalt vorhanden, Zuweisender
    /// bestimmbar und im erlaubten Kreis (Repository-Owner, überwachtes Konto,
    /// <see cref="TrustedAutomationLogins"/>). Der Zuweisender-Kreis trägt im Assign-Pfad
    /// die Legitimation — die eigene-Antwort-Markierung ist hier kein Ablehnungsgrund;
    /// die Marker-Abwehr wirkt nur noch über den Texttrigger-Fallback bzw. den regulären
    /// Texttrigger-Pfad, sodass eine Marker-Beschreibung keinen fremden Zuweisender
    /// legitimieren kann.
    /// Scheitert die Zuweisungsprüfung, legitimiert ein gleichzeitig gültiger Texttrigger
    /// (z. B. eine Erwähnung des separaten Kontos in derselben Beschreibung) die Beobachtung
    /// dennoch: Die abgelehnte Zuweisung gewinnt zwar die Notification-Deduplizierung, darf
    /// einen davon unabhängigen, regulären Auslöser aber nicht unterdrücken. Lehnt der
    /// Texttrigger ebenfalls ab, bleibt die spezifischere Zuweisungsbegründung führend.</summary>
    private static string? GetAssignRejection(ScmEventObservation observation, ScmEventTrigger trigger, string? triggerKeyword)
    {
        var repository = observation.Reference.Repository;
        string? rejection;
        if (string.IsNullOrWhiteSpace(observation.Body))
            rejection = AssignEmptyDescriptionRejection;
        else if (string.IsNullOrWhiteSpace(trigger.ActorId) && string.IsNullOrWhiteSpace(trigger.ActorLogin))
            rejection = AssignerUnknownRejection;
        else if (!SameIdentity(trigger.ActorId, trigger.ActorLogin, repository.OwnerId, repository.Owner)
                && !SameIdentity(trigger.ActorId, trigger.ActorLogin, repository.ViewerId, repository.ViewerLogin)
                && !IsTrustedAutomationLogin(trigger.ActorLogin))
            rejection = AssignerNotAllowedRejection;
        else
            return null;
        // Fallback auf den regulären Texttrigger: Ein frei stehender Auslöser in der
        // Beschreibung (inklusive seiner Autor-/Editor- und Markerprüfung) legitimiert
        // die Beobachtung unabhängig vom Zuweisender-Kreis.
        return GetRejection(observation with { Trigger = null }, triggerKeyword) is null ? null : rejection;
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

    /// <summary>Der Login gehört einem vertrauenswürdigen Automatisierungskonto
    /// (<see cref="TrustedAutomationLogins"/>, case-insensitive).</summary>
    private static bool IsTrustedAutomationLogin(string? login)
        => !string.IsNullOrWhiteSpace(login)
            && TrustedAutomationLogins.Contains(login, StringComparer.OrdinalIgnoreCase);
}
