# Offene Aufgaben

Erstellt am: 2026-06-30
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — alle Planelemente sind vollständig umgesetzt.

## Code-Review-Befunde

### EntwicklungsprozessService.cs

- [ ] **Long Parameter List** — Vollständiger Konstruktor nimmt 9 Parameter an. Optionale Abhängigkeiten (`ProjektService?`, `RepositoryStartskriptService?`, `KiAusfuehrungsService?`) in ein Parameter-Objekt (`EntwicklungsprozessServiceOptions`) auslagern.
- [ ] **Temporäre Felder** — `_projektService`, `_repositoryStartskriptService`, `_kiAusfuehrungsService` sind in den meisten Konstruktorvarianten null. Durch dedizierte Methoden oder separate Klassen für die jeweiligen Szenarien ersetzen.
- [ ] **God-Methode** — `ProzessStartenAsync` (~100 Zeilen, 9+ Aufgaben) in kleinere private Hilfsmethoden aufteilen: `ResolvePluginAsync`, `PrepareCloneDirectoryAsync`, `SetupBranchAsync`, `FinalizeStartAsync`.

### EntwicklungsprozessServiceTests.cs

- [ ] **Doppelter Setup-Code** — Mock-Setup für `CloneRepositoryAsync`/`CreateBranchAsync` in mehreren Tests inline wiederholt, obwohl `SetupCloneWithDirectoryCreation` existiert. Überladung ohne Verzeichniserstellung ergänzen und konsequent einsetzen.
- [ ] **Doppelter Cleanup-Code** — `try/finally`-Löschmuster für Klon-Verzeichnisse in mind. 8 Testmethoden dupliziert. Private Hilfsmethode `DeleteDirectoryIfExists(string path)` extrahieren.
- [ ] **Test prüft mehrere Fälle** — `ProzessStartenAsync_ShouldCloneAndCreateBranch_WhenAufgabeExists` verifiziert 6 separate Aspekte. Auf Kernaspekt (Klon + Branch) beschränken; issue.md- und .gitignore-Prüfungen in dedizierte Tests auslagern.
- [ ] **Test prüft mehrere Fälle** — `ProzessStartenAsync_ShouldCreateIssueFileAndUpdateGitignore_WhenCloneSucceeds` prüft beide Aspekte gleichzeitig, obwohl Einzeltests die Abdeckung sicherstellen. Entfernen oder auf einen Aspekt beschränken.
- [ ] **Inkonsistente Benennung** — 3 Testmethoden weichen vom Muster `Methode_ShouldXxx_WhenXxx` ab: `ProzessStartenUndCliStartenAsync_Success`, `ProzessStartenUndCliStartenAsync_RepositoryCloneFails_RollbackStatus`, `ProzessStartenUndCliStartenAsync_CliStartFails_RollbackStatus`.

## Fehlgeschlagene Tests

Alle fehlgeschlagenen Tests sind E2E-UI-Automationstests mit pre-existing TimeoutExceptions (hostpolicy.dll-Problem in der CI-Umgebung). Sie sind nicht durch unsere Änderungen verursacht.

- [ ] ProjectDetailE2ETests.RepositoryZuweisen_DialogOeffnetUndSchliessbarPerAbbrechen_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] ProjectDetailE2ETests.RepositoryOeffnen_ButtonExistiertInDetailansicht_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] ProjectDetailE2ETests.ZurueckZurUebersicht_SchliesstOverlayUndZeigtListe_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_TaskDetailNavigation.ZurueckButtonInTaskDetail_NavigiertZuProjectDetailView_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_TaskDetailNavigation.TaskDetailView_ZeigtKorrekteAufgabendaten_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_TaskDetailNavigation.AufgabeOeffnen_ZeigtTaskDetailViewFensterumfassend_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_SettingsCommandLineParameters.Einstellungen_HilfeButton_OeffnetDialogDerMitSchliessen_GeschlossenWerdenKann_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_SettingsCommandLineParameters.Einstellungen_ZeigtCommandLineParametersTextBox_BeiCodexCliPlugin_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_SettingsCommandLineParameters.Einstellungen_SpeichertUndLaeadtCommandLineParameters_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_PluginSelectionDialog.PluginSelectionDialog_OeffnetUndSchliesstProperly_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_PluginSelectionDialog.PluginSelectionDialog_TextboxChangesKiPlugin_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_CreateNewTaskNavigation.CreateNewTaskButton_NavigiertZuCreateTaskView_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_CreateNewTaskNavigation.CreateTaskView_Abbruch_NavigiertZurueck_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_TaskExecutionCommandLineParameters.AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_SettingsKiPluginPersistence.Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_PersistiertBeides_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_PluginWechsel.PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_PluginProjectDefault.PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_PluginProjectDefault_NextTask.ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_ConPtyKeyboardInput.ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_ConPtyProcessEnd.ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_ConPtyResize.ConPtyResize_NachStart_KorrekteBreiteUndHoehe_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_ConPtyTerminalStart.ConPtyTerminalStart_NachStart_PromptSichtbar_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_AufgabeStarten.AufgabeStarten_MitStandardPlugin_KiSimulatorStartetKorrekt_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] E2E_AutoStartCli.AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
- [ ] WpfE2ETests.MainWindowLoading_ShouldLoad_E2E — System.TimeoutException: Element wurde nicht innerhalb von 10s gefunden.
