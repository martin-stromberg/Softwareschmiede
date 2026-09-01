using FlaUI.Core.AutomationElements;
using Softwareschmiede.Tests.E2E.Views;

namespace Softwareschmiede.Tests.E2E;

/// <summary>
/// E2E-Tests für die Anzeige des Plugin-Auswahl-Dialogs beim Starten einer Aufgabe ohne
/// gespeichertes KI-Plugin sowie für den Plugin-Wechsel bei laufender CLI (Feature 72).
///
/// Voraussetzungen:
/// - Windows-Desktop-Session (kein Headless-CI)
/// - Softwareschmiede.App muss im Debug-Modus gebaut sein
/// - Im Test-Modus steht ausschließlich das LocalDirectoryPlugin als SCM-Plugin zur Verfügung;
///   als KI-Plugins sind u.a. Softwareschmiede.KiSimulator und Softwareschmiede.ClaudeCli verfügbar.
///
/// Konsolidierung (Issue #153): Die "OK"-Phase des Auswahl-Dialogs endet bereits mit laufender CLI
/// (Softwareschmiede.KiSimulator) - genau die Vorbedingung, die der Plugin-Wechsel-Test benötigt.
/// Beide laufen daher als Phasen an derselben Aufgabe in einem gemeinsamen App-Lifecycle statt zwei
/// eigenständiger App-Starts.
///
/// CI-Regular-Lauf: dotnet test --filter "Category!=OsInterface"
/// </summary>
public partial class End2EndTest
{
    /// <summary>
    /// Führt die Plugin-Auswahl (Abbrechen, dann OK) und den anschließenden Plugin-Wechsel bei
    /// laufender CLI als zwei Phasen an derselben Aufgabe aus.
    /// </summary>
    /// <param name="mainWindow">Das bereits laufende Hauptfenster, in dem beide Phasen ausgeführt werden.</param>
    protected void PluginAuswahlAbbrechenOkUndWechsel_E2E(Window mainWindow)
    {
        ConfirmLocalDirectoryGitInitInSourceDirectory();

        SetupProjectMitNeuerAufgabe(mainWindow, "PluginDialog-Repo", "PluginDialog-Projekt");

        var taskDetail = PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E(mainWindow);
        PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E(taskDetail);

        taskDetail.GoBack();
        var projectDetail = Assert.IsType<ProjectDetailView>(mainWindow.CurrentView());
        projectDetail.DeleteProject();
    }

    /// <summary>
    /// Szenario: Beim ersten Start einer Aufgabe ohne gespeichertes Plugin (kein Aufgaben-,
    /// Projekt- oder globaler Default) wird der Plugin-Auswahl-Dialog angezeigt. Zunächst wird die
    /// Auswahl über "Abbrechen" verworfen (Phase Abbrechen); anschließend wird derselbe Start erneut
    /// versucht, ein Plugin ausgewählt und mit "OK" bestätigt (Phase OK).
    /// Prüft: Im Abbrechen-Pfad wird der Start-Ablauf nicht fortgesetzt (Aufgabe bleibt im Status
    /// "Neu", Edit-Panel weiterhin sichtbar). Im OK-Pfad wird nach Auswahl und Bestätigung der
    /// kombinierte Start-Ablauf fortgesetzt (CLI startet).
    /// </summary>
    /// <param name="mainWindow">Das Hauptfenster mit der neu angelegten Aufgabe im Edit-Panel.</param>
    /// <returns>Die Aufgabendetailansicht mit laufender CLI (Softwareschmiede.KiSimulator).</returns>
    private TaskDetailView PluginAuswahl_AbbrechenBleibtNeu_UndOkStartetCli_E2E(Window mainWindow)
    {
        // Phase Abbrechen
        var taskDetail = new TaskDetailView(mainWindow);
        var abbrechenDialog = new Views.Dialogs.PluginSelectionDialogView(mainWindow).ForceShow();
        abbrechenDialog.Cancel();

        // Edit-Panel weiterhin sichtbar (Status nach wie vor "Neu")
        Assert.True(taskDetail.IsVisible);
        Assert.False(taskDetail.IsCliRunning());

        // Phase OK
        taskDetail.Start("Softwareschmiede.KiSimulator", fuerProjektVerwenden: false);

        // Nach Bestätigung: kombinierter Start-Ablauf läuft weiter, CLI startet
        taskDetail.WaitForCliRunning();

        return taskDetail;
    }

    /// <summary>
    /// Szenario: Über "Plugin ändern" wird im Dialog ein anderes Plugin gewählt, während die CLI
    /// bereits läuft. Prüft: Der laufende CLI-Prozess wird gestoppt und mit dem neuen Plugin neu
    /// gestartet (CLI-Panel bleibt während des gesamten Wechsels sichtbar, Status bleibt "Gestartet") -
    /// Regressionstest für die Korrektur des Arbeitsablaufs: Während des kurzzeitigen Zwischenstands
    /// (AusfuehrungsStatus wechselt auf "Beendet", bevor der neue Prozess "Aktiv" setzt) darf das
    /// CLI-Panel (CliViewButton, gebunden an ShowCliPanel) nicht verschwinden.
    /// </summary>
    /// <param name="taskDetail">Die Aufgabendetailansicht mit bereits laufender CLI (Softwareschmiede.KiSimulator).</param>
    private void PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E(TaskDetailView taskDetail)
    {
        // Kurze Stabilisierungspause nach Schließen des vorherigen Dialogs, damit die UIA-Elemente
        // wieder einen gültigen Klickpunkt liefern (sonst NoClickablePointException möglich).
        Thread.Sleep(500);

        // Vor dem Wechsel: CLI-Panel sichtbar (ShowCliPanel==true, AusfuehrungsStatus==Aktiv)
        Assert.True(taskDetail.HasCliPanel());

        // Plugin ändern: Dialog mit aktuellem Plugin vorselektiert anzeigen
        var wechselDialog = taskDetail.OpenPluginChangeDialog();
        wechselDialog.SelectPlugin("Softwareschmiede.ClaudeCli");
        wechselDialog.Confirm();

        // Nach dem Wechsel: alter Prozess gestoppt, neuer CLI-Prozess läuft (Stoppen-Button weiterhin sichtbar)
        taskDetail.WaitForCliRunning();

        // CLI-Panel bleibt während des gesamten Wechsels sichtbar (kein Verschwinden im Zwischenstand)
        Assert.True(taskDetail.HasCliPanel());

        Assert.False(new ErrorView(taskDetail.Window).IsVisible);
    }
}
