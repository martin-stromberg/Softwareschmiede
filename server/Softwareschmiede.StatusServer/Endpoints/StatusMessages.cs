namespace Softwareschmiede.StatusServer.Endpoints;

/// <summary>Feste Schlüssel für die Status- und Fehlermeldungen, die
/// Post/Redirect/Get-Redirects per <c>?msg=</c>- bzw. <c>?err=</c>-Query an die
/// Zielseite transportieren. In der URL steht nur der kurze Schlüssel, die
/// GET-Handler lösen ihn über <see cref="Resolve(string?)"/> auf den deutschen
/// Text auf — ein präparierter Link kann so keinen Freitext als offiziell
/// wirkende Statusmeldung rendern. Unbekannte oder leere Schlüssel ergeben
/// keine Meldungsbox.</summary>
internal static class StatusMessages
{
    /// <summary>„Der Benutzer wurde angelegt."</summary>
    public const string UserCreated = "user-created";

    /// <summary>„Die Berechtigungsstufe wurde geändert."</summary>
    public const string RoleChanged = "role-changed";

    /// <summary>„Der Benutzer wurde deaktiviert."</summary>
    public const string UserDeactivated = "user-deactivated";

    /// <summary>„Der Benutzer wurde aktiviert."</summary>
    public const string UserActivated = "user-activated";

    /// <summary>„Der Benutzer wurde gelöscht."</summary>
    public const string UserDeleted = "user-deleted";

    /// <summary>„Die Sperrung wurde zurückgesetzt."</summary>
    public const string LockoutReset = "lockout-reset";

    /// <summary>„Das Token wurde widerrufen."</summary>
    public const string TokenRevoked = "token-revoked";

    /// <summary>„Die Sitzungsdauer wurde gespeichert."</summary>
    public const string SettingsSaved = "settings-saved";

    /// <summary>„Das Passwort wurde geändert."</summary>
    public const string PasswordChanged = "password-changed";

    /// <summary>„Der Benutzer wurde nicht gefunden." (Fehlerschlüssel)</summary>
    public const string UserNotFound = "user-not-found";

    /// <summary>„Das Token wurde nicht gefunden." (Fehlerschlüssel)</summary>
    public const string TokenNotFound = "token-not-found";

    private static readonly IReadOnlyDictionary<string, string> s_texts =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UserCreated] = "Der Benutzer wurde angelegt.",
            [RoleChanged] = "Die Berechtigungsstufe wurde geändert.",
            [UserDeactivated] = "Der Benutzer wurde deaktiviert.",
            [UserActivated] = "Der Benutzer wurde aktiviert.",
            [UserDeleted] = "Der Benutzer wurde gelöscht.",
            [LockoutReset] = "Die Sperrung wurde zurückgesetzt.",
            [TokenRevoked] = "Das Token wurde widerrufen.",
            [SettingsSaved] = "Die Sitzungsdauer wurde gespeichert.",
            [PasswordChanged] = "Das Passwort wurde geändert.",
            [UserNotFound] = "Der Benutzer wurde nicht gefunden.",
            [TokenNotFound] = "Das Token wurde nicht gefunden.",
        };

    /// <summary>Löst einen einzelnen <c>msg</c>-/<c>err</c>-Schlüssel auf.</summary>
    /// <param name="key">Der Schlüssel aus dem Query-String.</param>
    /// <returns>Der zugeordnete deutsche Text oder <see langword="null"/>
    /// bei unbekanntem oder fehlendem Schlüssel.</returns>
    public static string? Resolve(string? key)
        => key is not null && s_texts.TryGetValue(key, out var text) ? text : null;

    /// <summary>Löst das Parameterpaar eines GET-Handlers auf: Ein vorhandener
    /// <paramref name="err"/>-Schlüssel hat Vorrang und erzwingt das Fehler-Styling;
    /// auflösbare Texte kommen ausschließlich aus der festen Tabelle.</summary>
    /// <param name="msg">Der Erfolgs-Schlüssel.</param>
    /// <param name="err">Der Fehler-Schlüssel.</param>
    /// <returns>Der anzuzeigende Text (oder <see langword="null"/> für keine Box)
    /// und ob er als Fehler gerendert wird.</returns>
    public static (string? Text, bool IsError) Resolve(string? msg, string? err)
        => err is not null
            ? (Resolve(err), true)
            : (Resolve(msg), false);
}
