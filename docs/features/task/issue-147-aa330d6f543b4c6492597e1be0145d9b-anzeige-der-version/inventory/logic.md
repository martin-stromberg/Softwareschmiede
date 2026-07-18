# Logik: LocalDirectoryPlugin

## `LocalDirectoryPlugin`
Datei: `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`

Diese Klasse erbt von `GitPluginBase<LocalDirectoryPlugin>` und implementiert Git-Operationen für lokale Verzeichnisse ohne Remote-Provider.

### Öffentliche Methode zum Laden der Repository-Struktur

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRepositoryStructureAsync(string repositoryUrl, int maxDepth = 2, CancellationToken ct = default)` | Public (override) | Lädt die Verzeichnisstruktur und gibt nur erfolgreiche Einträge zurück (Zeilen 289–293) |
| `GetRepositoryStructureLoadResultAsync(string repositoryUrl, int maxDepth = 2, CancellationToken ct = default)` | Public (override) | Lädt die Verzeichnisstruktur mit Status-Information; enthält die Cancellation-Logik (Zeilen 296–317) |

### Private Hilfsmethoden (relevant für Cancellation)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CollectDirectoryEntries(string rootPath, string currentPath, int depth, int maxDepth, List<RepositoryDirectoryEntry> entries, CancellationToken ct)` | Private | Rekursive Traversierung der Verzeichnisstruktur; führt `ct.ThrowIfCancellationRequested()` auf Zeile 345 durch **vor jeder Verzeichnis-Iteration** |

### Detaillierte Implementierung der problematischen Methode

**`GetRepositoryStructureLoadResultAsync()`** (Zeilen 296–317):
```csharp
public override async Task<RepositoryStructureLoadResult> GetRepositoryStructureLoadResultAsync(
    string repositoryUrl, 
    int maxDepth = 2, 
    CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();  // Zeile 298: Upfront-Abbruch-Check
    
    if (string.IsNullOrWhiteSpace(repositoryUrl) || !Directory.Exists(repositoryUrl))
    {
        return RepositoryStructureLoadResult.Failed("Das lokale Repository-Verzeichnis existiert nicht.");
    }
    
    var rootPath = ResolveAndNormalizePath(repositoryUrl);
    
    var entries = await Task.Run(
        () =>
        {
            var entries = new List<RepositoryDirectoryEntry>();
            CollectDirectoryEntries(rootPath, rootPath, depth: 0, maxDepth, entries, ct);
            return (IEnumerable<RepositoryDirectoryEntry>)entries;
        },
        ct);  // Task.Run erhält ebenfalls das CancellationToken
    
    return RepositoryStructureLoadResult.Success(entries);
}
```

**`CollectDirectoryEntries()`** (Zeilen 319–368):
- **Zeile 345:** Führt `ct.ThrowIfCancellationRequested()` **vor der Verarbeitung jedes Verzeichnisses** aus
- Die Traversierung ist rekursiv: nach einer erfolgreichen Verarbeitung eines Verzeichnisses (Zeile 361) wird die Methode für Unterverzeichnisse aufgerufen
- Die Cancellation-Prüfung sitzt **innerhalb der Schleife** über `Directory.EnumerateDirectories()`, nicht außerhalb

### Cancellation-Verhalten

1. **Upfront-Abbruch:** Wird in `GetRepositoryStructureLoadResultAsync()` auf Zeile 298 abgefangen
2. **During-Execution-Abbruch:** Wird in `CollectDirectoryEntries()` Zeile 345 abgefangen — allerdings nur wenn die rekursive Traversierung bereits läuft und eine neue Verzeichnis-Iteration beginnt
3. **Anfälligkeit:** Ist das Cancellation-Fenster zu klein (5ms im Test) und die Verzeichnisanzahl gering, kann die gesamte Traversierung vor der nächsten Prüfung abgeschlossen sein

### Abhängigkeiten

- **`ICredentialStore`:** Wird für Konfigurationsabfragen verwendet (nicht relevant für Cancellation)
- **`ILogger<LocalDirectoryPlugin>`:** Wird für Fehlerprotokolle verwendet
- **`CancellationToken`:** Wird als Parameter an `Task.Run()` und recursive Aufrufe weitergegeben
