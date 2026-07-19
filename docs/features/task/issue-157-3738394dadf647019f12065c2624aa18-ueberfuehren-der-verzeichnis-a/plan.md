# Umsetzungsplan: Überführen der Verzeichnis-Aktionsbuttons in das Ribbon-Menü

## Übersicht

Die Aktionsbuttons des Dateiexplorers (Ansichtswechsel Standard/Vergleich, Aktualisieren, Datei mit Standardanwendung öffnen) werden aus `FileExplorerView.xaml` in das Ribbon-Menü der Aufgabendetailansicht (`TaskDetailView.xaml`) überführt. Zusätzlich entstehen zwei neue, immer sichtbare Ribbon-Buttons „Arbeitsverzeichnis öffnen" (startet den OS-Dateiexplorer) und „IDE öffnen" (öffnet die `*.sln`-Datei des Arbeitsverzeichnisses mit dem registrierten Standardhandler, i. d. R. Visual Studio; bei mehreren gefundenen Solutions über einen Auswahl-Dialog). Betroffen sind primär die UI-Schicht (`Softwareschmiede.App`) sowie eine neue OS-Prozessstart-Abstraktion nebst zwei Anwendungsdiensten.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Prozessstart-Abstraktion `IProzessStarter` | **Gateway** (Domain.Interfaces) mit realer Infrastruktur-Implementierung `SystemProzessStarter` und im Testmodus registriertem `AufzeichnenderProzessStarter` | Macht die beiden OS-Aktionen unit- und E2E-testbar; spiegelt exakt das bestehende Muster `IPseudoConsoleProcessLauncher` / `SimulatedPseudoConsoleProcessLauncher`, das per `SOFTWARESCHMIEDE_TEST_DB_PATH` in `App.xaml.cs` getauscht wird. Direkter `Process.Start`-Aufruf (wie bei `IssueBrowserOeffnen`) wäre nicht testbar, die Anforderung verlangt aber Unit- und E2E-Tests. |
| `ProzessStartAnfrage` | **Value Object** (Domain.ValueObjects), Felder `DateiName`, `Argumente`, `ShellAusfuehren` | Hält `System.Diagnostics.ProcessStartInfo` aus der Domäne heraus; der reale Starter mappt darauf, der aufzeichnende serialisiert die Anfrage. |
| `ArbeitsverzeichnisOeffnenService`, `IdeOeffnenService` | **Service Layer** in `Application.Services`, Abhängigkeit auf `IProzessStarter` | Plattformbefehl-Auflösung bzw. `*.sln`-Suche sind testbare Logik; konsistent mit `GitWorkspaceBrowserService`, der ebenfalls in `Application.Services` liegt und OS-nahe Aufrufe über eine Abstraktion (`ICliRunner`) kapselt. |
| IDE-Start | **Shell-Execute der `*.sln`** (`ShellAusfuehren = true`), keine Visual-Studio-Pfadsuche, kein `IdeType`-Enum | Einfachster Weg; der OS-Handler für `.sln` ist typischerweise Visual Studio. Vermeidet brüchige VS-Installationserkennung und deckt sich mit `IssueBrowserOeffnen` / `DateiMitStandardanwendungOeffnen` (beide `UseShellExecute = true`). |
| **Verhalten bei mehreren `*.sln`** | **Auswahl-Dialog** über `IDialogService.ShowSolutionSelectionDialogAsync(...)`; bei genau einer Solution direkte Öffnung ohne Dialog | Geklärter offener Punkt: der Nutzer soll bei mehreren Solutions selbst wählen (nicht automatisch die erste alphabetisch). Modelliert nach dem bestehenden Muster `ShowIssueSelectionDialogAsync` / `IssueSelectionDialog` / `IssueSelectionDialogViewModel`. |
| **Ort der Auswahl-Logik** | **`TaskDetailViewModel`** entscheidet Einzel-/Mehrfachfall und ruft ggf. den Dialog; `IdeOeffnenService` liefert nur die Solution-Liste und öffnet einen übergebenen Pfad | `IdeOeffnenService` liegt in der App-unabhängigen Schicht `Application.Services` und darf nicht auf `IDialogService` (App-Schicht) zugreifen. `TaskDetailViewModel` besitzt bereits `IDialogService _dialogService`. |
| `SolutionSelectionDialog` / `SolutionSelectionDialogViewModel` | **Eigener modaler WPF-Dialog** analog `IssueSelectionDialog` | Konsistente Dialog-Infrastruktur; `WpfDialogService` instanziiert und zeigt ihn per `Application.Current.Dispatcher.InvokeAsync`, gibt den gewählten Pfad oder `null` (Abbruch) zurück. |
| Ribbon-Gruppierung | **Zwei Gruppen**: „Dateien" (Sichtbarkeit an `ShowFileExplorerPanel`) mit den vier überführten Buttons + neue, immer sichtbare Gruppe „Werkzeuge" mit den zwei neuen Buttons | Die Anforderung verlangt, dass die Gruppe „Dateien" nur bei aktivem Dateibrowser sichtbar ist, die zwei neuen Buttons aber immer. Beides in einer Gruppe ist nicht möglich, sobald die Gruppe als Ganzes ausgeblendet wird. |
| Aktivierbarkeit der immer sichtbaren Buttons | Steuerung über **`Command.CanExecute`** statt `Visibility` | `RibbonLargeButton` graut deaktivierte Buttons automatisch aus (Template-Trigger `IsEnabled=False` → Opacity 0.4). „Immer sichtbar, aber ohne Repository/Solution deaktiviert" ist damit ohne Zusatz-Property abbildbar. |
| Überführung der vorhandenen Buttons | **Verschieben statt duplizieren**: Entfernen aus `FileExplorerView.xaml`, Ribbon bindet an `FileExplorer.<Command>` | „Überführen" laut Titel/Anforderung; doppelte Buttons wären ein Regressionsrisiko. Die Diff-Navigations-Buttons (◀ ▶) bleiben in der View, da nicht Teil der Anforderung. |
| Prozessstart-Fehler (z. B. IDE nicht installiert) | **`FehlerMeldung`-Banner** im `TaskDetailViewModel` (Grid.Row=1 der View), kein modaler Dialog | Geklärter offener Punkt: konsistent mit bestehender Fehleranzeige; der Dienst fängt/loggt, das ViewModel setzt `FehlerMeldung`. |
| `*.sln`-Suche | Nur **oberste Ebene** von `LokalerKlonPfad` (nicht rekursiv), Ergebnis als alphabetisch sortierte Liste | Geklärter offener Punkt: deterministisch und einfach; das Wurzelverzeichnis eines Arbeitsverzeichnisses enthält die relevante(n) Solution(s). Sortierung liefert eine stabile Reihenfolge im Auswahl-Dialog. |

## Programmabläufe

### Arbeitsverzeichnis öffnen

1. Nutzer klickt im Ribbon (Gruppe „Werkzeuge") auf „Arbeitsverzeichnis öffnen".
2. `OeffneArbeitsverzeichnisCommand` prüft `CanExecute` = `ShowFileExplorerPanel` (Arbeitsverzeichnis existiert).
3. `TaskDetailViewModel.OeffneArbeitsverzeichnis()` ruft `ArbeitsverzeichnisOeffnenService.Oeffne(_aufgabe.LokalerKlonPfad)` auf.
4. `ArbeitsverzeichnisOeffnenService` ermittelt plattformabhängig den Öffnen-Befehl (Windows: `explorer.exe` mit Verzeichnis als Argument; Linux: `xdg-open`; macOS: `open`) und erstellt eine `ProzessStartAnfrage`.
5. Der Dienst übergibt die Anfrage an `IProzessStarter.Starten(...)`.
6. `SystemProzessStarter` startet den Prozess via `Process.Start`. Fehler werden geloggt und vom ViewModel als `FehlerMeldung` angezeigt (kein Absturz).

Beteiligte Klassen/Komponenten: `TaskDetailView.xaml`, `TaskDetailViewModel`, `ArbeitsverzeichnisOeffnenService`, `IProzessStarter`, `SystemProzessStarter`, `ProzessStartAnfrage`

### IDE öffnen

1. Nutzer klickt im Ribbon (Gruppe „Werkzeuge") auf „IDE öffnen".
2. `OeffneIdeCommand` prüft `CanExecute` = `SolutionFileExists` (mindestens eine `*.sln` wurde beim Laden der Aufgabe gefunden).
3. `TaskDetailViewModel.OeffneIde()` liest die gecachte Solution-Liste `_solutionPfade`:
   - **Genau eine Solution:** direkt weiter mit diesem Pfad.
   - **Mehrere Solutions:** `_dialogService.ShowSolutionSelectionDialogAsync(_solutionPfade, ct)` zeigt den Auswahl-Dialog. Bricht der Nutzer ab (Rückgabe `null`), endet der Ablauf ohne weitere Aktion.
4. Mit dem ermittelten Solution-Pfad ruft das ViewModel `IdeOeffnenService.OeffneSolution(solutionPfad)` auf.
5. `IdeOeffnenService` erstellt eine `ProzessStartAnfrage` mit `DateiName = solutionPfad` und `ShellAusfuehren = true`.
6. `IProzessStarter.Starten(...)` startet den Prozess; der OS-Handler für `.sln` (Visual Studio) öffnet die Solution. Fehler werden geloggt und als `FehlerMeldung` angezeigt.

Beteiligte Klassen/Komponenten: `TaskDetailView.xaml`, `TaskDetailViewModel`, `IDialogService`, `SolutionSelectionDialog`, `SolutionSelectionDialogViewModel`, `IdeOeffnenService`, `IProzessStarter`, `SystemProzessStarter`, `ProzessStartAnfrage`

### Ermittlung von `SolutionFileExists` beim Laden der Aufgabe

1. Im Setter von `TaskDetailViewModel.Aufgabe` wird — analog zum bereits vorhandenen `_showFileExplorerPanel`-Caching — einmalig `IdeOeffnenService.FindeSolutions(value?.LokalerKlonPfad)` aufgerufen.
2. Das Ergebnis (Liste, ggf. leer) wird im Feld `_solutionPfade` (`IReadOnlyList<string>`) gecacht; `SolutionFileExists` liefert `_solutionPfade.Count > 0`.
3. `OnPropertyChanged(nameof(SolutionFileExists))` wird ausgelöst, damit `OeffneIdeCommand.CanExecute` neu ausgewertet wird.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `IdeOeffnenService`

### Auswahl-Dialog bei mehreren Solutions

1. `WpfDialogService.ShowSolutionSelectionDialogAsync(solutionPfade, ct)` erstellt ein `SolutionSelectionDialogViewModel` mit der Liste der Solution-Pfade.
2. Der Dialog `SolutionSelectionDialog` (modal, `Owner = MainWindow`) zeigt die Pfade in einer Liste zur Auswahl; Bestätigen setzt `SelectedSolution`, Abbrechen liefert `null`.
3. Rückgabe: gewählter Solution-Pfad (bei `DialogResult == true`) oder `null` (Abbruch).

Beteiligte Klassen/Komponenten: `IDialogService`, `WpfDialogService`, `SolutionSelectionDialog`, `SolutionSelectionDialogViewModel`

### Dateiaktionen über das Ribbon (überführte Buttons)

1. Die vier Buttons „Standard", „Vergleich", „Aktualisieren", „Datei öffnen" liegen jetzt in der Ribbon-Gruppe „Dateien".
2. Ihre `ButtonCommand`-Bindungen zeigen auf `FileExplorer.StandardAnsichtCommand`, `FileExplorer.VergleichCommand`, `FileExplorer.AktualisierenCommand`, `FileExplorer.DateiMitStandardanwendungOeffnenCommand` (der Ribbon-DataContext ist das `TaskDetailViewModel`, das `FileExplorer` exportiert).
3. Verhalten und `CanExecute` bleiben unverändert (die Commands existieren bereits im `FileExplorerViewModel`); es ändert sich ausschließlich der Aufrufort.

Beteiligte Klassen/Komponenten: `TaskDetailView.xaml`, `FileExplorerView.xaml`, `FileExplorerViewModel`

### Sichtbarkeit der Ribbon-Gruppen

1. Gruppe „Dateien": `Visibility` an `ShowFileExplorerPanel` (BoolToVisibilityConverter) — nur sichtbar, wenn ein Arbeitsverzeichnis existiert.
2. Gruppe „CLI": `Visibility` an `ShowCliPanel` ergänzen (bisher ohne Bindung, laut Anforderung nur bei aktivem CLI-Panel anzeigen).
3. Gruppe „Werkzeuge" (neu): keine `Visibility`-Bindung → immer sichtbar; die enthaltenen Buttons steuern ihre Aktivierbarkeit über `CanExecute`.

Beteiligte Klassen/Komponenten: `TaskDetailView.xaml`, `TaskDetailViewModel`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IProzessStarter` | Interface (Domain.Interfaces) | Gateway über OS-Prozessstart; entkoppelt `Process.Start` für Tests. |
| `ProzessStartAnfrage` | Value Object (Domain.ValueObjects) | Beschreibt eine Prozessstartanforderung (`DateiName`, `Argumente`, `ShellAusfuehren`) ohne `System.Diagnostics`-Abhängigkeit. |
| `SystemProzessStarter` | Klasse (Infrastructure.Services) | Reale Implementierung von `IProzessStarter` via `Process.Start`; mit Logging. |
| `AufzeichnenderProzessStarter` | Klasse (Infrastructure.Services) | Testmodus-Implementierung: hängt jede `ProzessStartAnfrage` an eine Logdatei neben der Test-DB an, statt einen echten Prozess zu starten. |
| `ArbeitsverzeichnisOeffnenService` | Klasse / Service Layer (Application.Services) | Löst den plattformabhängigen Öffnen-Befehl auf und startet den OS-Dateiexplorer für ein Verzeichnis. |
| `IdeOeffnenService` | Klasse / Service Layer (Application.Services) | Findet die `*.sln`-Dateien eines Arbeitsverzeichnisses (`FindeSolutions`) und öffnet eine übergebene Solution (`OeffneSolution`). |
| `SolutionSelectionDialogViewModel` | Klasse / Presentation Model (App.ViewModels) | Hält die Liste der Solution-Pfade und die Auswahl (`Solutions`, `SelectedSolution`) für den Auswahl-Dialog. |
| `SolutionSelectionDialog` | WPF-Window (App.Views) | Modaler Auswahl-Dialog für mehrere Solutions, analog `IssueSelectionDialog`. |

Abhängigkeitsreihenfolge: `ProzessStartAnfrage` und `IProzessStarter` müssen vor `SystemProzessStarter`/`AufzeichnenderProzessStarter` und vor den beiden Anwendungsdiensten existieren. `SolutionSelectionDialogViewModel` muss vor `SolutionSelectionDialog` und vor der `IDialogService`-Erweiterung existieren. Die Anwendungsdienste und die Dialog-Erweiterung müssen vor der `TaskDetailViewModel`-Erweiterung existieren.

## Änderungen an bestehenden Klassen

### `TaskDetailViewModel` (App-ViewModel)

- **Neue Konstruktorparameter:** `ArbeitsverzeichnisOeffnenService arbeitsverzeichnisOeffnenService`, `IdeOeffnenService ideOeffnenService` (Constructor Injection, analog zu den übrigen Diensten). `IDialogService` ist bereits injiziert.
- **Neue Felder:** `_arbeitsverzeichnisOeffnenService`, `_ideOeffnenService`, `_solutionPfade` (`IReadOnlyList<string>`, initial leer).
- **Neue Properties:** `SolutionFileExists` (`bool`, read-only) — `true`, wenn mindestens eine Solution gefunden wurde. Steuert `OeffneIdeCommand.CanExecute`.
- **Neue Commands:** `OeffneArbeitsverzeichnisCommand` (`RelayCommand`, `CanExecute` = `ShowFileExplorerPanel`), `OeffneIdeCommand` (`AsyncRelayCommand`, `CanExecute` = `SolutionFileExists`; asynchron wegen des potenziellen Auswahl-Dialogs).
- **Neue Methoden:** `OeffneArbeitsverzeichnis()` (ruft den Dienst, fängt/loggt Fehler → `FehlerMeldung`); `OeffneIdeAsync()` (ermittelt Einzel-/Mehrfachfall, zeigt ggf. `_dialogService.ShowSolutionSelectionDialogAsync`, ruft `IdeOeffnenService.OeffneSolution`, fängt/loggt Fehler → `FehlerMeldung`).
- **Geänderte Methode:** Setter von `Aufgabe` — zusätzlich `_solutionPfade = _ideOeffnenService.FindeSolutions(value?.LokalerKlonPfad)` cachen und `OnPropertyChanged(nameof(SolutionFileExists))` auslösen.

### `IDialogService` (App-Service-Interface)

- **Neue Methode:** `Task<string?> ShowSolutionSelectionDialogAsync(IReadOnlyList<string> solutionPfade, CancellationToken ct = default)` — zeigt den Auswahl-Dialog und liefert den gewählten Pfad oder `null` bei Abbruch.

### `WpfDialogService` (App-Service-Implementierung)

- **Neue Methode:** Implementierung von `ShowSolutionSelectionDialogAsync` analog zu `ShowIssueSelectionDialogAsync`: `Application.Current.Dispatcher.InvokeAsync`, `SolutionSelectionDialog` mit `Owner = MainWindow`, Rückgabe des gewählten Pfades bei `DialogResult == true`, sonst `null`.

### `TaskDetailView.xaml` (View)

- Neue Ribbon-Gruppe „Dateien" mit vier `RibbonLargeButton` (Bindung an `FileExplorer.StandardAnsichtCommand`, `FileExplorer.VergleichCommand`, `FileExplorer.AktualisierenCommand`, `FileExplorer.DateiMitStandardanwendungOeffnenCommand`), `AutomationName` je Button; Gruppen-`Visibility` an `ShowFileExplorerPanel`.
- Neue Ribbon-Gruppe „Werkzeuge" (immer sichtbar) mit zwei `RibbonLargeButton`: „Arbeitsverzeichnis öffnen" (`OeffneArbeitsverzeichnisCommand`, `AutomationName="ArbeitsverzeichnisOeffnen"`) und „IDE öffnen" (`OeffneIdeCommand`, `AutomationName="IdeOeffnen"`).
- Gruppe „CLI": `Visibility="{Binding ShowCliPanel, Converter={StaticResource BoolToVisibilityConverter}}"` ergänzen.

### `FileExplorerView.xaml` (View)

- Entfernen der Mode-Umschaltungs-Buttons „Standard"/„Vergleich"/„Aktualisieren" (obere `StackPanel`, Grid.Row=0) und des „Datei öffnen"-Buttons (`FileExplorerDateiOeffnenButton`) aus der rechten Spalte.
- Die Diff-Navigations-Buttons (`FileExplorerVorherigeAenderungButton`, `FileExplorerNaechsteAenderungButton`) bleiben erhalten.

### `App.xaml.cs` (Composition Root)

- DI-Registrierung: `ArbeitsverzeichnisOeffnenService`, `IdeOeffnenService` (analog zu bestehenden App-Diensten).
- `IProzessStarter`: bei gesetztem `SOFTWARESCHMIEDE_TEST_DB_PATH` `AufzeichnenderProzessStarter`, sonst `SystemProzessStarter` (Singleton, analog zum bestehenden `IPseudoConsoleProcessLauncher`-Switch).
- `SolutionSelectionDialog` benötigt keine eigene DI-Registrierung (wird vom `WpfDialogService` direkt instanziiert, analog `IssueSelectionDialog`).

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `LokalerKlonPfad` (Arbeitsverzeichnis öffnen) | Nicht leer und Verzeichnis existiert (bereits durch `ShowFileExplorerPanel`/`CanExecute` abgedeckt) | Button deaktiviert; kein Aufruf |
| `_solutionPfade` (IDE öffnen) | Mindestens eine `*.sln` im Arbeitsverzeichnis vorhanden (durch `SolutionFileExists`/`CanExecute` abgedeckt) | Button deaktiviert; kein Aufruf |

Prozessstart-Fehler (z. B. Handler/IDE nicht installiert) werden im Dienst abgefangen, geloggt und als `FehlerMeldung` angezeigt — keine eigenständige Eingabevalidierung nötig.

## Konfigurationsänderungen

Keine. (Bewusster Verzicht auf IDE-Pfad-/IDE-Typ-Konfiguration, siehe Designentscheidung „IDE-Start".)

## Seiteneffekte und Risiken

- **`FileExplorerView`:** Nach Entfernen der Aktionsbuttons müssen die zugehörigen `AutomationName`-Referenzen (`FileExplorerStandardButton`, `FileExplorerVergleichButton`, `FileExplorerAktualisierenButton`, `FileExplorerDateiOeffnenButton`) in E2E-Tests auf die neuen Ribbon-Buttons umgestellt werden. Andernfalls schlägt `E2E_FileExplorer` fehl.
- **CLI-Gruppe:** Das neu hinzugefügte `Visibility`-Binding an `ShowCliPanel` blendet die gesamte CLI-Gruppe aus, wenn kein CLI-Panel aktiv ist. Bestehende Ribbon-Interaktionen mit CLI-Buttons erfolgen nur in Status Gestartet/Wartend, sodass kein bestehender Ablauf betroffen sein sollte — vor der Umsetzung Sicht-Regressionen prüfen.
- **`TaskDetailViewModel`-Konstruktor:** Zwei neue Pflichtparameter brechen alle Konstruktionsstellen in Tests (kompilierzeitlich). Zentral über `TaskDetailViewModelTestFactory` behebbar.
- **`IDialogService`-Erweiterung:** Die neue Methode bricht alle Mocks/Fakes von `IDialogService` in Tests nur, wenn sie explizit implementiert statt gemockt werden. Bei `Mock<IDialogService>` (Moq) genügt ein Setup; strikte Fakes müssen die Methode ergänzen.
- **Synchroner Dateisystemzugriff im `Aufgabe`-Setter:** `FindeSolutions` (ein `EnumerateFiles` auf oberster Ebene) wird einmalig pro Aufgabenwechsel ausgeführt und gecacht — analog zum bestehenden `Directory.Exists`-Aufruf, geringes Risiko.

## Umsetzungsreihenfolge

1. **`ProzessStartAnfrage` Value Object anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Record mit `DateiName`, `Argumente` (`string?`), `ShellAusfuehren` (`bool`) in `Softwareschmiede.Domain.ValueObjects`.

2. **`IProzessStarter` Interface anlegen**
   - Voraussetzungen: `ProzessStartAnfrage`.
   - Beschreibung: `void Starten(ProzessStartAnfrage anfrage)` in `Softwareschmiede.Domain.Interfaces`.

3. **`SystemProzessStarter` implementieren**
   - Voraussetzungen: `IProzessStarter`, `ProzessStartAnfrage`.
   - Beschreibung: Mappt die Anfrage auf `ProcessStartInfo` und ruft `Process.Start`; Logging analog zu `SystemShutdownService`.

4. **`AufzeichnenderProzessStarter` implementieren**
   - Voraussetzungen: `IProzessStarter`, `ProzessStartAnfrage`.
   - Beschreibung: Testmodus-Implementierung; schreibt jede Anfrage als Zeile in eine Logdatei (Pfad aus dem Verzeichnis von `SOFTWARESCHMIEDE_TEST_DB_PATH` abgeleitet, Konstante z. B. `prozess-starts.log`).

5. **`ArbeitsverzeichnisOeffnenService` implementieren**
   - Voraussetzungen: `IProzessStarter`, `ProzessStartAnfrage`.
   - Beschreibung: `Oeffne(string arbeitsverzeichnis)` löst plattformabhängig den Öffnen-Befehl auf und ruft `IProzessStarter.Starten(...)`.

6. **`IdeOeffnenService` implementieren**
   - Voraussetzungen: `IProzessStarter`, `ProzessStartAnfrage`.
   - Beschreibung: `FindeSolutions(string? arbeitsverzeichnis)` → `IReadOnlyList<string>` (alle `*.sln` oberste Ebene, alphabetisch sortiert; leere Liste bei fehlendem/leerem Pfad) und `OeffneSolution(string solutionPfad)` (Shell-Execute).

7. **`SolutionSelectionDialogViewModel` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Presentation Model mit `Solutions` (`IReadOnlyList<string>`) und `SelectedSolution` (`string?`) in `Softwareschmiede.App.ViewModels`.

8. **`SolutionSelectionDialog` (Window) anlegen**
   - Voraussetzungen: `SolutionSelectionDialogViewModel`.
   - Beschreibung: Modaler WPF-Dialog analog `IssueSelectionDialog` (Liste der Solutions, OK/Abbrechen, `AutomationName`s für E2E).

9. **`IDialogService` + `WpfDialogService` erweitern**
   - Voraussetzungen: `SolutionSelectionDialog`, `SolutionSelectionDialogViewModel`.
   - Beschreibung: Methode `ShowSolutionSelectionDialogAsync(IReadOnlyList<string>, CancellationToken)` deklarieren und in `WpfDialogService` implementieren.

10. **DI-Registrierungen in `App.xaml.cs`**
    - Voraussetzungen: Schritte 3–6.
    - Beschreibung: `IProzessStarter` (Test-Switch), `ArbeitsverzeichnisOeffnenService`, `IdeOeffnenService` registrieren.

11. **`TaskDetailViewModel` erweitern**
    - Voraussetzungen: `ArbeitsverzeichnisOeffnenService`, `IdeOeffnenService` (DI verfügbar, Schritt 10), `IDialogService.ShowSolutionSelectionDialogAsync` (Schritt 9).
    - Beschreibung: Konstruktorparameter, Felder, `SolutionFileExists`, `OeffneArbeitsverzeichnisCommand`, `OeffneIdeCommand`, Methoden (`OeffneArbeitsverzeichnis`, `OeffneIdeAsync` inkl. Auswahl-Dialog-Logik) und `Aufgabe`-Setter-Caching ergänzen.

12. **`TaskDetailViewModelTestFactory` anpassen**
    - Voraussetzungen: Schritt 11.
    - Beschreibung: Zwei neue Dienste (mit `Mock<IProzessStarter>` bzw. Fakes) instanziieren und an den Konstruktor übergeben; `Mock<IDialogService>` um Setup für die neue Methode ergänzen, damit die Testsuite wieder kompiliert.

13. **`TaskDetailView.xaml` erweitern**
    - Voraussetzungen: Schritt 11.
    - Beschreibung: Gruppen „Dateien" und „Werkzeuge" anlegen, CLI-Gruppe-`Visibility` ergänzen, Bindungen und `AutomationName`s setzen.

14. **`FileExplorerView.xaml` bereinigen**
    - Voraussetzungen: Schritt 13 (Ribbon-Buttons vorhanden, damit keine Funktionalität verlorengeht).
    - Beschreibung: Überführte Buttons entfernen; Diff-Navigation belassen.

15. **Unit-Tests schreiben** (siehe Abschnitt Tests).
    - Voraussetzungen: Schritte 5, 6, 11.

16. **E2E-Tests anpassen/erweitern** (siehe Abschnitt Tests).
    - Voraussetzungen: Schritte 13, 14 + Testmodus-`IProzessStarter` (Schritt 4/10).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Oeffne_StartetPlattformbefehlMitVerzeichnis` | `ArbeitsverzeichnisOeffnenServiceTests` (neu) | `IProzessStarter` erhält die korrekte `ProzessStartAnfrage` (Windows: `explorer.exe` + Verzeichnis). |
| `FindeSolutions_LiefertAlleSlnAlphabetischSortiert` | `IdeOeffnenServiceTests` (neu) | Bei mehreren `*.sln` werden alle als sortierte Liste geliefert. |
| `FindeSolutions_OhneSln_LiefertLeereListe` | `IdeOeffnenServiceTests` (neu) | Ohne `.sln` bzw. bei fehlendem/leerem Pfad → leere Liste. |
| `OeffneSolution_StartetShellExecuteFuerSln` | `IdeOeffnenServiceTests` (neu) | `IProzessStarter` erhält Anfrage mit `ShellAusfuehren = true` und dem `.sln`-Pfad. |
| `OeffneArbeitsverzeichnisCommand_CanExecute_FolgtShowFileExplorerPanel` | `TaskDetailViewModelTests` | `CanExecute` nur `true`, wenn Arbeitsverzeichnis existiert. |
| `OeffneArbeitsverzeichnisCommand_RuftDienstMitLokalemKlonPfad` | `TaskDetailViewModelTests` | Ausführung delegiert an `ArbeitsverzeichnisOeffnenService` mit korrektem Pfad (Mock). |
| `SolutionFileExists_WirdBeimLadenGesetzt` | `TaskDetailViewModelTests` | `SolutionFileExists`/`OeffneIdeCommand.CanExecute` folgen dem `FindeSolutions`-Ergebnis (Mock). |
| `OeffneIdeCommand_MitEinerSolution_OeffnetOhneDialog` | `TaskDetailViewModelTests` | Bei genau einer Solution wird `IdeOeffnenService.OeffneSolution` direkt mit dem Pfad aufgerufen, ohne `ShowSolutionSelectionDialogAsync`. |
| `OeffneIdeCommand_MitMehrerenSolutions_ZeigtAuswahlDialog` | `TaskDetailViewModelTests` | Bei mehreren Solutions wird `ShowSolutionSelectionDialogAsync` aufgerufen und die zurückgegebene Solution an `OeffneSolution` übergeben (Mock). |
| `OeffneIdeCommand_MitMehrerenSolutions_AbbruchOeffnetKeine` | `TaskDetailViewModelTests` | Liefert der Dialog `null`, wird `OeffneSolution` nicht aufgerufen (Mock). |
| `Starten_SchreibtAnfrageInLogdatei` | `AufzeichnenderProzessStarterTests` (neu) | Aufzeichnender Starter serialisiert die Anfrage korrekt (Grundlage der E2E-Beobachtung). |
| `WaitForProzessStartEintragAsync(substring)` (Hilfsmethode) | `WpfTestBase` | Wartet, bis die Prozessstart-Logdatei einen Eintrag mit dem erwarteten Text enthält. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTestFactory` | Neue Konstruktorparameter von `TaskDetailViewModel` (zwei Dienste) müssen übergeben werden; `Mock<IDialogService>` ggf. um Setup für `ShowSolutionSelectionDialogAsync` ergänzen. |
| `TaskDetailViewModelTests` | Konstruieren `TaskDetailViewModel` (über Factory bzw. `new`) — nach Signaturänderung anzupassen. |
| `TaskDetailViewModelTests_ZeitgesteuerterPrompt` | Ebenfalls Konstruktionsstelle von `TaskDetailViewModel`. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Klick auf „Arbeitsverzeichnis öffnen" zeichnet einen Prozessstart des OS-Dateiexplorers mit dem `LokalerKlonPfad` auf | `E2E_FileExplorer` (neue Phase) bzw. neue `E2E_VerzeichnisAktionen` | „Arbeitsverzeichnis öffnen" startet den OS-Dateiexplorer mit korrektem Pfad. |
| Klick auf „IDE öffnen" bei genau einer `*.sln` zeichnet einen Shell-Execute-Start der `.sln` auf | `E2E_FileExplorer` (neue Phase) bzw. neue `E2E_VerzeichnisAktionen` | „IDE öffnen" öffnet die Solution des Arbeitsverzeichnisses ohne Dialog. |
| Klick auf „IDE öffnen" bei mehreren `*.sln` zeigt den Auswahl-Dialog; nach Auswahl wird die gewählte `.sln` per Shell-Execute gestartet | `E2E_FileExplorer` (neue Phase) bzw. neue `E2E_VerzeichnisAktionen` | Bei mehreren Solutions erscheint ein Auswahl-Dialog; die gewählte Solution wird geöffnet. |
| „IDE öffnen" ist ohne `*.sln` deaktiviert | `E2E_FileExplorer` (neue Phase) | Button-Aktivierbarkeit abhängig von Solution-Existenz. |
| Überführte Ribbon-Buttons (Standard/Vergleich/Aktualisieren/Datei öffnen) sind im Ribbon erreichbar und schalten die Ansicht | `E2E_FileExplorer` (angepasst) | Aktionsbuttons wurden ins Ribbon überführt und funktionieren. |

Empfehlung: Die neuen Szenarien als zusätzliche Phasen in `E2E_FileExplorer` (bereits vorhandenes Klon-Setup, Repository → `LokalerKlonPfad` gesetzt) ausführen — konsistent mit der Konsolidierungslinie (Issue #153). Alle neuen/erweiterten E2E-Tests laufen unter `[OsInterface]` / `Category=OsInterface`.

Betroffene bestehende E2E-Tests:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E_FileExplorer.DateiExplorer_ZeigtBaumUndModeButtons_UndWechseltZuInfoUndZurueck_E2E` | Prüft `FileExplorerStandardButton`/`FileExplorerVergleichButton`/`FileExplorerAktualisierenButton`/`FileExplorerDateiOeffnenButton` in der View; diese wandern ins Ribbon und erhalten neue `AutomationName`s — Assertions umstellen. |

## Offene Punkte

Keine.
