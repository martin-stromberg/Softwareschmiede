using System.Text;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Pages;

/// <summary>Rendert die Admin-Verwaltungsseiten (Benutzer, Relay-Tokens, Einstellungen)
/// im <c>StatusPageComposer</c>-Stil (JS-frei, HTML-encodiert).</summary>
public static class AdminPagesComposer
{
    /// <summary>Rendert die Benutzerverwaltung: Tabelle aller Benutzer inkl. Status,
    /// Stufen-Änderungs-Control und Deaktivieren-/Aktivieren-/Löschen-/Freischalten-Aktionen
    /// sowie das Anlege-Formular.</summary>
    /// <param name="user">Der Benutzerkontext des angemeldeten Administrators.</param>
    /// <param name="users">Die Benutzerliste.</param>
    /// <param name="message">Optionale Status-/Fehlermeldung.</param>
    /// <param name="isError">Ob <paramref name="message"/> ein Fehler ist.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderUserList(
        PageUserContext user,
        IReadOnlyList<UserAdminListItem> users,
        string? message = null,
        bool isError = false)
    {
        var sb = new StringBuilder(8_000);
        PageShell.BeginPage(sb, "Benutzerverwaltung – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Benutzerverwaltung");
        PageShell.AppendNav(sb, user);
        sb.Append("<section>");
        PageShell.AppendMessage(sb, message, isError);
        sb.Append("<table><tr><th>Benutzername</th><th>Stufe</th><th>Status</th><th>Aktionen</th></tr>");
        foreach (var item in users)
            RenderUserRow(sb, user, item);
        sb.Append("</table></section>");

        sb.Append("<section><h2>Benutzer anlegen</h2>");
        sb.Append("<form method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/users\">");
        PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
        sb.Append("<label>Benutzername<input type=\"text\" name=\"username\" required autocomplete=\"off\" pattern=\"")
            .Append(UserNamePolicy.AllowedCharactersPattern).Append("\" title=\"")
            .Append(PageShell.E(UserNamePolicy.FieldHint)).Append("\"></label>");
        sb.Append("<p class=\"muted\">").Append(PageShell.E(UserNamePolicy.FieldHint)).Append("</p>");
        sb.Append("<label>Passwort (mindestens ").Append(PasswordPolicy.MinLength)
            .Append(" Zeichen)<input type=\"password\" name=\"password\" required minlength=\"")
            .Append(PasswordPolicy.MinLength).Append("\" autocomplete=\"new-password\"></label>");
        sb.Append("<label>Berechtigungsstufe<select name=\"role\">");
        sb.Append("<option value=\"").Append(StatusRoles.Ueberwacher).Append("\">Überwacher (nur lesen)</option>");
        sb.Append("<option value=\"").Append(StatusRoles.Administration).Append("\">Administration (Verwaltung)</option>");
        sb.Append("</select></label>");
        sb.Append("<button type=\"submit\">Benutzer anlegen</button>");
        sb.Append("</form></section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    /// <summary>Rendert die Relay-Token-Verwaltung inkl. einmaliger Klartext-Anzeige
    /// direkt nach dem Anlegen eines neuen Tokens.</summary>
    /// <param name="user">Der Benutzerkontext des angemeldeten Administrators.</param>
    /// <param name="tokens">Die Token-Liste.</param>
    /// <param name="message">Optionale Status-/Fehlermeldung.</param>
    /// <param name="isError">Ob <paramref name="message"/> ein Fehler ist.</param>
    /// <param name="createdToken">Der Klartext eines gerade erzeugten Tokens (einmalige Anzeige).</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderTokenList(
        PageUserContext user,
        IReadOnlyList<RelayToken> tokens,
        string? message = null,
        bool isError = false,
        string? createdToken = null)
    {
        var sb = new StringBuilder(8_000);
        PageShell.BeginPage(sb, "Relay-Tokens – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Relay-Tokens");
        PageShell.AppendNav(sb, user);
        sb.Append("<section>");
        PageShell.AppendMessage(sb, message, isError);
        if (createdToken is not null)
        {
            sb.Append("<div class=\"token-once\"><strong>Neues Relay-Token erstellt.</strong> ")
                .Append("Der Klartext wird nur jetzt einmalig angezeigt — kopiere ihn und hinterlege ihn ")
                .Append("in den App-Einstellungen (Remote-Übersicht).<code>")
                .Append(PageShell.E(createdToken)).Append("</code></div>");
        }

        if (tokens.Count == 0)
        {
            sb.Append("<div class=\"empty\">Noch keine Relay-Tokens vorhanden — ")
                .Append("der <code>/relay</code>-Endpunkt lehnt aktuell alle Verbindungen ab.</div>");
        }
        else
        {
            sb.Append("<table><tr><th>Bezeichnung</th><th>Erstellt (UTC)</th><th>Status</th><th>Aktionen</th></tr>");
            foreach (var token in tokens)
            {
                sb.Append("<tr><td>").Append(PageShell.E(token.Label)).Append("</td><td>")
                    .Append(token.CreatedUtc.ToString("dd.MM.yyyy HH:mm")).Append("</td><td>");
                if (token.RevokedUtc is { } revoked)
                {
                    sb.Append("<span class=\"pill err\">widerrufen ")
                        .Append(revoked.ToString("dd.MM.yyyy HH:mm")).Append(" UTC</span>");
                }
                else
                {
                    sb.Append("<span class=\"pill done\">gültig</span>");
                }

                sb.Append("</td><td>");
                if (token.RevokedUtc is null)
                {
                    // Der Widerruf läuft über eine JS-freie Zwischenseite (GET → Bestätigungs-POST).
                    sb.Append("<a class=\"action-link danger\" href=\"").Append(PageShell.BasePath).Append("/admin/tokens/")
                        .Append(token.Id).Append("/revoke\">Widerrufen</a>");
                }
                else
                {
                    sb.Append("<form class=\"inline\"><button type=\"button\" disabled>Widerrufen</button></form>");
                }
                sb.Append("</td></tr>");
            }

            sb.Append("</table>");
        }

        sb.Append("</section>");
        sb.Append("<section><h2>Token anlegen</h2>");
        sb.Append("<form method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/tokens\">");
        PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
        sb.Append("<label>Bezeichnung<input type=\"text\" name=\"label\" required maxlength=\"")
            .Append(RelayTokenService.MaxLabelLength).Append("\"></label>");
        sb.Append("<button type=\"submit\">Token anlegen</button>");
        sb.Append("</form></section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    /// <summary>Rendert die JS-freie Bestätigungsseite vor dem unwiderruflichen
    /// Löschen eines Benutzerkontos (GET → Bestätigungs-POST auf denselben Pfad).</summary>
    /// <param name="user">Der Benutzerkontext des angemeldeten Administrators.</param>
    /// <param name="targetUserId">Die Id des zu löschenden Benutzers.</param>
    /// <param name="targetUserName">Der Name des zu löschenden Benutzers.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderUserDeleteConfirm(PageUserContext user, string targetUserId, string targetUserName)
    {
        var sb = new StringBuilder(2_000);
        PageShell.BeginPage(sb, "Benutzer löschen – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Benutzer löschen");
        PageShell.AppendNav(sb, user);
        sb.Append("<section><h1>Benutzer „").Append(PageShell.E(targetUserName)).Append("“ löschen?</h1>");
        sb.Append("<p class=\"muted\">Das Konto wird <strong>unwiderruflich</strong> gelöscht — ")
            .Append("die betroffene Person kann sich danach nicht mehr anmelden. ")
            .Append("Diese Aktion kann nicht rückgängig gemacht werden.</p>");
        sb.Append("<form method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/users/")
            .Append(PageShell.E(targetUserId)).Append("/delete\">");
        PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
        sb.Append("<button type=\"submit\" class=\"danger\">Endgültig löschen</button> ")
            .Append("<a href=\"").Append(PageShell.BasePath).Append("/admin/users\">Abbrechen</a>");
        sb.Append("</form></section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    /// <summary>Rendert die JS-freie Bestätigungsseite vor dem unwiderruflichen
    /// Widerruf eines Relay-Tokens (GET → Bestätigungs-POST auf denselben Pfad).</summary>
    /// <param name="user">Der Benutzerkontext des angemeldeten Administrators.</param>
    /// <param name="token">Das zu widerrufende Token.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderTokenRevokeConfirm(PageUserContext user, RelayToken token)
    {
        var sb = new StringBuilder(2_000);
        PageShell.BeginPage(sb, "Token widerrufen – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Token widerrufen");
        PageShell.AppendNav(sb, user);
        sb.Append("<section><h1>Token „").Append(PageShell.E(token.Label)).Append("“ widerrufen?</h1>");
        sb.Append("<p class=\"muted\">Das Token wird <strong>unwiderruflich</strong> ungültig — ")
            .Append("die damit verbundenen App-Instanzen können sich danach nicht mehr am Relay ")
            .Append("anmelden; für eine erneute Anbindung muss ein neues Token hinterlegt werden.</p>");
        sb.Append("<form method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/tokens/")
            .Append(token.Id).Append("/revoke\">");
        PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
        sb.Append("<button type=\"submit\" class=\"danger\">Token endgültig widerrufen</button> ")
            .Append("<a href=\"").Append(PageShell.BasePath).Append("/admin/tokens\">Abbrechen</a>");
        sb.Append("</form></section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    /// <summary>Rendert die Einstellungsseite (Sitzungsdauer der Anmeldesession).</summary>
    /// <param name="user">Der Benutzerkontext des angemeldeten Administrators.</param>
    /// <param name="sessionMinutes">Die aktuell konfigurierte Sitzungsdauer in Minuten.</param>
    /// <param name="message">Optionale Status-/Fehlermeldung.</param>
    /// <param name="isError">Ob <paramref name="message"/> ein Fehler ist.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public static string RenderSettings(
        PageUserContext user,
        int sessionMinutes,
        string? message = null,
        bool isError = false)
    {
        var sb = new StringBuilder(4_000);
        PageShell.BeginPage(sb, "Einstellungen – Softwareschmiede-Status");
        PageShell.AppendHeader(sb, "Einstellungen");
        PageShell.AppendNav(sb, user);
        sb.Append("<section>");
        PageShell.AppendMessage(sb, message, isError);
        sb.Append("<h2>Sitzungsdauer</h2>");
        sb.Append("<p class=\"muted\">Dauer der Anmeldesession in Minuten (")
            .Append(ServerSettingsService.MinSessionDurationMinutes).Append("–")
            .Append(ServerSettingsService.MaxSessionDurationMinutes)
            .Append("). Gilt ab der nächsten Ausstellung bzw. Erneuerung der Anmelde-Cookies.</p>");
        sb.Append("<form method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/settings/session-duration\">");
        PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
        sb.Append("<label>Sitzungsdauer in Minuten<input type=\"number\" name=\"minutes\" required min=\"")
            .Append(ServerSettingsService.MinSessionDurationMinutes).Append("\" max=\"")
            .Append(ServerSettingsService.MaxSessionDurationMinutes).Append("\" value=\"")
            .Append(sessionMinutes).Append("\"></label>");
        sb.Append("<button type=\"submit\">Speichern</button>");
        sb.Append("</form></section>");
        PageShell.EndPage(sb);
        return sb.ToString();
    }

    private static void RenderUserRow(StringBuilder sb, PageUserContext user, UserAdminListItem item)
    {
        sb.Append("<tr><td><strong>").Append(PageShell.E(item.UserName)).Append("</strong>");
        if (item.IsLastActiveAdmin)
            sb.Append(" <span class=\"pill pause\">letzter Admin</span>");
        sb.Append("</td><td>").Append(PageShell.E(StatusRoles.DisplayName(item.Role))).Append("</td><td>");
        if (item.IsDeactivated)
            sb.Append("<span class=\"pill err\">deaktiviert</span>");
        else if (item.IsLockedOut && item.LockoutEnd is { } end)
            sb.Append("<span class=\"pill wait\">gesperrt bis ").Append(end.ToString("dd.MM.yyyy HH:mm")).Append(" UTC</span>");
        else
            sb.Append("<span class=\"pill done\">aktiv</span>");
        sb.Append("</td><td>");

        AppendRoleAction(sb, user, item);
        AppendActivationAction(sb, user, item);
        AppendUnlockAction(sb, user, item);
        AppendDeleteAction(sb, user, item);

        sb.Append("</td></tr>");
    }

    /// <summary>Stufen-Änderungs-Control: jeweils die andere Stufe als POST-Aktion.</summary>
    private static void AppendRoleAction(StringBuilder sb, PageUserContext user, UserAdminListItem item)
    {
        if (item.Role == StatusRoles.Administration)
        {
            var disabled = item.IsLastActiveAdmin ? " disabled" : string.Empty;
            var title = item.IsLastActiveAdmin
                ? " title=\"Der letzte aktive Administrator kann nicht herabgestuft werden.\""
                : string.Empty;
            sb.Append("<form class=\"inline\" method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/users/")
                .Append(PageShell.E(item.Id)).Append("/role\">");
            PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
            sb.Append("<input type=\"hidden\" name=\"role\" value=\"").Append(StatusRoles.Ueberwacher).Append("\">");
            sb.Append("<button type=\"submit\"").Append(disabled).Append(title).Append(">Zu Überwacher herabstufen</button></form>");
        }
        else if (item.Role == StatusRoles.Ueberwacher)
        {
            sb.Append("<form class=\"inline\" method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/users/")
                .Append(PageShell.E(item.Id)).Append("/role\">");
            PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
            sb.Append("<input type=\"hidden\" name=\"role\" value=\"").Append(StatusRoles.Administration).Append("\">");
            sb.Append("<button type=\"submit\">Zu Administration befördern</button></form>");
        }
    }

    /// <summary>Aktivieren- bzw. Deaktivieren-Aktion je nach Kontostatus.</summary>
    private static void AppendActivationAction(StringBuilder sb, PageUserContext user, UserAdminListItem item)
    {
        if (item.IsDeactivated)
        {
            sb.Append("<form class=\"inline\" method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/users/")
                .Append(PageShell.E(item.Id)).Append("/activate\">");
            PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
            sb.Append("<button type=\"submit\">Aktivieren</button></form>");
        }
        else
        {
            var disabled = item.IsLastActiveAdmin ? " disabled" : string.Empty;
            var title = item.IsLastActiveAdmin
                ? " title=\"Der letzte aktive Administrator kann nicht deaktiviert werden.\""
                : string.Empty;
            sb.Append("<form class=\"inline\" method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/users/")
                .Append(PageShell.E(item.Id)).Append("/deactivate\">");
            PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
            sb.Append("<button type=\"submit\"").Append(disabled).Append(title).Append(">Deaktivieren</button></form>");
        }
    }

    /// <summary>Sperrung zurücksetzen (nur aktiv, wenn gesperrt).</summary>
    private static void AppendUnlockAction(StringBuilder sb, PageUserContext user, UserAdminListItem item)
    {
        sb.Append("<form class=\"inline\" method=\"post\" action=\"").Append(PageShell.BasePath).Append("/admin/users/")
            .Append(PageShell.E(item.Id)).Append("/unlock\">");
        PageShell.AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
        sb.Append("<button type=\"submit\"").Append(item.IsLockedOut ? string.Empty : " disabled")
            .Append(">Sperrung zurücksetzen</button></form>");
    }

    /// <summary>Löschen-Aktion — als Link auf die JS-freie Bestätigungsseite
    /// (GET → Bestätigungs-POST); der letzte aktive Admin erhält einen deaktivierten Button.</summary>
    private static void AppendDeleteAction(StringBuilder sb, PageUserContext user, UserAdminListItem item)
    {
        if (item.IsLastActiveAdmin)
        {
            sb.Append("<form class=\"inline\"><button type=\"button\" disabled")
                .Append(" title=\"Der letzte aktive Administrator kann nicht gelöscht werden.\"")
                .Append(">Löschen</button></form>");
            return;
        }

        sb.Append("<a class=\"action-link danger\" href=\"").Append(PageShell.BasePath).Append("/admin/users/")
            .Append(PageShell.E(item.Id)).Append("/delete\">Löschen</a>");
    }
}
