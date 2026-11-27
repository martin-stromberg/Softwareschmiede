using System.Text;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Pages;

/// <summary>Rendert die Formularseiten Registrierung, Login, Profil und
/// Zugriffsverweigerung im <c>StatusPageComposer</c>-Stil (JS-frei, HTML-encodiert).</summary>
public static class AuthPagesComposer
{
    /// <summary>Rendert das Erst-Registrierungsformular (Setup-Modus ohne vorhandene Benutzer).</summary>
    /// <param name="antiforgeryFieldName">Der Feldname des Antiforgery-Hidden-Felds.</param>
    /// <param name="antiforgeryToken">Das Antiforgery-Request-Token.</param>
    /// <param name="error">Optionale Fehlermeldung über dem Formular.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderRegister(string antiforgeryFieldName, string antiforgeryToken, string? error = null)
    {
        var sb = new StringBuilder(4_000);
        PageShell.BeginPage(sb, "Ersteinrichtung – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Softwareschmiede-Status");
        sb.Append("<section><h1>Ersteinrichtung</h1>");
        sb.Append("<p class=\"muted\">Es existiert noch kein Benutzerkonto. Lege jetzt das ")
            .Append("Administratorkonto an — die Registrierung schließt sich danach dauerhaft.</p>");
        PageShell.AppendMessage(sb, error, isError: true);
        sb.Append("<form method=\"post\" action=\"/register\">");
        PageShell.AppendHiddenToken(sb, antiforgeryFieldName, antiforgeryToken);
        sb.Append("<label>Benutzername<input type=\"text\" name=\"username\" required autocomplete=\"username\" pattern=\"")
            .Append(UserNamePolicy.AllowedCharactersPattern).Append("\" title=\"")
            .Append(PageShell.E(UserNamePolicy.FieldHint)).Append("\"></label>");
        sb.Append("<p class=\"muted\">").Append(PageShell.E(UserNamePolicy.FieldHint)).Append("</p>");
        sb.Append("<label>Passwort (mindestens ").Append(PasswordPolicy.MinLength)
            .Append(" Zeichen)<input type=\"password\" name=\"password\" required minlength=\"")
            .Append(PasswordPolicy.MinLength).Append("\" autocomplete=\"new-password\"></label>");
        sb.Append("<label>Passwort wiederholen<input type=\"password\" name=\"confirmPassword\" required autocomplete=\"new-password\"></label>");
        sb.Append("<button type=\"submit\">Administratorkonto anlegen</button>");
        sb.Append("</form></section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    /// <summary>Rendert das Anmeldeformular.</summary>
    /// <param name="antiforgeryFieldName">Der Feldname des Antiforgery-Hidden-Felds.</param>
    /// <param name="antiforgeryToken">Das Antiforgery-Request-Token.</param>
    /// <param name="error">Optionale Fehlermeldung über dem Formular.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderLogin(string antiforgeryFieldName, string antiforgeryToken, string? error = null)
    {
        var sb = new StringBuilder(4_000);
        PageShell.BeginPage(sb, "Anmeldung – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Softwareschmiede-Status");
        sb.Append("<section><h1>Anmeldung</h1>");
        PageShell.AppendMessage(sb, error, isError: true);
        sb.Append("<form method=\"post\" action=\"/login\">");
        PageShell.AppendHiddenToken(sb, antiforgeryFieldName, antiforgeryToken);
        sb.Append("<label>Benutzername<input type=\"text\" name=\"username\" required autocomplete=\"username\"></label>");
        sb.Append("<label>Passwort<input type=\"password\" name=\"password\" required autocomplete=\"current-password\"></label>");
        sb.Append("<button type=\"submit\">Anmelden</button>");
        sb.Append("</form></section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    /// <summary>Rendert das Profil des angemeldeten Benutzers (Passwort ändern).</summary>
    /// <param name="user">Der Benutzerkontext (Name, Stufe, Antiforgery-Token).</param>
    /// <param name="message">Optionale Status-/Fehlermeldung.</param>
    /// <param name="isError">Ob <paramref name="message"/> ein Fehler ist.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderProfile(PageUserContext user, string? message = null, bool isError = false)
    {
        var sb = new StringBuilder(4_000);
        PageShell.BeginPage(sb, "Profil – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Profil");
        PageShell.AppendNav(sb, user);
        sb.Append("<section><h2>Konto</h2>");
        sb.Append("<p class=\"muted\">Angemeldet als <strong>").Append(PageShell.E(user.UserName))
            .Append("</strong> · Stufe ")
            .Append(PageShell.E(StatusRoles.DisplayName(user.IsAdmin ? StatusRoles.Administration : StatusRoles.Ueberwacher)))
            .Append("</p>");
        PageShell.AppendMessage(sb, message, isError);
        sb.Append("<h2>Passwort ändern</h2>");
        sb.Append("<form method=\"post\" action=\"/profile/password\">");
        PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
        sb.Append("<label>Aktuelles Passwort<input type=\"password\" name=\"currentPassword\" required autocomplete=\"current-password\"></label>");
        sb.Append("<label>Neues Passwort (mindestens ").Append(PasswordPolicy.MinLength)
            .Append(" Zeichen)<input type=\"password\" name=\"newPassword\" required minlength=\"")
            .Append(PasswordPolicy.MinLength).Append("\" autocomplete=\"new-password\"></label>");
        sb.Append("<label>Neues Passwort wiederholen<input type=\"password\" name=\"confirmPassword\" required autocomplete=\"new-password\"></label>");
        sb.Append("<button type=\"submit\">Passwort ändern</button>");
        sb.Append("</form></section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    /// <summary>Rendert die Fehlerseite für fehlgeschlagene Formular-Absendungen
    /// (Antiforgery-/CSRF-Validierung) — mit Erklärung und Rückweg statt nacktem 400.</summary>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderFormExpired()
    {
        var sb = new StringBuilder(2_000);
        PageShell.BeginPage(sb, "Formular abgelaufen – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Softwareschmiede-Status");
        sb.Append("<section><h1>Formular abgelaufen</h1>");
        sb.Append("<p class=\"muted\">Das Formular ist abgelaufen oder ungültig — das passiert z. B., ")
            .Append("wenn die Seite lange geöffnet war oder aus einem veralteten Browser-Tab ")
            .Append("abgesendet wurde. Deine Eingaben wurden nicht übernommen.</p>");
        sb.Append("<p class=\"muted\">Lade die Seite neu und sende das Formular erneut ab.</p>");
        sb.Append("<p><a href=\"/\">Zur Status-Übersicht</a></p>");
        sb.Append("</section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    /// <summary>Rendert die Zugriffsverweigerungs-Seite (403).</summary>
    /// <param name="user">Optionaler Benutzerkontext für die Navigation.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderAccessDenied(PageUserContext? user = null)
    {
        var sb = new StringBuilder(2_000);
        PageShell.BeginPage(sb, "Zugriff verweigert – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Softwareschmiede-Status");
        if (user is not null)
            PageShell.AppendNav(sb, user);
        sb.Append("<section><h1>Zugriff verweigert</h1>");
        sb.Append("<p class=\"muted\">Deine Berechtigungsstufe reicht für diesen Bereich nicht aus. ")
            .Append("Die Status-Übersicht bleibt dir weiterhin zugänglich.</p>");
        sb.Append("<p><a href=\"/\">Zur Status-Übersicht</a></p>");
        sb.Append("</section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }
}
