# Offene Aufgaben

Erstellt am: 2026-06-26
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 1: 23 offene Punkte, Iteration 2: 24 offene Punkte)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — Plan ist vollständig umgesetzt (review.md: Vollständig umgesetzt).

## Code-Review-Befunde

- [ ] **[CONFIRMED]** Migration lässt zwei Altdaten-Klassen unangetastet: `PluginTyp='SourceCodeManagement'` mit On-Premises-URLs (kein github.com/bitbucket.org) und `PluginTyp='GitHub'` (Blazor-UI-Legacy) finden keinen Plugin-Prefix-Treffer → Issues dauerhaft leer. Zwei zusätzliche UPDATE-Statements benötigt in `20260626204930_202606260001_MigrateGitRepositoryPluginTyp.cs` (Z. 15).
- [ ] **[CONFIRMED]** `SelectedRepository`-Setter in `ProjectDetailViewModel.cs` (Z. 114) benachrichtigt nur über `"SelectedRepository"`, nie über `"KannIssuesLaden"` → XAML-Binding für Issue-Panel aktualisiert sich nie.
- [ ] **[CONFIRMED]** `IssueZuweisenAsync` in `TaskDetailViewModel.cs` (Z. 511) wählt blind das erste SCM-Plugin statt nach `_aufgabe.GitRepository.PluginTyp` zu filtern → falscher SCM-Host in Multi-Plugin-Umgebung.
- [ ] **[CONFIRMED]** `KannIssuesLaden` in `ProjectDetailViewModel.cs` (Z. 149) hat keinen Cache und keine `PropertyChanged`-Benachrichtigung → doppelt defekt (Performance + UI-Binding).
- [ ] **[CONFIRMED]** `ValidateRequiredFields` in `ProjektService.cs` (Z. 376) kennt nur LocalDirectory und GitHub; `Softwareschmiede.Bitbucket` fällt durch, `RepositoryName` wird für Bitbucket nicht geprüft.
- [ ] **[Vereinfachung]** Überflüssiges `.OfType<IGitPlugin>()` in `ProjectDetailViewModel.cs` (Z. 472) entfernen — `GetSourceCodeManagementPlugins()` gibt bereits `IReadOnlyList<IGitPlugin>` zurück.

## Fehlgeschlagene Tests

- [ ] TaskDetailViewModelTests.LoeschenCommand_CanExecuteFalse_WennStatusBeendet — Expected False, but found True
- [ ] TaskDetailViewModelTests.KannLoeschen_IsTrue_WhenStatusGestartet — Expected True, but found False
- [ ] TaskDetailViewModelTests.KannLoeschen_IsFalse_WhenStatusArchiviert — Expected False, but found True
- [ ] TaskDetailViewModelTests.KannLoeschen_IsFalse_WennStatusBeendet — Expected False, but found True
- [ ] WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E — TimeoutException: Element nicht gefunden (10s)
- [ ] WpfE2ETests.DarkModeAktivierenUndPersistieren_E2E — TimeoutException: Element nicht gefunden (5s)
- [ ] WpfE2ETests.AufgabeAnlegen_ZeigtStartenButton_E2E — TimeoutException: Element nicht gefunden (10s)
- [ ] WpfE2ETests.EinstellungenNavigation_BleibtNachMehrerenKlicks_Stabil_E2E — TimeoutException: Element nicht gefunden (10s)
- [ ] WpfE2ETests.ProjektErstellen_ZeigtAufgabenListe_E2E — TimeoutException: Element nicht gefunden (10s)
- [ ] WpfE2ETests.EinstellungenArbeitsverzeichnis_Aendern_UndSpeichern_E2E — Could not find process
- [ ] ProjectDetailE2ETests.ProjektOeffnenUndZurueck_ErneutOeffnen_E2E — Could not find process
- [ ] ProjectDetailE2ETests.RepositoryZuweisenDialog_ScmPluginListe_EnthaeltErwartetePlugins_E2E — Could not find process
- [ ] ProjectDetailE2ETests.ProjektNamenAendern_KachelAktualisiert_UndErneutoeffnen_E2E — Could not find process
- [ ] ProjectDetailE2ETests.AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E — TimeoutException: Element nicht gefunden (10s)
- [ ] ProjectDetailE2ETests.ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E — Could not find process
- [ ] ProjectDetailE2ETests.AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E — Could not find process
- [ ] ProjectDetailE2ETests.ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E — TimeoutException: Element nicht gefunden (10s)
- [ ] ProjectDetailE2ETests.NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E — TimeoutException: Element nicht gefunden (10s)
