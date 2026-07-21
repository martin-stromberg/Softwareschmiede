# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `EntwicklungsprozessService.CreateIssueFileAsync` (private Methode) — erweitert: neuer Parameter `RepositoryStartKonfiguration? startKonfiguration`; effektives Zielverzeichnis über `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(lokalerKlonPfad, startKonfiguration?.WorkingDirectoryRelativePath)` (Zeilen 616–617), `Directory.CreateDirectory` (Zeile 618), `issue.md` in das effektive Verzeichnis geschrieben (Zeilen 620–621). Fehlerbehandlung (Warnung, kein Abbruch) unverändert.
- [x] `EntwicklungsprozessService.UpdateGitignoreAsync` (private Methode) — erweitert: neuer Parameter `RepositoryStartKonfiguration? startKonfiguration`; effektives Zielverzeichnis aufgelöst (Zeilen 638–639), `Directory.CreateDirectory` (Zeile 640), `.gitignore` in diesem Verzeichnis gelesen/geschrieben (Zeilen 642–657). Idempotenz-Prüfung (`Contains("issue.md")`) und Fehlerbehandlung unverändert.
- [x] `EntwicklungsprozessService.FinalizeStartAsync` (private Methode) — erweitert: übergibt `repository.StartKonfiguration` an `CreateIssueFileAsync` (Zeile 502) und `UpdateGitignoreAsync` (Zeile 503). Reihenfolge Startskript → issue.md → .gitignore unverändert.
- [x] Pfad-Auflösung via `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory` (synchrone statische Überladung, ohne `gitPlugin`) — wie in Designentscheidung festgelegt genutzt; kein `DetermineEffectiveWorkingDirectoryAsync`.
- [x] Rückwärtskompatibilität (`StartKonfiguration`/`WorkingDirectoryRelativePath` null → Root) — durch `ResolveEffectiveWorkingDirectory` abgedeckt (leerer/`null` relativePath ⇒ normalisierter Root).
- [x] Test `ProzessStartenAsync_ShouldWriteIssueFileAndGitignoreIntoWorkingDirectory_WhenWorkingDirectoryConfigured` (`EntwicklungsprozessServiceTests`) — deckt Plan-Tests „issue.md in Arbeitsverzeichnis" und „.gitignore in Arbeitsverzeichnis" konsolidiert ab; prüft zusätzlich, dass beide Dateien nicht im Root liegen (Zeilen 926–980).
- [x] Test „issue.md im Root ohne Arbeitsverzeichnis" (Rückwärtskompatibilität) — abgedeckt durch `CreateIssueFileAsync_ShouldCreateIssueFileWithCorrectContent_WhenAufgabeExists` (schreibt/prüft `issue.md` im Klon-Root, Zeilen 763–786).
- [x] Test „.gitignore im Root ohne Arbeitsverzeichnis" (Rückwärtskompatibilität) — abgedeckt durch `UpdateGitignoreAsync_ShouldCreateGitignore_WhenFileDoesNotExist` (Zeilen 861–878).
- [x] Wiederverwendung der Helper `SetupCloneWithDirectoryCreation` und `DeleteDirectoryIfExists` — in allen genannten Tests genutzt; kein neuer Helper angelegt (wie im Plan-Hinweis vorgesehen).
- [x] Neue Klassen — Keine (plankonform).
- [x] Datenbankmigrationen — Keine (plankonform).
- [x] Konfigurationsänderungen — Keine (plankonform).

## Offene Aufgaben

Keine.

## Hinweise

- Die vier im Plan gelisteten Testmethoden wurden gemäß dem ausdrücklichen Plan-Hinweis (und der CLAUDE.md-Regel zur Test-Konsolidierung) zusammengefasst: Ein neuer konsolidierter Test deckt beide Dateien im Arbeitsverzeichnis-Fall ab, während die beiden Root-Fälle durch die bereits bestehenden Tests `CreateIssueFileAsync_ShouldCreateIssueFileWithCorrectContent_WhenAufgabeExists` bzw. `UpdateGitignoreAsync_ShouldCreateGitignore_WhenFileDoesNotExist` abgedeckt sind. Die im Plan geforderte Coverage (mit/ohne Arbeitsverzeichnis × beide Dateien) bleibt vollständig erhalten.
- Verifikation dieses Reviews: statische Prüfung der Quelldatei `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` gegen alle Planelemente sowie erfolgreicher Teil-Build von `src/Softwareschmiede/Softwareschmiede.csproj` (0 Warnungen, 0 Fehler). Der vollständige Testlauf (inkl. der genannten Testmethoden) wurde in diesem Review-Schritt nicht ausgeführt — er ist dem separaten Test-Schritt (Schritt 8b) vorbehalten.
