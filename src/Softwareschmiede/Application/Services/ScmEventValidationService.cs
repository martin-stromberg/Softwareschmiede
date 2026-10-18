using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Prüft Trigger und tatsächlichen Bearbeiter, bevor fachliche Aufträge entstehen.</summary>
public sealed class ScmEventValidationService
{
    private const string Trigger = "$Softwareschmiede";
    private const string QuoteChars = "'\"`´‚‘’„“”«»";

    /// <summary>Prüft die vertrauenswürdige Provideridentität der beobachteten Textversion.</summary>
    public static string? GetRejection(ScmEventObservation observation)
    {
        var repository = observation.Reference.Repository;
        if (!HasFreeStandingTrigger(observation.Body))
            return "Kein Trigger";
        if (!SameIdentity(repository.OwnerId, repository.Owner, repository.ViewerId, repository.ViewerLogin))
            return "Angemeldeter Benutzer ist nicht Repository-Owner";
        var edited = observation.LastEditedAt is not null;
        var actorId = edited ? observation.EditorId : observation.AuthorId;
        var actorLogin = edited ? observation.EditorLogin : observation.AuthorLogin;
        if (string.IsNullOrWhiteSpace(actorId) && string.IsNullOrWhiteSpace(actorLogin))
            return "Bearbeiter nicht bestimmbar";
        if (!SameIdentity(actorId, actorLogin, repository.ViewerId, repository.ViewerLogin))
            return "Textversion stammt nicht vom Repository-Owner";
        if (observation.Body.Contains("<!-- softwareschmiede:", StringComparison.Ordinal))
            return "Eigene Antwort";
        return null;
    }

    /// <summary>Der Trigger zählt nur frei stehend; unmittelbar zitierte Vorkommen lösen nichts aus.</summary>
    private static bool HasFreeStandingTrigger(string body)
    {
        var index = -1;
        while ((index = body.IndexOf(Trigger, index + 1, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var before = index > 0 ? body[index - 1] : '\0';
            var afterIndex = index + Trigger.Length;
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
