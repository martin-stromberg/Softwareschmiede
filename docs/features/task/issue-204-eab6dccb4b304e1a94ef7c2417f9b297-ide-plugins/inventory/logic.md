# Bestandsaufnahme: Logik-Komponenten

## `IdeOeffnenService`

**Datei:** `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs`

**Abhängigkeiten:**
- `IProzessStarter prozessStarter` — Constructor-Parameter
- `PluginSelectionService? pluginSelectionService` — Constructor-Parameter (optional, Standard: null)

| Methode | Sichtbarkeit | Rückgabewert | Kurzbeschreibung |
|---------|-----------|-------------|------------------|
| `FindeSolutions(string? arbeitsverzeichnis)` | public | `IReadOnlyList<string>` | Ermittelt alle `.sln`/`.slnx`-Dateien auf oberster Ebene, alphabetisch sortiert; delegiert zu `VisualStudioIdePlugin.FindSolutionFiles()` |
| `OeffneSolution(string solutionPfad)` | public | `void` | Öffnet die übergebene Solution-Datei via Shell-Execute; delegiert zu `VisualStudioIdePlugin.OpenSolutionFile()` |
| `OpenRepositoryInIdeAsync(string repositoryPath, Func<IReadOnlyList<string>, CancellationToken, Task<string?>>? waehleSolutionAsync = null, CancellationToken ct = default)` | public | `Task` | **Wichtigste Methode für die Anforderung.** Zeigt die aktuelle Leaky-Abstraktion: <br/><br/>**Aktuelle Implementierung (Zeilen 48-75):** <ul><li>Ruft `pluginSelectionService.ResolveIdePluginAsync(repositoryPath, ct)` auf, um das zu verwendende Plugin zu ermitteln</li><li>**Type-Check (Zeile 60):** `if (plugin is VisualStudioIdePlugin && waehleSolutionAsync is not null)` — hier liegt die Leaky Abstraction</li><li>Falls Bedingung erfüllt: ruft `FindeSolutions(repositoryPath)` auf</li><li>Falls >1 Solution: ruft `waehleSolutionAsync(solutionPfade, ct)` auf (Callback vom UI-Layer)</li><li>Falls Callback null zurückgibt: kehrt zurück ohne zu öffnen</li><li>Falls Callback eine Solution wählt: öffnet diese via `OeffneSolution(solutionPfad)`</li><li>**Fallback (Zeile 74):** `plugin.OpenRepositoryAsync(repositoryPath, ct)` wird aufgerufen (betrifft VS Code und alle anderen Plugins)</li></ul> |

**Callback-Signatur des `waehleSolutionAsync`-Parameters:**
```csharp
Func<IReadOnlyList<string>, CancellationToken, Task<string?>>
```
- **Eingabe:** Liste gefundener Solution-Pfade (Strings)
- **Ausgabe:** Gewählter Solution-Pfad oder `null` bei Abbruch durch Benutzer

**Aufruf von:** `TaskDetailViewModel.OeffneIdeAsync()` (Hauptaufrufer)

---

## `VisualStudioIdePlugin`

**Datei:** `src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs`

**Implementiert:** `IIdePlugin`

**Abhängigkeiten:**
- `IProzessStarter prozessStarter` — Constructor-Parameter

**Eigenschaften:**
- `PluginName` → `"Visual Studio"`
- `PluginPrefix` → `"Softwareschmiede.VisualStudio"`
- `PluginType` → `PluginType.Ide`

| Methode | Sichtbarkeit | Parameter | Rückgabewert | Kurzbeschreibung |
|---------|-----------|-----------|-------------|------------------|
| `CheckCompatibilityAsync` | public | `string repositoryPath`, `CancellationToken ct = default` | `Task<IdePluginCompatibility>` | Sucht nach `.sln`/`.slnx`-Dateien via `FindSolutionFiles()`. Gibt `Explicit` zurück, wenn ≥1 gefunden, sonst `Incompatible` |
| `OpenRepositoryAsync` | public | `string repositoryPath`, `CancellationToken ct = default` | `Task` | Ruft `FindSolutionFiles()` auf, nimmt die erste alphabetisch, ruft `OpenSolutionFile()` auf. **Sonderfall:** Ignoriert multiple Solutions und öffnet immer nur die erste (keine User-Interaktion für Auswahl) |
| `FindSolutionFiles` | internal static | `string repositoryPath` | `List<string>` | Enumeriert alle `*.sln` und `*.slnx`-Dateien im Repository-Root (top-level, `SearchOption.TopDirectoryOnly`), sortiert alphabetisch, gibt leere Liste bei nicht existierendem Verzeichnis zurück |
| `OpenSolutionFile` | internal static | `IProzessStarter prozessStarter`, `string solutionPath` | `void` | Startet `prozessStarter.Starten()` mit `ProzessStartAnfrage(solutionPath, Argumente: null, ShellAusfuehren: true)` |
| `GetSettingGroups` | public | — | `IReadOnlyList<PluginSettingGroup>` | Gibt leere Liste zurück |

**Erkenntnis:** Die Implementierung von `OpenRepositoryAsync()` ruft automatisch nur die erste `.sln` auf — mehrere Solutions werden stillschweigend ignoriert. Die Benutzer-Interaktion für Mehrfach-Solutions erfolgt **außerhalb** des Plugins in `IdeOeffnenService.OpenRepositoryInIdeAsync()` via Type-Check.

---

## `VisualStudioCodeIdePlugin`

**Datei:** `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs`

**Implementiert:** `IIdePlugin`

**Abhängigkeiten:**
- `IProzessStarter prozessStarter` — Constructor-Parameter
- `IVisualStudioCodeLocator visualStudioCodeLocator` — Constructor-Parameter

**Eigenschaften:**
- `PluginName` → `"Visual Studio Code"`
- `PluginPrefix` → `"Softwareschmiede.VisualStudioCode"`
- `PluginType` → `PluginType.Ide`

| Methode | Sichtbarkeit | Parameter | Rückgabewert | Kurzbeschreibung |
|---------|-----------|-----------|-------------|------------------|
| `CheckCompatibilityAsync` | public | `string repositoryPath`, `CancellationToken ct = default` | `Task<IdePluginCompatibility>` | Gibt immer `Fallback` zurück (universeller Rückfall-Plugin) |
| `OpenRepositoryAsync` | public | `string repositoryPath`, `CancellationToken ct = default` | `Task` | Ruft `OpenDirectory()` auf mit aufgelöstem VS Code-Executable |
| `OpenDirectory` | internal static | `IProzessStarter prozessStarter`, `IVisualStudioCodeLocator visualStudioCodeLocator`, `string path` | `void` | <ul><li>Ruft `visualStudioCodeLocator.Locate()` auf</li><li>Wirft `InvalidOperationException`, falls VS Code nicht verfügbar</li><li>Startet `prozessStarter.Starten()` mit `ProzessStartAnfrage(executablePath, QuoteArgument(path), ShellAusfuehren: false)`</li></ul> |
| `QuoteArgument` | private static | `string argument` | `string` | Quoted Argument und escapet innere Anführungszeichen |
| `GetSettingGroups` | public | — | `IReadOnlyList<PluginSettingGroup>` | Gibt leere Liste zurück |

**Erkenntnis:** VS Code Plugin hat **einen einzigen Einstiegspunkt pro Repository** (das Repository-Root selbst). Keine Mehrfach-Szenarien.

---

## `PluginSelectionService`

**Datei:** Nicht vollständig gelesen, aber aus Tests erkannt:

**Wichtigste Methode (für Anforderung relevant):**
- `ResolveIdePluginAsync(string repositoryPath, CancellationToken ct)` → `Task<IIdePlugin>` — Wählt das beste verfügbare IDE-Plugin basierend auf Aktivierungsstatus, Reihenfolge und Kompatibilität

**Abhängigkeiten:**
- `PluginActivationService` — Prüft Aktivierungsstatus von Plugins
- `AppEinstellungService` — Liest `plugins.ide.order` und andere Settings
- `PluginManager` — Registriert verfügbare Plugins

**Nicht relevant für die vorliegende Anforderung** (Auflösung-Logik ändert sich nicht, nur die Einstiegspunkt-Behandlung im `OpenRepositoryInIdeAsync`).
