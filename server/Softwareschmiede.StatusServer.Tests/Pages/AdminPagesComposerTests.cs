using FluentAssertions;
using msTools.Updater;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Pages;
using Softwareschmiede.StatusServer.Security;
using Softwareschmiede.StatusServer.Update;

namespace Softwareschmiede.StatusServer.Tests.Pages;

/// <summary>Tests für <see cref="AdminPagesComposer"/> (Benutzer-, Token- und Einstellungslisten).</summary>
public sealed class AdminPagesComposerTests
{
    private static PageUserContext Admin()
        => new("admin", IsAdmin: true, "token-value", "__RequestVerificationToken");

    /// <summary>Die Benutzerliste zeigt Namen, Stufe, Status-Pill und Aktionsformulare;
    /// der letzte aktive Admin erhält deaktivierte Buttons.</summary>
    [Fact]
    public void RenderUserList_ShowsRowsActionsAndLastAdminHint()
    {
        var users = new List<UserAdminListItem>
        {
            new("id-1", "admin", StatusRoles.Administration, false, null, false, IsLastActiveAdmin: true),
            new("id-2", "watcher", StatusRoles.Ueberwacher, false, null, false, IsLastActiveAdmin: false),
            new("id-3", "locked", StatusRoles.Ueberwacher, false,
                new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero), IsLockedOut: true, IsLastActiveAdmin: false),
            new("id-4", "ghost", StatusRoles.Ueberwacher, true, null, false, IsLastActiveAdmin: false),
        };

        var html = AdminPagesComposer.RenderUserList(Admin(), users);

        html.Should().Contain("admin");
        html.Should().Contain("watcher");
        html.Should().Contain("letzter Admin");
        html.Should().Contain("gesperrt bis 01.01.2026 13:00 UTC");
        html.Should().Contain("deaktiviert");
        html.Should().Contain("aktiv");
        html.Should().Contain("action=\"/admin/users\"");
        html.Should().Contain("action=\"/admin/users/id-2/role\"");
        html.Should().Contain("action=\"/admin/users/id-2/deactivate\"");
        html.Should().Contain("action=\"/admin/users/id-3/unlock\"");
        // Löschen führt über die GET-Bestätigungsseite (Link, kein Direkt-POST).
        html.Should().Contain("href=\"/admin/users/id-2/delete\"");
        html.Should().Contain("name=\"__RequestVerificationToken\" value=\"token-value\"");
        // Das Anlegefeld kommuniziert den erlaubten Benutzername-Zeichensatz.
        html.Should().Contain("Erlaubte Zeichen");
        html.Should().Contain("pattern=\"[a-zA-Z0-9._@+-]+\"");
        // Stufe zeigt den deutschen Anzeigenamen, nicht den internen Schlüssel
        // (das Ü wird HTML-encodiert ausgegeben).
        html.Should().Contain(">&#220;berwacher<");
        html.Should().NotContain(">Ueberwacher<");
        // Geschützte Buttons des letzten Admins sind disabled.
        html.Should().MatchRegex("disabled[^>]*>Zu Überwacher herabstufen");
        html.Should().MatchRegex("disabled[^>]*>Löschen");
    }

    /// <summary>Die Token-Liste zeigt gültige und widerrufene Einträge; ein frisch
    /// erzeugter Klartext wird einmalig im Hinweisblock angezeigt.</summary>
    [Fact]
    public void RenderTokenList_ShowsRowsAndOneTimePlaintext()
    {
        var tokens = new List<RelayToken>
        {
            new() { Id = 1, Label = "Instanz-A", TokenHash = "hash-a", CreatedUtc = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero) },
            new() { Id = 2, Label = "Instanz-B", TokenHash = "hash-b", CreatedUtc = new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), RevokedUtc = new DateTimeOffset(2026, 1, 3, 10, 0, 0, TimeSpan.Zero) },
        };

        var html = AdminPagesComposer.RenderTokenList(Admin(), tokens, createdToken: "klartext-xyz");

        html.Should().Contain("nur jetzt");
        html.Should().Contain("klartext-xyz");
        html.Should().Contain("Instanz-A");
        html.Should().Contain("gültig");
        html.Should().Contain("widerrufen 03.01.2026 10:00 UTC");
        // Widerruf führt über die GET-Bestätigungsseite (Link, kein Direkt-POST).
        html.Should().Contain("href=\"/admin/tokens/1/revoke\"");
        html.Should().NotContain("action=\"/admin/tokens/1/revoke\"");
        html.Should().Contain("action=\"/admin/tokens\"");
    }

    /// <summary>Fehlermeldungen der Token-Seite werden in der Fehler-Box (msg err)
    /// gerendert — nicht als grüne Erfolgsmeldung.</summary>
    [Fact]
    public void RenderTokenList_ErrorMessage_UsesErrorBox()
    {
        var html = AdminPagesComposer.RenderTokenList(Admin(), [], "Das Token wurde nicht gefunden.", isError: true);

        html.Should().Contain("class=\"msg err\"");
        html.Should().Contain("Das Token wurde nicht gefunden.");
    }

    /// <summary>Ohne Tokens wird der ehrliche Leerzustand gerendert.</summary>
    [Fact]
    public void RenderTokenList_Empty_ShowsEmptyState()
    {
        var html = AdminPagesComposer.RenderTokenList(Admin(), []);

        html.Should().Contain("Noch keine Relay-Tokens vorhanden");
        html.Should().Contain("action=\"/admin/tokens\"");
    }

    /// <summary>Die Einstellungsseite zeigt den aktuellen Wert mit den Bereichsgrenzen.</summary>
    [Fact]
    public void RenderSettings_ShowsCurrentValueAndRange()
    {
        var html = AdminPagesComposer.RenderSettings(Admin(), 120, DefaultUpdateSettings(), DefaultUpdateStatus());

        html.Should().Contain("action=\"/admin/settings/session-duration\"");
        html.Should().Contain("value=\"120\"");
        html.Should().Contain("5");
        html.Should().Contain("43200");
    }

    /// <summary>Die Update-Sektion zeigt Status, Kanal-/Modus-Auswahl,
    /// Installationsuhrzeit und die manuellen Update-Aktionen.</summary>
    [Fact]
    public void RenderSettings_ShowsUpdateSection()
    {
        var settings = new StatusServerUpdateSettings(
            StatusServerUpdateChannel.ReleaseCandidate,
            StatusServerAutomationMode.CheckAndInstall,
            new TimeOnly(3, 30));
        var status = new StatusServerUpdateStatus(
            "1.2.3", "9.9.9", AutoUpdateState.ReadyToInstall,
            new DateTimeOffset(2026, 2, 1, 4, 0, 0, TimeSpan.Zero),
            null, IsLocked: false, IsLockStale: false, "updates/update.log");

        var html = AdminPagesComposer.RenderSettings(Admin(), 120, settings, status);

        html.Should().Contain("<h2>Updates</h2>");
        html.Should().Contain("Installierte Version");
        html.Should().Contain("1.2.3");
        html.Should().Contain("9.9.9");
        html.Should().Contain("Bereit zur Installation");
        // Der Zeitpunkt wird als LocalDateTime gerendert — erwartet in lokaler Zeitzone.
        html.Should().Contain(status.LastCheckedAt!.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm"));
        html.Should().Contain("action=\"/admin/settings/update\"");
        html.Should().Contain("<option value=\"prerelease\" selected>");
        html.Should().Contain("<option value=\"install\" selected>");
        html.Should().Contain("value=\"03:30\"");
        html.Should().Contain("action=\"/admin/settings/update/check\"");
        html.Should().Contain("action=\"/admin/settings/update/install\"");
        html.Should().Contain("Downtime bestätigen");
        // Das Skript-Protokoll wird als Pfad in der Statustabelle angezeigt.
        html.Should().Contain("Update-Protokoll");
        html.Should().Contain("updates/update.log");
        // Ohne verwaisten Lock gibt es kein Reset-Formular.
        html.Should().NotContain("/admin/settings/update/lock-reset");
    }

    /// <summary>Ein verwaister Lock blendet das Reset-Formular ein; ein Fehler wird
    /// in der Fehler-Pill der Statustabelle angezeigt.</summary>
    [Fact]
    public void RenderSettings_StaleLockAndError_ShowsResetFormAndErrorPill()
    {
        var status = new StatusServerUpdateStatus(
            "1.2.3", null, AutoUpdateState.Failed, null,
            "Quelle nicht erreichbar", IsLocked: true, IsLockStale: true, null);

        var html = AdminPagesComposer.RenderSettings(Admin(), 120, DefaultUpdateSettings(), status);

        html.Should().Contain("action=\"/admin/settings/update/lock-reset\"");
        html.Should().Contain("Update-Sperre zurücksetzen");
        html.Should().Contain("verwaist");
        html.Should().Contain("Quelle nicht erreichbar");
        html.Should().Contain("Fehlgeschlagen");
    }

    /// <summary>Die Ergebnisseite nach dem Installationsanlauf erklärt den Neustart
    /// und verweist auf das Update-Protokoll — der Pfad kommt als Parameter
    /// (konfigurierbares <c>Update:WorkingDirectory</c>), nicht als Literal.</summary>
    [Fact]
    public void RenderUpdateInstallStarted_ExplainsRestart()
    {
        var html = AdminPagesComposer.RenderUpdateInstallStarted(Admin(), "custom-workdir/update.log");

        html.Should().Contain("startet neu");
        html.Should().Contain("custom-workdir/update.log");
        html.Should().NotContain("updates/update.log");
    }

    private static StatusServerUpdateSettings DefaultUpdateSettings()
        => new(StatusServerUpdateChannel.Stable, StatusServerAutomationMode.Off,
            ServerSettingsService.DefaultUpdateInstallTime);

    private static StatusServerUpdateStatus DefaultUpdateStatus()
        => new("1.2.3", null, AutoUpdateState.Idle, null, null,
            IsLocked: false, IsLockStale: false, null);

    /// <summary>Meldungen werden escaped; Admin-Navigation ist für Admins sichtbar.</summary>
    [Fact]
    public void RenderUserList_MessageEncoded_AndAdminNavVisible()
    {
        var html = AdminPagesComposer.RenderUserList(Admin(), [], "Fehler <i>x</i>", isError: true);

        html.Should().Contain("Fehler &lt;i&gt;x&lt;/i&gt;");
        html.Should().Contain("href=\"/admin/users\"");
        html.Should().Contain("href=\"/admin/tokens\"");
        html.Should().Contain("href=\"/admin/settings\"");
    }

    /// <summary>Die Lösch-Bestätigungsseite zeigt Ziel, Konsequenz, POST-Formular
    /// (mit Antiforgery-Token) und einen Abbrechen-Rückweg.</summary>
    [Fact]
    public void RenderUserDeleteConfirm_ShowsTargetConsequenceAndForm()
    {
        var html = AdminPagesComposer.RenderUserDeleteConfirm(Admin(), "id-9", "watcher");

        html.Should().Contain("watcher");
        html.Should().Contain("unwiderruflich");
        html.Should().Contain("action=\"/admin/users/id-9/delete\"");
        html.Should().Contain("name=\"__RequestVerificationToken\" value=\"token-value\"");
        html.Should().Contain("Abbrechen");
    }

    /// <summary>Die Widerruf-Bestätigungsseite zeigt das Token-Label, die Konsequenz,
    /// das POST-Formular und einen Abbrechen-Rückweg.</summary>
    [Fact]
    public void RenderTokenRevokeConfirm_ShowsLabelConsequenceAndForm()
    {
        var token = new RelayToken { Id = 7, Label = "Instanz-X", TokenHash = "h" };

        var html = AdminPagesComposer.RenderTokenRevokeConfirm(Admin(), token);

        html.Should().Contain("Instanz-X");
        html.Should().Contain("unwiderruflich");
        html.Should().Contain("action=\"/admin/tokens/7/revoke\"");
        html.Should().Contain("name=\"__RequestVerificationToken\" value=\"token-value\"");
        html.Should().Contain("Abbrechen");
    }
}
