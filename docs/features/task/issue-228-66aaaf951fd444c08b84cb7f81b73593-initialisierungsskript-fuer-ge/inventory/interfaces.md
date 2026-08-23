# Interfaces

## `ICliRunner`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync` | `command: string`, `args: IEnumerable<string>`, `workingDirectory: string?`, `environmentVariables: IDictionary<string, string>?`, `ct: CancellationToken` | `Task<CliResult>` | Führt einen CLI-Befehl aus und gibt das Ergebnis zurück. stdout und stderr werden parallel gelesen um Deadlocks zu vermeiden. |
| `StreamAsync` | `command: string`, `args: IEnumerable<string>`, `workingDirectory: string?`, `environmentVariables: IDictionary<string, string>?`, `ct: CancellationToken` | `IAsyncEnumerable<string>` | Führt einen CLI-Befehl aus und streamt stdout zeilenweise. |

**Bemerkungen:**
- Wird von `RepositoryStartskriptService` zur Script-Ausführung verwendet
- Argumente werden sicher über ArgumentList übergeben (keine Shell-Injection möglich)
- Arbeitsverzeichnis kann spezifiziert werden
- Umgebungsvariablen können übergeben werden (z.B. für Tokens)
- Wird auch für die neue Initialisierungsskript-Ausführung benötigt

---

## `IPluginManager`

Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins` | - | `IReadOnlyList<IGitPlugin>` | Gibt alle geladenen SCM-Plugins zurück. |
| `GetDevelopmentAutomationPlugins` | - | `IReadOnlyList<IKiPlugin>` | Gibt alle geladenen Development-Automation-Plugins zurück. |
| `GetDefaultSourceCodeManagementPlugin` | - | `IGitPlugin` | Gibt das erste verfügbare SCM-Plugin zurück. |
| `GetDefaultDevelopmentAutomationPlugin` | - | `IKiPlugin` | Gibt das priorisierte Development-Automation-Plugin zurück. |
| `GetIdePlugins` | - | `IReadOnlyList<IIdePlugin>` | Gibt alle geladenen IDE-Plugins zurück. |
| `GetDefaultIdePlugin` | - | `IIdePlugin` | Gibt das erste verfügbare IDE-Plugin zurück. |

**Bemerkungen:**
- Wird in `ProjectDetailViewModel` zur Auflösung von SCM-Plugins für Remote-Repository-Zugriff verwendet
- Kann für die Implementierung der Vorschlagslogik für Initialisierungsskripte verwendet werden
- Ermöglicht Plugin-agnostische Arbeit mit verschiedenen SCM-Systemen (GitHub, Bitbucket, etc.)

