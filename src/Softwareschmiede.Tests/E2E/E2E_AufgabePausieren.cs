using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Tests.E2E.Views;
using Softwareschmiede.Tests.E2E.Views.Dialogs;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für das manuelle Pausieren und Aufheben einer regulären Aufgabe (Issue 151):
/// Ribbon-Button „Pause einstellen" → modaler Dialog mit Vorbelegung → Kachel-Status
/// „⏸ Pausiert (noch …)" mit Abblendungs-Marker → vorzeitiges Aufheben.
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI), Windows 10 Build 17763 oder neuer
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: Eine per Datenbank-Seed auf „Gestartet"/„Aktiv" gesetzte Aufgabe wird über den
    /// Ribbon-Button „Pause einstellen" pausiert (Dialog-Vorbelegung ≈ aktueller Zeitpunkt,
    /// Zielzeitpunkt +2 h). Die Seitenleisten-Kachel zeigt anschließend „⏸ Pausiert (noch …)" und den
    /// Abblendungs-Marker „Pausiert:True" im HelpText. Ein erneut geöffneter Dialog zeigt die aktive
    /// Pause; „Pause aufheben" stellt den Normalstatus („Pausiert:False") wieder her.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task AufgabePausieren_DialogCountdownAbblendungUndAufheben_E2E(Window mainWindow)
    {
        var projektName = $"Pause-Projekt {Guid.NewGuid():N}"[..30];
        var titel = $"Pause-Aufgabe {Guid.NewGuid():N}"[..30];

        // Projekt und Aufgabe über die UI anlegen; ein CLI-Start ist für die Pause nicht nötig.
        NavigateToProjects(mainWindow);
        CreateAndOpenProject(mainWindow, projektName);
        var projectDetail = new ProjectDetailView(mainWindow);
        var taskDetail = projectDetail.CreateTask();
        taskDetail.SetTaskTitle(titel);
        taskDetail.SaveTask();
        taskDetail.WaitForPersisted();

        // Aufgabe direkt in der Test-DB auf Gestartet/Aktiv setzen, damit sie als aktive Aufgabe
        // in der Seitenleiste erscheint (über die UI nicht abbildbar ohne echten CLI-Start).
        await using (var db = OpenTestDbContext())
        {
            var aufgabe = await db.Aufgaben.SingleAsync(a => a.Titel == titel);
            aufgabe.Status = AufgabeStatus.Gestartet;
            aufgabe.AusfuehrungsStatus = AufgabeAusfuehrungsStatus.Aktiv;
            aufgabe.KiPluginPrefix = "Softwareschmiede.KiSimulator";
            await db.SaveChangesAsync();
        }

        // Die Seitenleiste aktualisiert sich über den 5-s-Timer; auf die Kachel warten.
        // Anker ist der "→"-Button 'AufgabeNavigieren:{titel}' — er existiert nur im
        // Seitenleisten-Template (ShowNavigationButton=True) und beweist damit gezielt die
        // Seitenleisten-Kachel (die Kachel-Border selbst erzeugt keinen AutomationPeer).
        WaitForElement(
            mainWindow,
            cf => cf.ByName($"AufgabeNavigieren:{titel}"),
            Long);

        // Über die Seitenleiste in die (frisch geladene) Detailansicht wechseln. Als
        // Synchronisationssignal auf den „Zurück"-Button der TaskDetailView warten — der Aufgabentitel
        // allein wäre bereits über die Seitenleisten-Kachel im Automation-Baum auffindbar.
        var detail = taskDetail.Menu.NavigateToTask(titel);
        WaitForElement(mainWindow, cf => cf.ByName("Zurück"), Short);

        // Phase 1: Dialog öffnen — Vorbelegung muss ≈ dem aktuellen Zeitpunkt entsprechen.
        var pauseDialog = new AufgabePausierenDialogView(mainWindow).ForceShow();
        var (stunde, minute) = pauseDialog.GetVorbelegteZeit();
        var jetzt = DateTime.Now;
        var vorbelegungMinuten = stunde * 60 + minute;
        var jetztMinuten = jetzt.Hour * 60 + jetzt.Minute;
        var diffMinuten = Math.Abs(vorbelegungMinuten - jetztMinuten);
        Assert.True(
            Math.Min(diffMinuten, 24 * 60 - diffMinuten) <= 1,
            $"Vorbelegung ({stunde:00}:{minute:00}) weicht zu stark vom aktuellen Zeitpunkt ({jetzt:HH:mm}) ab.");
        Assert.False(pauseDialog.IsAufhebenEnabled(), "Ohne bestehende Pause muss 'Pause aufheben' deaktiviert sein.");

        // Phase 2: Zeitpunkt +2 h bestätigen → Pause wird persistiert und in der Kachel sichtbar.
        var ziel = DateTime.Now.AddHours(2);
        pauseDialog.SetzeZeitpunkt(ziel);
        pauseDialog.Bestaetigen();

        await using (var db = OpenTestDbContext())
        {
            var aufgabe = await db.Aufgaben.SingleAsync(a => a.Titel == titel);
            Assert.NotNull(aufgabe.PausiertBisUtc);
            Assert.True(
                Math.Abs((aufgabe.PausiertBisUtc.Value - new DateTimeOffset(ziel)).TotalMinutes) <= 1.5,
                $"PausiertBisUtc '{aufgabe.PausiertBisUtc:O}' weicht vom erwarteten Ziel '{ziel:O}' ab.");
        }

        // Kachel zeigt „⏸ Pausiert (noch …)" (HelpText des Status-Elements) und „Pausiert:True"
        // (HelpText des 'AufgabeKachel:'-Marker-Elements — E2E-Anker für die Opacity-Abblendung).
        WarteAufKachelStatus(mainWindow, titel, status => status.StartsWith("⏸ Pausiert", StringComparison.Ordinal), Long);
        WarteAufKachelPausiertMarker(mainWindow, titel, "Pausiert:True", Long);

        // Phase 3: Dialog erneut öffnen — die bestehende Pause wird angezeigt, „Pause aufheben" ist aktiv.
        var pauseDialog2 = new AufgabePausierenDialogView(mainWindow).ForceShow();
        Assert.True(pauseDialog2.IsAufhebenEnabled(), "Bei bestehender Pause muss 'Pause aufheben' aktiviert sein.");
        pauseDialog2.WaitForAktuellePauseAnzeige();

        // Phase 4: Vorzeitiges Aufheben → Normalstatus und „Pausiert:False" kehren zurück.
        pauseDialog2.Aufheben();

        await using (var db = OpenTestDbContext())
        {
            var aufgabe = await db.Aufgaben.SingleAsync(a => a.Titel == titel);
            Assert.Null(aufgabe.PausiertBisUtc);
        }

        WarteAufKachelStatus(mainWindow, titel, status => !status.StartsWith("⏸ Pausiert", StringComparison.Ordinal), Long);
        WarteAufKachelPausiertMarker(mainWindow, titel, "Pausiert:False", Long);

        // Aufräumen: Die Aufgabe hat keinen laufenden CLI-Prozess; das Projekt kann regulär gelöscht
        // werden. Die Detailansicht wurde über die Seitenleiste geöffnet — „Zurück" führt zum Dashboard.
        detail.ForceClose(recurseToDashboard: false);
        var dashboard = Assert.IsType<DashboardView>(mainWindow.CurrentView());
        var projectList = dashboard.Menu.NavigateToProjects();
        var projectDetailForDelete = projectList.OpenProject(projektName);
        projectDetailForDelete.DeleteProject();
        projectDetailForDelete.Menu.NavigateToDashboard();
    }

    /// <summary>
    /// Wartet, bis der <c>HelpText</c> des Kachel-Status-Elements <c>AufgabeStatus:{titel}</c> das
    /// gegebene Prädikat erfüllt (Polling, da die Seitenleiste sich über den 5-s-Timer bzw. den
    /// Laufdaten-Notifier aktualisiert).
    /// </summary>
    private static void WarteAufKachelStatus(Window mainWindow, string titel, Func<string, bool> predikat, TimeSpan timeout)
        => WarteAufElementHelpText(mainWindow, $"AufgabeStatus:{titel}", predikat, timeout, $"Statuskachel von '{titel}'");

    /// <summary>
    /// Wartet, bis der <c>HelpText</c> des Marker-Elements <c>AufgabeKachel:{titel}</c> den erwarteten
    /// <c>Pausiert:…</c>-Marker enthält (E2E-Anker für die Opacity-Abblendung pausierter Kacheln —
    /// die Kachel-Border selbst erzeugt keinen AutomationPeer und ist per UIA unsichtbar).
    /// </summary>
    private static void WarteAufKachelPausiertMarker(Window mainWindow, string titel, string erwarteterMarker, TimeSpan timeout)
        => WarteAufElementHelpText(
            mainWindow,
            $"AufgabeKachel:{titel}",
            h => h.Contains(erwarteterMarker, StringComparison.Ordinal),
            timeout,
            $"Kachel von '{titel}' mit Marker '{erwarteterMarker}'");

    /// <summary>
    /// Wartet per Polling, bis der <c>HelpText</c> des benannten Elements das gegebene Prädikat erfüllt
    /// (die Seitenleiste aktualisiert sich über den 5-s-Timer bzw. den Laufdaten-Notifier).
    /// </summary>
    private static void WarteAufElementHelpText(Window mainWindow, string elementName, Func<string, bool> predikat, TimeSpan timeout, string beschreibung)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? lastHelpText = null;
        while (DateTime.UtcNow < deadline)
        {
            var element = mainWindow.FindFirstDescendant(cf => cf.ByName(elementName));
            lastHelpText = element?.HelpText;
            if (lastHelpText is not null && predikat(lastHelpText))
                return;

            Thread.Sleep(200);
        }

        throw new TimeoutException(
            $"{beschreibung} erfüllte innerhalb von {timeout.TotalSeconds}s nicht das erwartete Prädikat. Zuletzt gesehen: '{lastHelpText}'.");
    }
}
