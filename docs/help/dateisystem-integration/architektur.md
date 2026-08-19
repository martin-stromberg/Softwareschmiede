← [Zurück zur Übersicht](index.md)

# Dateisystem-Integration — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|------------|-----|-------|
| `IProzessStarter` | Interface (Domain.Interfaces) | Gateway-Abstraktion für Prozessstart; entkoppelt `System.Diagnostics.Process` von der Domain-Logik. |
| `ProzessStartAnfrage` | Value Object (Domain.ValueObjects) | Kapselt Prozessstart-Parameter (`DateiName`, `Argumente`, `ShellAusfuehren`) ohne System.Diagnostics-Abhängigkeit. |
| `SystemProzessStarter` | Klasse (Infrastructure.Services) | Reale Implementierung von `IProzessStarter`; mappt auf `ProcessStartInfo` und ruft `Process.Start()` auf. |
| `AufzeichnenderProzessStarter` | Klasse (Infrastructure.Services) | Test-Implementierung von `IProzessStarter`; schreibt `ProzessStartAnfrage` in Logdatei statt echte Prozesse zu starten. |
| `WorkingDirectoryResolver` | Klasse (Application.Services, statisch) | Löst das effektive Arbeitsverzeichnis auf durch Kombination von Repository-Root mit optionalem konfiguriertem `WorkingDirectoryRelativePath`; validiert das Ergebnis. Stellt `ResolveEffectiveWorkingDirectory()` (sync) und `DetermineEffectiveWorkingDirectoryAsync()` (async) zur Verfügung. |
| `ArbeitsverzeichnisOeffnenService` | Klasse (Application.Services) | Löst Plattformbefehl auf (Windows/Linux/macOS) und delegiert Prozessstart mit aufgelöstem Arbeitsverzeichnis. |
| `IVisualStudioCodeLocator` | Interface (Application.Services) | Abstraktion zur Auflösung eines startbaren Visual-Studio-Code-Befehls. |
| `VisualStudioCodeLocator` | Klasse (Infrastructure.Services) | Sucht `code.cmd`/`code` in `PATH` und typischen Windows-Installationspfaden. |
| `TaskDetailViewModel.OeffneIdeInternAsync` | Methode (App.ViewModels) | Löst über `PluginSelectionService.ResolveIdePluginAsync()` das zuständige `IIdePlugin` auf und ruft `IIdePlugin.FindEntryPointsAsync()` auf um 0..n `IdeEntryPoint`-Kandidaten zu ermitteln; verzweigt nach deren Anzahl (0 → Exception, 1 → direkt öffnen, >1 → Dialog-Callback) und öffnet den gewählten Einstiegspunkt über `plugin.OpenEntryPointAsync()`. |
| `PluginSelectionService` | Klasse (Application.Services) | Löst über `ResolveIdePluginAsync(repositoryPath, ct)` das für das Arbeitsverzeichnis zuständige `IIdePlugin` auf: erstes explizit kompatibles Plugin gewinnt, sonst erstes fallback-kompatibles aktives Plugin, sonst `IPluginManager.GetDefaultIdePlugin()`. |
| `IIdePlugin` | Interface (Domain.Interfaces) | Vertrag für IDE-Plugins: `CheckCompatibilityAsync()` (Explicit/Fallback/Incompatible) sowie das generische Mehreinstiegspunkt-Paar `FindEntryPointsAsync()`/`OpenEntryPointAsync()`. |
| `IdeEntryPoint` | Value Object (Plugin.Contracts.Domain.ValueObjects) | Immutabler Datenträger für einen konkreten IDE-Einstiegspunkt (`Path`, optional `DisplayName`). |
| `VisualStudioIdePlugin` | Klasse (Domain.PluginImpl) | Eingebautes IDE-Plugin für Visual Studio; `Explicit`-kompatibel bei vorhandener `.sln`/`.slnx`-Datei; `FindEntryPointsAsync()` liefert je gefundener Solution-Datei einen `IdeEntryPoint`, `OpenEntryPointAsync()` öffnet den gewählten Einstiegspunkt per Shell-Execute. |
| `VisualStudioCodeIdePlugin` | Klasse (Domain.PluginImpl) | Eingebautes IDE-Plugin für Visual Studio Code; immer `Fallback`-kompatibel; `FindEntryPointsAsync()` liefert immer genau einen `IdeEntryPoint` (das Repository-Root), `OpenEntryPointAsync()` öffnet das Arbeitsverzeichnis über `IVisualStudioCodeLocator`/`code`. |
| `TaskDetailViewModel` | Klasse (App.ViewModels) | Stellt Commands bereit und koordiniert Dialog/Service-Aufrufe; nutzt `WorkingDirectoryResolver` zur Auflösung des Arbeitsverzeichnisses; `OeffneIdeInternAsync()` löst Plugin und Einstiegspunkte über `ErmittleIdeEntryPointsAsync()` auf und übergibt bei mehreren `IdeEntryPoint`-Kandidaten (nur über den Dropdown-Button) einen Auswahl-Callback (`WaehleEntryPointAsync`) für den Solution-Auswahl-Dialog. |
| `IDialogService` / `WpfDialogService` | Interface / Klasse (App.Services) | Dialog-Gateway; implementiert `ShowSolutionSelectionDialogAsync()`. |
| `SolutionSelectionDialog` | WPF-Window (App.Views) | Modales Fenster für Solution-Auswahl bei mehreren Dateien. |
| `SolutionSelectionDialogViewModel` | Klasse (App.ViewModels) | Presentation Model für Dialog; verwaltet Solution-Liste und Benutzer-Auswahl. |

## Abhängigkeiten

```
Domain-Schicht (Abstraktion):
├─ IProzessStarter (Interface, kein konkreter Service)
└─ ProzessStartAnfrage (Value Object, unabhängig)

Infrastructure-Schicht (Reale / Test-Implementierung):
├─ SystemProzessStarter (implementiert IProzessStarter)
└─ AufzeichnenderProzessStarter (implementiert IProzessStarter)

Application-Schicht (Services):
├─ WorkingDirectoryResolver (statische Utility-Klasse)
│  ├─ `ResolveEffectiveWorkingDirectory()` (sync, für UI-Caching)
│  ├─ `DetermineEffectiveWorkingDirectoryAsync()` (async, für Command-Handler)
│  └─ `ValidateWorkingDirectory()` (Validierung und Fehlerbehandlung)
├─ ArbeitsverzeichnisOeffnenService
│  └─ Abhängigkeit: IProzessStarter
├─ PluginSelectionService
│  ├─ Abhängigkeit: IPluginManager (liefert u. a. VisualStudioIdePlugin, VisualStudioCodeIdePlugin)
│  └─ Abhängigkeit: PluginActivationService (aktivierte IDE-Plugins, Reihenfolge über plugins.ide.order)
└─ (keine direkten DB/Repository-Abhängigkeiten von ArbeitsverzeichnisOeffnenService)

App-Schicht (UI/ViewModels):
├─ TaskDetailViewModel
│  ├─ WorkingDirectoryResolver (zur Auflösung des Arbeitsverzeichnisses)
│  ├─ ArbeitsverzeichnisOeffnenService
│  ├─ PluginSelectionService (zur Auflösung des IDE-Plugins)
│  ├─ IDialogService (zeigt Dialog bei mehreren IdeEntryPoint-Kandidaten)
│  └─ Commands: OeffneIdeCommand, OeffneIdeAuswahlCommand (Split-Button-Muster)
├─ WpfDialogService (implementiert IDialogService)
│  └─ Erstellt SolutionSelectionDialog und SolutionSelectionDialogViewModel
├─ SolutionSelectionDialog (XAML)
│  └─ DataContext: SolutionSelectionDialogViewModel
└─ RibbonSplitButton (neue WPF-Komponente)
   └─ Bindet OeffneIdeCommand und OeffneIdeAuswahlCommand
```

Sicherheitsrichtlinien für `IProzessStarter`:
- Plattformabhängige Prozessstart-Logik bleibt in `SystemProzessStarter` (Infrastructure).
- Die Domain-Schicht (Services wie `ArbeitsverzeichnisOeffnenService`) kennt nur `IProzessStarter`.
- Test-Implementierung (`AufzeichnenderProzessStarter`) wird via Dependency Injection in `App.xaml.cs` getauscht (ähnlich `IPseudoConsoleProcessLauncher`).

## Datenfluss

### Arbeitsverzeichnis öffnen

```
Benutzer klickt Button
  ↓
TaskDetailViewModel.OeffneArbeitsverzeichnisCommand
  ↓
OeffneArbeitsverzeichnisAsync() Methode (async)
  ↓
WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()
  ├─ Kombiniert LokalerKlonPfad + WorkingDirectoryRelativePath
  ├─ Validiert das Ergebnis (existiert, ist erreichbar)
  └─ Rückgabe: aufgelöstes Arbeitsverzeichnis (z. B. C:\repo\src\backend)
  ↓
ArbeitsverzeichnisOeffnenService.Oeffne(effectiveWorkdir)
  ↓
Plattformbefehl auflösen (Explorer/xdg-open/open mit aufgelöstem Pfad)
  ↓
ProzessStartAnfrage erstellen
  ↓
IProzessStarter.Starten(anfrage)
  ├─→ SystemProzessStarter (Production)
  │   ↓
  │   Process.Start() mit aufgelöstem Pfad
  │   ↓
  │   OS-Dateiexplorer öffnet aufgelöstes Verzeichnis
  │
  └─→ AufzeichnenderProzessStarter (Test)
      ↓
      Logdatei schreiben (prozess-starts.log mit aufgelöstem Pfad)
```

### IDE öffnen (Haupt-Button — direkt öffnen)

```
Benutzer klickt Haupt-Button des Split-Buttons (aktiv sobald ShowFileExplorerPanel true ist)
  ↓
TaskDetailViewModel.OeffneIdeCommand.OeffneIdeAsync()
  ↓
WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()
  └─ Rückgabe: aufgelöstes Arbeitsverzeichnis (z. B. C:\repo\src\backend)
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
        └─→ Treffer: ProzessStartAnfrage für code "<Pfad des Einstiegspunkts>"
              ↓
              IProzessStarter.Starten(anfrage)
                ├─→ SystemProzessStarter: Process.Start()
                └─→ AufzeichnenderProzessStarter (Test): Logdatei schreiben
              ↓
              IDE öffnet den Einstiegspunkt (Arbeitsverzeichnis / Solution)
```

### IDE öffnen (Dropdown-Button — Auswahl-Dialog)

```
Benutzer klickt Dropdown-Button des Split-Buttons (nur sichtbar bei ≥2 Einstiegspunkten)
  ↓
TaskDetailViewModel.OeffneIdeAuswahlCommand.OeffneIdeAuswahlAsync()
  ↓
WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()
  └─ Rückgabe: aufgelöstes Arbeitsverzeichnis
  ↓
PluginSelectionService.ResolveIdePluginAsync(effectiveWorkdir, ct)
  ├─ Prüft alle aktivierten IDE-Plugins (Reihenfolge aus plugins.ide.order) via CheckCompatibilityAsync()
  └─ Rückgabe: aufgelöstes Plugin
  ↓
plugin.FindEntryPointsAsync(effectiveWorkdir, ct)
  └─ Rückgabe: alle gefundenen IdeEntryPoint-Kandidaten
  ↓
Anzahl gefundener Einstiegspunkte?
  ├─→ 0: FileNotFoundException
  │
  ├─→ Genau 1: sofort weiter zu plugin.OpenEntryPointAsync() (Dialog nicht nötig)
  │
  └─→ Mehr als 1:
        ↓
        TaskDetailViewModel.waehleEntryPointAsync(entryPoints, ct) — Dialog-Callback
          ↓
          IDialogService.ShowSolutionSelectionDialogAsync(entryPoints.Select(ep => ep.Path oder DisplayName))
            ↓
            WpfDialogService (UI-Thread) → SolutionSelectionDialog (Modal) → SolutionSelectionDialogViewModel
              ↓
              Benutzer wählt Einstiegspunkt oder bricht ab → Rückgabe: Pfad oder null
          ↓
          Callback bildet Pfad zurück auf passenden IdeEntryPoint ab → Rückgabe: IdeEntryPoint oder null
        ↓
        null? → Ablauf endet, kein Prozessstart (Abbruch durch Benutzer)
        IdeEntryPoint? → weiter zu plugin.OpenEntryPointAsync()
  ↓
plugin.OpenEntryPointAsync(entryPoint, ct)
  └─ IDE öffnet den gewählten Einstiegspunkt
```

Welche IDE-Plugins aktiv sind und in welcher Reihenfolge sie geprüft werden, konfigurieren Anwender über **Einstellungen → Plugins → Integrierte Entwicklungsumgebungen (IDE)** (`PluginActivationService`, Setting `plugins.ide.order`). Mindestens ein IDE-Plugin bleibt dabei stets aktiv, sodass `ResolveIdePluginAsync` nie ohne Ergebnis zurückkehrt.

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
    
    RB["RibbonSplitButton"] -->|binds to OeffneIdeCommand| G
    RB -->|binds to OeffneIdeAuswahlCommand| G
    RB -->|binds KannIdeAuswaehlen| G
```

## Skalierung und Zuverlässigkeit

### Fehlertoleranz

- **Prozessstart-Fehler:** Vollständig abgefangen und geloggt. Fehler blockiert nicht die Anwendung.
- **Dateisuche-Fehler:** `VisualStudioIdePlugin.FindEntryPointsAsync()` gibt eine leere Liste bei jedem Fehler zurück (sicherer Fallback); `TaskDetailViewModel.OeffneIdeInternAsync()` wirft in diesem Fall eine aussagekräftige `FileNotFoundException`.
- **Dialog-Abbruch:** Normales Verhalten, keine Fehlerbehandlung erforderlich.

### Caching und Performance

- **`CanExecute` ohne Vorab-Suche:** `OeffneIdeCommand.CanExecute`/`OeffneIdeAuswahlCommand.CanExecute` hängen nur von `ShowFileExplorerPanel` (vorhandenes Arbeitsverzeichnis) ab, nicht von einer Einstiegspunkt-Suche.
- **Einmalige Vorab-Ermittlung nur für Dropdown-Sichtbarkeit:** Am Ende von `LadenAsync()` ruft `AktualisiereKannIdeAuswaehlenAsync()` einmalig `ErmittleIdeEntryPointsAsync()` auf (Plugin-Kompatibilitätsprüfung + `plugin.FindEntryPointsAsync()`), um `KannIdeAuswaehlen` zu setzen und damit die Sichtbarkeit des Dropdown-Teils des Split-Buttons zu bestimmen — ohne dabei etwas zu öffnen. Die eigentliche Öffnen-Aktion beim Klick führt dieselbe Ermittlung erneut aus (kein Zwischenspeichern der Einstiegspunkte).
- **Typischerweise schnell:** Für ein durchschnittliches Repository mit 1–5 Solutions dauert `VisualStudioIdePlugin.FindEntryPointsAsync()` < 10 ms.
- **Keine rekursive Suche:** Verhindert Performance-Degradation in großen Verzeichnisstrukturen.

### Test-Isolation

- **Separate Logdatei:** Test-Prozessstart-Anfragen werden in `prozess-starts.log` neben der Test-DB aufgezeichnet, nicht in der Production-Log.
- **Keine echten Prozesse:** `AufzeichnenderProzessStarter` startet nie echte Prozesse, isoliert Tests vollständig.
- **Unkritisch für Parallelisierung:** Mehrere Tests können gleichzeitig laufen, da jeder Test seine eigene Testdatenbank und Logdatei hat.
