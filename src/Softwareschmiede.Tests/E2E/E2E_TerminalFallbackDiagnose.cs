using FlaUI.Core.AutomationElements;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test für die PTY-Fallback-Diagnose (Issue #271): Mit dem Test-Override
/// <see cref="TerminalSessionService.ForcePtyUnavailableKey"/> wird die PTY-Verfügbarkeit
/// erzwungen auf false — der KiSimulator (nur SupportsPty) muss dann auf dem Pipe-Backend laufen
/// und eine <c>[Terminal-Diagnose]</c>-Markerzeile mit <c>PtyVerfuegbar=False</c> im
/// CliOutput-Protokoll zeigen. Ein Plugin mit <c>RequiresPty</c> (Softwareschmiede.Devin) muss
/// dagegen hart mit Fehlermeldung fehlschlagen — unabhängig davon, ob die <c>devin</c>-Executable
/// installiert ist (NotFound vs. RequiresPty-Fehler; geprüft wird auf Fehlerbanner + Marker,
/// nicht auf die spezifische Fehlerursache). Der Plugin-Wechsel muss bei laufender CLI erfolgen,
/// da der "Plugin ändern"-Button an <c>IsCliRunning</c> gebunden ist; die Normalstart-Prüfung nach
/// dem Entfernen des Overrides läuft über eine frische Aufgabe, weil der gescheiterte Wechsel den
/// Plugin-Prefix der ersten Aufgabe auf Devin belässt.
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Szenario: PTY-Verfügbarkeit per Test-Override erzwingen → KiSimulator läuft auf Pipe mit
    /// Diagnose-Marker → Devin-Wechsel schlägt mit Fehlermeldung fehl → nach dem Löschen des
    /// Overrides startet KiSimulator auf einer zweiten Aufgabe wieder normal.
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster der Anwendung.</param>
    protected async Task TerminalFallbackDiagnose_PipeFallbackUndRequiresPtyFehler_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        await SetForcePtyUnavailableAsync(true);

        try
        {
            SetupProjectMitNeuerAufgabe(mainWindow, "TerminalDiag-Repo", "TerminalDiag-Projekt");
            var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
            taskDetail.WaitForCliRunning();

            // Pipe-Fallback ist diagnostiziert: Markerzeile mit Einzelcheck-Ergebnissen im
            // CliOutput-Protokoll; die Session läuft trotzdem (Interaktivität via Pipe).
            await WarteAufCliOutputAsync("[Terminal-Diagnose]");
            await WarteAufCliOutputAsync("PtyVerfuegbar=False");
            Assert.True(taskDetail.IsCliRunning());

            // Der eingeschränkte Modus muss auch ohne Blick ins Protokoll sichtbar sein —
            // die Statusleiste der Aufgabe meldet ihn direkt.
            var statusText = taskDetail.GetCliStatusText();
            Assert.Contains("eingeschränkter Modus", statusText);

            // RequiresPty-Plugin bei laufender CLI wechseln ("Plugin ändern" ist nur bei
            // laufender CLI aktiv): der Start muss fehlschlagen und eine Fehlermeldung zeigen —
            // tolerant gegenüber der Installationslage von devin (NotFound oder RequiresPty-Fehler).
            var wechselDialog = taskDetail.OpenPluginChangeDialog();
            wechselDialog.SelectPlugin("Softwareschmiede.Devin");
            wechselDialog.Confirm();

            var fehlerBanner = new ErrorView(mainWindow);
            await WartenBisAsync(() => Task.FromResult(fehlerBanner.IsVisible), maxVersuche: 50);
            var fehlertext = fehlerBanner.GetErrorMessage();
            Assert.False(string.IsNullOrWhiteSpace(fehlertext));
            Assert.False(taskDetail.IsCliRunning());
        }
        finally
        {
            await SetForcePtyUnavailableAsync(null);
        }

        // Schlüssel gelöscht → Normalstart muss wieder funktionieren. Da der gescheiterte Wechsel
        // den Plugin-Prefix der ersten Aufgabe auf Devin belässt, erfolgt die Prüfung über eine
        // zweite Aufgabe im selben Projekt.
        new TaskDetailView(mainWindow).ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        var taskNormal = projectDetail.CreateTask();
        taskNormal.SetTaskTitle("Normalstart-Aufgabe");
        taskNormal.SaveTask();
        taskNormal.GoBack();

        var projectDetailNachSpeichern = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        taskNormal = projectDetailNachSpeichern.OpenTask("Normalstart-Aufgabe");
        taskNormal.Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskNormal.WaitForCliRunning();
        Assert.True(taskNormal.IsCliRunning());

        taskNormal.StopCli();
        taskNormal.ForceClose(recurseToDashboard: false);
        var projectDetailFinal = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailFinal.DeleteProject();
    }

    /// <summary>Setzt oder löscht den Test-Override <see cref="TerminalSessionService.ForcePtyUnavailableKey"/>
    /// direkt in der laufenden Test-Datenbank.</summary>
    private async Task SetForcePtyUnavailableAsync(bool? wert)
    {
        await using var db = OpenTestDbContext();
        var eintrag = await db.AppEinstellungen
            .FirstOrDefaultAsync(e => e.Schluessel == TerminalSessionService.ForcePtyUnavailableKey);
        if (wert is null)
        {
            if (eintrag is not null)
            {
                db.AppEinstellungen.Remove(eintrag);
                await db.SaveChangesAsync();
            }
            return;
        }

        if (eintrag is null)
        {
            eintrag = new AppEinstellung
            {
                Id = Guid.NewGuid(),
                Schluessel = TerminalSessionService.ForcePtyUnavailableKey,
            };
            db.AppEinstellungen.Add(eintrag);
        }
        eintrag.Wert = wert.Value ? "true" : "false";
        eintrag.AktualisiertAm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }
}
