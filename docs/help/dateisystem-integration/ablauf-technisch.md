← [Zurück zur Übersicht](index.md)

# Dateisystem-Integration — Technischer Ablauf

## Übersicht

Das Feature implementiert zwei plattformabhängige Dateiexplorer-Funktionen über eine abstrakte `IProzessStarter`-Schnittstelle. Prozessstart-Anfragen werden gekapselt, geloggt und entweder direkt ausgeführt (Production) oder aufgezeichnet (Test). Solutions werden beim Laden der Aufgabe gecacht; ein modaler Dialog ermöglicht Auswahl bei mehreren Dateien. Ohne Solution kann optional ein VS-Code-Fallback greifen, wenn die Programmeinstellung aktiviert ist.

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

### 2. IDE öffnen (Caching der Solutions)

Beim Laden einer Aufgabe (Property `Aufgabe` wird gesetzt):

1. `TaskDetailViewModel.Aufgabe` Setter wird aufgerufen.
2. Setter ermittelt das effektive Arbeitsverzeichnis (analog zu Schritt 1):
   - Ruft `WorkingDirectoryResolver.ResolveEffectiveWorkingDirectory(LokalerKlonPfad, startConfig?.WorkingDirectoryRelativePath)` auf (synchrone Variante für UI-Caching).
   - Rückgabe: aufgelöstes Arbeitsverzeichnis.
3. Setter ruft `IdeOeffnenService.FindeSolutions(effectiveWorkdir)` mit dem **aufgelösten** Verzeichnis auf.
4. `IdeOeffnenService.FindeSolutions()`:
   - Prüft, ob der Pfad nicht null/leer und das Verzeichnis existiert.
   - Ruft `Directory.EnumerateFiles(effectiveWorkdir, "*.sln", SearchOption.TopDirectoryOnly)` auf (sucht **im aufgelösten Verzeichnis**, nicht im Root).
   - Sortiert die Ergebnisse alphabetisch (OrdinalIgnoreCase).
   - Gibt die Liste als `IReadOnlyList<string>` zurück (leer, wenn keine Solutions gefunden).
5. Feld `_solutionPfade` speichert das Ergebnis.
6. Property `SolutionsVorhanden` / Binding `SolutionFileExists` wird geändert.
7. `TaskDetailViewModel` lädt `ide.vscode.openWhenNoSolutionFound` über `AppEinstellungService`.
8. `KannIdeOeffnen` wird neu bewertet: `true` bei vorhandener Solution im aufgelösten Verzeichnis oder bei vorhandenem Arbeitsverzeichnis und aktiviertem VS-Code-Fallback.

Beteiligte Komponenten:
- `TaskDetailViewModel.Aufgabe` Setter — Trigger für Solution-Suche
- `WorkingDirectoryResolver` — Auflösung des effektiven Arbeitsverzeichnisses
- `IdeOeffnenService.FindeSolutions()` — Dateisuche im aufgelösten Verzeichnis und Sortierung
- `_solutionPfade` Feld — Gecachte Ergebnisse

### 3. IDE öffnen (Dialog bei mehreren Solutions)

1. Benutzer klickt Button → `TaskDetailViewModel.OeffneIdeCommand.Execute()` wird aufgerufen → `OeffneIdeAsync()` wird aufgerufen (async).
2. ViewModel ermittelt das effektive Arbeitsverzeichnis (async):
   - Ruft `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` auf.
   - Setzt `effectiveWorkdir`.
3. Prüfung der gecachten `_solutionPfade`:
   - **Genau eine Solution:** Sprung zu Schritt 4 (direkt öffnen).
   - **Mehrere Solutions:** Weiterfahrt mit Schritt 3a.
   - **Keine Solution:** Wenn der VS-Code-Fallback deaktiviert ist, ist der Button deaktiviert. Wenn er aktiviert ist, wird das aufgelöste Arbeitsverzeichnis in VS Code geöffnet (Fallback-Pfad).
3a. **Dialog anzeigen:**
   - `TaskDetailViewModel` ruft `_dialogService.ShowSolutionSelectionDialogAsync(_solutionPfade, ct)` auf.
   - `WpfDialogService.ShowSolutionSelectionDialogAsync()`:
     - Erstellt `SolutionSelectionDialogViewModel` mit der Liste der Pfade.
     - Erstellt Modal-Dialog `SolutionSelectionDialog` mit `Owner = MainWindow`.
     - Zeigt Dialog mittels `Application.Current.Dispatcher.InvokeAsync()` (UI-Thread).
     - Wartet auf Benutzeraktion:
       - **OK:** Gibt `SelectedSolution`-Pfad zurück.
       - **Abbrechen:** Gibt `null` zurück.
   - ViewModel erhält die Rückgabe.
   - **Wenn `null` (Abbruch):** Ablauf endet hier.
   - **Wenn Pfad:** Weiterfahrt mit Schritt 4.

Beteiligte Komponenten:
- `TaskDetailViewModel` — Verzweigungslogik
- `IDialogService.ShowSolutionSelectionDialogAsync()` — Dialog-Gateway
- `WpfDialogService` — Dialog-Anzeige und Koordination
- `SolutionSelectionDialog` — WPF-Fenster (Modal)
- `SolutionSelectionDialogViewModel` — Presentation Model

### 3b. IDE öffnen (VS-Code-Fallback)

1. Bei `0` gefundenen Solutions prüft `TaskDetailViewModel`, ob `_openVisualStudioCodeWhenNoSolutionFound` gesetzt ist.
2. Ist die Einstellung deaktiviert, endet der Ablauf ohne Prozessstart.
3. Ist die Einstellung aktiviert, ruft `TaskDetailViewModel` `IdeOeffnenService.OeffneVisualStudioCode(effectiveWorkdir)` mit dem **aufgelösten Arbeitsverzeichnis** auf.
4. `IdeOeffnenService` validiert das aufgelöste Arbeitsverzeichnis und fragt `IVisualStudioCodeLocator.Locate()` ab.
5. `VisualStudioCodeLocator` sucht zuerst `code.cmd` und `code` in `PATH`, danach typische Windows-Pfade unter `%LOCALAPPDATA%`, `%ProgramFiles%` und `%ProgramFiles(x86)%`.
6. Bei Treffer erstellt der Service `ProzessStartAnfrage(DateiName=<code-Pfad>, Argumente="\"<aufgelöster Arbeitsverzeichnis>\"", ShellAusfuehren=false)` — **mit dem aufgelösten Pfad als Arbeitsverzeichnis**, nicht dem Repository-Root.
7. Wenn kein VS Code gefunden wird, setzt das ViewModel die Meldung: „Keine Visual-Studio-Solution gefunden und Visual Studio Code wurde nicht gefunden."

### 4. IDE öffnen (Prozessstart)

1. Mit dem ermittelten Solution-Pfad (entweder direkt bei einer Solution oder nach Dialog-Auswahl) ruft `TaskDetailViewModel` `IdeOeffnenService.OeffneSolution(solutionPfad)` auf.
2. `IdeOeffnenService.OeffneSolution()` erstellt `ProzessStartAnfrage(DateiName=solutionPfad, Argumente=null, ShellAusfuehren=true)`.
3. Service ruft `IProzessStarter.Starten(anfrage)` auf.
4. `SystemProzessStarter` mappt auf `ProcessStartInfo` mit `UseShellExecute=true` (Shell-Execute).
5. `Process.Start()` startet den Prozess; das Betriebssystem ruft den registrierten Handler für `.sln` auf (üblicherweise Visual Studio).
6. Fehler werden geloggt und in `FehlerMeldung` angezeigt.

Beteiligte Komponenten:
- `TaskDetailViewModel.OeffneIdeAsync()` — Koordination
- `IdeOeffnenService.OeffneSolution()` — Service-Methode
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
    
    D --> L["_solutionPfade lesen"]
    L --> M{Anzahl Solutions?}
    M -->|0| N{"VS-Code-Fallback aktiv?"}
    N -->|Nein| U["Button deaktiviert"]
    N -->|Ja| V["VisualStudioCodeLocator"]
    V --> W{"VS Code gefunden?"}
    W -->|Nein| X["FehlerMeldung"]
    W -->|Ja| Y["IdeOeffnenService.OeffneVisualStudioCode"]
    Y --> H
    M -->|1| O["Direkt öffnen"]
    M -->|>1| P["Dialog anzeigen"]
    P --> Q{"Benutzer bestätigt?"}
    Q -->|Nein| R["Abbruch"]
    Q -->|Ja| O
    O --> S["IdeOeffnenService.OeffneSolution"]
    S --> T["ProzessStartAnfrage<br/>ShellAusfuehren=true"]
    T --> H
```

## Fehlerbehandlung

### Arbeitsverzeichnis-Auflösung (Fehler)

Wenn `WorkingDirectoryResolver.DetermineEffectiveWorkingDirectoryAsync()` eine Ausnahme wirft (z. B. das konfigurierte Unterverzeichnis existiert nicht oder ist nicht erreichbar):

1. Ausnahme wird geloggt mit Details (konfig. Pfad, Grund: `DirectoryNotFoundException`, `UnauthorizedAccessException`, etc.).
2. `TaskDetailViewModel` fängt die Ausnahme ab (in `OeffneArbeitsverzeichnisAsync()`, `OeffneIdeAsync()`, `OeffneVisualStudioCodeFallbackAsync()`).
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

Wenn `IdeOeffnenService.FindeSolutions()` eine leere Liste zurückgibt:

1. Property `SolutionFileExists` wird `false`.
2. Ist `ide.vscode.openWhenNoSolutionFound` deaktiviert, gibt `OeffneIdeCommand.CanExecute()` `false` zurück.
3. Ist die Einstellung aktiviert und ein Arbeitsverzeichnis vorhanden, kann der Button geklickt werden.
4. Beim Klick wird VS Code über `IVisualStudioCodeLocator` gesucht. Ohne Treffer wird eine verständliche Fehlermeldung angezeigt.

### Dialog-Abbruch

Wenn Benutzer im `SolutionSelectionDialog` auf „Abbrechen" klickt:

1. `ShowSolutionSelectionDialogAsync()` gibt `null` zurück.
2. `OeffneIdeAsync()` bricht ab, ruft nicht `OeffneSolution()` auf.
3. Keine Fehlermeldung; kein Prozess wird gestartet.

## Test-Implementierung

Im Test-Modus (wenn `SOFTWARESCHMIEDE_TEST_DB_PATH` gesetzt ist):

1. `App.xaml.cs` registriert `AufzeichnenderProzessStarter` statt `SystemProzessStarter`.
2. `AufzeichnenderProzessStarter.Starten()` serialisiert die `ProzessStartAnfrage` und schreibt sie als Zeile in eine Logdatei (`prozess-starts.log` neben der Test-DB).
3. Tatsächliche Prozesse werden nicht gestartet.
4. E2E-Tests lesen die Logdatei über `WpfTestBase.WaitForProzessStartEintragAsync()` und prüfen, ob der erwartete Eintrag vorhanden ist.
