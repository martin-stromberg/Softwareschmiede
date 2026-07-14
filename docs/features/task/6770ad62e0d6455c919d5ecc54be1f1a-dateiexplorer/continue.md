# Offene Aufgaben

Erstellt am: 2026-07-14
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (jeweils 9 offene Punkte: Iteration 1 = 0 Plan + 6 Code + 3 Tests, Iteration 2 = 0 Plan + 5 Code + 4 Tests)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. `review.md` bestätigt: Vollständig umgesetzt.

## Code-Review-Befunde

- [ ] TextDiffService.ComputeLineOperations allokiert die volle LCS-DP-Matrix `int[n+1, m+1]` ohne Zeilen-Obergrenze; bei einer ~1-MB-Commit-Vorschau mit ~30.000 Zeilen droht `OutOfMemoryException` (~3,6 GB Allokation). Empfehlung: Zeilenanzahl-Obergrenze vor der O(n·m)-Berechnung prüfen (analog `MaxWorkingTreeNodeCount`) und bei Überschreitung auf Hinweistext/einfachen Blockdiff ausweichen, oder speichereffizienten Diff-Algorithmus (Myers, linearer Speicher) verwenden.
- [ ] FileExplorerViewModel.InitialisierenAsync/AktualisierenAsync setzen `_ausgewaehlterKnoten = null` direkt über das Backing-Field und umgehen dabei den Setter, der `_dateiLadenCts` abbricht — ein noch laufender `DateiLadenAsync`-Vorgang wird nicht abgebrochen und kann den gerade geleerten Zustand mit veraltetem Inhalt überschreiben (z. B. beim Aufgabenwechsel). Fix: laufenden Ladevorgang wie im `AusgewaehlterKnoten`-Setter über `_dateiLadenCts?.Cancel()`/`Dispose()` abbrechen, bevor der Zustand zurückgesetzt wird.
- [ ] FileExplorerViewModel.DateiLadenAsync: der allgemeine `catch (Exception)`-Block protokolliert den Fehler nur und lässt `DateiInhalt`/`DiffZeilen` unverändert — bei fehlgeschlagenem Laden bleibt der Inhalt der vorherigen Datei sichtbar, ohne dass der Nutzer den Fehler erkennt. Fix: im `catch` `DateiInhalt` auf einen Hinweistext setzen und `ClearDiffZeilen()` aufrufen.
- [ ] CommitAufklappenAsync/BranchCommit: `IsLoadingFiles`/`ErrorMessage` werden gesetzt, aber `BranchCommit` implementiert kein `INotifyPropertyChanged` und `FileExplorerView.xaml` bindet diese Felder nicht — Ladefehler bzw. Ladezustand eines Commits bleiben in der WPF-Oberfläche unsichtbar (Commit erscheint einfach leer). Fix: Felder in der WPF-Ansicht binden/darstellen oder bewusst dokumentieren, dass diese Zustände im WPF-Explorer nicht angezeigt werden.
- [ ] TaskDetailViewModel.ShowFileExplorerPanel ruft bei jedem Property-Zugriff synchron `Directory.Exists(_aufgabe.LokalerKlonPfad)` auf dem UI-Thread auf. Fix: Ergebnis einmalig beim Setzen von `Aufgabe`/`LokalerKlonPfad` cachen und das gecachte Feld zurückgeben.

## Fehlgeschlagene Tests

- [ ] Softwareschmiede.Tests.E2E.WpfE2ETests.ProjektErstellen_UndNeueAufgabeAnlegen_E2E — TimeoutException: Element wurde nicht gefunden (25,22 s)
- [ ] Softwareschmiede.Tests.E2E.ProjectDetailE2ETests.NeuanlageAbbrechen_ErstesProjektNochAufrufbar_E2E — TimeoutException: Element wurde nicht gefunden (2:07,20 min)
- [ ] Softwareschmiede.Tests.E2E.E2E_TaskWechselUeberMenue.AufgabeWechselUeberSeitenleiste_ZeigtNeueAufgabeMitEigenerCli_E2E — TimeoutException: Element wurde nicht gefunden (3:32,71 min)
- [ ] Softwareschmiede.Tests.E2E.E2E_CreateNewTaskNavigation.NeueAufgabeErstellenUndSpeichern_ErscheintInListeUndNavigiertZuruec — TimeoutException: Element wurde nicht gefunden (5:37,52 min)

**Hinweis zu den fehlgeschlagenen Tests:** Alle vier sind WPF-E2E-Tests, die auf Element-Timeouts stoßen; keiner betrifft den Dateiexplorer direkt (Projekt-/Aufgaben-Erstellung, Aufgabenwechsel). Die App-Logdatei (`src/Softwareschmiede.App/bin/Debug/net10.0-windows10.0.17763.0/logs/softwareschmiede-20260714.log`) wurde für den gesamten Tageslauf auf Startup-Exceptions geprüft (`Fatal`, `Unhandled`, `XamlParseException`, „MainWindow konnte nicht angezeigt werden") — keine Treffer. Die ansteigenden Timeout-Dauern (25 s → 2 min → 3,5 min → 5,6 min) über den Testlauf hinweg deuten auf eine zunehmende UI-Automatisierungs-Verlangsamung in dieser Sandbox hin (keine interaktive Desktop-Session), nicht auf eine Code-Regression. Vor einer erneuten Bearbeitung sollte dennoch geprüft werden, ob diese Tests auch auf `main` bzw. vor diesem Feature fehlschlagen, um eine Regression sicher auszuschließen.
