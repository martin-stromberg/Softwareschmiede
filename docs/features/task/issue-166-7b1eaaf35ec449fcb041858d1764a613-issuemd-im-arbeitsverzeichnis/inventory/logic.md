# Logikklassen

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenAsync` | `public` | Klont das Repository, legt einen Branch an, führt optional das Startskript aus und finalisiert die Aufgabe. |
| `ProzessStartenUndCliStartenAsync` | `public` | Kombiniert Repository-Setup und CLI-Start in einem Schritt; rollback bei Fehler. |
| `CommitDurchfuehrenAsync` | `public` | Führt einen manuellen Commit durch. |
| `ResetDurchfuehrenAsync` | `public` | Setzt Commits zurück. |
| `PushDurchfuehrenAsync` | `public` | Pusht den Branch auf den Remote. |
| `PullDurchfuehrenAsync` | `public` | Holt Änderungen vom Remote. |
| `PullRequestErstellenAsync` | `public` | Erstellt einen Pull Request für die Aufgabe. |
| `AbschliessenAsync` | `public` | Schließt die Aufgabe ab (Klon löschen, Status setzen). |
| `GetRemoteBranchesAsync` | `public` | Gibt Remote-Branches eines Repositories zurück. |
| `RepositoryStartskriptAusfuehrenAsync` | `public` | Führt das Repository-Startskript manuell aus. |
| `FinalizeStartAsync` | `private` | Finalisiert den Repository-Setup: Startskript, `issue.md`, `.gitignore`, Status aktualisieren. |
| `CreateIssueFileAsync` | `private` | **[RELEVANT]** Erstellt die `issue.md`-Datei mit Aufgabenbeschreibung **derzeit im Repository-Root** (Zeile 616: `Path.Combine(lokalerKlonPfad, "issue.md")`). |
| `UpdateGitignoreAsync` | `private` | **[RELEVANT]** Aktualisiert die `.gitignore`-Datei im **Repository-Root** mit einem Eintrag für `issue.md` (Zeile 634). |

### Details zu relevanten Methoden

#### `CreateIssueFileAsync` (Zeile 596–628)
- **Aktueller Stand:** Schreibt `issue.md` direkt in `lokalerKlonPfad` (Repository-Root).
- **Parameter:** 
  - `lokalerKlonPfad` (string): Root des geklonten Repositories
  - `aufgabe` (Aufgabe): Aufgabendaten
  - `branchName` (string): Aktueller Branch-Name
  - `ct` (CancellationToken): Abbruchtoken
- **Fehlerbehandlung:** Loggt Warnung bei Fehler, bricht nicht ab.
- **Keine Referenz zu Arbeitsverzeichnis:** Die Methode erhält nicht die `StartKonfiguration`.

#### `UpdateGitignoreAsync` (Zeile 630–660)
- **Aktueller Stand:** Schreibt `.gitignore` im Repository-Root.
- **Parameter:**
  - `lokalerKlonPfad` (string): Root des geklonten Repositories
  - `ct` (CancellationToken): Abbruchtoken
- **Fehlerbehandlung:** Loggt Warnung bei Fehler, bricht nicht ab.
- **Keine Referenz zu Arbeitsverzeichnis:** Analog zu `CreateIssueFileAsync`.

#### `FinalizeStartAsync` (Zeile 474–516)
- **Aufrufer von `CreateIssueFileAsync` und `UpdateGitignoreAsync`:**
  - Ruft `CreateIssueFileAsync(lokalerKlonPfad, aufgabe, branchName, ct)` auf (Zeile 502).
  - Ruft `UpdateGitignoreAsync(lokalerKlonPfad, ct)` auf (Zeile 503).
- **Empfänger von `repository`:** Die `GitRepository` mit `StartKonfiguration` ist lokal verfügbar (Parameter), wird aber nicht an die Hilfsmethoden weitergegeben.

---

## `GitOrchestrationService`
Datei: `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IssuesAbrufenAsync` | `public` | Ruft Issues aus einem Repository ab. |
| `CommitAsync` | `public` | Führt einen Commit durch und protokolliert. |
| `ResetAsync` | `public` | Setzt Commits zurück und protokolliert. |
| `PushAsync` | `public` | Pusht den Branch und protokolliert. |
| `PullAsync` | `public` | Holt Änderungen und protokolliert. |
| `MergeToSourceAsync` | `public` | Übernimmt Änderungen vom Arbeitsverzeichnis ins Quellverzeichnis. |
| `ValidateWorkingDirectoryAfterCloneAsync` | `public` | **[RELEVANT]** Validiert nach dem Git-Klon, dass das konfigurierte Arbeitsverzeichnis existiert (Zeile 261–281). |

### Details zu relevanten Methoden

#### `ValidateWorkingDirectoryAfterCloneAsync` (Zeile 261–281)
- **Aufgerufen von:** `EntwicklungsprozessService.ProzessStartenAsync` (Zeile 101).
- **Parameter:**
  - `clonePath` (string): Pfad zum geklonten Repository.
  - `startConfig` (RepositoryStartKonfiguration?): Optionale Startkonfiguration.
  - `gitPlugin` (IGitPlugin?): Optional für Auflösung des tatsächlichen Pfads (z.B. bei `LocalDirectoryPlugin` im `InSourceDirectory`-Modus).
- **Verhalten:**
  - Wenn `startConfig?.WorkingDirectoryRelativePath` ist null, return ohne Validierung.
  - Ruft `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync` auf.
  - Wirft `InvalidOperationException` oder `DirectoryNotFoundException` bei Fehler.
- **Fehlerbehandlung:** Loggt Fehler und re-throwen.

---

## Abhängigkeiten und Zusammenspiel

- **`EntwicklungsprozessService.FinalizeStartAsync`** empfängt `repository: GitRepository` (mit `StartKonfiguration`).
- **`FinalizeStartAsync` ruft auf:**
  - `CreateIssueFileAsync` → empfängt derzeit nur `lokalerKlonPfad`, nicht `StartKonfiguration`.
  - `UpdateGitignoreAsync` → empfängt derzeit nur `lokalerKlonPfad`, nicht `StartKonfiguration`.
- **`EntwicklungsprozessService.ProzessStartenAsync` ruft auf:**
  - `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync` (mit `repository.StartKonfiguration`).
  - `FinalizeStartAsync` (mit `repository`).

**Bestehende Validierung:** Das Arbeitsverzeichnis wird bereits nach dem Klon validiert (Zeile 99–102 in `ProzessStartenAsync`), bevor `FinalizeStartAsync` aufgerufen wird. Dies geschieht über `GitOrchestrationService.ValidateWorkingDirectoryAfterCloneAsync`.
