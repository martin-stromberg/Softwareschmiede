# Anforderung

## Fachliche Zusammenfassung

Derzeit wird die `issue.md`-Datei mit der Aufgabenbeschreibung immer im Root des geklonten Repositories erstellt. Dies soll erweitert werden, um das konfigurierte Arbeitsverzeichnis (`WorkingDirectoryRelativePath`) zu berücksichtigen: Wenn für ein Repository ein Unterordner als Arbeitsverzeichnis angegeben ist, soll die `issue.md` auch in diesem Arbeitsverzeichnis statt im Repository-Root erstellt werden. Dies ermöglicht es dem KI-Agenten und dem Entwickler, die Aufgabenbeschreibung direkt im relevanten Arbeitsverzeichnis zu finden, ohne zwischen der Root-Ebene und dem konfigurierten Arbeitsverzeichnis navigieren zu müssen.

## Betroffene Klassen und Komponenten

- **Datenmodellklassen:**
  - `GitRepository` – enthält bereits die `StartKonfiguration` mit `WorkingDirectoryRelativePath`
  - `RepositoryStartKonfiguration` – enthält das Feld `WorkingDirectoryRelativePath`

- **Logikklassen / Services:**
  - `EntwicklungsprozessService.CreateIssueFileAsync` – bestehende Methode (Zeile 596–628), die derzeit `issue.md` im `lokalerKlonPfad` Root erstellt
  - `EntwicklungsprozessService.FinalizeStartAsync` – ruft `CreateIssueFileAsync` auf, müsste die Repository-Konfiguration weitergeben

- **Interfaces:**
  - `IArbeitsverzeichnisResolver` – möglicherweise wiederverwendbar zur Pfad-Auflösung (optional, siehe Implementierungsansatz)

- **Tests:**
  - `EntwicklungsprozessServiceTests` – Tests der `CreateIssueFileAsync`-Methode müssen erweitert werden, um den Fall mit und ohne Arbeitsverzeichnis abzudecken

## Implementierungsansatz

### Änderungen in `EntwicklungsprozessService`

1. **Methode `CreateIssueFileAsync` anpassen:**
   - Zusätzlicher Parameter: `RepositoryStartKonfiguration? startKonfiguration` (oder die vollständige `GitRepository`)
   - Im Körper: Bestimmung des effektiven Zielverzeichnisses:
     - Wenn `startKonfiguration?.WorkingDirectoryRelativePath` gesetzt und nicht `"."` ist:
       - Effektiver Pfad = `Path.Combine(lokalerKlonPfad, startKonfiguration.WorkingDirectoryRelativePath)`
     - Andernfalls: Effektiver Pfad = `lokalerKlonPfad` (bisheriges Verhalten)
   - Vor dem Schreiben der Datei: Zielverzeichnis mit `Directory.CreateDirectory` sicherstellen (für den Fall, dass das Arbeitsverzeichnis noch nicht existiert)
   - Schreiben der Datei in das effektive Zielverzeichnis: `Path.Combine(effektivesPfad, "issue.md")`

2. **Methode `FinalizeStartAsync` anpassen:**
   - Übergabe der `repository.StartKonfiguration` (oder `repository`) an `CreateIssueFileAsync`

3. **Gleiches Prinzip für `UpdateGitignoreAsync`:**
   - Analog zu `CreateIssueFileAsync`: Die `.gitignore`-Datei sollte ebenfalls im Arbeitsverzeichnis aktualisiert werden, wenn konfiguriert
   - Dies gewährleistet kohärentes Verhalten: `issue.md` und ihr `.gitignore`-Eintrag befinden sich in demselben Verzeichnis

### Path-Traversal-Sicherheit

- Das Arbeitsverzeichnis wird bereits beim Speichern in `RepositoryStartKonfiguration` validiert (siehe `dialog-arbeitsverzeichnis-bearbeiten.md`: keine absoluten Pfade, keine `..`-Segmente, keine ungültigen Zeichen).
- `Path.Combine` in .NET normalisiert Pfade und ist sicher gegen `..`-Traversal, solange die Eingabe bereits validiert wurde.
- Zusätzliche Sicherheit: Nach `Path.Combine` den resultierenden Pfad gegen `lokalerKlonPfad` validieren (optionaler Zusatzcheck: `resultPath.StartsWith(lokalerKlonPfad, StringComparison.OrdinalIgnoreCase)`).

## Konfiguration

Keine neue Konfiguration erforderlich. Das Feature nutzt die bestehende Eigenschaft `RepositoryStartKonfiguration.WorkingDirectoryRelativePath`, die bereits vom Benutzer über den „Arbeitsverzeichnis"-Dialog konfiguriert wird.

## Offene Fragen

1. **Fehlerbehandlung bei fehlendem Arbeitsverzeichnis:** Wenn das konfigurierte Arbeitsverzeichnis nach dem Klon nicht existiert (z. B. weil die Repository-Struktur sich geändert hat), soll:
   - a) Die `issue.md` im Root erstellt werden (Fallback)?
   - b) Ein Fehler geloggt, aber nicht fatal behandelt werden (wie derzeit)?
   - c) Ein expliziter Fehler geworfen werden?
   
   Empfehlung: Fall b) – bestehend bleibt das `CreateIssueFileAsync`-Fehlerverhalten (Warnung, kein Abbruch). Fallback auf Root bei fehlender Verzeichnis-Existenz würde zusätzliche Komplexität bringen; das Problem sollte stattdessen durch die bereits vorhandene `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync`-Validierung früh erkannt werden.

2. **Konsistenz mit LocalDirectoryPlugin (InSourceDirectory-Modus):** In `dialog-arbeitsverzeichnis-bearbeiten.md` wird erwähnt, dass Repositories im `InSourceDirectory`-Modus den Arbeitsverzeichnis-Pfad gegen den tatsächlichen Quellordner aufgelöst haben (über `IGitPlugin.ResolveEffectiveRepositoryPathAsync`). Muss dieselbe Logik hier angewendet werden?
   - Empfehlung: Nein, nicht erforderlich. Die `issue.md` wird lokal im geklonten Verzeichnis erstellt, nicht im Quellverzeichnis. Der Klon ist immer `lokalerKlonPfad`, und das Arbeitsverzeichnis wird relativ dazu aufgelöst — unabhängig vom Plugin.
