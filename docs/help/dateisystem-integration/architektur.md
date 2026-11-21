← [Zurück zur Übersicht](index.md)

# Dateisystem-Integration — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|------------|-----|-------|
| `IProzessStarter` | Interface (Domain.Interfaces) | Gateway-Abstraktion für Prozessstart; entkoppelt `System.Diagnostics.Process` von der Domain-Logik. |
| `ProcessStartRequest` | Value Object (Domain.ValueObjects) | Kapselt Prozessstart-Parameter (`DateiName`, `Argumente`, `ShellAusfuehren`) ohne System.Diagnostics-Abhängigkeit. |
| `SystemProcessStarter` | Klasse (Infrastructure.Services) | Reale Implementierung von `IProzessStarter`; mappt auf `ProcessStartInfo` und ruft `Process.Start()` auf. |
| `RecordingProcessStarter` | Klasse (Infrastructure.Services) | Test-Implementierung von `IProzessStarter`; schreibt `ProcessStartRequest` in Logdatei statt echte Prozesse zu starten. |
| `WorkingDirectoryResolver` | Klasse (Application.Services, statisch) | Löst das effektive Arbeitsverzeichnis auf durch Kombination von Repository-Root mit optionalem konfiguriertem `WorkingDirectoryRelativePath`; validiert das Ergebnis. Stellt `ResolveEffectiveWorkingDirectory()` (sync) und `DetermineEffectiveWorkingDirectoryAsync()` (async) zur Verfügung. |
| `WorkingDirectoryOpenService` | Klasse (Application.Services) | Löst Plattformbefehl auf (Windows/Linux/macOS) und delegiert Prozessstart mit aufgelöstem Arbeitsverzeichnis. |
| `IVisualStudioCodeLocator` | Interface (Application.Services) | Abstraktion zur Auflösung eines startbaren Visual-Studio-Code-Befehls. |
| `VisualStudioCodeLocator` | Klasse (Infrastructure.Services) | Sucht `code.cmd`/`code` in `PATH` und typischen Windows-Installationspfaden. |
| `TaskDetailViewModel.OeffneIdeInternAsync` | Methode (App.ViewModels) | Gemeinsame Implementierung für Haupt- und Dropdown-Button. Haupt-Button (`waehleEntryPointAsync is null`): löst über `ResolveIdeEntryPointsAsync()`/`PluginSelectionService.ResolveIdePluginAsync()` **ein** zuständiges `IIdePlugin` auf und ruft dessen `IIdePlugin.FindEntryPointsAsync()` auf; öffnet bei ≥1 Einstiegspunkt direkt den ersten. Dropdown-Button: löst über `ResolveAggregatedIdeEntryPointsAsync()`/`PluginSelectionService.ResolveAllCompatibleIdePluginsAsync()` **alle** kompatiblen `IIdePlugin`s auf, aggregiert deren `FindEntryPointsAsync()`-Ergebnisse zu `(Plugin, EntryPoint)`-Tupeln; verzweigt nach deren Gesamtanzahl (0 → Exception, 1 → direkt öffnen, >1 → Dialog-Callback) und öffnet den gewählten Einstiegspunkt über das zugehörige `plugin.OpenEntryPointAsync()`. |
| `PluginSelectionService` | Klasse (Application.Services) | Löst über `ResolveIdePluginAsync(repositoryPath, ct)` das eine für das Arbeitsverzeichnis zuständige, priorisierte `IIdePlugin` auf: erstes explizit kompatibles Plugin gewinnt, sonst erstes fallback-kompatibles aktives Plugin, sonst `IPluginManager.GetDefaultIdePlugin()`. Zusätzlich liefert `ResolveAlleKompatiblenIdePluginsAsync(repositoryPath, ct)` **alle** aktivierten, explizit oder fallback-kompatiblen `IIdePlugin`s als sortierte Liste (erst alle Explicit-, dann alle Fallback-kompatiblen, jeweils in konfigurierter `plugins.ide.order`-Reihenfolge) — genutzt vom Dropdown-Button, um Einstiegspunkte über mehrere Plugins hinweg zu aggregieren. Beide Methoden teilen sich die private Hilfsmethode `GetOrderedEnabledIdePluginsAsync()`. |
| `IIdePlugin` | Interface (Domain.Interfaces) | Vertrag für IDE-Plugins: `CheckCompatibilityAsync()` (Explicit/Fallback/Incompatible) sowie das generische Mehreinstiegspunkt-Paar `FindEntryPointsAsync()`/`OpenEntryPointAsync()`. |
| `IdeEntryPoint` | Value Object (Plugin.Contracts.Domain.ValueObjects) | Immutabler Datenträger für einen konkreten IDE-Einstiegspunkt (`Path`, optional `DisplayName`). |
| `VisualStudioIdePlugin` | Klasse (Domain.PluginImpl) | Eingebautes IDE-Plugin für Visual Studio; `Explicit`-kompatibel bei vorhandener `.sln`/`.slnx`-Datei; `FindEntryPointsAsync()` liefert je gefundener Solution-Datei einen `IdeEntryPoint`, `OpenEntryPointAsync()` öffnet den gewählten Einstiegspunkt per Shell-Execute. |
| `VisualStudioCodeIdePlugin` | Klasse (Domain.PluginImpl) | Eingebautes IDE-Plugin für Visual Studio Code; immer `Fallback`-kompatibel; `FindEntryPointsAsync()` liefert immer genau einen `IdeEntryPoint` (das Repository-Root), `OpenEntryPointAsync()` öffnet das Arbeitsverzeichnis über `IVisualStudioCodeLocator`/`code`. |
| `TaskDetailViewModel` | Klasse (App.ViewModels) | Stellt Commands bereit und koordiniert Dialog/Service-Aufrufe; nutzt `WorkingDirectoryResolver` zur Auflösung des Arbeitsverzeichnisses; `OeffneIdeInternAsync()` löst für den Haupt-Button Plugin und Einstiegspunkte über `ResolveIdeEntryPointsAsync()` (Single-Plugin) auf, für den Dropdown-Button über `ResolveAggregatedIdeEntryPointsAsync()` (Multi-Plugin-Aggregation über alle kompatiblen Plugins) — und übergibt bei mehreren aggregierten `(Plugin, EntryPoint)`-Kandidaten (nur über den Dropdown-Button) einen Auswahl-Callback (`SelectEntryPointAsync`) für den Solution-Auswahl-Dialog, der die Kandidaten über `FormatDisplayValue()` plugin-qualifiziert anzeigt. |
| `IDialogService` / `WpfDialogService` | Interface / Klasse (App.Services) | Dialog-Gateway; implementiert `ShowSolutionSelectionDialogAsync()`. |
| `SolutionSelectionDialog` | WPF-Window (App.Views) | Modales Fenster für Solution-Auswahl bei mehreren Dateien. |
| `SolutionSelectionDialogViewModel` | Klasse (App.ViewModels) | Presentation Model für Dialog; verwaltet Solution-Liste und Benutzer-Auswahl. |

## Abhängigkeiten

```
Domain-Schicht (Abstraktion):
├─ IProzessStarter (Interface, kein konkreter Service)
└─ ProcessStartRequest (Value Object, unabhängig)

Infrastructure-Schicht (Reale / Test-Implementierung):
├─ SystemProzessStarter (implementiert IProzessStarter)
└─ RecordingProcessStarter (implementiert IProzessStarter)

Application-Schicht (Services):
├─ WorkingDirectoryResolver (statische Utility-Klasse)
│  ├─ `ResolveEffectiveWorkingDirectory()` (sync, für UI-Caching)
│  ├─ `DetermineEffectiveWorkingDirectoryAsync()` (async, für Command-Handler)
│  └─ `ValidateWorkingDirectory()` (Validierung und Fehlerbehandlung)
├─ WorkingDirectoryOpenService
│  └─ Abhängigkeit: IProzessStarter
├─ PluginSelectionService
│  ├─ Abhängigkeit: IPluginManager (liefert u. a. VisualStudioIdePlugin, VisualStudioCodeIdePlugin)
│  ├─ Abhängigkeit: PluginActivationService (aktivierte IDE-Plugins, Reihenfolge über plugins.ide.order)
│  ├─ ResolveIdePluginAsync() → ein priorisiertes Plugin (Haupt-Button)
│  └─ ResolveAlleKompatiblenIdePluginsAsync() → alle kompatiblen Plugins (Dropdown-Button)
└─ (keine direkten DB/Repository-Abhängigkeiten von WorkingDirectoryOpenService)

App-Schicht (UI/ViewModels):
├─ TaskDetailViewModel
│  ├─ WorkingDirectoryResolver (zur Auflösung des WorkingDirectoryses)
│  ├─ WorkingDirectoryOpenService
│  ├─ PluginSelectionService (zur Auflösung des IDE-Plugins)
│  ├─ IDialogService (zeigt Dialog bei mehreren IdeEntryPoint-Kandidaten)
│  └─ Commands: OpenIdeCommand, OpenIdeSelectionCommand (Split-Button-Muster)
├─ WpfDialogService (implementiert IDialogService)
│  └─ Erstellt SolutionSelectionDialog und SolutionSelectionDialogViewModel
├─ SolutionSelectionDialog (XAML)
│  └─ DataContext: SolutionSelectionDialogViewModel
└─ RibbonSplitButton (neue WPF-Komponente)
   └─ Bindet OpenIdeCommand und OpenIdeSelectionCommand
```

Sicherheitsrichtlinien für `IProzessStarter`:
- Plattformabhängige Prozessstart-Logik bleibt in `SystemProcessStarter` (Infrastructure).
- Die Domain-Schicht (Services wie `WorkingDirectoryOpenService`) kennt nur `IProzessStarter`.
- Test-Implementierung (`RecordingProcessStarter`) wird via Dependency Injection in `App.xaml.cs` getauscht (ähnlich `IPseudoConsoleProcessLauncher`).

## Datenfluss

### Arbeitsverzeichnis öffnen

```
Benutzer klickt Button
  ↓
TaskDetailViewModel.OeffneWorkingDirectoryCommand
  ↓
OeffneWorkingDirectoryAsync() Methode (async)
  ↓
WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()
  ├─ Kombiniert LocalClonePath + WorkingDirectoryRelativePath
  ├─ Validiert das Ergebnis (existiert, ist erreichbar)
  └─ Rückgabe: aufgelöstes WorkingDirectory (z. B. C:\repo\src\backend)
  ↓
WorkingDirectoryOpenService.Oeffne(effectiveWorkdir)
  ↓
Plattformbefehl auflösen (Explorer/xdg-open/open mit aufgelöstem Pfad)
  ↓
ProcessStartRequest erstellen
  ↓
IProcessStarter.Start(anfrage)
  ├─→ SystemProzessStarter (Production)
  │   ↓
  │   Process.Start() mit aufgelöstem Pfad
  │   ↓
  │   OS-Dateiexplorer öffnet aufgelöstes Verzeichnis
  │
  └─→ RecordingProcessStarter (Test)
      ↓
      Logdatei schreiben (prozess-starts.log mit aufgelöstem Pfad)
```

### IDE öffnen (Haupt-Button — direkt öffnen)

```
Benutzer klickt Haupt-Button des Split-Buttons (aktiv sobald ShowFileExplorerPanel true ist)
  ↓
TaskDetailViewModel.OpenIdeCommand.OeffneIdeAsync()
  ↓
WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()
  └─ Rückgabe: aufgelöstes WorkingDirectory (z. B. C:\repo\src\backend)
  ↓
PluginSelectionService.ResolveIdePluginAsync(effectiveWorkdir, ct)
  ├─ Prüft alle aktivierten IDE-Plugins (Reihenfolge aus plugins.ide.order) via CheckCompatibilityAsync()
  ├─ Erstes Explicit-kompatibles Plugin gewinnt (z. B. VisualStudioIdePlugin bei gefundener .sln/.slnx)
  ├─ Sonst erstes Fallback-kompatibles aktives Plugin (z. B. VisualStudioCodeIdePlugin, immer Fallback)
  └─ Sonst IPluginManager.GetDefaultIdePlugin()
  ↓
plugin.FindEntryPointsAsync(effectiveWorkdir, ct)  — generisch für jedes IIdePlugin
  ├─→ VisualStudioIdePlugin: ein IdeEntryPoint je gefundener .sln/.slnx-Datei
  └─→ VisualStudioCodeIdePlugin: immer genau ein IdeEntryPoint (Repository-Root)
  ↓
Anzahl gefundener Einstiegspunkte?
  ├─→ 0: FileNotFoundException
  │
  ├─→ Genau 1: sofort weiter zu plugin.OpenEntryPointAsync() (kein Dialog)
  │
  └─→ Mehr als 1: Fallback — erster Einstiegspunkt wird direkt geöffnet, kein Dialog
  ↓
plugin.OpenEntryPointAsync(entryPoint, ct)
  ├─→ VisualStudioIdePlugin: öffnet die Solution-Datei des Einstiegspunkts per Shell-Execute
  └─→ VisualStudioCodeIdePlugin: IVisualStudioCodeLocator.Locate()
        ├─→ Kein Treffer: wirft InvalidOperationException → FehlerMeldung, kein Prozessstart
        └─→ Treffer: ProcessStartRequest für code "<Pfad des Einstiegspunkts>"
              ↓
              IProcessStarter.Start(anfrage)
                ├─→ SystemProzessStarter: Process.Start()
                └─→ RecordingProcessStarter (Test): Logdatei schreiben
              ↓
              IDE öffnet den Einstiegspunkt (WorkingDirectory / Solution)
```

### IDE öffnen (Dropdown-Button — aggregierte Auswahl über alle kompatiblen Plugins)

```
Benutzer klickt Dropdown-Button des Split-Buttons
(nur sichtbar, wenn die AGGREGIERTE Gesamtanzahl an Einstiegspunkten ≥2 ist)
  ↓
TaskDetailViewModel.OpenIdeSelectionCommand.OeffneIdeAuswahlAsync()
  ↓
WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()
  └─ Rückgabe: aufgelöstes WorkingDirectory
  ↓
PluginSelectionService.ResolveAlleKompatiblenIdePluginsAsync(effectiveWorkdir, ct)
  ├─ Prüft ALLE aktivierten IDE-Plugins (Reihenfolge aus plugins.ide.order) via CheckCompatibilityAsync()
  │  — kein früher Abbruch beim ersten Explicit-Treffer wie bei ResolveIdePluginAsync()
  └─ Rückgabe: alle Explicit-kompatiblen Plugins, gefolgt von allen Fallback-kompatiblen Plugins
     (jeweils in konfigurierter Reihenfolge)
  ↓
TaskDetailViewModel.ResolveAggregatedIdeEntryPointsAsync():
für JEDES zurückgegebene Plugin: plugin.FindEntryPointsAsync(effectiveWorkdir, ct)
  ├─ Fehler bei einzelnem Plugin → geloggt, übersprungen (Aggregation läuft weiter)
  └─ Rückgabe: (Plugin, EntryPoint)-Tupel aller Plugins aggregiert, Plugin-/Einstiegspunkt-Reihenfolge erhalten
  ↓
Anzahl aggregierter Einstiegspunkte?
  ├─→ 0: FileNotFoundException
  │
  ├─→ Genau 1: sofort weiter zu plugin.OpenEntryPointAsync() (Dialog nicht nötig)
  │
  └─→ Mehr als 1:
        ↓
        TaskDetailViewModel.waehleEntryPointAsync(entries, ct) — Dialog-Callback
          ↓
          entries.Select(e => FormatiereAnzeigeWert(e.Plugin, e.EntryPoint))
          → „{PluginName}: {Bezeichnung}" bzw. nur „{PluginName}" (falls Bezeichnung == PluginName)
          ↓
          IDialogService.ShowSolutionSelectionDialogAsync(anzeigeWerte)
            ↓
            WpfDialogService (UI-Thread) → SolutionSelectionDialog (Modal) → SolutionSelectionDialogViewModel
              ↓
              Benutzer wählt Anzeigewert oder bricht ab → Rückgabe: Anzeigewert oder null
          ↓
          Callback bildet Anzeigewert über Listenindex zurück auf (Plugin, EntryPoint)-Tupel ab
          → Rückgabe: (Plugin, EntryPoint) oder null
        ↓
        null? → Ablauf endet, kein Prozessstart (Abbruch durch Benutzer)
        (Plugin, EntryPoint)? → weiter zu plugin.OpenEntryPointAsync() — Plugin des gewählten Tupels,
                                 nicht zwingend das für den Haupt-Button priorisierte Plugin
  ↓
plugin.OpenEntryPointAsync(entryPoint, ct)
  └─ Die zum gewählten Eintrag gehörende IDE öffnet den gewählten Einstiegspunkt
```

Welche IDE-Plugins aktiv sind und in welcher Reihenfolge sie geprüft werden, konfigurieren Anwender über **Einstellungen → Plugins → Integrierte Entwicklungsumgebungen (IDE)** (`PluginActivationService`, Setting `plugins.ide.order`). Mindestens ein IDE-Plugin bleibt dabei stets aktiv, sodass weder `ResolveIdePluginAsync` noch `ResolveAllCompatibleIdePluginsAsync` je ohne Ergebnis zurückkehren.

## Diagramm

```mermaid
graph TD
    A["App.xaml.cs<br/>DI-Registrierung"]
    A -->|Environment: Production| B["SystemProzessStarter"]
    A -->|Environment: Test| C["AufzeichnenderProzessStarter"]
    
    B -->|implements| D["IProzessStarter"]
    C -->|implements| D
    
    E["ArbeitsverzeichnisOeffnenService"] -->|uses| D
    L["VisualStudioIdePlugin"] -->|uses| D
    M["VisualStudioCodeIdePlugin"] -->|uses| D
    M -->|uses| N["IVisualStudioCodeLocator"]

    P["PluginSelectionService"] -->|resolves via CheckCompatibilityAsync| L
    P -->|resolves via CheckCompatibilityAsync| M

    G["TaskDetailViewModel"] -->|uses| E
    G -->|uses| P
    G -->|invokes| L
    G -->|invokes| M
    G -->|uses| H["IDialogService"]
    
    I["WpfDialogService"] -->|implements| H
    I -->|creates| J["SolutionSelectionDialog"]
    J -->|DataContext| K["SolutionSelectionDialogViewModel"]
    
    G -->|invokes| J
    G -->|binds to| E
    
    RB["RibbonSplitButton"] -->|binds to OpenIdeCommand| G
    RB -->|binds to OpenIdeSelectionCommand| G
    RB -->|binds SelectCanIde| G
```

## Skalierung und Zuverlässigkeit

### Fehlertoleranz

- **Prozessstart-Fehler:** Vollständig abgefangen und geloggt. Fehler blockiert nicht die Anwendung.
- **Dateisuche-Fehler:** `VisualStudioIdePlugin.FindEntryPointsAsync()` gibt eine leere Liste bei jedem Fehler zurück (sicherer Fallback); `TaskDetailViewModel.OeffneIdeInternAsync()` wirft in diesem Fall eine aussagekräftige `FileNotFoundException`.
- **Dialog-Abbruch:** Normales Verhalten, keine Fehlerbehandlung erforderlich.

### Caching und Performance

- **`CanExecute` ohne Vorab-Suche:** `OpenIdeCommand.CanExecute`/`OpenIdeSelectionCommand.CanExecute` hängen nur von `ShowFileExplorerPanel` (vorhandenes Arbeitsverzeichnis) ab, nicht von einer Einstiegspunkt-Suche.
- **Einmalige Vorab-Ermittlung nur für Dropdown-Sichtbarkeit:** Am Ende von `LoadAsync()` ruft `UpdateCanSelectIdeAsync()` einmalig `ResolveAggregatedIdeEntryPointsAsync()` auf (Kompatibilitätsprüfung + `FindEntryPointsAsync()` **je aktiviertem, kompatiblem Plugin**, nicht nur des einen priorisierten), um `SelectCanIde` anhand der aggregierten Gesamtanzahl zu setzen und damit die Sichtbarkeit des Dropdown-Teils des Split-Buttons zu bestimmen — ohne dabei etwas zu öffnen. Die eigentliche Öffnen-Aktion beim Klick führt für den jeweiligen Button dieselbe Ermittlung erneut aus (Haupt-Button: `ResolveIdeEntryPointsAsync()` für das eine priorisierte Plugin, zusätzlich `ResolveAggregatedIdeEntryPointsAsync()` zur Aktualisierung von `SelectCanIde`; Dropdown-Button: ausschließlich `ResolveAggregatedIdeEntryPointsAsync()`) — kein Zwischenspeichern der Einstiegspunkte.
- **Typischerweise schnell:** Für ein durchschnittliches Repository mit 1–5 Solutions dauert `VisualStudioIdePlugin.FindEntryPointsAsync()` < 10 ms.
- **Keine rekursive Suche:** Verhindert Performance-Degradation in großen Verzeichnisstrukturen.

### Test-Isolation

- **Separate Logdatei:** Test-Prozessstart-Anfragen werden in `prozess-starts.log` neben der Test-DB aufgezeichnet, nicht in der Production-Log.
- **Keine echten Prozesse:** `RecordingProcessStarter` startet nie echte Prozesse, isoliert Tests vollständig.
- **Unkritisch für Parallelisierung:** Mehrere Tests können gleichzeitig laufen, da jeder Test seine eigene Testdatenbank und Logdatei hat.
