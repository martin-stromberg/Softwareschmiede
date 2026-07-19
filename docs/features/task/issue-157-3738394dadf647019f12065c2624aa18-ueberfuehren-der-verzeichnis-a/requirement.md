# Kundenanforderung: Überführen der Verzeichnis-Aktionsbuttons in das Ribbon-Menü

## Fachliche Zusammenfassung

Die Buttons des Dateiexplorers (Ansichtswechsel zwischen „Standard" und „Vergleich", Refresh, Dateiöffnung) werden aus der Baum-UI in eine neue Ribbon-Gruppe „Dateien" integriert. Zusätzlich werden zwei neue Aktionsbuttons hinzugefügt: „Arbeitsverzeichnis öffnen" (zum Starten des Betriebssystem-Dateiexplorers) und „IDE öffnen" (Visual Studio mit der Solution-Datei des Arbeitsverzeichnisses, falls vorhanden). Die Sichtbarkeit der Ribbon-Gruppen wird dynamisch angepasst: die Gruppe „CLI" nur wenn das CLI-Panel angezeigt wird, die Gruppe „Dateien" nur wenn der Dateibrowser aktiv ist — mit Ausnahme der beiden neuen Buttons, die immer sichtbar sind.

## Betroffene Klassen und Komponenten

### ViewModel-Klassen
- `TaskDetailViewModel` (Erweiterung um neue Properties für Sichtbarkeitsbindungen)
- `FileExplorerViewModel` (keine Änderungen nötig — Commands existieren bereits)

### Service-Klassen (neu oder erweitert)
- `WorkspaceExplorerService` oder `WorkdirService` (NEU, für „Arbeitsverzeichnis öffnen")
- `IdeService` oder `VisualStudioService` (NEU, für „IDE öffnen")

### UI-Komponenten (XAML/Views)
- Ribbon-Struktur in `TaskDetailView.xaml` oder zentraler Ribbon-Definition
- Neue Ribbon-Gruppe „Dateien" mit Buttons für:
  - Ansichtswechsel (Standard/Vergleich)
  - Refresh
  - Dateiöffnung
  - Arbeitsverzeichnis öffnen
  - IDE öffnen

### Enums/Value Objects
- Eventuell neue Enum für IDE-Typen (Visual Studio, VS Code, etc.), falls mehrere IDEs unterstützt werden sollen

### Tests
- Unit-Tests für `TaskDetailViewModel`-Sichtbarkeitseigenschaften (Bindung zwischen `ShowCliPanel`, `ShowFileExplorerPanel` und Ribbon-Sichtbarkeit)
- E2E-Tests für die neuen Buttons „Arbeitsverzeichnis öffnen" und „IDE öffnen"

## Implementierungsansatz

1. **Neue Services erstellen:**
   - `WorkspaceExplorerService.OpenWorkingDirectoryAsync(workingDirectoryPath)`: Startet den Standard-Dateiexplorer des OS mit dem übergebenen Verzeichnis (Windows: `explorer.exe`, Linux: `xdg-open`, macOS: `open`).
   - `IdeService.OpenSolutionAsync(solutionFilePath)`: Sucht nach `*.sln`-Dateien im Repository, startet Visual Studio (oder eine konfigurierbare IDE) mit der Solution, falls vorhanden.

2. **TaskDetailViewModel erweitern:**
   - Neue Commands: `OeffneArbeitsverzeichnisCommand`, `OeffneIdeCommand`
   - Neue Properties für Sichtbarkeit:
     - `ShowFileSystemGroup` (gebunden an `ShowFileExplorerPanel`)
     - `ShowSystemActionsGroup` (immer `true` — für „Arbeitsverzeichnis öffnen" und „IDE öffnen")
   - Property `SolutionFileExists`: `bool` — ermöglicht, ob `OeffneIdeCommand` ausführbar ist
   - Dependency Injection für neue Services

3. **FileExplorerViewModel-Integration:**
   - Keine Änderungen am ViewModel selbst; die Commands und Properties werden wie bisher genutzt, nur über das Ribbon statt über die Baum-UI aufgerufen.

4. **Ribbon-UI (XAML):**
   - Neue `RibbonGroup` „Dateien" mit Buttons für `StandardAnsichtCommand`, `VergleichCommand`, `AktualisierenCommand`, `DateiMitStandardanwendungOeffnenCommand`
   - Neue `RibbonGroup` für Systemaktionen („Arbeitsverzeichnis öffnen", „IDE öffnen")
   - Binding der `Visibility` beider Gruppen an die entsprechenden ViewModel-Properties
   - Disabling von Buttons basierend auf `CanExecute` der Commands

5. **Abhängigkeiten:**
   - Die neuen Services benötigen Zugriff auf `ProcessStartService` oder direkten `Process.Start()`-Zugriff für das OS.
   - `IdeService` muss nach Solution-Dateien scannen — nutzt möglicherweise `IGitWorkspaceBrowserService` oder direkten `Directory.EnumerateFiles()`-Zugriff.
   - Beide Services müssen über DI in `TaskDetailViewModel` verfügbar sein.

## Konfiguration

### Ebene: Anwendungseinstellungen
- Optional: IDE-Typ oder IDE-Pfad (falls mehrere IDEs unterstützt werden sollen, z. B. Visual Studio, VS Code, JetBrains Rider).
- Optional: Dateiexplorer-Befehl (z. B. für Custom-Explorer auf Linux).

### Ebene: TaskDetailViewModel-Initialization
- Services werden per Constructor Injection eingespritzt; keine runtime-Konfiguration nötig.

## Offene Fragen

1. **Unterstützung mehrerer IDEs:** Ist nur Visual Studio gewünscht, oder sollen auch VS Code und andere IDEs unterstützt werden?
2. **Fehlerbehandlung bei fehlender IDE:** Was soll passieren, wenn Visual Studio nicht installiert ist und der „IDE öffnen"-Button geklickt wird? (Fehlermeldung, Silent Fail, Button deaktiviert)
3. **Solution-Datei-Suche:** Sollen alle `*.sln`-Dateien im Repository aufgelistet werden, oder nur eine spezifische (z. B. mit Namen der Aufgabe/Branch)?
4. **Sichtbarkeit von „Arbeitsverzeichnis öffnen" und „IDE öffnen":** Aktuell ist vorgegeben, dass diese immer sichtbar sind. Sollten sie trotzdem deaktiviert (disabled) sein, wenn kein Repository/Arbeitsverzeichnis vorhanden ist?
