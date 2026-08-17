← [Zurück zur Übersicht](index.md)

# Dateisystem-Integration — Technischer Ablauf

## Übersicht

Das Feature implementiert zwei plattformabhängige Dateiexplorer-Funktionen über eine abstrakte `IProzessStarter`-Schnittstelle. Prozessstart-Anfragen werden gekapselt, geloggt und entweder direkt ausgeführt (Production) oder aufgezeichnet (Test). „IDE öffnen" löst das zu verwendende IDE-Plugin bei jedem Klick über `PluginSelectionService.ResolveIdePluginAsync()` auf (kein Vorab-Caching mehr beim Laden der Aufgabe); `IdeOeffnenService.OpenRepositoryInIdeAsync()` fragt das aufgelöste Plugin anschließend generisch über `IIdePlugin.FindEntryPointsAsync()` nach seinen verfügbaren Einstiegspunkten (bei `VisualStudioIdePlugin`: je gefundener `.sln`/`.slnx`-Datei ein Einstiegspunkt; bei `VisualStudioCodeIdePlugin`: immer genau einer, das Repository-Root) — ein modales Dialogfenster ermöglicht die Auswahl, sobald mehr als ein Einstiegspunkt gefunden wird, unabhängig davon, welches Plugin aufgelöst wurde. Ist kein Plugin explizit kompatibel, greift automatisch das erste aktive Fallback-Plugin (standardmäßig Visual Studio Code).

## Ablauf

### 1. Arbeitsverzeichnis öffnen

1. Benutzer klickt Button im Ribbon → `TaskDetailViewModel.OeffneArbeitsverzeichnisCommand.Execute()` wird aufgerufen.
2. `TaskDetailViewModel.OeffneArbeitsverzeichnisAsync()` wird aufgerufen (async void Command-Handler).
3. ViewModel ermittelt das effektive Arbeitsverzeichnis:
   - Liest `Aufgabe.GitRepository.StartKonfiguration` (optional).
   - Ruft `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync(LokalerKlonPfad, startConfig, gitPlugin: null, CancellationToken)` auf.
   - `WorkingDirectoryResolver` kombiniert den Repository-Root mit dem optionalen `WorkingDirectoryRelativePath` und validiert das Ergebnis.
   - Rückgabe: effektives Arbeitsverzeichnis (z. B. `C:\repos\project\src\backend` falls `WorkingDirectoryRelativePath="src/backend"`).
4. ViewModel ruft `ArbeitsverzeichnisOeffnenService.Oeffne(effectiveWorkdir)` mit dem aufgelösten Pfad auf.
5. `ArbeitsverzeichnisOeffnenService` ermittelt den plattformabhängigen Befehl:
   - Windows: `explorer.exe` + aufgelöster Verzeichnispfad (als Argument mit Anführungszeichen)
   - Linux: `xdg-open` + aufgelöster Verzeichnispfad
   - macOS: `open` + aufgelöster Verzeichnispfad
6. Service erstellt `ProzessStartAnfrage(DateiName="explorer.exe", Argumente="\"C:\\...\\backend\"", ShellAusfuehren=false)`.
7. Service ruft `IProzessStarter.Starten(anfrage)` auf.
8. `SystemProzessStarter` mappt die Anfrage auf `ProcessStartInfo` und ruft `Process.Start()` auf.
9. Der Prozess wird gestartet; Fehler werden geloggt und in `FehlerMeldung` angezeigt (z. B. bei nicht-existentem oder nicht-erreichbarem Verzeichnis).

Beteiligte Komponenten:
- `TaskDetailView.xaml` — Ribbon-Button
- `TaskDetailViewModel` — Command-Handler, Auflösung des Arbeitsverzeichnisses
- `WorkingDirectoryResolver` — Auflösung des effektiven Arbeitsverzeichnisses mit Validierung
- `ArbeitsverzeichnisOeffnenService` — Plattformauflösung und Service-Logik
- `IProzessStarter` (Gateway) — Abstraktionsschicht
- `SystemProzessStarter` — Reale Implementierung
- `ProzessStartAnfrage` — Value Object für Prozessstart-Parameter

### 2. IDE öffnen (CanExecute)

Der `OeffneIdeCommand`-Button ist bereits aktiv, sobald ein gültiges Arbeitsverzeichnis existiert — unabhängig davon, ob eine Solution gefunden wird:

1. `TaskDetailViewModel.Aufgabe` Setter setzt `ShowFileExplorerPanel` (gültiger `LokalerKlonPfad`, Verzeichnis existiert).
2. `KannIdeOeffnen` liefert direkt `ShowFileExplorerPanel` zurück.
3. Es findet **keine** Solution-Vorab-Suche mehr beim Laden der Aufgabe statt — die Suche und die Plugin-Auflösung passieren erst beim Klick (Schritt 3).

Beteiligte Komponenten:
- `TaskDetailViewModel.Aufgabe` Setter — setzt `ShowFileExplorerPanel`
- `TaskDetailViewModel.KannIdeOeffnen` — CanExecute für `OeffneIdeCommand`

### 3. IDE öffnen (Plugin-Auflösung, generische Einstiegspunkt-Ermittlung und Dialog bei mehreren Einstiegspunkten)

1. Benutzer klickt Button → `TaskDetailViewModel.OeffneIdeCommand.Execute()` wird aufgerufen → `OeffneIdeAsync()` wird aufgerufen (async).
2. ViewModel ermittelt das effektive Arbeitsverzeichnis (async):
   - Ruft `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` auf.
   - Setzt `effectiveWorkdir`.
3. ViewModel ruft `_ideOeffnenService.OpenRepositoryInIdeAsync(effectiveWorkdir, waehleEntryPointAsync, ct)` auf und übergibt dabei einen Auswahl-Callback. `IdeOeffnenService.OpenRepositoryInIdeAsync()`:
   - Ruft `_pluginSelectionService.ResolveIdePluginAsync(effectiveWorkdir, ct)` auf:
     - Ermittelt die aktivierten IDE-Plugins (`PluginActivationService.GetEnabledIdePluginsAsync()`) in der konfigurierten Reihenfolge (`plugins.ide.order`).
     - Prüft jedes Plugin über `CheckCompatibilityAsync(effectiveWorkdir, ct)`.
     - Das erste `Explicit`-kompatible Plugin gewinnt sofort (z. B. `VisualStudioIdePlugin` bei gefundener `.sln`/`.slnx`).
     - Andernfalls gewinnt das erste `Fallback`-kompatible Plugin (z. B. `VisualStudioCodeIdePlugin`, das sich immer als Fallback meldet).
     - Sind keine Plugins aktiviert oder kompatibel, liefert `IPluginManager.GetDefaultIdePlugin()` das Ergebnis (mindestens ein IDE-Plugin bleibt systemseitig immer aktiv).
   - Ruft anschließend generisch `plugin.FindEntryPointsAsync(effectiveWorkdir, ct)` auf — unabhängig davon, welches Plugin aufgelöst wurde (`VisualStudioIdePlugin` liefert je gefundener `.sln`/`.slnx`-Datei einen `IdeEntryPoint`, `VisualStudioCodeIdePlugin` immer genau einen mit dem Repository-Root).
   - **0 Einstiegspunkte:** `FileNotFoundException` wird geworfen.
   - **Genau 1 Einstiegspunkt:** Weiterfahrt mit Schritt 5 (`plugin.OpenEntryPointAsync()` direkt, kein Dialog).
   - **Mehr als 1 Einstiegspunkt:** Weiterfahrt mit Schritt 4a (Dialog).

4a. **Dialog anzeigen (bei mehr als einem gefundenen Einstiegspunkt, unabhängig vom aufgelösten Plugin):**
   - `IdeOeffnenService` ruft den von `TaskDetailViewModel` übergebenen Callback mit allen gefundenen `IdeEntryPoint`-Objekten auf.
   - Der Callback bildet die Einstiegspunkte auf ihre Pfade ab und ruft `_dialogService.ShowSolutionSelectionDialogAsync(solutionPfade, ct)` auf.
   - `WpfDialogService.ShowSolutionSelectionDialogAsync()`:
     - Erstellt `SolutionSelectionDialogViewModel` mit der Liste der Pfade.
     - Erstellt Modal-Dialog `SolutionSelectionDialog` mit `Owner = MainWindow`.
     - Zeigt Dialog mittels `Application.Current.Dispatcher.InvokeAsync()` (UI-Thread).
     - Wartet auf Benutzeraktion:
       - **OK:** Gibt `SelectedSolution`-Pfad zurück.
       - **Abbrechen:** Gibt `null` zurück.
   - **Wenn `null` (Abbruch):** Der Callback liefert `null` an `IdeOeffnenService`; der Ablauf endet dort, kein Prozessstart.
   - **Wenn Pfad:** Der Callback bildet den gewählten Pfad zurück auf den passenden `IdeEntryPoint` ab und gibt ihn zurück; `IdeOeffnenService` ruft damit `plugin.OpenEntryPointAsync()` auf (siehe Schritt 6), Ablauf endet danach.

Beteiligte Komponenten:
- `TaskDetailViewModel.OeffneIdeAsync()` — Koordination; übergibt den Auswahl-Callback an `IdeOeffnenService`
- `IdeOeffnenService.OpenRepositoryInIdeAsync()` — generische Plugin-Auflösung, Einstiegspunkt-Ermittlung und Verzweigung nach Anzahl
- `PluginSelectionService.ResolveIdePluginAsync()` — Plugin-Auflösung
- `IIdePlugin.CheckCompatibilityAsync()` — Kompatibilitätsprüfung je Plugin
- `IIdePlugin.FindEntryPointsAsync()` — generische Einstiegspunkt-Ermittlung je Plugin
- `IDialogService.ShowSolutionSelectionDialogAsync()` — Dialog-Gateway
- `WpfDialogService` — Dialog-Anzeige und Koordination
- `SolutionSelectionDialog` — WPF-Fenster (Modal)
- `SolutionSelectionDialogViewModel` — Presentation Model

### 5. IDE öffnen (direktes Öffnen über den ermittelten Einstiegspunkt)

Bei genau einem gefundenen Einstiegspunkt sowie nach einer Dialog-Auswahl ruft `IdeOeffnenService` `plugin.OpenEntryPointAsync(entryPoint, ct)` auf:

- **`VisualStudioIdePlugin.OpenEntryPointAsync()`:** Öffnet die Solution-Datei des übergebenen `IdeEntryPoint` per Shell-Execute (siehe Schritt 6).
- **`VisualStudioCodeIdePlugin.OpenEntryPointAsync()`:**
  1. Fragt `IVisualStudioCodeLocator.Locate()` ab.
  2. `VisualStudioCodeLocator` sucht zuerst `code.cmd` und `code` in `PATH`, danach typische Windows-Pfade unter `%LOCALAPPDATA%`, `%ProgramFiles%` und `%ProgramFiles(x86)%`.
  3. Bei Treffer erstellt es `ProzessStartAnfrage(DateiName=<code-Pfad>, Argumente="\"<Pfad des Einstiegspunkts>\"", ShellAusfuehren=false)` — **mit dem aufgelösten Arbeitsverzeichnis-Pfad**, nicht zwingend dem Repository-Root.
  4. Ohne Treffer wirft es `InvalidOperationException("Visual Studio Code wurde nicht gefunden.")`; `OeffneIdeAsync()` fängt das ab und setzt `FehlerMeldung = "IDE konnte nicht geöffnet werden: Visual Studio Code wurde nicht gefunden."`.

### 6. IDE öffnen (Prozessstart eines Einstiegspunkts)

1. Innerhalb von `VisualStudioIdePlugin.OpenEntryPointAsync()` (Schritt 5) wird letztlich die interne `VisualStudioIdePlugin.OpenSolutionFile()`-Logik aufgerufen (dieselbe Hilfsmethode, die auch `IdeOeffnenService.OeffneSolution()` für rückwärtskompatible Aufrufer nutzt).
2. Diese erstellt `ProzessStartAnfrage(DateiName=solutionPfad, Argumente=null, ShellAusfuehren=true)`.
3. `IProzessStarter.Starten(anfrage)` wird aufgerufen.
4. `SystemProzessStarter` mappt auf `ProcessStartInfo` mit `UseShellExecute=true` (Shell-Execute).
5. `Process.Start()` startet den Prozess; das Betriebssystem ruft den registrierten Handler für `.sln` auf (üblicherweise Visual Studio).
6. Fehler werden in `OeffneIdeAsync()` geloggt und in `FehlerMeldung` angezeigt.

Beteiligte Komponenten:
- `TaskDetailViewModel.OeffneIdeAsync()` — Koordination
- `VisualStudioIdePlugin.OpenEntryPointAsync()` / `OpenSolutionFile()` — Solution-Start
- `IProzessStarter` (Gateway)
- `SystemProzessStarter` — Reale Implementierung

## Diagramm

```mermaid
flowchart TD
    A["Benutzer klickt Button"] --> B{"Welcher Button?"}
    B -->|Arbeitsverzeichnis| C["OeffneArbeitsverzeichnis"]
    B -->|IDE öffnen| D["OeffneIdeAsync"]
    
    C --> E["ArbeitsverzeichnisOeffnenService.Oeffne"]
    E --> F["Plattformbefehl auflösen<br/>Windows: explorer.exe<br/>Linux: xdg-open<br/>macOS: open"]
    F --> G["ProzessStartAnfrage erstellen"]
    G --> H["IProzessStarter.Starten"]
    H --> I["SystemProzessStarter"]
    I --> J["Process.Start"]
    J --> K["Fehler? → FehlerMeldung"]
    
    D --> L["PluginSelectionService.ResolveIdePluginAsync"]
    L --> L2["plugin.FindEntryPointsAsync"]
    L2 --> M{"Anzahl<br/>Einstiegspunkte?"}
    M -->|0| M0["FileNotFoundException"]
    M -->|1| Z["plugin.OpenEntryPointAsync"]
    M -->|">1"| P["Dialog anzeigen"]
    P --> Q{"Benutzer bestätigt?"}
    Q -->|Nein| R["Abbruch, kein Prozessstart"]
    Q -->|Ja| Z
    Z -->|VisualStudioIdePlugin| S["OpenSolutionFile"]
    Z -->|VisualStudioCodeIdePlugin| V["VisualStudioCodeLocator.Locate"]
    V --> W{"VS Code gefunden?"}
    W -->|Nein| X["FehlerMeldung"]
    W -->|Ja| Y["ProzessStartAnfrage<br/>ShellAusfuehren=false"]
    Y --> H
    S --> T["ProzessStartAnfrage<br/>ShellAusfuehren=true"]
    T --> H
```

## Fehlerbehandlung

### Arbeitsverzeichnis-Auflösung (Fehler)

Wenn `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` eine Ausnahme wirft (z. B. das konfigurierte Unterverzeichnis existiert nicht oder ist nicht erreichbar):

1. Ausnahme wird geloggt mit Details (konfig. Pfad, Grund: `DirectoryNotFoundException`, `UnauthorizedAccessException`, etc.).
2. `TaskDetailViewModel` fängt die Ausnahme ab (in `OeffneArbeitsverzeichnisAsync()` bzw. `OeffneIdeAsync()`).
3. Fehlermeldung wird in Property `FehlerMeldung` gespeichert (z. B. „Arbeitsverzeichnis konnte nicht geöffnet werden: Verzeichnis nicht gefunden").
4. UI zeigt Fehler-Banner an.
5. Buttons bleiben inaktiv, solange das konfigurierte Arbeitsverzeichnis nicht erreichbar ist.

### Prozessstart-Fehler

Wenn `SystemProzessStarter.Starten()` eine Ausnahme wirft (z. B. Befehl nicht gefunden, keine Berechtigung):

1. Ausnahme wird geloggt mit vollständigen Details (`DateiName`, `Argumente`, `ShellAusfuehren`, aufgelöster Pfad).
2. `TaskDetailViewModel` fängt die Ausnahme (in `OeffneArbeitsverzeichnisAsync()` oder `OeffneIdeAsync()`) ab.
3. Fehlermeldung wird in Property `FehlerMeldung` gespeichert.
4. UI zeigt Fehler-Banner an.
5. Benutzer kann den Fehler einblenden (durch Fehlerbanner-Klick oder Bestätigung).

### Keine Solution gefunden

Wenn kein Plugin explizit kompatibel ist (kein `VisualStudioIdePlugin` mit gefundener `.sln`/`.slnx`):

1. `OeffneIdeCommand.CanExecute()` bleibt unabhängig davon `true`, solange ein Arbeitsverzeichnis vorhanden ist (`ShowFileExplorerPanel`).
2. `PluginSelectionService.ResolveIdePluginAsync()` liefert das erste aktive Fallback-Plugin (standardmäßig `VisualStudioCodeIdePlugin`) oder — falls kein Plugin aktiv/kompatibel ist — `IPluginManager.GetDefaultIdePlugin()`.
3. Beim Klick liefert `plugin.FindEntryPointsAsync()` für `VisualStudioCodeIdePlugin` immer genau einen Einstiegspunkt (das Repository-Root); dieser wird direkt über `plugin.OpenEntryPointAsync()` geöffnet, wobei VS Code über `IVisualStudioCodeLocator` gesucht wird. Ohne Treffer wird eine verständliche Fehlermeldung angezeigt, ohne dass ein Prozess gestartet wird.

### Dialog-Abbruch

Wenn Benutzer im `SolutionSelectionDialog` auf „Abbrechen" klickt:

1. `ShowSolutionSelectionDialogAsync()` gibt `null` zurück.
2. Der Auswahl-Callback in `TaskDetailViewModel` gibt daraufhin ebenfalls `null` an `IdeOeffnenService` zurück.
3. `IdeOeffnenService.OpenRepositoryInIdeAsync()` bricht ab, ruft nicht `plugin.OpenEntryPointAsync()` auf.
4. Keine Fehlermeldung; kein Prozess wird gestartet.

## Test-Implementierung

Im Test-Modus (wenn `SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt ist):

1. `App.xaml.cs` registriert `AufzeichnenderProzessStarter` statt `SystemProzessStarter`.
2. `AufzeichnenderProzessStarter.Starten()` serialisiert die `ProzessStartAnfrage` und schreibt sie als Zeile in eine Logdatei (`prozess-starts.log` neben der Test-DB).
3. Tatsächliche Prozesse werden nicht gestartet.
4. E2E-Tests lesen die Logdatei über `WpfTestBase.WaitForProzessStartEintragAsync()` und prüfen, ob der erwartete Eintrag vorhanden ist.
