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
| `IdeOeffnenService` | Klasse (Application.Services) | Findet `.sln`-Dateien im **aufgelösten Arbeitsverzeichnis** (für die Solution-Auswahl bei mehreren Treffern) und öffnet eine gewählte Solution per Shell-Execute. |
| `PluginSelectionService` | Klasse (Application.Services) | Löst über `ResolveIdePluginAsync(repositoryPath, ct)` das für das Arbeitsverzeichnis zuständige `IIdePlugin` auf: erstes explizit kompatibles Plugin gewinnt, sonst erstes fallback-kompatibles aktives Plugin, sonst `IPluginManager.GetDefaultIdePlugin()`. |
| `IIdePlugin` | Interface (Domain.Interfaces) | Vertrag für IDE-Plugins: `CheckCompatibilityAsync()` (Explicit/Fallback/Incompatible) und `OpenRepositoryAsync()`. |
| `VisualStudioIdePlugin` | Klasse (Domain.PluginImpl) | Eingebautes IDE-Plugin für Visual Studio; `Explicit`-kompatibel bei vorhandener `.sln`/`.slnx`-Datei, öffnet die erste gefundene Solution per Shell-Execute. |
| `VisualStudioCodeIdePlugin` | Klasse (Domain.PluginImpl) | Eingebautes IDE-Plugin für Visual Studio Code; immer `Fallback`-kompatibel, öffnet das Arbeitsverzeichnis über `IVisualStudioCodeLocator`/`code`. |
| `TaskDetailViewModel` | Klasse (App.ViewModels) | Stellt Commands bereit und koordiniert Dialog/Service-Aufrufe; nutzt `WorkingDirectoryResolver` zur Auflösung des Arbeitsverzeichnisses und `PluginSelectionService` zur Auflösung des zu verwendenden IDE-Plugins. |
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
├─ IdeOeffnenService
│  ├─ Abhängigkeit: IProzessStarter
│  └─ Abhängigkeit: IVisualStudioCodeLocator
├─ PluginSelectionService
│  ├─ Abhängigkeit: IPluginManager (liefert u. a. VisualStudioIdePlugin, VisualStudioCodeIdePlugin)
│  └─ Abhängigkeit: PluginActivationService (aktivierte IDE-Plugins, Reihenfolge über plugins.ide.order)
└─ (keine direkten DB/Repository-Abhängigkeiten von ArbeitsverzeichnisOeffnenService/IdeOeffnenService)

App-Schicht (UI/ViewModels):
├─ TaskDetailViewModel
│  ├─ WorkingDirectoryResolver (zur Auflösung des Arbeitsverzeichnisses)
│  ├─ ArbeitsverzeichnisOeffnenService
│  ├─ IdeOeffnenService (Solution-Suche/-Öffnen für die Dialog-Auswahl bei mehreren Solutions)
│  ├─ PluginSelectionService (Auflösung des zu verwendenden IDE-Plugins)
│  └─ IDialogService
├─ WpfDialogService (implementiert IDialogService)
│  └─ Erstellt SolutionSelectionDialog und SolutionSelectionDialogViewModel
└─ SolutionSelectionDialog (XAML)
   └─ DataContext: SolutionSelectionDialogViewModel
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

### IDE öffnen

```
Benutzer klickt Button (aktiv sobald ShowFileExplorerPanel true ist, d. h. ein gültiges Arbeitsverzeichnis existiert)
  ↓
TaskDetailViewModel.OeffneIdeAsync()
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
Ist das aufgelöste Plugin VisualStudioIdePlugin?
  ├─→ Ja, und IdeOeffnenService.FindeSolutions(effectiveWorkdir) liefert mehrere Treffer:
  │     ↓
  │     IDialogService.ShowSolutionSelectionDialogAsync()
  │       ↓
  │       WpfDialogService (UI-Thread) → SolutionSelectionDialog (Modal) → SolutionSelectionDialogViewModel
  │         ↓
  │         Benutzer wählt Solution oder bricht ab → Rückgabe: Pfad oder null
  │     ↓
  │     IdeOeffnenService.OeffneSolution(gewählterPfad)
  │       ↓
  │       ProzessStartAnfrage mit ShellAusfuehren=true
  │       ↓
  │       IProzessStarter.Starten(anfrage)
  │       ↓
  │       Visual Studio öffnet die gewählte Solution
  │
  └─→ Sonst (VisualStudioIdePlugin mit ≤1 Solution, oder jedes andere Plugin wie VisualStudioCodeIdePlugin):
        ↓
        plugin.OpenRepositoryAsync(effectiveWorkdir, ct)
          ├─→ VisualStudioIdePlugin: findet die (einzige) Solution selbst und öffnet sie per Shell-Execute
          └─→ VisualStudioCodeIdePlugin: IVisualStudioCodeLocator.Locate()
                ├─→ Kein Treffer: wirft InvalidOperationException → FehlerMeldung, kein Prozessstart
                └─→ Treffer: ProzessStartAnfrage für code "<aufgelöstes Arbeitsverzeichnis>"
                      ↓
                      IProzessStarter.Starten(anfrage)
                        ├─→ SystemProzessStarter: Process.Start()
                        └─→ AufzeichnenderProzessStarter (Test): Logdatei schreiben
                      ↓
                      IDE öffnet aufgelöstes Arbeitsverzeichnis / Solution
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
    F["IdeOeffnenService"] -->|uses| D
    L["VisualStudioIdePlugin"] -->|uses| D
    M["VisualStudioCodeIdePlugin"] -->|uses| D
    M -->|uses| N["IVisualStudioCodeLocator"]

    P["PluginSelectionService"] -->|resolves via CheckCompatibilityAsync| L
    P -->|resolves via CheckCompatibilityAsync| M

    G["TaskDetailViewModel"] -->|uses| E
    G -->|uses| F
    G -->|uses| P
    G -->|uses| H["IDialogService"]
    
    I["WpfDialogService"] -->|implements| H
    I -->|creates| J["SolutionSelectionDialog"]
    J -->|DataContext| K["SolutionSelectionDialogViewModel"]
    
    G -->|invokes| J
    G -->|binds to| E
    G -->|binds to| F
```

## Skalierung und Zuverlässigkeit

### Fehlertoleranz

- **Prozessstart-Fehler:** Vollständig abgefangen und geloggt. Fehler blockiert nicht die Anwendung.
- **Dateisuche-Fehler:** `IdeOeffnenService.FindeSolutions()` gibt leere Liste bei jedem Fehler zurück (sicherer Fallback).
- **Dialog-Abbruch:** Normales Verhalten, keine Fehlerbehandlung erforderlich.

### Caching und Performance

- **Keine Solution-Vorab-Suche mehr beim Laden:** `OeffneIdeCommand.CanExecute` hängt nur noch von `ShowFileExplorerPanel` (vorhandenes Arbeitsverzeichnis) ab; die Solution-Suche (`IdeOeffnenService.FindeSolutions()`) und die Plugin-Kompatibilitätsprüfung laufen erst beim Klick auf „IDE öffnen".
- **Typischerweise schnell:** Für ein durchschnittliches Repository mit 1–5 Solutions dauert `FindeSolutions()` < 10 ms.
- **Keine rekursive Suche:** Verhindert Performance-Degradation in großen Verzeichnisstrukturen.

### Test-Isolation

- **Separate Logdatei:** Test-Prozessstart-Anfragen werden in `prozess-starts.log` neben der Test-DB aufgezeichnet, nicht in der Production-Log.
- **Keine echten Prozesse:** `AufzeichnenderProzessStarter` startet nie echte Prozesse, isoliert Tests vollständig.
- **Unkritisch für Parallelisierung:** Mehrere Tests können gleichzeitig laufen, da jeder Test seine eigene Testdatenbank und Logdatei hat.
