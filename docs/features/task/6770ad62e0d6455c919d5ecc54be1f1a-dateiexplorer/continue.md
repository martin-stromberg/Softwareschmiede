# Offene Aufgaben

Erstellt am: 2026-07-14
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 1 = 0 Plan + 2 Code + 3 Tests = 5 offene Punkte, Iteration 2 = 0 Plan + 3 Code + 4 Tests = 7 offene Punkte)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Vorgeschichte (bereits erledigt, zur Einordnung)

In den ersten beiden Nacharbeitsrunden dieses Zyklus wurden bereits behoben und unabhängig
verifiziert (Details siehe Git-Historie von `continue.md` bzw. `review-code.1.md`–`review-code.3.md`):
- 5 ursprüngliche Code-Review-Befunde (OOM-Risiko in `TextDiffService`, umgangener CTS-Abbruch in
  `FileExplorerViewModel`, stumme Ladefehler, fehlendes `INotifyPropertyChanged` in `BranchCommit`,
  synchrones `Directory.Exists` in `TaskDetailViewModel.ShowFileExplorerPanel`).
- 1 zuvor fehlgeschlagener E2E-Test (`ProjectDetailE2ETests.NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E`).
- Unbenutzte Snapshot-Kategorisierung (`WorkspaceSnapshot.RootNodes/FlatFiles/CodeFiles/PlanningDocuments/ChangedFileCount`
  und zugehörige `GitWorkspaceBrowserService`-Hilfsmethoden) vollständig entfernt, nachdem bestätigt wurde,
  dass weder `plan.md`/`requirement.md` sie vorsehen noch die WPF-UI sie konsumiert.

## Offene Planelemente

Keine. `review.md` bestätigt weiterhin: Vollständig umgesetzt.

## Code-Review-Befunde

- [ ] `GitWorkspaceBrowserService.IncrementAncestorCounts` (aufgerufen aus `BuildCommitFileTree`) pflegt pro Verzeichnisknoten `WorkspaceFileNode.ChangedFileCount`. Dieser Wert wird von keinem kompilierten Konsumenten gelesen — die WPF-`TreeView`-Templates in `FileExplorerView.xaml` zeigen keinen Änderungszähler an; die einzigen Lesezugriffe liegen in den nicht kompilierten Blazor-Tests (`<Compile Remove="Components\**\*.cs" />`). Fix: `IncrementAncestorCounts` und den Aufruf in `BuildCommitFileTree` entfernen; die dann schreib-nur genutzte Eigenschaft `WorkspaceFileNode.ChangedFileCount` (`src/Softwareschmiede/Domain/ValueObjects/WorkspaceFileNode.cs`) ebenfalls entfernen.
- [ ] `FileTextDiff.AddedCount`/`RemovedCount`/`ModifiedCount` sowie die zugehörige Zählerlogik in `TextDiffService.BuildDiff` werden von der Anwendung nicht konsumiert (`FileExplorerViewModel.DateiLadenAsync` nutzt nur `diff.Lines`); nur `TextDiffServiceTests` wertet die drei Zähler aus. Fix (niedrige Priorität): entweder bewusst als getesteten Bestandteil des Value Objects dokumentieren/belassen, oder konsistent zur bisherigen Aufräumzielsetzung entfernen (Zähler in `FileTextDiff`, Zählerlogik in `BuildDiff`, zugehörige Test-Assertions).
- [ ] (Beobachtung, nicht blockierend) `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/*` (`AufgabeDetailWorkspacePreviewBunitTests.cs`, `AufgabeDetailFolgePromptTests.cs`, `AufgabeDetailGitActionsBunitTests.cs`) ist verwaister Blazor-bUnit-Testcode einer nicht mehr existierenden Komponente `AufgabeDetail`; durch `<Compile Remove="Components\**\*.cs" />` vom Build ausgeschlossen, daher kein Fehler. Optionaler, vom Dateiexplorer-Feature unabhängiger Aufräumschritt: Verzeichnis `src/Softwareschmiede.Tests/Components/` löschen.

## Fehlgeschlagene Tests

- [ ] Softwareschmiede.Tests.E2E.WpfE2ETests.ProjektErstellen_ZeigtAufgabenListe_E2E — TimeoutException: Element wurde nicht innerhalb von 20s gefunden.
- [ ] Softwareschmiede.Tests.E2E.WpfE2ETests.EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E — TimeoutException: Element wurde nicht innerhalb von 20s gefunden.
- [ ] Softwareschmiede.Tests.E2E.E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E — TimeoutException: Element wurde nicht innerhalb von 15s gefunden.
- [ ] Softwareschmiede.Tests.E2E.E2E_CreateNewTaskNavigation.NeueAufgabeErstellenUndSpeichern_ErscheintInListeUndNavigiertZurueck_E2E — TimeoutException: Element wurde nicht innerhalb von 20s gefunden.

**Hinweis zu den fehlgeschlagenen Tests (wichtig für die weitere Bearbeitung):** Über die drei bisherigen
vollen Testläufe dieses Zyklus hinweg wechselte die Menge der fehlschlagenden E2E-Tests:
- Lauf 1 (vor allen Fixes, isolierte Einzel-Reruns): 4 Tests fehlgeschlagen, davon 2 nach den ersten 5 Fixes wieder grün.
- Lauf 2 (voller Testlauf nach den ersten 5 Fixes, unabhängig vom Orchestrator verifiziert): 929 Tests,
  3 fehlgeschlagen — `WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E`, `E2E_TaskWechselUeberMenue...`,
  `E2E_CreateNewTaskNavigation...`.
- Lauf 3 (voller Testlauf nach der Snapshot-Bereinigung): 922 Tests (7 weniger, da dabei 7 nur die entfernte
  Kategorisierung prüfende Tests gelöscht wurden), 4 fehlgeschlagen — `WpfE2ETests.ProjektErstellen_ZeigtAufgabenListe_E2E`
  (neu), `WpfE2ETests.EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E` (neu), `E2E_TaskWechselUeberMenue...`
  (erneut), `E2E_CreateNewTaskNavigation...` (erneut). `ProjektErstellen_UndNeueAufgabeAnlegen_E2E` aus Lauf 2
  bestand in Lauf 3 wieder.

Nur `E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E` und
`E2E_CreateNewTaskNavigation.NeueAufgabeErstellenUndSpeichern_ErscheintInListeUndNavigiertZurueck_E2E` schlugen in
**beiden** vollen Testläufen (2 und 3) fehl; alle anderen betroffenen Tests wechselten zwischen den Läufen. Keiner
der in irgendeinem Lauf fehlgeschlagenen Tests berührt Dateiexplorer-Code (`FileExplorerViewModel`,
`TaskDetailViewModel.ShowFileExplorerPanel`, `BranchCommit`, `TextDiffService`, `GitWorkspaceBrowserService`) — das
wurde durch Lesen des jeweiligen Testcodes und seiner privaten Hilfsmethoden mehrfach bestätigt. Die App-Logdatei
wurde nach jedem Lauf auf `[FTL]`/`Fatal`/`Unhandled`/`XamlParseException` geprüft — keine Treffer, die App startet
und beendet sich jedes Mal sauber. Dieses inkonsistente, nicht auf denselben Testfall festgelegte Fehlerbild über
mehrere unabhängige volle Testläufe hinweg ist ein starkes Indiz für last-/timingabhängige UI-Automatisierungs-
Flakiness dieser Sandbox (keine interaktive Desktop-Session), nicht für eine Code-Regression durch dieses Feature.
Vor einer erneuten automatisierten Bearbeitung sollte geprüft werden, ob dieselben Tests auch auf `main` bzw. vor
diesem Feature-Branch fehlschlagen, um eine Regression endgültig auszuschließen — das würde idealerweise ein Mensch
mit Zugriff auf eine interaktive Desktop-Session verifizieren, da die Flakiness-Hypothese in dieser Sandbox nicht
abschließend beweisbar ist.
