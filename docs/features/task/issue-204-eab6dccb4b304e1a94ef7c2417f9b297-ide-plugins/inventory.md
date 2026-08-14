# Bestandsaufnahme: Generalisierung des IDE-Plugin-Mehreinstiegspunkt-Vertrags

Bestandsaufnahme des aktuellen Repository-Zustands bezüglich der Generalisierung des IDE-Plugin-Vertrags (`IIdePlugin`). Ziel: Entfernung des leaky Type-Checks (`plugin is VisualStudioIdePlugin`) in `IdeOeffnenService.OpenRepositoryInIdeAsync()` durch Einführung eines generischen Mehreinstiegspunkt-Konzepts.

---

## Zusammenfassung

### Wesentliche Befunde

| Aspekt | Status | Bemerkung |
|--------|--------|----------|
| **Leaky Abstraction (Type-Check)** | Existiert | `IdeOeffnenService.OpenRepositoryInIdeAsync()`, Zeile 60: `if (plugin is VisualStudioIdePlugin && ...)` — Sonderbehandlung nur für Visual Studio |
| **`IdeEntryPoint` Value Object** | Nicht existiert | Muss neu definiert werden (minimalistisch: `Path` + optional `DisplayName`) |
| **`IIdePlugin.FindEntryPointsAsync()`** | Nicht existiert | Muss neu definiert werden (abstrakte Methode) |
| **`IIdePlugin.OpenEntryPointAsync()`** | Nicht existiert | Muss neu definiert werden (abstrakte Methode) |
| **`VisualStudioIdePlugin.FindSolutionFiles()`** | Existiert | Internal static, wird aktuell nur von `IdeOeffnenService` und `VisualStudioIdePlugin` verwendet; kann Grundlage für `FindEntryPointsAsync()` sein |
| **`VisualStudioIdePlugin.OpenSolutionFile()`** | Existiert | Internal static, wird aktuell nur von `IdeOeffnenService` und `VisualStudioIdePlugin` verwendet; kann Grundlage für `OpenEntryPointAsync()` sein |
| **`VisualStudioCodeIdePlugin.OpenDirectory()`** | Existiert | Internal static, Grundlage für künftiges `OpenEntryPointAsync()` |
| **Test-Abdeckung** | Teilweise relevant | Bestehende Tests prüfen nur die alte Logik; neue Tests für Mehreinstiegspunkt-Methoden erforderlich |
| **Dokumentation** | Teilweise veraltet | Architektur und Ablauf erwähnen den Sonderfall, aber Mehrfach-Lösungen-Handling ist nicht generalisiert |

### Was bereits vorhanden ist

1. **Infrastructure für Einstiegspunkte (VisualStudioIdePlugin):**
   - `FindSolutionFiles()` — findet alle `.sln`/`.slnx`-Dateien
   - `OpenSolutionFile()` — öffnet eine einzelne `.sln`-Datei

2. **Infrastructure für einfache Repositories (VisualStudioCodeIdePlugin):**
   - `OpenDirectory()` — öffnet das Repository-Root selbst

3. **Test-Datenstruktur:**
   - Umfangreiche Tests für beide Plugins und den Service
   - Callback-Mechanismus für Mehrfach-Solutions ist funktionsfähig

### Was fehlt oder geändert werden muss

1. **Plugin-Kontrakt-Erweiterung:**
   - `IIdePlugin` braucht zwei neue Methoden (`FindEntryPointsAsync`, `OpenEntryPointAsync`)
   - Neuer Value-Object-Typ `IdeEntryPoint`

2. **Service-Generalisierung:**
   - `IdeOeffnenService.OpenRepositoryInIdeAsync()` muss Type-Check entfernen
   - Callback-Signatur ändern von `Func<IReadOnlyList<string>, ...>` zu `Func<IReadOnlyList<IdeEntryPoint>, ...>`

3. **Plugin-Implementierungen anpassen:**
   - `VisualStudioIdePlugin`: Implementiere `FindEntryPointsAsync()` und `OpenEntryPointAsync()`
   - `VisualStudioCodeIdePlugin`: Implementiere `FindEntryPointsAsync()` und `OpenEntryPointAsync()`

4. **Tests aktualisieren:**
   - Type-Check-Test wird obsolet
   - Neue Unit-Tests für beide `Async`-Methoden
   - Callback-Signature-Änderungen propagieren

5. **Dokumentation aktualisieren:**
   - Architektur-Diagramm: `IdeEntryPoint` als neuer Typ hinzufügen
   - Ablauf-Diagramm: Generischer Mehreinstiegspunkt-Workflow statt Sonderfall-VS
   - Entfernung von Hinweisen auf Type-Check-Sonderfall

---

## Details

### [Interfaces](inventory/interfaces.md)

**`IIdePlugin` (aktuell):**
- Zwei abstrakte Methoden: `CheckCompatibilityAsync()`, `OpenRepositoryAsync()`
- Erbt von `IPlugin` (PluginName, PluginPrefix, GetSettingGroups(), PluginType)

**Fehlende neue Methoden:**
- `FindEntryPointsAsync(string repositoryPath, CancellationToken ct)` → `Task<IReadOnlyList<IdeEntryPoint>>`
- `OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct)` → `Task`

**Neuer Value Object (zu definieren):**
- `IdeEntryPoint` — minimalistisch: `public record IdeEntryPoint(string Path, string? DisplayName = null)`

---

### [Logik-Komponenten](inventory/logic.md)

**`IdeOeffnenService.OpenRepositoryInIdeAsync()` (kritisch):**
- Zeilen 60–72: Leaky-Abstraction-Code (Type-Check für VisualStudioIdePlugin)
- Wenn `plugin is VisualStudioIdePlugin && waehleSolutionAsync is not null`:
  - Ruft `FindeSolutions()` (delegiert zu `VisualStudioIdePlugin.FindSolutionFiles()`)
  - Bei >1 Solution: Callback aufrufen, gewählte Solution öffnen via `OeffneSolution()`
- Fallback (Zeile 74): `plugin.OpenRepositoryAsync()`

**Zu ändernde Logik:**
- Entfernen des Type-Checks
- Immer `plugin.FindEntryPointsAsync()` aufrufen
- Je nach Anzahl Einstiegspunkte: direkt öffnen oder Callback aufrufen

**`VisualStudioIdePlugin`:**
- `FindSolutionFiles()` — kann Grundlage für `FindEntryPointsAsync()` sein
- `OpenSolutionFile()` — kann Grundlage für `OpenEntryPointAsync()` sein
- `OpenRepositoryAsync()` öffnet aktuell nur die erste `.sln` (ignoriert Mehrfach-Szenarien)

**`VisualStudioCodeIdePlugin`:**
- `OpenDirectory()` — kann Grundlage für `OpenEntryPointAsync()` sein
- Immer nur ein Einstiegspunkt (Repository-Root)

---

### [Enums](inventory/enums.md)

**`IdePluginCompatibility`:**
- `Explicit` — Plugin ist explizit kompatibel
- `Fallback` — Rückfall-Plugin
- `Incompatible` — nicht kompatibel

**Keine Änderungen erforderlich** — wird weiterhin für Kompatibilitätsprüfung verwendet.

---

### [Tests](inventory/tests.md)

**Existierende Tests mit Anpassungsbedarf:**

1. **`IdeOeffnenServiceTests` (14 Tests)**
   - Test für Type-Check wird obsolet: `OpenRepositoryInIdeAsync_MitMehrerenSolutionsUndVisualStudioPlugin_RueftCallbackAufUndOeffnetGewaehlteSolution()` (Zeilen 175–201)
   - Callback-Parameter ändert sich: `IReadOnlyList<string>` → `IReadOnlyList<IdeEntryPoint>`
   - Tests müssen auf `FindEntryPointsAsync()` / `OpenEntryPointAsync()` Calls umgeschrieben werden

2. **`VisualStudioIdePluginTests` (6 Tests, Erweiterung erforderlich)**
   - Neue Tests für `FindEntryPointsAsync()`: mehrere `.sln`, eine `.sln`, keine `.sln`, Sortierung
   - Neue Tests für `OpenEntryPointAsync(IdeEntryPoint)`

3. **`VisualStudioCodeIdePluginTests` (6 Tests, Erweiterung erforderlich)**
   - Neue Tests für `FindEntryPointsAsync()` — sollte immer genau 1 zurückgeben
   - Neue Tests für `OpenEntryPointAsync(IdeEntryPoint)`

4. **`PluginSelectionServiceTests_IdePlugin` (7 Tests)**
   - Keine direkten Änderungen erforderlich (Plugin-Auflösung ändert sich nicht)

5. **`TaskDetailViewModelTests_VisualStudioCode` (4 Tests)**
   - Callback-Signature ändert sich, Tests müssen angepasst werden

---

## Auswirkungen auf die Architektur

### Datenfluss-Änderung

**Aktuell:**
```
IdeOeffnenService.OpenRepositoryInIdeAsync()
  → Type-Check: is VisualStudioIdePlugin?
    → Ja: FindeSolutions() → Callback → OeffneSolution() (direkt)
    → Nein: plugin.OpenRepositoryAsync()
```

**Nach Refactoring:**
```
IdeOeffnenService.OpenRepositoryInIdeAsync()
  → plugin.FindEntryPointsAsync()
  → if (entryPoints.Count == 0): Exception
  → if (entryPoints.Count == 1): plugin.OpenEntryPointAsync(entryPoints[0])
  → if (entryPoints.Count > 1 && callback): entryPoint = callback(entryPoints) → plugin.OpenEntryPointAsync(entryPoint)
  → else: plugin.OpenEntryPointAsync(entryPoints[0])
```

### Plugin-Verträge-Änderung

**Vor Refactoring:**
- `IIdePlugin.OpenRepositoryAsync()` — verantwortlich für alles: Mehrfach-Szenarien ignorieren, öffnen

**Nach Refactoring:**
- `IIdePlugin.FindEntryPointsAsync()` — neue abstrakte Methode (erzwingt Implementierung)
- `IIdePlugin.OpenEntryPointAsync()` — neue abstrakte Methode (erzwingt Implementierung)

---

## Kritische Abhängigkeiten

### Komponenten mit Änderungsbedarf

| Komponente | Datei | Grund |
|-----------|-------|-------|
| `IIdePlugin` | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIdePlugin.cs` | Neue Methoden hinzufügen |
| `IdeEntryPoint` | Neues File in `src/Softwareschmiede.Plugin.Contracts/Domain/...` | Value Object definieren |
| `IdeOeffnenService` | `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs` | Type-Check entfernen, Logik generalisieren |
| `VisualStudioIdePlugin` | `src/Softwareschmiede/Domain/PluginImpl/VisualStudioIdePlugin.cs` | Neue Methoden implementieren |
| `VisualStudioCodeIdePlugin` | `src/Softwareschmiede/Domain/PluginImpl/VisualStudioCodeIdePlugin.cs` | Neue Methoden implementieren |
| `IdeOeffnenServiceTests` | `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs` | Tests anpassen/erweitern |
| `VisualStudioIdePluginTests` | `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioIdePluginTests.cs` | Tests erweitern |
| `VisualStudioCodeIdePluginTests` | `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioCodeIdePluginTests.cs` | Tests erweitern |
| `TaskDetailViewModelTests_VisualStudioCode` | `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs` | Callback-Signature anpassen |

### Unverändert bleiben (sollten)

- `PluginSelectionService` — Plugin-Auflösung ändert sich nicht
- `CheckCompatibilityAsync()` / `IdePluginCompatibility` — Kompatibilitätslogik ändert sich nicht
- Enums `PluginType`, `IdePluginCompatibility` — keine neuen Werte erforderlich

---

## Zitate aus dem aktuellen Code

### Type-Check in `IdeOeffnenService` (die Leaky Abstraction)

```csharp
// Zeilen 60–72
if (plugin is VisualStudioIdePlugin && waehleSolutionAsync is not null)
{
    var solutionPfade = FindeSolutions(repositoryPath);
    if (solutionPfade.Count > 1)
    {
        var solutionPfad = await waehleSolutionAsync(solutionPfade, ct);
        if (solutionPfad is null)
            return;

        OeffneSolution(solutionPfad);
        return;
    }
}

await plugin.OpenRepositoryAsync(repositoryPath, ct);
```

Dieser Code enthält:
1. Type-Diskriminierung (`is VisualStudioIdePlugin`) — gegen Abstraktionsprinzipien
2. Hardcodierte Solution-Suche innerhalb des Service (statt im Plugin)
3. Spezialbehandlung nur für VS, nicht generalisierbar für künftige Plugins mit Mehreinstiegspunkten

---

## Implementierungshinweise

### Callback-Signatur-Änderung

**Alt:**
```csharp
Func<IReadOnlyList<string>, CancellationToken, Task<string?>>
```
Parameter: Solution-Pfade (Strings)

**Neu:**
```csharp
Func<IReadOnlyList<IdeEntryPoint>, CancellationToken, Task<IdeEntryPoint?>>
```
Parameter: `IdeEntryPoint`-Objekte (Path + optional DisplayName)

Diese Änderung propagiert bis zu:
- `TaskDetailViewModel.OeffneIdeAsync()` — muss `waehleSolutionAsync` Parameter anpassen
- Alle Callbacks in Tests — müssen `IdeEntryPoint` statt `string` verwenden

### Umfang der Refactoring-Arbeit

Basierend auf der Anforderung und dieser Bestandsaufnahme:
- **Code-Änderungen:** ~150–200 Zeilen (zwei Plugin-Methoden + Service-Logik + Value Object)
- **Testabdeckung:** ~200–300 Zeilen (Tests für neue Methoden + Test-Anpassungen)
- **Dokumentation:** ~50–80 Zeilen (Architektur + Datenfluss aktualisieren)
- **Gesamtaufwand:** Kleine, fokussierte Refactoring-Aufgabe (1–2 Lifecycle-Läufe möglich)

---

## Nächste Schritte

1. **Anforderung freigeben** — Diese Bestandsaufnahme dokumentiert alle notwendigen Code-Änderungen
2. **Implementierung planen** — Details siehe `requirement.md` (Implementierungsansatz, Test-Strategie)
3. **Code-Implementierung** — Siehe [Plan](plan.md) (falls erstellt)
