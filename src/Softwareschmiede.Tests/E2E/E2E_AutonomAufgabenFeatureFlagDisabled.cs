using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für das Verhalten der Autonome-Aufgaben-UI, wenn das Feature-Flag
/// <see cref="Softwareschmiede.Application.Services.AutonomAufgabenOptions.Enabled"/> über die Umgebungsvariable
/// <c>AutonomAufgaben__Enabled</c> deaktiviert ist (Issue 205). Läuft bewusst als eigener App-Lifecycle
/// (statt als Phase in <c>End2EndTest.RunGeneralTests</c>): Das Feature-Flag wird ausschließlich beim
/// Start über <c>IOptions&lt;AutonomAufgabenOptions&gt;</c> gebunden und kann nicht nachträglich in einer
/// bereits laufenden App-Instanz umgeschaltet werden (siehe Plan Issue 205, "Abhängigkeit zu GUI-Refresh").
///
/// Konsolidiert alle drei im Plan beschriebenen Deaktivierungs-Szenarien (Dialog erscheint nicht, Fallback-Button
/// "Starten" bleibt verfügbar, Agent/AutonomAufgabeKonfiguration wird nicht angelegt) in einer einzigen
/// Testmethode mit einem gemeinsamen App-Lifecycle (siehe CLAUDE.md, Abschnitt FlaUI-Konsolidierung).
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection("E2E")]
public sealed class E2E_AutonomAufgabenFeatureFlagDisabled : WpfTestBase
{
    /// <summary>
    /// Szenario: Bei deaktiviertem Feature-Flag zeigt ein Klick auf "Autonome Aufgabe starten" statt des
    /// Initialisierungsdialogs eine Fehlermeldung ("Autonome Aufgaben sind in den Einstellungen deaktiviert.")
    /// über die Guard-Klausel in <c>AutonomAufgabeStartService.StarteAsync()</c>. Der Fallback-Button "Starten"
    /// (nicht-autonomer Weg) bleibt unabhängig vom Feature-Flag sichtbar und funktionsfähig. In der Datenbank
    /// wird keine <c>AutonomAufgabeKonfiguration</c> angelegt, der Projektleiter-Agent startet also nicht.
    /// </summary>
    [Fact]
    public async Task AutonomAufgabeInitialisieren_ZeigtFehlermeldungStattDialog_WennFeatureFlagDeaktiviert()
    {
        Environment.SetEnvironmentVariable("AutonomAufgaben__Enabled", "false");
        try
        {
            var repositoryFolderName = "autonom-flag-disabled-repo";
            var projektName = "AutonomAufgabe-FlagDisabled-Projekt";
            var aufgabeTitel = $"Feature-Flag-Test {Guid.NewGuid():N}"[..40];

            var mainWindow = LaunchAppAndGetMainWindow();

            SetupProjectMitNeuerAufgabe(mainWindow, repositoryFolderName, projektName);
            var taskDetail = new TaskDetailView(mainWindow);
            taskDetail.SetTaskTitle(aufgabeTitel);
            taskDetail.SaveTask();

            // Fallback-Button "Starten" (nicht-autonomer Weg) bleibt verfügbar, unabhängig vom Feature-Flag.
            WaitForElement(mainWindow, cf => cf.ByName("Starten"), Short);

            var initialisierenButton = WaitForElement(mainWindow, cf => cf.ByName("AutonomAufgabeInitialisieren"), Short);
            initialisierenButton.AsButton().Click();

            var fehlerMeldung = WaitForElement(mainWindow, cf => cf.ByName("FehlerMeldung"), Short);
            Assert.Equal(
                "Autonome Aufgaben sind in den Einstellungen deaktiviert.",
                GetHelpTextOrName(fehlerMeldung));

            // Der Initialisierungsdialog darf nicht erscheinen: die Guard-Klausel greift, bevor der Dialog
            // geöffnet wird (AutonomAufgabeStartService.StarteAsync gibt vorher ein Fehlerresultat zurück).
            Assert.Null(Automation.GetDesktop().FindFirstDescendant(
                cf => cf.ByName("Autonome Aufgabe initialisieren").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))));

            await using (var db = OpenTestDbContext())
            {
                var existiertKonfiguration = await db.AutonomAufgabeKonfigurationen
                    .Include(k => k.Aufgabe)
                    .AnyAsync(k => k.Aufgabe.Titel == aufgabeTitel);
                Assert.False(existiertKonfiguration, "Es darf keine AutonomAufgabeKonfiguration angelegt worden sein, solange das Feature-Flag deaktiviert ist.");
            }

            taskDetail.ForceClose(recurseToDashboard: false);
            var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
            projectDetail.DeleteProject();
            projectDetail.Menu.NavigateToDashboard();
        }
        finally
        {
            // Umgebungsvariable zurücksetzen: gilt prozessweit, darf nicht in andere (parallel im selben
            // Testprozess laufende, außerhalb der [Collection("E2E")]-Gruppe befindliche) Tests hineinlecken.
            Environment.SetEnvironmentVariable("AutonomAufgaben__Enabled", null);
        }
    }
}
