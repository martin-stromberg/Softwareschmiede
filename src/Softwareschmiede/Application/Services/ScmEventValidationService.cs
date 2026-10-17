using Softwareschmiede.Domain.ValueObjects;

namespace Softwareschmiede.Application.Services;

/// <summary>Prüft Trigger und tatsächlichen Bearbeiter, bevor fachliche Aufträge entstehen.</summary>
public sealed class ScmEventValidationService
{
    /// <summary>Prüft die vertrauenswürdige Provideridentität der beobachteten Textversion.</summary>
    public static string? GetRejection(ScmEventObservation observation)
    {
        var repository = observation.Reference.Repository;
        if (!observation.Body.Contains("$Softwareschmiede", StringComparison.OrdinalIgnoreCase))
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

    private static bool SameIdentity(string? leftId, string? leftLogin, string? rightId, string? rightLogin)
        => !string.IsNullOrWhiteSpace(leftId) && !string.IsNullOrWhiteSpace(rightId)
            ? string.Equals(leftId, rightId, StringComparison.Ordinal)
            : !string.IsNullOrWhiteSpace(leftLogin) && !string.IsNullOrWhiteSpace(rightLogin)
              && string.Equals(leftLogin, rightLogin, StringComparison.OrdinalIgnoreCase);
}
