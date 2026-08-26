using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Test: Zeitgesteuerter Prompt-Versand über das CLI-Ribbon der Aufgabendetailansicht.
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
    /// Szenario: Nutzer startet die CLI, trägt eine in der Zukunft liegende Zielzeit ein, wählt eine
    /// Promptvorlage und klickt "Zeitgesteuert senden". Die Statusanzeige "Prompt in Wartestellung"
    /// muss erscheinen, ohne dass ein Fehlerbanner sichtbar wird.
    /// </summary>
    protected void ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E(Window mainWindow)
    {
        SkipWennConPtyNichtVerfuegbar();
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "ZeitgesteuertPrompt-Repo", "ZeitgesteuertPrompt-Projekt");
        var taskDetail = new TaskDetailView(mainWindow).Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);
        taskDetail.WaitForCliRunning();

        // Die App berechnet die Zielzeit stets aus dem heutigen Datum. Läge "jetzt + 5 Minuten" bereits am
        // nächsten Tag (kurz vor Mitternacht), wäre die daraus resultierende heutige Uhrzeit bereits vergangen,
        // wodurch die App sofort statt zeitgesteuert versendet und die erwartete Wartestellung-Anzeige ausbleibt.
        // Da dieser E2E-Test die reale Systemuhr nutzt (kein injizierbarer TimeProvider in der App-UI-Schicht),
        // wird die Zielzeit in diesem Fall auf den letztmöglichen Zeitpunkt des heutigen Tages begrenzt.
        // In der letzten Tagesminute (23:59:00-23:59:59) läge auch diese Begrenzung bereits in der
        // Vergangenheit, weshalb der Test in diesem terminalen 1-Minuten-Fenster übersprungen wird,
        // statt eine unerreichbare Zielzeit zu wählen.
        var jetzt = DateTime.Now;
        Skip.If(jetzt.Hour == 23 && jetzt.Minute >= 59, "Letzte Tagesminute: Mitternachts-Guard würde eine bereits vergangene Zielzeit liefern.");

        var zielzeitKandidat = jetzt.AddMinutes(5);
        var zielzeit = zielzeitKandidat.Date == jetzt.Date
            ? zielzeitKandidat
            : new DateTime(jetzt.Year, jetzt.Month, jetzt.Day, 23, 59, 0);

        taskDetail.SetScheduledPromptTime(zielzeit.Hour, zielzeit.Minute);
        taskDetail.SelectPromptTemplate("Weitermachen");

        // Erscheint erst, sobald ScheduledPromptStatus gesetzt ist (NullOrEmptyToVisibilityConverter).
        taskDetail.SendScheduledPrompt();

        Assert.False(new ErrorView(mainWindow).IsVisible);

        taskDetail.ForceClose(recurseToDashboard: false);
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
        projectDetail.Menu.NavigateToDashboard();
    }
}
