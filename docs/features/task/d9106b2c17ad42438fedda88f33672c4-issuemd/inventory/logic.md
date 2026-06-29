# Logik - Bestandsaufnahme

## `EntwicklungsprozessService`
Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

### Betroffene Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenAsync` | public | Richtet das Git-Repository für eine Aufgabe ein: Klon, Branch-Erstellung, optionales Startskript. Setzt Status auf `AufgabeStatus.Gestartet`. Diese Methode ist der Integrationspunkt für die neue Funktionalität der `issue.md`-Erstellung. |
| `ProzessStartenUndCliStartenAsync` | public | Kombiniert Repository-Setup und CLI-Start in einem Schritt. Nutzt intern `ProzessStartenAsync`. Im Fehlerfall Rollback. |
| `RollbackStartAsync` | private | Rollt den Status zurück und löscht das Klon-Verzeichnis bei Fehler. |
| `CommitDurchfuehrenAsync` | public | Führt einen manuellen Commit durch. |
| `ResetDurchfuehrenAsync` | public | Setzt Commits zurück. |
| `PushDurchfuehrenAsync` | public | Pusht den Branch auf den Remote. |
| `PullDurchfuehrenAsync` | public | Holt Änderungen vom Remote. |
| `PullRequestErstellenAsync` | public | Erstellt einen Pull Request. |
| `AbschliessenAsync` | public | Schließt die Aufgabe ab: löscht Klon-Verzeichnis, setzt Status auf `AufgabeStatus.Beendet`. |
| `GetRemoteBranchesAsync` | public | Gibt die Remote-Branches eines Repositories zurück. |
| `RepositoryStartskriptAusfuehrenAsync` | public | Führt das Repository-Startskript manuell aus. |
| `ResolveRepositoryAsync` | private | Löst das Git-Repository auf Basis der Aufgabe oder des Projekts. |
| `ErstelleTaskBranchName` | private static | Erstellt einen Task-Branch-Namen basierend auf Aufgabentitel und Issue-Nummer. |
| `ErstelleTitelSlug` | private static | Erstellt einen URL-sicheren Slug aus dem Aufgabentitel. |
| `DeleteDirectoryForce` | private static | Löscht ein Verzeichnis mit rekursivem Löschen aller Dateien (auch Read-Only). |

### Abhängigkeiten (Injected)

- `AufgabeService` - Service für Aufgabenverwaltung
- `ProtokollService` - Service für Protokollierung
- `ProjektService` (optional) - Service für Projektverwaltung
- `IGitPlugin` - Plugin-Interface für Git-Operationen
- `PluginSelectionService` - Service zur Plugin-Auswahl
- `IArbeitsverzeichnisResolver` - Resolver für Arbeitsverzeichnis-Pfade
- `RepositoryStartskriptService` (optional) - Service für Repository-Startskript-Ausführung
- `KiAusfuehrungsService` (optional) - Service für KI-Ausführung
- `ILogger<EntwicklungsprozessService>` - Logger

### Integration in `ProzessStartenAsync`

Zeile 138: `await gitPlugin.CloneRepositoryAsync(repository.RepositoryUrl, lokalerKlonPfad, ct);` - Klon wird erstellt
Zeile 180: `await _aufgabeService.StartenAsync(aufgabeId, branchName, lokalerKlonPfad, ct);` - Aufgabe wird gestartet

**Integrationspunkt für neue Funktionalität:** Zwischen diesen beiden Aufrufen (Zeile 138-180) sollen die neuen Methoden `CreateIssueFileAsync` und `UpdateGitignoreAsync` eingefügt werden.

### Fehlerbehandlung

- Exceptions werden mit `ILogger.LogWarning` / `ILogger.LogError` protokolliert
- Bei `RepositoryStartskriptService` Fehler: Fehler wird geloggt, aber Prozess stoppt nicht (graceful degradation)
- Bei `ProzessStartenUndCliStartenAsync` Fehler: Rollback wird durchgeführt

### Protokollierung

- Log-Einträge via `ILogger<EntwicklungsprozessService>` für wichtige Operationen
- `ProtokollService.AddEintragAsync` für Aufgaben-spezifische Protokollierungen mit Typ `ProtokollTyp.GitAktion`
