# Logik / Services

## `AgentPackageReader`

Datei: `src/Softwareschmiede/Infrastructure/Services/AgentPackageReader.cs`

Implementierung von `IAgentPackageService`. Liest Agentenpakete aus dem Dateisystem (read-only).

### Öffentliche Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `.ctor(ILogger<AgentPackageReader>, IHostEnvironment)` | public | Konstruktor; initialisiert den Basispfad (`agent-packages` relativ zu ContentRootPath) und erstellt das Verzeichnis falls notwendig |
| `GetPackagesAsync(CancellationToken)` | public | Implementiert `IAgentPackageService.GetPackagesAsync`; enumiert alle Verzeichnisse im Basispfad und konvertiert diese in `AgentPackageInfo` |
| `GetPackageAsync(string, CancellationToken)` | public | Implementiert `IAgentPackageService.GetPackageAsync`; gibt null zurück, falls das Paket nicht existiert |

### Private Hilfsmethoden

| Methode | Kurzbeschreibung |
|---------|------------------|
| `EnsurePackagesDirectoryExists()` | Erstellt das Basis-Verzeichnis falls notwendig |
| `ReadPackage(string)` | Konvertiert ein Paket-Verzeichnis in ein `AgentPackageInfo`-Objekt; ermittelt alle Dateien rekursiv |

### Abhängigkeiten

- `ILogger<AgentPackageReader>` (Logging)
- `IHostEnvironment` (für ContentRootPath)
- `AgentPackageInfo` (Rückgabewert)

### Hinweise

- Die Klasse gibt `AgentInfo` immer leer zurück (`Array.Empty<AgentInfo>()`), auch wenn `.agent.md`-Dateien vorhanden sind
- Keine DI-Registrierung in `Program.cs`

---

## `AgentPackageFileService`

Datei: `src/Softwareschmiede/Infrastructure/Services/AgentPackageFileService.cs`

Implementierung von `IAgentPackageFileService`. Verwaltet Agentenpakete, Verzeichnisse und Dateien im Dateisystem (CRUD-Operationen).

### Öffentliche Methoden

#### Paket-Verwaltung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `.ctor(ILogger<AgentPackageFileService>, IHostEnvironment)` | public | Konstruktor; initialisiert den Basispfad und erstellt das Verzeichnis |
| `CreatePackageAsync(string, CancellationToken)` | public | Erstellt ein neues Paket-Verzeichnis; validiert Namen und prüft auf Duplikate |
| `RenamePackageAsync(string, string, CancellationToken)` | public | Benannt ein Paket um; prüft auf Path-Traversal-Angriffe |
| `DeletePackageAsync(string, CancellationToken)` | public | Löscht ein Paket inkl. aller Inhalte |
| `BuildPackageTreeAsync(string, CancellationToken)` | public | Erstellt eine hierarchische `FileTreeNode`-Struktur für ein Paket |

#### Verzeichnis-Verwaltung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateDirectoryAsync(string, string, CancellationToken)` | public | Erstellt ein Unterverzeichnis im Paket |
| `RenameDirectoryAsync(string, string, string, CancellationToken)` | public | Benannt ein Verzeichnis um; validiert Path-Traversal |
| `DeleteDirectoryAsync(string, string, CancellationToken)` | public | Löscht ein Verzeichnis rekursiv |

#### Datei-Verwaltung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateEmptyFileAsync(string, string, CancellationToken)` | public | Erstellt eine leere Datei; erstellt Eltern-Verzeichnis falls notwendig |
| `WriteFileAsync(string, string, string, CancellationToken)` | public async | Schreibt Textinhalt in eine Datei; erstellt Eltern-Verzeichnis falls notwendig |
| `ReadFileAsync(string, string, CancellationToken)` | public async | Liest den Textinhalt einer Datei |
| `UploadFileAsync(string, string, string, Stream, CancellationToken)` | public async | Speichert Dateiinhalt aus einem Stream |
| `RenameFileAsync(string, string, string, CancellationToken)` | public | Benannt eine Datei um; validiert Path-Traversal |
| `DeleteFileAsync(string, string, CancellationToken)` | public | Löscht eine Datei |

### Private Hilfsmethoden

| Methode | Kurzbeschreibung |
|---------|------------------|
| `GetPackageBasePath(string)` | Konstruiert den absoluten Pfad für ein Paket |
| `ResolveSafePath(string, string)` | Löst einen relativen Pfad auf und prüft auf Path-Traversal-Angriffe |
| `ValidateName(string, string?)` | Validiert Namen gegen ungültige Zeichen und Path-Traversal |
| `BuildNode(string, string, string)` | Erstellt rekursiv eine `FileTreeNode`-Hierarchie |

### Abhängigkeiten

- `ILogger<AgentPackageFileService>` (Logging)
- `IHostEnvironment` (für ContentRootPath)
- `AgentPackageInfo` (Rückgabewert)
- `FileTreeNode` (Rückgabewert von `BuildPackageTreeAsync`) – diese Klasse muss überprüft werden, ob sie nur hier verwendet wird

### Hinweise

- Umfangreiche Sicherheitsüberprüfungen gegen Path-Traversal-Angriffe
- Alle Operationen validieren Parameter und werfen aussagekräftige Exceptions
- Keine DI-Registrierung in `Program.cs`
- Logging erfolgt vor und während aller Operationen
