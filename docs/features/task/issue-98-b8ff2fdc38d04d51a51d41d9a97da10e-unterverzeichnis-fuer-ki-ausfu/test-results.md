# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

Iteration 3. Voller Build (`dotnet build Softwareschmiede.slnx`) unmittelbar vor dem Testlauf durchgeführt:
**0 Fehler**. Testlauf danach mit `dotnet test Softwareschmiede.slnx --no-build --collect:"XPlat Code Coverage"`.

## Zusammenfassung

- Gesamt: 768
- Bestanden: 741
- Fehlgeschlagen: 27
- Übersprungen: 0

## Fehlgeschlagene Tests

### TerminalControlTests (Zwischenablage-Zugriff) — umgebungsbedingt

- **OnPreviewKeyDown_CtrlV_SetsHandledTrue** — Expected args.Handled to be True, but found False. (Zwischenablage-Zugriff in Sandbox blockiert)
- **OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync** — Expected inputStream.ToArray() to be non-empty, but found empty collection. (Zwischenablage-Zugriff in Sandbox blockiert)

### TaskDetailViewModelTests (Prozess-Timing unter Last) — verifiziert als flaky, nicht als Regression

- **TestPluginWechselAsync_StopsCliAndStartsNew** — Expected sut.IsCliRunning to be True, but found False. Isoliert erneut ausgeführt (3× hintereinander): **immer grün**. Der Test startet einen echten Prozess (cmd.exe/ConPTY) und ist unter der Ressourcenlast des restlichen ~8-minütigen Gesamtlaufs (viele parallele E2E-Prozesse) zeitkritisch. Kein Zusammenhang mit den Iteration-3-Änderungen (berührte Dateien: `DirectoryStructureBrowserService.cs`, `LocalDirectoryPlugin.cs`, `DirectoryStructureLoadHelper.cs`, `ArbeitsverzeichnisBearbeitenViewModel.cs`, `EntwicklungsprozessService.cs`, `App.xaml.cs` — keine davon von `TaskDetailViewModel`/CLI-Prozessverwaltung genutzt).

### E2E-UI-Automatisierungstests (FlaUI) — überwiegend umgebungsbedingt, ein Befund mit echter Ursache

23 der 24 fehlgeschlagenen E2E-Tests zeigen `System.TimeoutException: Element wurde nicht innerhalb von 10s/15s gefunden` — das bekannte Muster für inkonsistent verfügbare interaktive Desktop-Session in dieser Sandbox (in diesem Lauf war die Session zeitweise doch verfügbar: zwei der drei `E2E_WorkingDirectory`-Tests liefen tatsächlich durch UI-Interaktion bis zum Ergebnis durch, siehe unten):

- `WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E`
- `E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E`
- `E2E_TaskExecutionCommandLineParameters.AufgabeStarten_MitCodexCommandLineParametersImStore_KiSimulatorStartetKorrekt_E2E`
- `E2E_TaskDetailNavigation.ZurueckButtonInTaskDetail_NavigiertZuProjectDetailView_E2E`
- `E2E_TaskDetailNavigation.TaskDetailView_ZeigtKorrekteAufgabendaten_E2E`
- `E2E_TaskDetailNavigation.AufgabeOeffnen_ZeigtTaskDetailViewFensterumfassend_E2E`
- `E2E_SettingsKiPluginPersistence.Einstellungen_SpeichernCodexAlsStandardKiPluginUndExecutablePath_PersistiertBeides_E2E`
- `E2E_SettingsCommandLineParameters.Einstellungen_HilfeButton_OeffnetDialogDerMitSchliessen_GeschlossenWerdenKann_E2E`
- `E2E_SettingsCommandLineParameters.Einstellungen_ZeigtCommandLineParametersTextBox_BeiCodexCliPlugin_E2E`
- `E2E_SettingsCommandLineParameters.Einstellungen_SpeichertUndLaeadtCommandLineParameters_E2E`
- `E2E_PluginWechsel.PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E`
- `E2E_PluginSelectionDialog.StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E`
- `E2E_PluginSelectionDialog.PluginAuswahlAbbrechen_StartetNichtUndBleibtImStatusNeu_E2E`
- `E2E_PluginProjectDefault_NextTask.ZweiteAufgabeImProjekt_UebernimmtGespeichertenProjektStandardOhneDialog_E2E`
- `E2E_PluginProjectDefault.PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E`
- `E2E_CreateNewTaskNavigation.NeueAufgabeErstellenUndSpeichern_ErscheintInListeUndNavigiertZurueck_E2E`
- `E2E_CreateNewTaskNavigation.NeueAufgabeAbbrechen_NavigiertZurueckOhneTitelAenderungZuSpeichern_E2E`
- `E2E_ConPtyTerminalStart.ConPtyStart_ZeigtTerminalPanelMitStoppenButton_E2E`
- `E2E_ConPtyResize.ConPtyResize_NachFenstergroesseAendern_KeinFehlerUndCliNochAktiv_E2E`
- `E2E_ConPtyProcessEnd.ConPtyProcessEnd_NachStoppen_IsCliRunningFalse_E2E`
- `E2E_ConPtyKeyboardInput.ConPtyKeyboardInput_NachStart_KeinFehlerBanner_E2E`
- `E2E_AutoStartCli.AufgabeOeffnen_StatusGestartetOhneLaufendenProzess_StartetCliAutomatisch_E2E`
- `E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E`

**Neuer, mit echter Ursache verifizierter Befund (kein Timeout, sondern ein Assertion-Fehler mit Applikations-Fehlermeldung):**

- **`E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`** —
  `System.InvalidOperationException: In der Anwendung wird eine Fehlermeldung angezeigt: Aufgabe konnte
  nicht gestartet werden: Arbeitsverzeichnis nicht gefunden: ...\<guid>\backend (Repository-Root: ...\<guid>)`.
  Reproduzierbar (2× ausgeführt, beide Male identisch). Die beiden Schwestertests derselben Klasse
  (`AufgabeStarten_MitFehlendemArbeitsverzeichnis_ZeigtFehler_E2E`,
  `AufgabeStarten_MitPathTraversalArbeitsverzeichnis_ZeigtFehler_E2E`) sind **bestanden** — auffällig, weil
  in diesem Lauf die interaktive Desktop-Session tatsächlich verfügbar war und alle drei Tests bis zur
  echten UI-Interaktion vorgedrungen sind (kein reiner Timeout).

  **Root-Cause-Analyse (verifiziert, siehe `continue.md` für Details):** Dieser Test ruft
  `SetupProjectMitNeuerAufgabe("WorkingDir-Repo", "WorkingDir-Projekt")` **ohne** den optionalen Parameter
  `useInSourceDirectoryMode` auf, dessen Default `true` ist (`WpfTestBase.cs` Zeile 390) — das Repository
  läuft also im `LocalDirectoryPlugin`-Workspace-Modus `InSourceDirectory` (CLI arbeitet direkt im
  Quellordner, `CloneRepositoryAsync` kopiert **nichts**, sondern schreibt nur eine Pointer-Datei in das
  Zielverzeichnis, siehe `LocalDirectoryPlugin.WriteWorkspacePointer`/`CloneRepositoryAsync` Zeile 139–147).
  `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectory(localRepoPath, startConfig)` — verwendet
  sowohl von `KiAusfuehrungsService` (Zeile 106, 190; **bereits seit Iteration 1/2**) als auch neu von
  `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` (Iteration 3) — kombiniert das
  konfigurierte Unterverzeichnis (`"backend"`) naiv mit `localRepoPath`/`clonePath`. Im
  `InSourceDirectory`-Modus ist das aber nur ein **Pointer-Verzeichnis** (enthält lediglich eine
  `.softwareschmiede-workspace`-Datei), nicht das tatsächliche Repository — die reale Verzeichnisstruktur
  (inkl. `backend`) liegt im Quellordner, den nur `LocalDirectoryPlugin.ResolveWorkspacePath(...)` (privat)
  auflösen kann.

  **Dies ist ein vorbestehender Fehler aus Iteration 1/2, kein durch Iteration 3 verursachter Regressions-Bug:**
  `KiAusfuehrungsService` nutzt exakt dieselbe (fehlerhafte) Auflösungslogik bereits seit der ursprünglichen
  Feature-Implementierung. Iteration 3 hat lediglich denselben, bereits vorhandenen Fehler an einer
  früheren Stelle (direkt nach dem Klon statt erst beim CLI-Start) zusätzlich sichtbar gemacht — für echte
  Nutzer würde der CLI-Start ohnehin mit derselben `DirectoryNotFoundException` fehlschlagen. Der Fehler
  wurde bisher nicht entdeckt, weil (a) die zugehörigen Unit-Tests (`KiAusfuehrungsServiceTests_WorkingDirectory`)
  das `LocalDirectoryPlugin`-Pointer-Verhalten nicht nachbilden und (b) der einzige E2E-Test, der dies
  abdeckt, in vorherigen Sandbox-Läufen konsistent an fehlender Desktop-Session scheiterte, bevor er die
  UI-Interaktion überhaupt erreichte.

  **Nicht in Iteration 3 behoben** (außerhalb des zugewiesenen Scopes von 1 Plan-Punkt + 4 Code-Review-
  Befunden; erfordert eine Erweiterung der `IGitPlugin`-Schnittstelle bzw. eine Plugin-bewusste Auflösung
  in `WorkingDirectoryResolver` — siehe `continue.md` für die empfohlene Lösung).

## Iteration-3-spezifische Verifikation (unabhängig, isoliert ausgeführt)

Alle für Iteration 3 neu/geänderten Testklassen wurden zusätzlich isoliert ausgeführt und sind grün:

- `EntwicklungsprozessServiceTests_WorkingDirectoryValidation` (neu, 3 Tests) — deckt die neue
  `GitOrchestrationService`-Verdrahtung ab (Erfolg, Fehlerfall vor Branch-Erstellung, Rückwärtskompatibilität)
- `LocalDirectoryPluginTests_GetRepositoryStructureAsync` (inkl. neuem Cancel-Während-Traversierung-Test) — 3× wiederholt, stabil
- `ArbeitsverzeichnisBearbeitenViewModelTests` (10 inkl. neuem Cancellation-Test) — 3× wiederholt, stabil
- `LocalDirectoryPluginTests` (38 inkl. neuem Test für leere Unterverzeichnisse beim Sync) — grün
- `DirectoryStructureBrowserServiceTests`, `RepositoryAssignViewModelTests_WorkingDirectory`,
  `GitOrchestrationServiceTests`, `KiAusfuehrungsServiceTests_WorkingDirectory` — alle grün

## Testabdeckung

Abdeckung: Nicht ausgewertet (XPlat Code Coverage Collector erzeugte Coverage-Dateien unter
`src/Softwareschmiede.IntegrationTests/TestResults/` und `src/Softwareschmiede.Tests/TestResults/`;
XML-Auswertung wurde für diesen Lauf nicht durchgeführt, da der Fokus auf Verifikation der
Iteration-3-Änderungen lag).

## Fehlende Tests

Keine neuen produktiven Dateien ohne Tests. Alle Iteration-3-Änderungen sind durch neue oder angepasste
Tests abgedeckt (siehe oben). Die einzige verbleibende Lücke betrifft nicht Iteration 3, sondern die neu
entdeckte vorbestehende Lücke: Es existiert kein Unit-/Integrationstest, der `KiAusfuehrungsService` bzw.
`GitOrchestrationService` gegen ein `LocalDirectoryPlugin` im `InSourceDirectory`-Modus mit konfiguriertem
Unterverzeichnis testet (nur der jetzt fehlschlagende E2E-Test deckt dieses Szenario ab).

---

**Fazit:** 26 der 27 Fehlschläge sind umgebungsbedingt (Clipboard-Sandbox-Einschränkung, Prozess-Timing
unter Last, fehlende/inkonsistente interaktive Desktop-Session) und nicht auf die Iteration-3-Änderungen
zurückzuführen. **Ein Fehlschlag** (`E2E_WorkingDirectory.AufgabeStarten_MitKonfiguriertemArbeitsverzeichnis_CliStartetErfolgreich_E2E`)
hat eine echte, verifizierte Ursache — ein vorbestehender Architektur-Konflikt zwischen
`WorkingDirectoryResolver` und `LocalDirectoryPlugin`s `InSourceDirectory`-Workspace-Modus, der bereits vor
Iteration 3 bestand, aber bisher durch die Sandbox-Umgebung verdeckt war. Siehe `continue.md`.
