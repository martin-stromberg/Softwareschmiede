# Umsetzungsplan: Export der Rohausgabe der CLI

## Übersicht

Die Aufgabendetailansicht wird um eine explizite Exportaktion erweitert, mit der Anwender die bisher protokollierte CLI-Rohausgabe als `*.raw` speichern können. Betroffen sind die Task-Detail-UI (`TaskDetailView.xaml`), deren Orchestrierung (`TaskDetailViewModel`) sowie die Dialog-Abstraktion (`IDialogService`/`WpfDialogService`) für die Dateipfadauswahl. Die bestehende Protokollpersistenz (`ProtokollService`, `ProtokollTyp.CliOutput`) wird wiederverwendet; Aufgabenstatus und CLI-Lifecycle bleiben unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Export-Orchestrierung im UI-Fluss | **Service Layer** über `TaskDetailViewModel` + `IDialogService` | Die bestehende MVVM-Struktur orchestriert Benutzeraktionen bereits im ViewModel; der Export fügt sich als weiterer Command ohne Bruch in das Muster ein. |
| Datenquelle für den Export | Persistenz-Snapshot über `ProtokollService.GetByAufgabeAsync` und Filter auf `ProtokollTyp.CliOutput` | „Bisher angefallene“ Ausgabe wird verlässlich aus dem aktuellen DB-Stand gebildet, unabhängig davon, ob die UI-Liste bereits vollständig geladen ist. |
| Format der `.raw`-Datei | Reine Zeileninhalte aus `Protokolleintrag.Inhalt` in bestehender Reihenfolge (`Zeitstempel`) als Plain Text (UTF-8 ohne BOM) | Entspricht der Anforderung „Rohausgabe“, vermeidet zusätzliche Metadaten und liefert ein weiterverarbeitbares, konsistentes Textformat. |

## Programmabläufe

### CLI-Rohausgabe exportieren (Happy Path)

1. Benutzer klickt in `TaskDetailView.xaml` auf den neuen Ribbon-Button (z. B. Automation-Name `CliRawExport`), gebunden an `ExportCliRawCommand`.
2. `TaskDetailViewModel.ExportCliRawAsync` prüft Vorbedingungen (`AufgabeId` gesetzt, Aufgabe geladen).
3. ViewModel ruft `IDialogService.ShowSaveFileDialogAsync(...)` auf (Filter `Raw files (*.raw)|*.raw`), optional mit Dateinamensvorschlag.
4. Nach gültiger Auswahl lädt das ViewModel den Persistenz-Snapshot über `_protokollService.GetByAufgabeAsync(_aufgabeId, ct)`.
5. ViewModel filtert auf `ProtokollTyp.CliOutput`, erhält die vorhandene Reihenfolge und erstellt den Exportinhalt ausschließlich aus `Inhalt`.
6. ViewModel schreibt die Datei asynchron mit UTF-8 ohne BOM auf den gewählten Pfad.
7. Bei Erfolg bleibt die aktuelle Ansicht unverändert; Fehlerzustand bleibt leer.

Beteiligte Klassen/Komponenten: `TaskDetailView`, `TaskDetailViewModel`, `IDialogService`, `WpfDialogService`, `ProtokollService`, `Protokolleintrag`, `ProtokollTyp`

### Export abbrechen

1. Benutzer startet Export, bricht den Save-Dialog ab.
2. `IDialogService.ShowSaveFileDialogAsync(...)` liefert `null`.
3. `TaskDetailViewModel.ExportCliRawAsync` beendet ohne Dateischreiben und ohne Fehleranzeige.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `IDialogService`, `WpfDialogService`

### Exportfehler behandeln

1. Dateischreiben schlägt fehl (z. B. ungültiger Pfad, fehlende Berechtigung, I/O-Fehler).
2. `TaskDetailViewModel.ExportCliRawAsync` fängt die Exception, protokolliert via `_logger`.
3. `FehlerMeldung` wird mit fachlichem Hinweis gesetzt; restlicher UI-/CLI-Zustand bleibt unverändert.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `WpfDialogService`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `IDialogService` (Interface)

- **Neue Methoden:** `ShowSaveFileDialogAsync` — liefert den vom Benutzer gewählten Zielpfad oder `null` bei Abbruch; Parameter für Titel, Filter, Standard-Dateiname und optionales Initialverzeichnis.

### `WpfDialogService` (Service)

- **Neue Methoden:** `ShowSaveFileDialogAsync` — öffnet `Microsoft.Win32.SaveFileDialog` auf dem UI-Dispatcher und gibt den gewählten Pfad zurück.
- **Geänderte Methoden:** keine fachliche Verhaltensänderung bestehender Methoden; Erweiterung nur additiv.

### `TaskDetailViewModel` (ViewModel)

- **Neue Eigenschaften:** `KannCliRawExportieren` (`bool`) — steuert Verfügbarkeit der Exportaktion (z. B. Aufgabe vorhanden + gültige `AufgabeId`).
- **Neue Methoden:** `ExportCliRawAsync(CancellationToken ct)` — orchestriert Dialog, Datenabruf, Filterung und Dateischreiben.
- **Geänderte Methoden:** Konstruktor (`TaskDetailViewModel(...)`) — registriert neuen `AsyncRelayCommand`; ggf. bestehende `OnPropertyChanged`-Blöcke ergänzen, damit `CanExecute` sauber aktualisiert wird.
- **Neue Events:** Keine.
- **Neue Event-Handler:** Keine.

### `TaskDetailView` (`TaskDetailView.xaml`, View)

- **Geänderte Bindings/Elemente:** Neuer Ribbon-Button in der Gruppe `CLI` mit Binding auf `ExportCliRawCommand` und stabilem Automation-Namen für UI/E2E-Tests.

### `TaskDetailView` (`src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`, E2E-View-Objekt)

- **Neue Methoden:** Hilfsmethoden zum Auslösen der Exportaktion und Bedienen des Save-Dialogs im E2E-Szenario.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Export-Zielpfad (Dialogergebnis) | Darf nicht `null`/leer sein; bei `null` gilt Abbruch ohne Fehler. | Kein Exportvorgang bei Abbruch. |
| Export-Zielpfad | Muss auf `.raw` enden (Dialogfilter + Fallback-Prüfung im ViewModel). | `FehlerMeldung` setzen, wenn ungültiger Pfad verarbeitet werden soll. |
| `AufgabeId` / aktuelle Aufgabe | Export nur bei geladener Aufgabe mit gültiger `AufgabeId`. | Frühzeitiger Return ohne Dateischreiben. |
| Exportinhalt | Es werden ausschließlich Einträge mit `Typ == ProtokollTyp.CliOutput` exportiert. | Nicht-CLI-Einträge werden verworfen (kein Mischformat). |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Interface-Erweiterung:** Neue Methode in `IDialogService` erzwingt Anpassung aller konkreten Implementierungen (aktuell `WpfDialogService`).
- **Parallel laufende CLI-Ausgabe:** Während laufender Session können nach dem Snapshot weitere `CliOutput`-Zeilen entstehen; exportiert wird der konsistente Stand zum Abrufzeitpunkt.
- **Dateisystemabhängigkeit:** Schreibfehler (Berechtigungen, gesperrte Datei, ungültige Pfade) müssen robust abgefangen werden, damit die Detailansicht bedienbar bleibt.
- **UI-Regression in CLI-Ribbon:** Neuer Button darf bestehende CLI-Aktionen (`PluginAendern`, `CliNeustarten`, `CliStoppen`, Promptvorlagen) weder verdrängen noch deren Sichtbarkeit beeinflussen.

## Umsetzungsreihenfolge

1. **Dialog-Abstraktion für Save-File ergänzen**
   - Voraussetzungen: Keine.
   - Beschreibung: `IDialogService` um `ShowSaveFileDialogAsync` erweitern und in `WpfDialogService` mit `SaveFileDialog` implementieren (inkl. `*.raw`-Filterunterstützung).

2. **Export-Command im `TaskDetailViewModel` einführen**
   - Voraussetzungen: Schritt 1 abgeschlossen (`IDialogService.ShowSaveFileDialogAsync` vorhanden).
   - Beschreibung: `ExportCliRawCommand`, `KannCliRawExportieren` und `ExportCliRawAsync` ergänzen; Protokolle über `ProtokollService.GetByAufgabeAsync` abrufen, auf `ProtokollTyp.CliOutput` filtern und als UTF-8 ohne BOM schreiben.

3. **UI-Aktion in `TaskDetailView.xaml` anbinden**
   - Voraussetzungen: Schritt 2 abgeschlossen (Command und CanExecute vorhanden).
   - Beschreibung: Export-Button in CLI-Ribbon ergänzen, Binding und Automation-Name setzen.

4. **Unit-Tests für Exportlogik ergänzen**
   - Voraussetzungen: Schritte 1–3 abgeschlossen; bestehende Testfabrik (`TaskDetailViewModelTestFactory`) nutzbar.
   - Beschreibung: Tests für Command-Ausführbarkeit, Dialog-Abbruch, korrekte Filterung (`CliOutput`), Reihenfolgeerhalt und Fehlerpfad beim Dateischreiben ergänzen.

5. **UI-/Strukturtests für XAML und E2E-Interaktion ergänzen**
   - Voraussetzungen: Schritt 3 abgeschlossen; E2E-View-Objekt erweiterbar.
   - Beschreibung: `TaskDetailViewTests` um Assertions für den neuen Button erweitern und mindestens einen E2E-Happy-Path für Export inkl. Dateierstellung einführen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ExportCliRawCommand_CanExecute_WhenAufgabeGeladen` | `TaskDetailViewModelTests` | Export ist bei geladener Aufgabe ausführbar. |
| `ExportCliRawAsync_ShouldAbort_WhenDialogCancelled` | `TaskDetailViewModelTests` | Kein Dateischreiben bei Dialog-Abbruch (`null`). |
| `ExportCliRawAsync_ShouldWriteOnlyCliOutput_InChronologicalOrder` | `TaskDetailViewModelTests` | Filter nur `ProtokollTyp.CliOutput`, Reihenfolge bleibt erhalten. |
| `ExportCliRawAsync_ShouldSetFehlerMeldung_WhenFileWriteFails` | `TaskDetailViewModelTests` | Robuste Fehlerbehandlung bei I/O-Fehlern. |
| `Xaml_ContainsCliRawExportButton` | `TaskDetailViewTests` | Neuer Ribbon-Button inkl. Automation-Name und Command-Binding ist vorhanden. |
| `ExportCliRaw(...)` / `HandleSaveFileDialog(...)` | `Softwareschmiede.Tests.E2E.Views.TaskDetailView` (+ ggf. Dialog-View) | Kapselt E2E-Bedienung für Exportaktion und Save-Dialog. |
| `CliRawExport_ErstelltRawDateiMitCliOutput_HappyPath_E2E` | `End2EndTest` (neue E2E-Datei) | Benutzer kann Export auslösen und erhält eine `.raw`-Datei mit CLI-Ausgabe. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewTests` | Erweiterung der XAML-Assertions um den zusätzlichen CLI-Button. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Export aus Task-Detail-CLI erzeugt `.raw`-Datei mit protokollierter CLI-Rohausgabe | `src/Softwareschmiede.Tests/E2E/E2E_CliRawExport.cs` / `End2EndTest` | Anwender kann Export aktiv auslösen; Speichern-Dialog wird verwendet; Ausgabe wird als `*.raw` gespeichert. |
| Export-Abbruch über Dialog erzeugt keine Datei und keinen Fehlerbanner | `src/Softwareschmiede.Tests/E2E/E2E_CliRawExport.cs` / `End2EndTest` | Abbruchpfad bleibt folgenlos und verändert den laufenden Arbeitszustand nicht. |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

## Offene Punkte

Keine.
