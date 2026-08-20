# Geschäftslogik

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

Das ViewModel für die Aufgabendetailansicht. Enthält die aktuelle IDE-Öffnen-Funktionalität.

### Relevante Properties

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `KannIdeOeffnen` | `bool` (read-only) | Gibt an, ob die IDE geöffnet werden kann; prüft nur das Bestehen des lokalen Klonpfads |
| `OeffneIdeCommand` | `ICommand` | Asynces Relay-Kommando für `OeffneIdeAsync` |

### Relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OeffneIdeAsync` | `private` | Lädt das Arbeitsverzeichnis, ruft `IdeOeffnenService.OpenRepositoryInIdeAsync` mit einem Callback auf, der einen Dialog bei mehreren Einstiegspunkten zeigt |

**Details zu `OeffneIdeAsync`:**
- Ruft `ErmittleEffektivesArbeitsverzeichnisAsync` auf, um das Arbeitsverzeichnis zu ermitteln.
- Übergibt einen Callback an `OpenRepositoryInIdeAsync`, der:
  - Die gefundenen `IdeEntryPoint`-Objekte extrahiert (nur die `Path`-Eigenschaft).
  - `_dialogService.ShowSolutionSelectionDialogAsync` mit der Liste der Pfade aufruft.
  - Den gewählten Pfad mit den `IdeEntryPoint`-Objekten abgleicht oder einen neuen erzeugt.
- Fehlerbehandlung ist vorhanden; Fehlermeldungen werden in `FehlerMeldung` angezeigt.
- Wird bei Klick auf den Button "IDE öffnen" in der Ribbon-Gruppe "Werkzeuge" ausgeführt.

---

## `IdeOeffnenService`
Datei: `src/Softwareschmiede.Application/Services/IdeOeffnenService.cs`

Service zur Ermittlung und Öffnung von IDE-Einstiegspunkten.

### Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `FindeSolutions` | `public` | Gibt alle `*.sln`-Dateien der obersten Verzeichnisebene alphabetisch sortiert zurück |
| `OeffneSolution` | `public` | Öffnet die übergebene `.sln`-Datei mit dem OS-Standard-Handler |
| `OpenRepositoryInIdeAsync` | `public` | Kermmethode: Löst IDE-Plugin auf, ermittelt Einstiegspunkte, ruft bei Mehrfach-Treffern einen Callback auf, öffnet den gewählten Einstiegspunkt |

**Details zu `OpenRepositoryInIdeAsync`:**
- **Parameter:**
  - `repositoryPath`: Pfad des zu öffnenden Repositories.
  - `waehleEntryPointAsync`: Optionaler Callback `Func<IReadOnlyList<IdeEntryPoint>, CancellationToken, Task<IdeEntryPoint?>>` zur Auswahl eines Einstiegspunkts bei mehreren Treffern.
  - `ct`: Cancellation Token.
- **Verhalten:**
  - Ruft `pluginSelectionService.ResolveIdePluginAsync` auf, um das zuständige IDE-Plugin zu ermitteln.
  - Ruft `plugin.FindEntryPointsAsync` auf.
  - **0 Einstiegspunkte:** Wirft `FileNotFoundException`.
  - **1 Einstiegspunkt:** Öffnet direkt über `plugin.OpenEntryPointAsync`.
  - **Mehrere Einstiegspunkte:**
    - Falls `waehleEntryPointAsync` gesetzt ist, ruft es den Callback auf.
    - Falls der Callback `null` zurückgibt, wird nichts geöffnet (Abbruch durch Anwender).
    - Falls kein Callback gesetzt ist, öffnet den ersten Einstiegspunkt (Fallback).
- Wird von `TaskDetailViewModel.OeffneIdeAsync` aufgerufen.

---

## `PluginSelectionService`
Datei: `src/Softwareschmiede.Application/Services/PluginSelectionService.cs`

Service zur Auflösung und Verwaltung von IDE- und anderen Plugin-Präferenzen.

### Relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveIdePluginAsync` | `public` | Löst das IDE-Plugin für ein Repository auf (Priorität: explizit → gespeichert → Fallback → Default) |
| `GetStoredDefaultPluginPrefixAsync` | `public` | Liest den gespeicherten Standard-Plugin-Prefix für einen Plugin-Typ |
| `SaveDefaultPluginPrefixAsync` | `public` | Speichert einen Standard-Plugin-Prefix |

**Details zu `ResolveIdePluginAsync`:**
- **Verhalten:**
  - Holt die Liste der aktivierten IDE-Plugins.
  - Wendet die konfigurierte Plugin-Reihenfolge an (`plugins.ide.order`).
  - Prüft jedes Plugin in Reihenfolge auf Kompatibilität (`CheckCompatibilityAsync`):
    - **Explicit:** Verwendung dieses Plugins.
    - **Fallback:** Speichert es als Fallback-Option, prüft aber weiter.
  - Gibt das erste Plugin mit Explicit zurück, oder das erste mit Fallback, oder das Standard-Plugin.
- Wird von `IdeOeffnenService.OpenRepositoryInIdeAsync` aufgerufen.

