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
    protected async Task TerminalFallbackDiagnostics_PipeFallbackAndRequiresPtyError_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();
        await SetForcePtyUnavailableAsync(true);

        try
        {
            SetupProjectWithNewWorkItem(mainWindow, "TerminalDiag-Repo", "TerminalDiag-Projekt");
            var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", forProjectUse: false);
            taskDetail.WaitForCliRunning();

            // Pipe-Fallback ist diagnostiziert: Markerzeile mit Einzelcheck-Ergebnissen im
            // CliOutput-Protokoll; die Session läuft trotzdem (Interaktivität via Pipe).
            await WaitOnCliOutputAsync("[Terminal-Diagnose]");
            await WaitOnCliOutputAsync("PtyVerfuegbar=False");
            Assert.True(taskDetail.IsCliRunning());

            // Der eingeschränkte Modus muss auch ohne Blick ins Protokoll sichtbar sein —
            // die Statusleiste der Aufgabe meldet ihn direkt.
            var statusText = taskDetail.GetCliStatusText();
            Assert.Contains("eingeschränkter Modus", statusText);

            // RequiresPty-Plugin bei laufender CLI wechseln ("Plugin ändern" ist nur bei
            // laufender CLI aktiv): der Start muss fehlschlagen und eine Fehlermeldung zeigen —
            // tolerant gegenüber der Installationslage von devin (NotFound oder RequiresPty-Fehler).
            var switchDialog = taskDetail.OpenPluginChangeDialog();
            switchDialog.SelectPlugin("Softwareschmiede.Devin");
            switchDialog.Confirm();

            var errorBanner = new ErrorView(mainWindow);
            await WaitUntilAsync(() => Task.FromResult(errorBanner.IsVisible), maxAttempts: 50);
            var fehlertext = errorBanner.GetErrorMessage();
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

        var projectDetailAfterSave = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        taskNormal = projectDetailAfterSave.OpenTask("Normalstart-Aufgabe");
        taskNormal.Start("Softwareschmiede.KiSimulator", forProjectUse: false);
        taskNormal.WaitForCliRunning();
        Assert.True(taskNormal.IsCliRunning());

        taskNormal.StopCli();
        taskNormal.ForceClose(recurseToDashboard: false);
        var projectDetailFinal = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetailFinal.DeleteProject();
    }

    /// <summary>Setzt oder löscht den Test-Override <see cref="TerminalSessionService.ForcePtyUnavailableKey"/>
    /// direkt in der laufenden Test-Datenbank.</summary>
    private async Task SetForcePtyUnavailableAsync(bool? value)
    {
        await using var db = OpenTestDbContext();
        var entry = await db.AppSettings
            .FirstOrDefaultAsync(e => e.Key == TerminalSessionService.ForcePtyUnavailableKey);
        if (value is null)
        {
            if (entry is not null)
            {
                db.AppSettings.Remove(entry);
                await db.SaveChangesAsync();
            }
            return;
        }

        if (entry is null)
        {
            entry = new AppSetting
            {
                Id = Guid.NewGuid(),
                Key = TerminalSessionService.ForcePtyUnavailableKey,
            };
            db.AppSettings.Add(entry);
        }
        entry.Value = value.Value ? "true" : "false";
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }
}
