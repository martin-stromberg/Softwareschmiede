# Services und Logik

## `RepositoryStartskriptService`

Datei: `src/Softwareschmiede/Application/Services/RepositoryStartskriptService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RunAsync(repositoryRootPath, configuration, ct)` | `public` | Führt das konfigurierte Startskript für ein Repository aus |
| `ResolveScriptPath(repositoryRootPath, relativePath)` | `private static` | Löst den Skriptpfad auf und validiert Path-Traversal-Sicherheit |
| `BuildArguments(scriptPath)` | `private static` | Erstellt PowerShell-Argumente für die Skriptausführung |

**Abhängigkeiten:**
- `ICliRunner` — zur Ausführung von CLI-Prozessen
- `ILogger<RepositoryStartskriptService>` — für Protokollierung

**Verhalten:**
- Prüft, ob Konfiguration aktiv ist; wenn nicht, bricht Methode ab (Logging: "Repository-Startskript ist deaktiviert.")
- Löst Skriptpfad auf mit `Path.GetFullPath()` und `Path.Combine()`
- Validiert Path-Traversal-Sicherheit: Skript muss innerhalb des Repository-Roots liegen
- Wirft `InvalidOperationException` wenn Datei nicht gefunden wird oder Pfad außerhalb Repository liegt
- Ruft `ICliRunner.RunAsync()` mit PowerShell-Executor auf
- Wirft `InvalidOperationException` wenn Ausführung fehlschlägt (basierend auf `CliResult.IsSuccess`)

**PowerShell-Argumente:**
```
"-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", <scriptPath>
```

**Bemerkungen:**
- Dient als Architektur-Vorbild für `RepositoryInitialisierungService`
- Fehlerbehandlung durch Exception-Throwing (nicht durch Logging-Only)
- Wird im `EntwicklungsprozessService` nach erfolgreichem Klonen aufgerufen

---

## `EntwicklungsprozessService`

Datei: `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProzessStartenAsync(aufgabeId, repositoryUrl, basisBranchName, selectedScmPluginPrefix, ct)` | `public` | Richtet Git-Repository ein: Klon, Branch, optionales Startskript |
| `ProzessStartenUndCliStartenAsync(aufgabeId, repositoryUrl, basisBranchName, kiPluginPrefix, ct)` | `public` | Kombiniert Repository-Setup und CLI-Start in einem Schritt |
| `CliNeustartenAsync(aufgabeId, kiPluginPrefix, optionalParameters, ct)` | `public` | Startet die KI-CLI im bereits vorbereiteten Klon neu |
| `RepositoryStartskriptAusfuehrenAsync(aufgabeId, ct)` | `public` | Führt das Repository-Startskript für eine Aufgabe manuell aus |
| `FinalizeStartAsync(aufgabeId, aufgabe, repository, lokalerKlonPfad, branchName, nutzeExistierendenBranch, ct)` | `private` | Finalisiert das Setup mit Startskript-Ausführung und `issue.md`-Erstellung |
| `PrepareCloneDirectoryAsync(gitPlugin, repositoryUrl, aufgabeId, ct)` | `private` | Bereitet Verzeichnis vor und führt Klon durch |
| `SetupBranchAsync(gitPlugin, repositoryUrl, lokalerKlonPfad, basisBranchName, defaultSourceBranchName, aufgabe, ct)` | `private` | Erstellt oder wechselt zu Task-Branch |
| `CommitDurchfuehrenAsync(aufgabeId, message, ct)` | `public` | Führt manuellen Commit durch |
| `ResetDurchfuehrenAsync(aufgabeId, resetType, targetRef, ct)` | `public` | Setzt Commits zurück |
| `PushDurchfuehrenAsync(aufgabeId, ct)` | `public` | Pusht Branch auf Remote |
| `PullDurchfuehrenAsync(aufgabeId, ct)` | `public` | Holt Änderungen vom Remote |
| `PullRequestErstellenAsync(aufgabeId, repositoryId, title, body, ct)` | `public` | Erstellt Pull Request |
| `AbschliessenAsync(aufgabeId, ct)` | `public` | Schließt Aufgabe ab: Klon löschen, Status setzen |

**Abhängigkeiten:**
- `AufgabeService` — für Aufgaben-Verwaltung
- `ProtokollService` — für Protokollierung
- `IGitPlugin` — für Git-Operationen
- `PluginSelectionService` — für Plugin-Auflösung
- `IArbeitsverzeichnisResolver` — für Verzeichnis-Auflösung
- `RepositoryStartskriptService` (optional, über `EntwicklungsprozessServiceOptions`) — für Startskript-Ausführung
- `KiAusfuehrungsService` (optional) — für CLI-Start
- `GitOrchestrationService` (optional) — für Arbeitsverzeichnis-Validierung

**Hook für Initialisierungsskript:**
Im `FinalizeStartAsync()` (Zeilen 549-575) wird nach erfolgreichem Klonen das Startskript aufgerufen:
- Wenn `repository.StartKonfiguration` existiert und `RepositoryStartskriptService` verfügbar ist
- Fehler werden abgefangen und nur als `Warning` geloggt
- Fehler blockieren nicht die weitere Aufgabenbearbeitung
- Fehlerhinweis wird in Protokoll-Nachricht aufgenommen

**Bemerkungen:**
- Zentrale Orchestrierungs-Komponente für Repository-Lifecycle
- Hat bereits Fehlerbehandlung für Startskripte als Vorbild
- Bietet Integrationspunkt nach dem Klonen für neue Initialisierungsskript-Logik
- `EntwicklungsprozessServiceOptions` Record ermöglicht flexible Abhängigkeitsinjektion von optionalen Services

