# Interfaces

## `IGitPlugin`
Datei: `src/Softwareschmiede/Domain/Interfaces/IGitPlugin.cs`

Plugin-Interface für Git-Verwaltung (z. B. GitHub, GitLab, Bitbucket).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRepositoryStructureLoadResultAsync(string repositoryUrl, int maxDepth, CancellationToken ct)` | URL, Tiefe, CT | `Task<RepositoryStructureLoadResult>` | Lädt Verzeichnisstruktur eines Repositories |

**Verwendung:** Wird von `DirectoryStructureBrowserService` aufgerufen. Der Dateiexplorer wird diese Methode nicht direkt nutzen (für externe Repos), aber die Basis-Struktur für lokale Repositories verstehen müssen.

## `IScmProvider`
Datei: `src/Softwareschmiede/Domain/Interfaces/IScmProvider.cs` (vermutlich)

Allgemeines Interface für Source Code Management.

**Hinweis:** Die Anforderung erwähnt, dass `GitDiffParserService` das bereits vorhandene `IScmProvider`/Plugin-Mechanismus nutzen kann oder direkt `git diff` aufruft. Dies Interface wurde nicht explizit durchsucht.

## Noch nicht vorhanden:

### `IDateibrowserService`
Benötigt für:
- Abstraktion des Dateiladevorgangs
- Austauschbarkeit (z. B. mit Mock-Implementierungen für Tests)

```csharp
public interface IDateibrowserService
{
    /// <summary>Lädt die Wurzel-Verzeichnisstruktur aus dem geklonten Repository.</summary>
    Task<FileTreeNode?> LoadDirectoryStructureAsync(string repositoryPath, CancellationToken ct);
    
    /// <summary>Lädt den Inhalt einer Datei aus dem geklonten Repository.</summary>
    Task<string?> LoadFileContentAsync(string repositoryPath, string relativePath, CancellationToken ct);
    
    /// <summary>Lädt die Liste von Dateien, die zwischen zwei Commits geändert wurden.</summary>
    Task<List<CommitDiffGroup>> LoadChangedFilesAsync(string repositoryPath, string sourceVersion, string targetVersion, CancellationToken ct);
    
    /// <summary>Invalidiert den Cache für ein Repository.</summary>
    Task InvalidateCacheAsync(string repositoryPath, CancellationToken ct);
}
```

### `IGitDiffParserService`
Benötigt für:
- Parsing von `git diff`-Output
- Strukturierung der Diff-Ergebnisse

```csharp
public interface IGitDiffParserService
{
    /// <summary>Parst Git-Diff-Output und gruppiert nach Commits.</summary>
    Task<List<CommitDiffGroup>> ParseGitDiffOutputAsync(string diffOutput, string repositoryPath, CancellationToken ct);
    
    /// <summary>Extrahiert Dateiänderungen aus git diff --name-status Output.</summary>
    Task<List<FileChange>> ExtractFileChangesAsync(string nameStatusOutput, CancellationToken ct);
}
```

## Bestehende Interfaces:

### `IMemoryCache`
Wird von `DirectoryStructureBrowserService` für Caching verwendet.

### `ILogger<T>`
Wird von allen Services für Logging verwendet.

