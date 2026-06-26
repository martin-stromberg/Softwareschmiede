# Offene Aufgaben

Erstellt am: 2026-06-26
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 1: 23 offene Punkte, Iteration 2: 24 offene Punkte)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — Plan ist vollständig umgesetzt (review.md: Vollständig umgesetzt).

## Code-Review-Befunde

- [x] **[CONFIRMED]** `SelectedRepository`-Setter in `ProjectDetailViewModel.cs` benachrichtigt nie über `"KannIssuesLaden"` → BEHOBEN: Backing-Feld + `AktualisiereKannIssuesLaden()` + PropertyChanged
- [x] **[CONFIRMED]** `IssueZuweisenAsync` in `TaskDetailViewModel.cs` wählt blind erstes SCM-Plugin → BEHOBEN: Plugin per `PluginTyp` gefiltert
- [x] **[CONFIRMED]** `KannIssuesLaden` ohne Cache und ohne `PropertyChanged` → BEHOBEN: gecachtes Backing-Feld
- [x] **[CONFIRMED]** `ValidateRequiredFields` ohne Bitbucket-Validierung → BEHOBEN: `IsBitbucketPlugin()`-Zweig ergänzt
- [x] **[Vereinfachung]** Überflüssiges `.OfType<IGitPlugin>()` → BEHOBEN in `TaskDetailViewModel.cs` (in `ProjectDetailViewModel.cs` bereits zuvor entfernt)

## Fehlgeschlagene Tests

- [x] TaskDetailViewModelTests.LoeschenCommand_CanExecuteFalse_WennStatusBeendet — BEHOBEN: KannLoeschen-Logik auf Beendet/Archiviert korrigiert
- [x] TaskDetailViewModelTests.KannLoeschen_IsTrue_WhenStatusGestartet — BEHOBEN
- [x] TaskDetailViewModelTests.KannLoeschen_IsFalse_WhenStatusArchiviert — BEHOBEN
- [x] TaskDetailViewModelTests.KannLoeschen_IsFalse_WennStatusBeendet — BEHOBEN
- [ ] WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E — TimeoutException: Element nicht gefunden (Umgebungsproblem)
- [ ] WpfE2ETests.DarkModeAktivierenUndPersistieren_E2E — TimeoutException: Element nicht gefunden (Umgebungsproblem)
- [ ] WpfE2ETests.AufgabeAnlegen_ZeigtStartenButton_E2E — TimeoutException: Element nicht gefunden (Umgebungsproblem)
- [ ] WpfE2ETests.EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E — TimeoutException: Element nicht gefunden (Umgebungsproblem)
- [ ] WpfE2ETests.ProjektErstellen_ZeigtAufgabenListe_E2E — TimeoutException: Element nicht gefunden (Umgebungsproblem)
- [ ] WpfE2ETests.EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E — Could not find process (Umgebungsproblem)
- [ ] ProjectDetailE2ETests.ProjektOeffnenUndZurueck_ErneutOeffnen_E2E — Could not find process (Umgebungsproblem)
- [ ] ProjectDetailE2ETests.RepositoryZuweisenDialog_ScmPluginListe_EnthaeltErwartetePlugins_E2E — Could not find process (Umgebungsproblem)
- [ ] ProjectDetailE2ETests.ProjektNamenAendern_KachelAktualisiert_UndErneutoeffnen_E2E — Could not find process (Umgebungsproblem)
- [ ] ProjectDetailE2ETests.AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E — TimeoutException: Element nicht gefunden (Umgebungsproblem)
- [ ] ProjectDetailE2ETests.ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E — Could not find process (Umgebungsproblem)
- [ ] ProjectDetailE2ETests.AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E — Could not find process (Umgebungsproblem)
- [ ] ProjectDetailE2ETests.ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E — TimeoutException: Element nicht gefunden (Umgebungsproblem)
- [ ] ProjectDetailE2ETests.NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E — TimeoutException: Element nicht gefunden (Umgebungsproblem)
