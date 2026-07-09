# Anforderung: Unterverzeichnis für KI-Ausführung

**Aufgaben-ID:** b8ff2fdc-38d0-4d51-a51d-41d9a97da10e  
**Feature-Branch:** task/issue-98-b8ff2fdc38d04d51a51d41d9a97da10e-unterverzeichnis-fuer-ki-ausfu  
**Erstellt:** 2026-07-08

---

## Fachliche Zusammenfassung

Die Anwendung wird um die Fähigkeit erweitert, für jedes Git-Repository in einem Projekt ein Arbeitsverzeichnis relativ zum Wurzelverzeichnis des geklonten Repositories zu definieren und zu speichern. Nach dem Klonen eines Repositories führt die CLI-Ausführung nicht mehr im Root-Verzeichnis des Repositories aus, sondern im angegebenen Unterverzeichnis. Die UI ermöglicht es bei der Repository-Auswahl, die Verzeichnisstruktur des externen Repositories voraus zu laden und ein Zielverzeichnis zu wählen, damit der Benutzer informierte Entscheidungen treffen kann.

---

## Betroffene Klassen und Komponenten

### Domain & Entities

- **`RepositoryStartKonfiguration`** (erweitern)
  - Neue Property: `WorkingDirectoryRelativePath` : `string` (nullable, Default: null = Wurzelverzeichnis)
    - Speichert den relativen Pfad zum Arbeitsverzeichnis innerhalb des Repositories
    - Beispiel: `"backend"`, `"apps/cli"`, `"."`
    - Wird persistiert und mit dem Repository-Klon verbunden

### Application Services

- **`KiAusfuehrungsService`** (erweitern)
  - Bestehende Methode: `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, ...)`
    - `localRepoPath` ist bereits das Wurzelverzeichnis des geklonten Repositories
    - Neue Logik: Falls `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` gesetzt, verkette mit `localRepoPath` zu effektivem Working Directory
    - Übergabe des effektiven Pfads an `PseudoConsoleProcessStarter` (als Working Directory für den Prozess)
  - Neue Helper-Methode (optional): `ResolveEffectiveWorkingDirectory(string repositoryRoot, string? relativePath)` : `string`
    - Kombiniert Wurzel-Pfad mit relativem Pfad
    - Validiert, dass das resultierende Verzeichnis existiert

- **`GitOrchestrationService`** (ggfs. erweitern)
  - Falls vorhanden: Überprüfung, dass der relative Pfad nach dem Klon verfügbar ist
  - Fehlerbehandlung, wenn das Zielverzeichnis im geklonten Repository nicht vorhanden ist

- **Neue Service-Methode (optional): `GitWorkspaceBrowserService`** oder erweitern bestehender Service
  - Neue Methode: `GetDirectoryStructureAsync(gitPluginInstance, repositoryUrl)` : `Task<List<string>>`
    - Ruft die Verzeichnisstruktur des externen Repositories ab (nur Directory-Namen, bis zu einer bestimmten Tiefe, z.B. 3 Level)
    - Nutzt `IGitPlugin.GetRepositoryStructure()` oder ähnliche Methode (Klärungsbedarf: Existiert diese?)
    - Caching: Cache-Ergebnisse pro Repository-URL mit TTL (z.B. 5 Minuten), um wiederholte API-Aufrufe zu reduzieren

### Presentation Layer

- **`RepositoryAssignViewModel`** (erweitern)
  - Neue Property: `AvailableWorkingDirectories` : `ObservableCollection<string>` (read-only)
    - Zeigt die verfügbaren Verzeichnisse des ausgewählten externen Repositories
  - Neue Property: `SelectedWorkingDirectory` : `string?` (Default: null)
    - Speichert die vom Benutzer ausgewählte relativer Pfad
  - Neue Property: `IsLoadingDirectoryStructure` : `bool`
    - Zeigt, ob gerade die Verzeichnisstruktur abgerufen wird
  - Bestehende Property: `SelectedRepository` (erweitern)
    - Bei Änderung: Auslösen des Ladens der Verzeichnisstruktur (asynchron)
    - Setze `SelectedWorkingDirectory` auf `null` (Reset)
  - Neue Methode: `LoadDirectoryStructureAsync(CancellationToken ct)` : `Task`
    - Wird aufgerufen, wenn `SelectedRepository` geändert wird
    - Ruft Service auf, um Verzeichnisstruktur zu laden
    - Befüllt `AvailableWorkingDirectories`
    - Fehlerbehandlung: Falls Laden fehlschlägt, Logging und leere Collection

- **`RepositoryAssignDialog.xaml`** (erweitern)
  - Neue UI-Sektion unterhalb Repository-Auswahl: "Arbeitsverzeichnis"
  - Elemente:
    - Label: "Arbeitsverzeichnis im Repository"
    - `ComboBox` oder `ListBox` mit `AvailableWorkingDirectories` als ItemsSource
    - Binding zu `SelectedWorkingDirectory`
    - Loading-Indikator (z.B. `ProgressRing` oder Spinner) gebunden an `IsLoadingDirectoryStructure`
    - Optionale Hilfe-Schaltfläche mit Tooltip: "Relative Pfade werden in Bezug auf das Wurzelverzeichnis des Repositories aufgelöst"
  - Visuelle Hinweise:
    - Deaktivierter Zustand während Struktur-Laden
    - Fallback-Option: Eingabefeld, falls vordefinierte Verzeichnisse nicht ausreichen (oder nur ComboBox ohne freie Eingabe)

### Tests

- **Unit-Tests: `RepositoryStartKonfigurationTests`**
  - Validierung der `WorkingDirectoryRelativePath` Property
  - Persistence und Abruf der Property

- **Unit-Tests: `RepositoryAssignViewModelTests`** (erweitern)
  - `SelectedRepository` Änderung triggert Directory-Struktur-Laden
  - `AvailableWorkingDirectories` wird korrekt befüllt
  - `SelectedWorkingDirectory` Reset bei Wechsel des Repositories
  - Fehlerbehandlung bei fehlgeschlagenem Laden

- **Unit-Tests / Integration-Tests für Working Directory Resolution**
  - `ResolveEffectiveWorkingDirectory(repositoryRoot, relativePath)` ergibt korrekten absoluten Pfad
  - Fehlerfall: Relativer Pfad führt außerhalb des Repositories (z.B. `"../../../etc"`) wird abgelehnt
  - Fehlerfall: Angegebenes Zielverzeichnis existiert nicht nach Klon

### Database Migration

- Neue Spalte: `RepositoryStartKonfiguration.WorkingDirectoryRelativePath` (nullable string)

---

## Implementierungsansatz

### 1. Domain-Änderungen

Erweitern der `RepositoryStartKonfiguration` Klasse:

```csharp
public sealed class RepositoryStartKonfiguration
{
    public Guid Id { get; set; }
    public Guid GitRepositoryId { get; set; }
    public string StartScriptRelativePath { get; set; } = string.Empty;
    public string? WorkingDirectoryRelativePath { get; set; } // NEUE PROPERTY
    public bool Aktiv { get; set; } = true;
    public GitRepository GitRepository { get; set; } = null!;
}
```

### 2. Service-Layer Erweiterung (`KiAusfuehrungsService`)

Logik zum Auflösen des effektiven Arbeitsverzeichnisses beim CLI-Start:

```csharp
public async Task<CliProcessHandle> StartCliAsync(
    Guid aufgabeId,
    IKiPlugin kiPlugin,
    string localRepoPath,
    RepositoryStartKonfiguration? startConfig, // Neuer Parameter
    string? optionalParameters = null,
    CancellationToken ct = default)
{
    string effectiveWorkdir = localRepoPath;
    if (startConfig?.WorkingDirectoryRelativePath is not null)
    {
        effectiveWorkdir = ResolveEffectiveWorkingDirectory(localRepoPath, startConfig.WorkingDirectoryRelativePath);
        ValidateWorkingDirectory(effectiveWorkdir, localRepoPath);
    }
    
    // Übergabe an PseudoConsoleProcessStarter
    return await StartProcessAsync(aufgabeId, kiPlugin, effectiveWorkdir, ...);
}

private string ResolveEffectiveWorkingDirectory(string repositoryRoot, string relativePath)
{
    string combined = Path.Combine(repositoryRoot, relativePath);
    string normalized = Path.GetFullPath(combined);
    
    // Sicherheitsprüfung: Zielverzeichnis darf nicht außerhalb des Repositories liegen
    string normalizedRoot = Path.GetFullPath(repositoryRoot);
    if (!normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Pfad verlässt Repository-Verzeichnis: {relativePath}");
    
    return normalized;
}

private void ValidateWorkingDirectory(string effectiveWorkdir, string repositoryRoot)
{
    if (!Directory.Exists(effectiveWorkdir))
        throw new DirectoryNotFoundException($"Arbeitsverzeichnis nicht gefunden: {effectiveWorkdir}");
}
```

### 3. Directory-Struktur-Abruf (Neue oder erweiterte Service)

Entweder neue Methode in `GitWorkspaceBrowserService` oder neue Service-Klasse:

```csharp
public class DirectoryStructureBrowserService
{
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<DirectoryStructureBrowserService> _logger;
    private readonly IMemoryCache _cache;
    
    /// <summary>Lädt die Verzeichnisstruktur eines externen Repositories (bis zu Tiefe 2).</summary>
    public async Task<List<string>> GetDirectoriesAsync(
        IGitPlugin gitPlugin,
        string repositoryUrl,
        CancellationToken ct = default)
    {
        string cacheKey = $"dirs:{repositoryUrl}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached))
            return cached!;
        
        try
        {
            // Option 1: Falls IGitPlugin.GetRepositoryStructure() existiert:
            var structure = await gitPlugin.GetRepositoryStructureAsync(repositoryUrl, maxDepth: 2, ct);
            var directories = structure
                .Where(item => item.IsDirectory)
                .Select(item => item.Path)
                .OrderBy(p => p)
                .ToList();
            
            // Cache für 5 Minuten
            _cache.Set(cacheKey, directories, TimeSpan.FromMinutes(5));
            return directories;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Laden der Verzeichnisstruktur für {RepositoryUrl}", repositoryUrl);
            return new List<string>();
        }
    }
}
```

### 4. ViewModel-Erweiterung (`RepositoryAssignViewModel`)

```csharp
private ObservableCollection<string> _availableWorkingDirectories = new();
private string? _selectedWorkingDirectory;
private bool _isLoadingDirectoryStructure;
private CancellationTokenSource? _dirStructureCts;

public ObservableCollection<string> AvailableWorkingDirectories
{
    get => _availableWorkingDirectories;
    private set => SetProperty(ref _availableWorkingDirectories, value);
}

public string? SelectedWorkingDirectory
{
    get => _selectedWorkingDirectory;
    set => SetProperty(ref _selectedWorkingDirectory, value);
}

public bool IsLoadingDirectoryStructure
{
    get => _isLoadingDirectoryStructure;
    private set => SetProperty(ref _isLoadingDirectoryStructure, value);
}

public override AvailableRepository? SelectedRepository
{
    get => _selectedRepository;
    set => SetProperty(ref _selectedRepository, value, OnSelectedRepositoryChanged);
}

private void OnSelectedRepositoryChanged()
{
    SelectedWorkingDirectory = null;
    _dirStructureCts?.Cancel();
    _dirStructureCts?.Dispose();
    _dirStructureCts = new CancellationTokenSource();
    CurrentLoadDirectoryStructureTask = LoadDirectoryStructureAsync(_dirStructureCts.Token);
}

private async Task LoadDirectoryStructureAsync(CancellationToken ct)
{
    if (_pluginManager == null || SelectedScmPlugin == null || SelectedRepository == null)
    {
        AvailableWorkingDirectories.Clear();
        return;
    }

    try
    {
        IsLoadingDirectoryStructure = true;
        var directories = await _directoryStructureService.GetDirectoriesAsync(
            SelectedScmPlugin,
            SelectedRepository.Url,
            ct);
        
        ct.ThrowIfCancellationRequested();
        
        AvailableWorkingDirectories.Clear();
        AvailableWorkingDirectories.Add("."); // Root-Option immer verfügbar
        foreach (var dir in directories)
            AvailableWorkingDirectories.Add(dir);
        
        SelectedWorkingDirectory = "."; // Default: Root
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        // Cancelled durch Plugin-/Repository-Wechsel
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Fehler beim Laden der Verzeichnisstruktur.");
        AvailableWorkingDirectories.Clear();
        AvailableWorkingDirectories.Add(".");
        SelectedWorkingDirectory = ".";
    }
    finally
    {
        if (!ct.IsCancellationRequested)
            IsLoadingDirectoryStructure = false;
    }
}
```

### 5. UI-Änderung (`RepositoryAssignDialog.xaml`)

Neue Sektion nach Repository-Auswahl:

```xaml
<!-- Arbeitsverzeichnis-Auswahl -->
<TextBlock Text="Arbeitsverzeichnis im Repository" 
           FontWeight="SemiBold" 
           Margin="0,16,0,8" />

<Grid ColumnDefinitions="*, Auto">
    <ComboBox Grid.Column="0"
              ItemsSource="{Binding AvailableWorkingDirectories}"
              SelectedItem="{Binding SelectedWorkingDirectory}"
              IsEnabled="{Binding IsLoadingDirectoryStructure, Converter={StaticResource InvertedBoolConverter}}"
              Margin="0,0,8,0" />
    
    <ProgressRing Grid.Column="1"
                  IsActive="{Binding IsLoadingDirectoryStructure}"
                  Width="24" Height="24" />
</Grid>

<TextBlock Text="Hinweis: '.' bedeutet Wurzelverzeichnis des Repositories"
           FontSize="11"
           Foreground="{DynamicResource SecondaryTextBrush}"
           Margin="0,4,0,0" />
```

### 6. Datenbankmigrationen

Entity Framework Core Migration für neue Spalte in `RepositoryStartKonfiguration`:

```sql
ALTER TABLE RepositoryStartConfigurations
ADD WorkingDirectoryRelativePath NVARCHAR(MAX) NULL;
```

---

## Konfiguration

### Optionale AppSettings-Einträge

- **`DirectoryStructureCacheDurationSeconds`** (int, Default: 300)
  - Caching-Dauer für abgerufene Verzeichnisstrukturen (in Sekunden)
  - Reduziert API-Aufrufe bei wiederholtem Laden

- **`DirectoryStructureMaxDepth`** (int, Default: 2)
  - Maximale Verzeichnis-Tiefe beim Abruf der Repository-Struktur
  - Verhindert Performance-Probleme bei großen Repositories

- **`DirectoryStructureEnabled`** (bool, Default: true)
  - Aktiviert/Deaktiviert die Voraus-Ladung der Verzeichnisstruktur im Dialog

---

## Offene Fragen

1. **Git-Plugin API:**
   - Existiert bereits eine Methode im `IGitPlugin` Interface (z.B. `GetRepositoryStructureAsync`), um die Verzeichnisstruktur eines externen Repositories abzurufen?
   - Falls nicht: Soll eine neue Methode im Interface definiert werden, oder soll die Struktur über Git-Clone-Simulation oder alternative API abgerufen werden?
   - Welche Plugins (GitHub, Bitbucket, GitLab) müssen dies unterstützen?

2. **Verzeichnis-Tiefe & Limit:**
   - Sollen nur oberste Level-Verzeichnisse angezeigt werden (z.B. bis Tiefe 2), oder vollständige Hierarchie?
   - Größenlimit: Wie viele Verzeichnisse sollten maximal angezeigt werden, um Performance zu gewährleisten?

3. **Fehlerbehandlung:**
   - Falls Verzeichnisstruktur nicht abgerufen werden kann (z.B. Private Repositories ohne Auth): Soll manualle Eingabe ermöglicht werden (Textfeld) oder nur vordefinierte Liste?
   - Sollen fehlerhafte Verzeichnis-Pfade bei der Aufgaben-Ausführung mit spezialisierter Fehlermeldung abgebrochen werden?

4. **Sicherheit:**
   - Path-Traversal Prävention: Ist die Validierung `normalized.StartsWith(normalizedRoot)` ausreichend, oder müssen zusätzliche Prüfungen durchgeführt werden?
   - Falls Benutzer manuell relative Pfade eingeben können: Wie sollte dies validiert werden?

5. **Migration & Bestandsdaten:**
   - Für bestehende `RepositoryStartKonfiguration`-Einträge ohne `WorkingDirectoryRelativePath`: Sollen diese als `"."` (Root) behandelt werden?
   - Soll eine Datenmigration durchgeführt werden oder ist `null` ausreichend (wird dann als Root interpretiert)?

6. **Ausführungskontext:**
   - Falls relative Pfade in anderen Kontexten verwendet werden (z.B. `RepositoryStartskriptService` für Startskripte): Muss `StartScriptRelativePath` ebenfalls in Relation zum `WorkingDirectoryRelativePath` interpretiert werden, oder bleibt es relativ zur Repository-Root?
   - Annahme: `StartScriptRelativePath` bleibt relativ zur Repository-Root, nicht zum Arbeitsverzeichnis

7. **UI-Details:**
   - Soll das Arbeitsverzeichnis optional sein (Default: Root) oder erforderlich?
   - Annahme: Optional, Default ist Wurzelverzeichnis (`"."` oder `null`)

8. **Caching & Refresh:**
   - Soll der Benutzer die Verzeichnisstruktur manuell aktualisieren können (z.B. via "Refresh"-Button), oder nur automatisches Laden beim Plugin-Wechsel?
   - Wie lange sollte der Cache erhalten bleiben (per Repository oder global)?
