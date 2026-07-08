# Logik-Services

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Beschreibung | Status |
|---------|-------------|-------------|--------|
| `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct)` | Public | Startet einen CLI-Prozess im Standard-Modus | Vorhanden — **Parameter für RepositoryStartKonfiguration fehlt** |
| `StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct)` | Public | Startet einen CLI-Prozess über ConPTY | Vorhanden — **Parameter für RepositoryStartKonfiguration fehlt** |
| `StartPseudoConsoleProcess(aufgabeId, localRepoPath, pluginCommand)` | Private | Erzeugt PseudoConsole und startet cmd.exe | Vorhanden — setzt WorkingDirectory auf `localRepoPath` (Zeilen 239–241) — **keine Logik für effektives Arbeitsverzeichnis** |
| `StopCliAsync(aufgabeId, ct)` | Public | Stoppt laufenden Prozess | Vorhanden |
| `GetRunningCount()` | Public | Gibt Anzahl laufender Prozesse zurück | Vorhanden |
| `IsRunning(aufgabeId)` | Public | Prüft, ob Prozess für Aufgabe läuft | Vorhanden |
| `GetLastExitCode(aufgabeId)` | Public | Gibt Exit-Code des letzten Prozesses zurück | Vorhanden |
| `GetPseudoConsoleSession(aufgabeId)` | Public | Gibt PseudoConsoleSession zurück | Vorhanden |
| `UpdateHeartbeat(aufgabeId)` | Public | Aktualisiert Heartbeat der Aufgabe | Vorhanden |

### Erforderliche Erweiterungen

1. **Methodensignaturen:** Beide `StartCliAsync` und `StartWithPseudoConsoleAsync` müssen einen Parameter `RepositoryStartKonfiguration? startConfig` erhalten
2. **Helper-Methode:** `ResolveEffectiveWorkingDirectory(string repositoryRoot, string? relativePath)` — **FEHLT**
3. **Validierung:** `ValidateWorkingDirectory(string effectiveWorkdir, string repositoryRoot)` — **FEHLT**
4. **Logik in StartPseudoConsoleProcess:** Muss das effektive Arbeitsverzeichnis berücksichtigen

## `GitOrchestrationService`
Datei: `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung | Status |
|---------|-------------|------------------|--------|
| `CommitAsync(aufgabeId, message, ct)` | Public | Führt Commit durch und protokolliert | Vorhanden |
| `ResetAsync(aufgabeId, resetType, targetRef, ct)` | Public | Setzt Commits zurück | Vorhanden |
| `PushAsync(aufgabeId, ct)` | Public | Pusht Branch auf Remote | Vorhanden |
| `PullAsync(aufgabeId, ct)` | Public | Holt Änderungen vom Remote | Vorhanden |
| `MergeToSourceAsync(aufgabeId, ct)` | Public | Übernimmt Änderungen ins Quellverzeichnis | Vorhanden |
| `GetGitActionCapabilitiesAsync(aufgabeId, ct)` | Public | Liefert Git-Aktions-Capabilities | Vorhanden |
| `PullRequestErstellenAsync(aufgabeId, title, body, ct)` | Public | Erstellt Pull Request | Vorhanden |
| `IssuesAbrufenAsync(repositoryId, ct)` | Public | Ruft Issues ab | Vorhanden |

### Erforderliche Erweiterungen

1. **Neue Methode:** Validierung des Arbeitsverzeichnisses nach dem Klon — **FEHLT**
   - Sollte überprüfen, dass das in `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` angegebene Verzeichnis nach dem Klon vorhanden ist
   - Fehlerbehandlung für fehlende Verzeichnisse

## `DirectoryStructureBrowserService`
Datei: **NICHT VORHANDEN** — zu erstellen

Diese neue Service-Klasse ist erforderlich für:
- Abruf der Verzeichnisstruktur eines externen Repositories
- Caching der Ergebnisse (TTL ca. 5 Minuten)
- Fehlerbehandlung bei API-Aufrufen

Erforderliche Methode:
```csharp
public async Task<List<string>> GetDirectoriesAsync(
    IGitPlugin gitPlugin,
    string repositoryUrl,
    CancellationToken ct = default)
```

### Abhängigkeiten

- `IPluginManager` — für Zugriff auf Git-Plugins
- `IMemoryCache` — für Caching der Verzeichnisstrukturen
- `ILogger<DirectoryStructureBrowserService>` — für Fehlerprotokollierung

