# Bestandsaufnahme: issue.md im Arbeitsverzeichnis

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezüglich der Anforderung, die `issue.md`-Datei in das konfigurierte Arbeitsverzeichnis (`WorkingDirectoryRelativePath`) statt in den Repository-Root zu schreiben.

## Zusammenfassung

### Vorhanden
- **Datenmodell:** Die `RepositoryStartKonfiguration` enthält bereits das Feld `WorkingDirectoryRelativePath` (nullable string, persistiert via Migration `20260708181234`).
- **Validierung nach Klon:** `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` validiert nach dem Klon, dass das konfigurierte Arbeitsverzeichnis existiert (frühe Fehler-Erkennung).
- **Arbeitsverzeichnis-Übergabe an KI-Plugin:** Der `EntwicklungsprozessService` übergibt das aufgelöste Arbeitsverzeichnis bereits an `KiAusfuehrungsService.StartWithPseudoConsoleAsync` (Zeile 162).
- **Testabdeckung für Arbeitsverzeichnis-Validierung:** Zwei dedizierte Testklassen (`EntwicklungsprozessServiceTests_WorkingDirectoryValidation`, relevante Tests in `EntwicklungsprozessServiceTests`) prüfen die Arbeitsverzeichnis-Konfiguration und -Validierung.

### Fehlt / Zu implementieren
- **`issue.md`-Pfad ist hardcodiert:** `CreateIssueFileAsync` schreibt `issue.md` direkt in `lokalerKlonPfad` (Repository-Root), ohne `StartKonfiguration` zu berücksichtigen.
- **`.gitignore`-Pfad ist hardcodiert:** `UpdateGitignoreAsync` schreibt `.gitignore` direkt in `lokalerKlonPfad`, ohne das Arbeitsverzeichnis zu beachten.
- **Keine Verzeichnis-Erstellung:** Wenn das Arbeitsverzeichnis noch nicht existiert, kann die Datei nicht geschrieben werden (sollte mit `Directory.CreateDirectory` abgefangen werden).
- **Signatur-Änderungen erforderlich:** Beide Methoden müssen `RepositoryStartKonfiguration` als Parameter erhalten, um den effektiven Pfad zu berechnen.
- **Tests für `issue.md` / `.gitignore` im Arbeitsverzeichnis:** Keine dedizierten Test-Cases für die Pfad-Anpassung dieser Dateien.

### Abhängigkeiten zwischen den Komponenten
- `EntwicklungsprozessService.FinalizeStartAsync` empfängt `repository: GitRepository` mit `StartKonfiguration`, gibt diese aber nicht an `CreateIssueFileAsync` und `UpdateGitignoreAsync` weiter.
- `EntwicklungsprozessService.ProzessStartenAsync` ruft `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` (mit `StartKonfiguration`) auf, bevor `FinalizeStartAsync` aufgerufen wird.
- `WorkingDirectoryResolver` (in `GitOrchestrationService`) bietet bereits die Logik zur Auflösung des effektiven Arbeitsverzeichnis-Pfads.

---

## Details

- [Datenmodelle](inventory/models.md) — `GitRepository`, `RepositoryStartKonfiguration`
- [Logikklassen](inventory/logic.md) — `EntwicklungsprozessService`, `GitOrchestrationService` (mit Fokus auf `CreateIssueFileAsync`, `UpdateGitignoreAsync`, `FinalizeStartAsync`, `ValidateWorkingDirectoryAfterCloneAsync`)
- [Interfaces](inventory/interfaces.md) — `IArbeitsverzeichnisResolver`
- [Tests](inventory/tests.md) — `EntwicklungsprozessServiceTests`, `EntwicklungsprozessServiceTests_WorkingDirectoryValidation`, Hilfsmethoden und fehlende Testabdeckung

---

## Wichtige Erkenntnisse

1. **Arbeitsverzeichnis ist konfigurierbar:** Der Benutzer kann über einen Dialog (erwähnt in Anforderung als "Arbeitsverzeichnis"-Dialog) das Arbeitsverzeichnis setzen; die Validierung beim Speichern in `RepositoryStartKonfiguration` prüft auf Sicherheit (keine absoluten Pfade, keine `..`-Segmente).

2. **Validierung erfolgt früh:** Direkt nach dem Klon (in `ProzessStartenAsync`, vor `FinalizeStartAsync`) wird via `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` geprüft, dass das Arbeitsverzeichnis existiert. Dies gewährleistet ein frühes, klares Fehlerbild.

3. **Path.Combine ist sicher:** .NET normalisiert Pfade und ist gegen `..`-Traversal geschützt, solange die Eingabe validiert wurde (was beim Speichern in `RepositoryStartKonfiguration` geschieht).

4. **Arbeitsverzeichnis-Auflösung existiert:** `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync` wird bereits von `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` verwendet; diese Logik kann ggf. wiederverwendet werden.

5. **Rückwärtskompatibilität:** Wenn `StartKonfiguration` null ist oder `WorkingDirectoryRelativePath` null / `"."` ist, soll weiterhin der Repository-Root verwendet werden (wie derzeit).

---

## Schnittschellen zum Refactoring

- **`EntwicklungsprozessService.CreateIssueFileAsync`:** Parameter erweitern: `RepositoryStartKonfiguration? startKonfiguration` hinzufügen.
- **`EntwicklungsprozessService.UpdateGitignoreAsync`:** Parameter erweitern: `RepositoryStartKonfiguration? startKonfiguration` hinzufügen.
- **`EntwicklungsprozessService.FinalizeStartAsync`:** `CreateIssueFileAsync` und `UpdateGitignoreAsync` mit `repository.StartKonfiguration` aufrufen.
- **Fehlerbehandlung:** Verzeichnis-Erstellung vor Datei-Schreiben mit `Directory.CreateDirectory(effektivesPfad)`.
