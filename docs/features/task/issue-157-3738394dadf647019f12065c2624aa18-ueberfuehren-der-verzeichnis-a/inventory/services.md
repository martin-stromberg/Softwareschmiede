# Services

Analyse der Service-Klassen und Interfaces bezüglich der Anforderung.

## `IGitWorkspaceBrowserService` (Interface)

Datei: `src/Softwareschmiede/Application/Services/IGitWorkspaceBrowserService.cs`

**Zusammenfassung:** Service zum Laden des lokalen Repository-Browsers.

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `LoadSnapshotAsync` | `repositoryPath: string, ct: CancellationToken` | `Task<WorkspaceSnapshot>` | Lädt den aktuellen Workspace-Zustand für ein Repository |
| `LoadPreviewAsync` | `repositoryPath: string, node: WorkspaceFileNode, ct: CancellationToken` | `Task<FilePreview>` | Lädt die Vorschau für eine selektierte Datei |
| `LoadCommitFilesAsync` | `repositoryPath: string, commitSha: string, ct: CancellationToken` | `Task<IReadOnlyList<WorkspaceFileNode>>` | Lädt commit-spezifische Dateiknoten |
| `LoadCommitPreviewAsync` | `repositoryPath: string, node: WorkspaceFileNode, ct: CancellationToken` | `Task<FilePreview>` | Lädt die Vorschau einer Datei innerhalb eines bestimmten Commits |
| `LoadWorkingTreeAsync` | `repositoryPath: string, ct: CancellationToken` | `Task<IReadOnlyList<WorkspaceFileNode>>` | Lädt den vollständigen Arbeitsbaum eines geklonten Repositories |

---

## `GitWorkspaceBrowserService` (Implementierung)

Datei: `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`

**Zusammenfassung:** Lädt Git-Status, Baum und Dateivorschauen aus dem lokalen Arbeitsverzeichnis. Verwendet `ICliRunner` zum Ausführen von Git-Befehlen.

### Abhängigkeiten

- `ICliRunner _cliRunner` - Injiziert im Constructor, führt Git-Befehle aus
- `ILogger<GitWorkspaceBrowserService> _logger` - Logging

### Konstanten

- `MaxInlineBytes = 1_048_576` - Max. Dateigröße für Inline-Darstellung (1 MB)
- `BinaryProbeBytes = 8_192` - Bytes zum Prüfen ob Datei binär ist
- `MaxWorkingTreeNodeCount = 100_000` - Max. Anzahl Knoten im Arbeitsbaum
- `GitDirectoryName = ".git"` - Wird ausgeschlossen

---

## Fehlende Services (NEU erforderlich)

### `WorkspaceExplorerService` / `WorkdirService`

**Status:** NICHT VORHANDEN

**Erwartete Signatur (aus Anforderung):**
```csharp
Task OpenWorkingDirectoryAsync(string workingDirectoryPath)
```

**Zweck:** Startet den Standard-Dateiexplorer des Betriebssystems mit dem übergebenen Verzeichnis.

**Plattform-spezifische Implementierung erforderlich:**
- Windows: `explorer.exe`
- Linux: `xdg-open`
- macOS: `open`

---

### `IdeService` / `VisualStudioService`

**Status:** NICHT VORHANDEN

**Erwartete Signatur (aus Anforderung):**
```csharp
Task OpenSolutionAsync(string solutionFilePath)
```

**Zweck:** Sucht nach `*.sln`-Dateien im Repository und startet Visual Studio (oder eine konfigurierbare IDE) mit der Solution, falls vorhanden.

**Abhängigkeiten:**
- Zugriff auf `ProcessStartService` oder direkten `Process.Start()`-Zugriff
- Möglicherweise Zugriff auf `IGitWorkspaceBrowserService` oder direkten `Directory.EnumerateFiles()`-Zugriff zum Scannen von Solution-Dateien

**Offene Fragen aus Anforderung:**
1. Nur Visual Studio oder auch VS Code, JetBrains Rider, etc.?
2. Fehlerbehandlung bei fehlender IDE?
3. Welche `*.sln`-Datei soll geöffnet werden (erste, spezifische mit Branch-Name, etc.)?
4. Sichtbarkeit/Aktivierbarkeit bei fehlendem Repository?

---

## Bestehende Services mit Relevanz

### `ProcessStartService`

**Erwartete Funktion:** Wird von beiden neuen Services benötigt.

**Verwendung in bestehendem Code:**
- `FileExplorerViewModel.DateiMitStandardanwendungOeffnen()` - Nutzt direkt `Process.Start()` mit `UseShellExecute = true`
- `IssueBrowserOeffnen()` in TaskDetailViewModel - Nutzt direkt `Process.Start()` mit `UseShellExecute = true`

**Fazit:** Ein dedizierter `ProcessStartService` existiert nicht; der Code nutzt direkt `System.Diagnostics.Process.Start()`.

---

## Zusammenfassung fehlender Infrastruktur

| Service | Existiert? | Abhängigkeiten | Priorität |
|---|---|---|---|
| `WorkspaceExplorerService` | Nein | `IProcessStarter` (oder `Process.Start()`) | Hoch - Anforderung Absatz 3 |
| `IdeService` / `VisualStudioService` | Nein | Datei-Scan, `IProcessStarter` (oder `Process.Start()`), Konfiguration | Hoch - Anforderung Absatz 3 |
| `IProcessStarter` (abstraktion für Process.Start) | Nein | - | Mittel - optional für besseres Testing |
