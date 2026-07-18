# Bestandsaufnahme: E2E-Tests und Testinfrastruktur

## E2E-Test-Klassen

Alle E2E-Test-Klassen befinden sich in `src/Softwareschmiede.Tests/E2E/` und erben von `WpfTestBase`. Sie sind mit `[Trait("Category", "E2E")]`, `[OsInterface]` und `[Collection("E2E")]` annotiert.

### Aufgaben-CRUD-Tests

#### `E2E_CreateNewTaskNavigation`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`

Tests für die Neuanlage von Aufgaben über die separate Aufgabendetailansicht (Feature 72).

| Testmethode | Beschreibung |
|-------------|-------------|
| `NeueAufgabeErstellenUndSpeichern_ErscheintInListeUndNavigiertZurueck_E2E()` | Erstellt neue Aufgabe, füllt Titel, speichert; prüft Persistierung und Navigation zurück |
| `NeueAufgabeAbbrechen_NavigiertZurueckOhneTitelAenderungZuSpeichern_E2E()` | Erstellt Aufgabe, gibt Titel ein, bricht ab; prüft, dass Titel nicht persistiert wird |

**Beobachtung:** Diese beiden Tests könnten konsolidiert werden in einen Test, der Create→Save, dann Create→Cancel durchläuft.

#### `E2E_TaskDetailNavigation`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`

Tests für die separate, fensterumfassende Aufgabendetailansicht (Feature 72).

| Testmethode | Beschreibung |
|-------------|-------------|
| `AufgabeOeffnen_ZeigtTaskDetailViewFensterumfassend_E2E()` | Öffnet Aufgabe per Doppelklick; prüft TaskDetailView fensterumfassend |
| `TaskDetailView_ZeigtKorrekteAufgabendaten_E2E()` | Prüft, dass TaskDetailView korrekte Aufgabendaten (Titel) anzeigt |
| `ZurueckButtonInTaskDetail_NavigiertZuProjectDetailView_E2E()` | Prüft Navigation zurück zur ProjectDetailView |

**Beobachtung:** Diese drei Tests könnten in einen konsolidiert werden: Create→Navigate→ViewData→Back.

### Aufgaben-Ausführungs-Tests

#### `E2E_AufgabeStarten`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_AufgabeStarten.cs`

Tests für den kombinierten Start-Ablauf (Klonen + CLI-Start) der Aufgabendetailansicht (Feature 72).

| Testmethode | Beschreibung |
|-------------|-------------|
| `AufgabeStarten_KlontRepositoryUndStartetCli_E2E()` | Startet Aufgabe, prüft Repository-Klonen und CLI-Start; testet auch Fehlerfall ohne Bestätigung |

### Plugin-Management-Tests

#### `E2E_PluginSelectionDialog`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_PluginSelectionDialog.cs`

Tests für die Anzeige des Plugin-Auswahl-Dialogs beim Starten ohne gespeichertes Plugin.

| Testmethode | Beschreibung |
|-------------|-------------|
| `StartenOhneGespeichertesPlugin_ZeigtPluginAuswahlDialog_E2E()` | Zeigt Plugin-Dialog, wählt Plugin, bestätigt mit OK; prüft CLI-Start |
| `PluginAuswahlAbbrechen_StartetNichtUndBleibtImStatusNeu_E2E()` | Startet Dialog, bricht ab; prüft, dass Start nicht fortgesetzt wird |

**Beobachtung:** Diese beiden Tests könnten konsolidiert werden: Dialog→Select→OK, dann Dialog→Cancel.

#### `E2E_PluginWechsel`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_PluginWechsel.cs`

Tests für den Plugin-Wechsel bei laufender CLI über den "Plugin ändern"-Button (Feature 72).

| Testmethode | Beschreibung |
|-------------|-------------|
| `PluginAendernBeiLaufenderCli_StopptUndStartetMitNeuemPlugin_E2E()` | Startet mit Simulator-Plugin, wechselt zu Claude-Plugin; prüft Neustart der CLI |

#### `E2E_PluginProjectDefault`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_PluginProjectDefault.cs`

Tests für das Speichern eines Projekt-Standard-KI-Plugins über die Checkbox im Plugin-Auswahl-Dialog.

| Testmethode | Beschreibung |
|-------------|-------------|
| `PluginDialogMitProjektCheckbox_SpeichertProjektStandardUndStartetCli_E2E()` | Wählt Plugin mit "Für dieses Projekt verwenden"-Checkbox; prüft Speicherung und CLI-Start |

#### `E2E_PluginProjectDefault_NextTask`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_PluginProjectDefault_NextTask.cs`

(Nicht vollständig geprüft, aber vermutlich Verifikation, dass ein Projekt-Standard das Plugin in der nächsten Aufgabe vorhält)

### Weitere E2E-Tests

Folgende weitere E2E-Test-Klassen existieren, wurden aber nicht vollständig analysiert:

| Klasse | Zweck |
|--------|-------|
| `E2E_ArbeitsstatusAktualisierung` | Tests für Arbeitsstatus-Updates |
| `E2E_AutoStartCli` | Tests für Auto-Start-CLI-Funktionalität |
| `E2E_ConPtyKeyboardInput` | Tests für Tastatur-Input in ConPTY-Prozessen |
| `E2E_ConPtyProcessEnd` | Tests für ConPTY-Prozess-Ende-Szenarien |
| `E2E_ConPtyResize` | Tests für Terminal-Resize in ConPTY |
| `E2E_ConPtyTerminalStart` | Tests für Terminal-Start in ConPTY |
| `E2E_FileExplorer` | Tests für File-Explorer-Integration |
| `E2E_SettingsCommandLineParameters` | Tests für Command-Line-Parameter in Settings |
| `E2E_SettingsKiPluginPersistence` | Tests für KI-Plugin-Persistierung in Settings |
| `E2E_TaskExecutionCommandLineParameters` | Tests für Command-Line-Parameter bei Aufgaben-Ausführung |
| `E2E_TaskWechselUeberMenue` | Tests für Aufgaben-Wechsel über Menü |
| `E2E_TerminalAusgabeIntegritaet` | Tests für Terminal-Ausgabe-Integrität |
| `E2E_VersionAnzeige` | Tests für Versions-Anzeige |
| `E2E_WorkingDirectory` | Tests für Working-Directory-Handling |
| `E2E_ZeitgesteuerterPrompt` | Tests für zeitgesteuerte Prompts |

## Testinfrastruktur-Klassen

### `AppStartupLogInspector`
(Wird von `WpfTestBase` verwendet für Diagnose)

Inspiziert die App-Logs beim Startup, um Fehler zu erkennen.

### `CredentialStoreSnapshot`
(Wird von `WpfTestBase` verwendet)

Erstellt einen Snapshot des Windows Credential Stores und kann ihn wiederherstellen, um Test-Isolation zu gewährleisten.

### `WindowsCredentialStore`
(Wird von `WpfTestBase` und Tests verwendet)

Wrapper für Windows Credential Store API.

### `ConPtyEnvironmentProbe`
(Wird von `WpfTestBase.SkipWennConPtyNichtVerfuegbar()` verwendet)

Prüft, ob ConPTY-Umgebung verfügbar ist (zur Entscheidung über Test-Skip).

## Test-Execution-Pattern

### Standardmuster (z.B. E2E_CreateNewTaskNavigation.NeueAufgabeErstellenUndSpeichern_E2E())

```csharp
1. StartAndNavigateToProjects("ProjektName") → App starten, Projekt anlegen/öffnen
2. WaitForElement(...) → UI-Element warten
3. Keyboard.Type(...) / Click() → UI-Interaktionen
4. Assert.NotNull(...) oder Assert.Null(...) → Verifikation
```

### Setup-intensives Muster (z.B. E2E_AufgabeStarten.AufgabeStarten_KlontRepositoryUndStartetCli_E2E())

```csharp
1. SetupProjectMitNeuerAufgabe(...) → Komplexes Setup: App, Plugin-Konfiguration, Projekt, Repository-Zuweisung, Aufgabe
2. StartenUndPluginWaehlen(...) → Plugin-Dialog
3. WaitForElement(...) / Assert.NotNull(...) → Verifikation
```

## Hilfsmethoden-Nutzung in Tests

Alle E2E-Tests verwenden die `protected`-Hilfsmethoden aus `WpfTestBase`:

- `StartAndNavigateToProjects()` - für einfaches Setup
- `SetupProjectMitNeuerAufgabe()` - für komplexes Setup mit Repository
- `WaitForElement()` - für Element-Wartenlogik (universell)
- `WaitForWindow()` - für Dialog-Wartenlogik
- `SelectComboBoxItemByClick()` - für ComboBox-Interaktionen
- `StartenUndPluginWaehlen()` - für Plugin-Dialog-Bedienung
- `CreateProject()` / `OpenProject()` - für Projekt-Operationen
- `NavigateToSettings()` / `NavigateToProjecten()` - für Navigation
- `ConfirmLocalDirectoryGitInitInSourceDirectory()` - für Credential-Setup

## Konsolidierungs-Potenzial

Auf Basis dieser Analyse können folgende Tests konsolidiert werden:

1. **E2E_CreateNewTaskNavigation**: Beide Tests könnten zu einem Test "TaskCrudOperations_AnlageUndAbbruch_E2E()" konsolidiert werden
2. **E2E_TaskDetailNavigation**: Alle drei Tests könnten zu "TaskDetailOperations_OeffnenDatenViewZurueck_E2E()" konsolidiert werden
3. **E2E_PluginSelectionDialog**: Beide Tests könnten zu "PluginSelectionFlow_AuswahlOkUndCancel_E2E()" konsolidiert werden

Dies hätte den Vorteil:
- Weniger Prozess-Starts pro Testlauf
- Schnellere Gesamttestlaufzeit
- Bessere Szenario-Abdeckung pro Testmethode (mehrere Schritte im gleichen App-Lifecycle)
- Erhalt der Granularität durch mehrere Assert-Punkte in einer Testmethode
