# Logik und Services

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

ViewModel für die Aufgabendetailansicht. Verwaltet Status, Protokoll, CLI-Prozesse und Fenstereinbettung.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `WaehleAnsicht(DetailAnsicht ansicht)` | private | Wechselt die aktuelle Ansicht (Info, Cli, Diff) |
| `WaehleStandardAnsicht()` | private | Setzt Standardansicht basierend auf Aufgabenstatus |
| `LadenAsync(CancellationToken ct)` | private | Lädt die Aufgabe asynchron mit allen Details |
| `StartenAsync(CancellationToken ct)` | private | Startet die Aufgabe kombiniert (Klonen, Plugin-Auflösung, CLI-Start) |
| `CliStoppenAsync(CancellationToken ct)` | private | Stoppt den laufenden CLI-Prozess |
| `CliNeustartenAsync(CancellationToken ct)` | private | Startet CLI neu nach manuellem Stopp |
| `SpeichernAsync(CancellationToken ct)` | private | Speichert Titel und Anforderungsbeschreibung |
| `LoeschenAsync(CancellationToken ct)` | private | Löscht die Aufgabe nach Bestätigung |
| `AufgabeAbschliessenAsync(CancellationToken ct)` | private | Beendet die Aufgabe (Status: Beendet) |
| `GetPseudoConsoleSession()` | public | Gibt aktive PseudoConsoleSession zurück, oder null |
| `Dispose()` | public | Räumt Ressourcen auf, entfernt Event-Listener |

**Private Enum DetailAnsicht:**
- `Info` - Stammdatenansicht
- `Cli` - Terminal-Ansicht
- `Diff` - Diff-Ansicht (Platzhalter in TaskDetailView.xaml vorhanden)

**Properties:**
- `AufgabeId` - ID der angezeigten Aufgabe
- `Aufgabe` - Geladene Aufgabe
- `IsInfoViewSelected`, `IsCliViewSelected`, `IsDiffViewSelected` - Sichtbarkeitszustände
- `ShowInfoPanel`, `ShowCliPanel`, `ShowDiffPanel` - Anzeige-Bedingungen
- `Protokolleintraege` - ObservableCollection von Protokolleinträgen
- `PromptVorlagen` - Verfügbare Promptvorlagen
- `CliStatusText`, `AktiverCliName` - Status-Anzeige in der Fußzeile

**Abonnierte Events:**
- `KiAusfuehrungsService.CliProcessStatusChanged` - reagiert auf CLI-Statuswechsel
- `PromptZeitVersandService.PromptSent` - reagiert auf versendete Prompts

**Publizierte Events:**
- `PseudoConsoleSessionGestartet` - neue Konsolen-Session gestartet
- `CliGestoppt` - CLI-Prozess beendet
- `PromptVorlageGesendet` - Promptvorlage erfolgreich versendet

**Hinweis:** Das ViewModel hat aktuell **keine** Properties oder Commands für den Dateibrowser-Modus. Diese müssen hinzugefügt werden:
- `AktuellerDateibrowserModus` (enum `DateibrowserAnsichtsmodus`: Standard, Vergleich)
- `AusgewaehlteDatui` (relative Pfad der ausgewählten Datei)
- `DateiInhalt` (Textinhalt der ausgewählten Datei)
- `DiffLines` (für Diff-Viewer im Vergleichsmodus)
- `Wurzelbaume` (ObservableCollection von FileTreeNode für Baum-Anzeige)
- `StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand` - Commands für Modus-Umschaltung
- `IsFileExplorerViewSelected` - Sichtbarkeitszustand

## `DiffService`
Datei: `src/Softwareschmiede/Application/Services/DiffService.cs`

Service für Diff-Verwaltung: Orchestriert Diff-Generierung, Caching und Persistierung.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GenerateDiffAsync(Guid aufgabeId, string filePath, string sourceContent, string targetContent, ...)` | public | Generiert Diff zwischen zwei Dateiinhalten (vereinfachte Überladung) |
| `GenerateDiffAsync(Guid aufgabeId, Guid? gitRepositoryId, string filePath, string sourceVersion, string targetVersion, ...)` | public | Generiert Diff mit Caching und Persistierung |
| `GetDiffAsync(Guid diffResultId, CancellationToken ct)` | public | Ruft Diff mit allen Details ab |
| `GetDiffsByAufgabeAsync(Guid aufgabeId, int skip, int take, CancellationToken ct)` | public | Ruft alle Diffs für eine Aufgabe ab (paginiert) |
| `SearchDiffsAsync(Guid aufgabeId, int skipCount, int takeCount, string? searchTerm, CancellationToken ct)` | public | Durchsucht Diffs nach Aufgabe |
| `DeleteDiffAsync(Guid diffResultId, CancellationToken ct)` | public | Löscht einen Diff und seinen Cache |
| `InvalidateDiffCacheAsync(Guid diffResultId, CancellationToken ct)` | public | Invalidiert Cache für einen Diff |
| `GetDiffCountAsync(Guid aufgabeId, CancellationToken ct)` | public | Zählt Diffs für eine Aufgabe |
| `GetStatisticsAsync(Guid aufgabeId, CancellationToken ct)` | public | Ruft Statistiken für eine Aufgabe ab |

**Injizierte Dependencies:**
- `SoftwareschmiededDbContext _db` - Datenbank-Kontext
- `DiffAlgorithmService _diffAlgorithmService` - Algorithmus für Diff-Generierung
- `DiffCachingService _diffCachingService` - Caching-Verwaltung
- `ILogger<DiffService> _logger`

**Hinweis:** Dieser Service verwaltet bereits Diffs auf Dateiebene. Der Dateiexplorer benötigt zusätzliche Services für:
- Gruppierung von Diffs nach Commits
- Filterung nach geänderten Dateien (Added/Modified/Deleted)
- Gitgetriebene Diff-Ermittlung (git diff origin/main..HEAD)

## `DirectoryStructureBrowserService`
Datei: `src/Softwareschmiede/Application/Services/DirectoryStructureBrowserService.cs`

Ruft die Verzeichnisstruktur externer Repositories über Git-Plugins ab und cacht die Ergebnisse.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetDirectoriesAsync(IGitPlugin gitPlugin, string repositoryUrl, CancellationToken ct)` | public | Ruft Verzeichnisliste eines Repositories ab |
| `GetDirectoryLoadResultAsync(IGitPlugin gitPlugin, string repositoryUrl, CancellationToken ct)` | public | Ruft Verzeichnisstruktur mit explizitem Lade-Status ab |

**Injizierte Dependencies:**
- `IMemoryCache _cache` - In-Memory-Cache
- `DirectoryStructureOptions _options` - Konfiguration (MaxDepth, CacheDurationSeconds, Enabled)
- `ILogger<DirectoryStructureBrowserService> _logger`

**Rückgabewerte:**
- `List<string>` - Liste von relativen Verzeichnis-Pfaden
- `RepositoryStructureLoadResult` - Struktur mit Status (Success, Failed, NotSupported), Message, Entries

**Hinweis:** Dieser Service lädt externe Repository-Strukturen. Für den Dateibrowser benötigen wir einen ähnlichen Service für **lokale** geklonte Repositories.

## `GitWorkspaceBrowserService`
Datei: `src/Softwareschmiede/Application/Services/GitWorkspaceBrowserService.cs`

Verwaltet Git-Workspace-Navigation und Datei-Informationen in geklonten Repositories.

**Hinweis:** Diese Service wird bereits für Workspace-Vorschau verwendet. Sie kann möglicherweise wiederverwendet oder erweitert werden für den Dateibrowser-Service.

## `DiffAlgorithmService`
Datei: `src/Softwareschmiede/Application/Services/DiffAlgorithmService.cs`

Führt die Diff-Algorithmen durch (z. B. Myers diff algorithm) und generiert strukturierte Diff-Blöcke mit Zeilenänderungen.

**Hinweis:** Dieser Service wird bereits vom DiffService genutzt. Er kann auch für die Diff-Anzeige im Dateibrowser verwendet werden.

## Noch nicht vorhanden:

### `DateibrowserService`
Benötigt für:
- Laden der lokalen Verzeichnisstruktur aus geklontem Repository
- Caching der Verzeichnisbäume pro Repository
- Filterung nach geänderten Dateien im Vergleichsmodus
- Git diff-Aufruf für Commit-Differenzen

### `GitDiffParserService`
Benötigt für:
- Parsing von `git diff`-Output
- Strukturierung nach Commits
- Extraktion von ChangeType (Added/Modified/Deleted)
- Optional: Caching von Diff-Ergebnissen
