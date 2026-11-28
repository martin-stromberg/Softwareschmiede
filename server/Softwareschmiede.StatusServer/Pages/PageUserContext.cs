using Softwareschmiede.StatusServer.Data;

namespace Softwareschmiede.StatusServer.Pages;

/// <summary>Kontext des angemeldeten Benutzers für Seiten-Composer und <c>PageShell</c>-Navigation.</summary>
/// <param name="UserName">Der Benutzername des angemeldeten Benutzers.</param>
/// <param name="IsAdmin">Ob der Benutzer die Stufe <c>Administration</c> besitzt (Admin-Links sichtbar).</param>
/// <param name="AntiforgeryToken">Das Antiforgery-Request-Token für Hidden-Formularfelder.</param>
/// <param name="AntiforgeryFieldName">Der Feldname des Antiforgery-Hidden-Felds.</param>
/// <param name="Theme">Die wirksame Theme-Einstellung des Benutzers (<c>data-theme</c>-Attribut der Seite).</param>
public sealed record PageUserContext(
    string? UserName,
    bool IsAdmin,
    string AntiforgeryToken,
    string AntiforgeryFieldName,
    ThemePreference Theme);
