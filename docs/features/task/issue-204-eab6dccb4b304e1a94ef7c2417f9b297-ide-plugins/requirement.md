# Anforderungsübersetzung: Generalisierung des IDE-Plugin-Mehreinstiegspunkt-Vertrags

**Status:** Optionale Architektur-Verbesserung (keine Fehlerkorrektur, kein Blocker)  
**Anlass:** Follow-up-Idee des Anwenders nach Commit des IDE-Plugin-Features  
**Entscheidung:** Noch offen — Abwägung zwischen Interface-Wachstum und Code-Bereinigung  

---

## Fachliche Zusammenfassung

Der IDE-Plugin-Vertrag (`IIdePlugin`) wird um generische Unterstützung für mehrere Einstiegspunkte erweitert. Dieser Schritt entfernt einen Type-Check-basierten Workaround (`plugin is VisualStudioIdePlugin`) in `IdeOeffnenService.OpenRepositoryInIdeAsync`, der aktuell hardcodiert prüft, ob mehrere Solution-Dateien vorhanden sind und diese Auswahl einem UI-Callback überlässt. Durch die Ergänzung des Plugin-Contracts um zwei Methoden (`FindEntryPointsAsync` und `OpenEntryPointAsync`) wird die Entscheidungslogik für Mehreinstiegspunkte generisch und erweiterbar — ohne Typ-Diskriminierung im Service-Code.

---

## Betroffene Klassen und Komponenten

### Interfaces
- `IIdePlugin` (in `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/`)
  - Neue Methode: `Task<IReadOnlyList<IdeEntryPoint>> FindEntryPointsAsync(string repositoryPath, CancellationToken ct)` — liefert 0..n Kandidaten pro Repository
  - Neue Methode: `Task OpenEntryPointAsync(IdeEntryPoint entryPoint, CancellationToken ct)` — öffnet einen konkreten Einstiegspunkt
  - Ev. neue Klasse: `IdeEntryPoint` (Value Object für den Einstiegspunkt; minimalistisch: Pfad + optionale Beschreibung)

### Plugin-Implementierungen
- `VisualStudioIdePlugin` (in `src/Softwareschmiede/Domain/PluginImpl/`)
  - Implementiert die neuen Methoden:
    - `FindEntryPointsAsync`: gibt gefundene `.sln`/`.slnx`-Dateien (gefunden via `FindSolutionFiles`) als `IdeEntryPoint`-Objekte zurück
    - `OpenEntryPointAsync`: öffnet die übergebene Solution via `OpenSolutionFile`

- `VisualStudioCodeIdePlugin` (in `src/Softwareschmiede/Domain/PluginImpl/`)
  - Implementiert die neuen Methoden:
    - `FindEntryPointsAsync`: gibt genau einen Einstiegspunkt zurück (das Repository-Root selbst)
    - `OpenEntryPointAsync`: öffnet das Repository via `OpenDirectory`

### Service-Klassen
- `IdeOeffnenService` (in `src/Softwareschmiede/Application/Services/`)
  - `OpenRepositoryInIdeAsync`: Wird generalisiert
    - Ruft immer `FindEntryPointsAsync` auf (nicht mehr: Typ-Check `is VisualStudioIdePlugin`)
    - Bei genau 1 Kandidat: direkt öffnen via `OpenEntryPointAsync`
    - Bei >1 Kandidaten: `waehleSolutionAsync`-Callback aufrufen (ggf. umbenennen zu `waehleEntryPointAsync`), dann `OpenEntryPointAsync` mit gewähltem Einstiegspunkt
    - Type-Check entfällt vollständig

### Test-Klassen
- `src/Softwareschmiede.Tests/Application/Services/IdeOeffnenServiceTests.cs`
  - Anpassung: Test-Doubles für `FindEntryPointsAsync` und `OpenEntryPointAsync` statt direkte `OpenRepositoryAsync`-Mocks

- `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioIdePluginTests.cs`
  - Ergänzung: Tests für `FindEntryPointsAsync` (mehrere `.sln`, eine `.sln`, keine `.sln`, Sortierung)
  - Ergänzung: Tests für `OpenEntryPointAsync` mit verschiedenen Pfaden

- `src/Softwareschmiede.Tests/Domain/PluginImpl/VisualStudioCodeIdePluginTests.cs`
  - Ergänzung: Tests für `FindEntryPointsAsync` (immer genau 1)
  - Ergänzung: Tests für `OpenEntryPointAsync`

- `src/Softwareschmiede.Tests/Application/Services/PluginSelectionServiceTests_IdePlugin.cs`
  - Ggf. Anpassung, wenn Testmocks auf Plugin-Methoden reagieren

- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_VisualStudioCode.cs`
  - Anpassung: Callback-Signatur von `waehleSolutionAsync(IReadOnlyList<string>)` → `waehleEntryPointAsync(IReadOnlyList<IdeEntryPoint>)` (falls Parameter-Typ sich ändert)

### Dokumentation
- `docs/help/entwicklungsumgebungen/architektur.md`
  - Beteiligte Komponenten: `IdeEntryPoint` als neuer Value-Object-Typ hinzufügen
  - Datenfluss: Aktualisierung des Datenfluss-Diagramms nach `OpenRepositoryInIdeAsync`
  - Entfernung von Hinweisen auf den `is VisualStudioIdePlugin`-Sonderfall

- `docs/help/entwicklungsumgebungen/ablauf-technisch.md`
  - Sequenzdiagramm oder Pseudocode-Beschreibung: Aktualisierung auf neuen Mehrfach-Einstiegspunkt-Workflow
  - Entfernung von Hinweisen auf den Type-Check

---

## Implementierungsansatz

### Architektur-Erweiterung (top-down)

1. **Value Object `IdeEntryPoint` definieren**
   - Minimalistisches Objekt: `public record IdeEntryPoint(string Path, string? DisplayName = null)`
   - Kann in `Domain/ValueObjects/` oder `Domain/Interfaces/` als Unterdatei von `IIdePlugin` definiert werden

2. **`IIdePlugin` um zwei neue Methoden ergänzen**
   - Methoden mit Standardimplementierung (`Task<IReadOnlyList<IdeEntryPoint>> FindEntryPointsAsync(string repositoryPath, CancellationToken ct) => Task.FromResult(new List<IdeEntryPoint>().AsReadOnly());`)? → Nein, zum Erreichen des Refactoring-Ziels sollten beide Methoden erforderlich sein.
   - Lieber als abstrakte/pure virtuelle Methoden beibehalten, so dass alle Implementierungen bewusst reagieren müssen

3. **`IdeOeffnenService.OpenRepositoryInIdeAsync` generalisieren**
   - Aufruf von `plugin.FindEntryPointsAsync(repositoryPath, ct)` statt Typ-Check
   - Logik-Fluss:
     ```
     entryPoints = await plugin.FindEntryPointsAsync(...)
     if (entryPoints.Count == 0)
         throw new FileNotFoundException(...)  // Keine Kandidaten
     else if (entryPoints.Count == 1)
         await plugin.OpenEntryPointAsync(entryPoints[0], ct)  // Direkt öffnen
     else if (waehleSolutionAsync is not null)
         selectedEntryPoint = await waehleSolutionAsync(entryPoints, ct)
         if (selectedEntryPoint is not null)
             await plugin.OpenEntryPointAsync(selectedEntryPoint, ct)
         else
             return  // Nutzer hat abgebrochen
     else
         // Fallback: erste Kandidaten öffnen
         await plugin.OpenEntryPointAsync(entryPoints[0], ct)
     ```

4. **Plugin-Implementierungen anpassen**
   - `VisualStudioIdePlugin`:
     - `FindEntryPointsAsync`: Rufe `FindSolutionFiles` auf, konvertiere zu `IdeEntryPoint`-Liste
     - `OpenEntryPointAsync`: Rufe `OpenSolutionFile` auf (wie bisher)
   
   - `VisualStudioCodeIdePlugin`:
     - `FindEntryPointsAsync`: Gib `[new IdeEntryPoint(repositoryPath, "Visual Studio Code")]` zurück
     - `OpenEntryPointAsync`: Rufe `OpenDirectory` auf (wie bisher)

5. **Callback-Signatur anpassen** (optional, je nach Rückwärtskompabilität)
   - `waehleSolutionAsync(IReadOnlyList<string>, CancellationToken)` → `waehleEntryPointAsync(IReadOnlyList<IdeEntryPoint>, CancellationToken)`
   - Ggf. beide Signaturen zunächst parallel unterstützen mit Deprecation-Warnung

### Test-Strategie

- **Unit-Tests:** Für `FindEntryPointsAsync` und `OpenEntryPointAsync` in den jeweiligen Plugin-Test-Klassen
- **Integration-Tests:** `IdeOeffnenServiceTests` überprüft den kompletten Fluss mit verschiedenen Szenarien (0 Einstiegspunkte, 1, >1)
- **E2E-Tests:** `TaskDetailViewModelTests_VisualStudioCode` überprüft UI-Integration (Callback-Aufruf bei mehreren Kandidaten)

---

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Das Feature ist rein Service-seitig und nicht Endbenutzerkonfigurierbar.

---

## Offene Fragen / Kritische Abwägung

### Entscheidungspunkt: Interface-Wachstum vs. Type-Check-Eliminierung

**Abwägung (bereits mit Anwender diskutiert, Entscheidung noch offen):**

- **Variante A: Implementieren (beide Methoden mandatory)**
  - **Kosten:** Alle IDE-Plugins müssen zwei Methoden statt einer implementieren (auch triviale wie VS Code)
  - **Nutzen:** Vollständig generischer Code ohne Typ-Diskriminierung; erweiterbar für künftige Plugins mit echten Mehreinstiegspunkte-Szenarien (z. B. mehrere `.csproj`/Workspace-Dateien)
  - **Wartung:** Klarer und testbarer; keine Sonder-Code-Paths in `IdeOeffnenService`

- **Variante B: Nicht implementieren (Status quo halten)**
  - **Kosten:** Type-Check bleibt; neuer Mehrfach-Kandidaten-Support erfordert später erneut Workarounds
  - **Nutzen:** Minimale Änderung; VS Code bleibt einfach
  - **Wartung:** Leaky Abstraction bleibt, schwerer zu erweitern

**Empfehlung:** Variante A ist langfristig sauberer, da sie die Abstraction explizit macht. Ein neues Plugin, das mehrere Kandidaten braucht, führt heute zwangsläufig zu ein Duplikat des Workarounds.

### Weitere klärungspunkte (vor Implementierung)

1. **`IdeEntryPoint`-Typ:** Soll es ein `record`, eine Klasse oder ein Interface sein?
   - Empfehlung: `record IdeEntryPoint(string Path, string? DisplayName = null)` für Einfachheit

2. **Fehlerbehandlung bei 0 Einstiegspunkten:**
   - Soll `FindEntryPointsAsync` eine leere Liste oder eine Exception zurückgeben?
   - Empfehlung: Leere Liste; `IdeOeffnenService` entscheidet, ob das kritisch ist

3. **Rückwärtskompatibilität der Callback-Signatur:**
   - Soll das alte `waehleSolutionAsync(IReadOnlyList<string>)` weiter unterstützt werden?
   - Empfehlung: Migration in einem Schwung; keine parallele Unterstützung

---

## Implementierungsumfang (Schätzung)

- **Code-Änderungen:** ~150–200 Zeilen (zwei Plugin-Methoden + Service-Logik + Value Object)
- **Testabdeckung:** ~200–300 Zeilen (Tests für neue Methoden + bestehende Test-Anpassung)
- **Dokumentation:** ~50–80 Zeilen (Architektur + Datenfluss-Diagramm aktualisieren)
- **Gesamtaufwand:** Kleine, fokussierte Refactoring-Aufgabe (1–2 Lifecycle-Läufe möglich)
