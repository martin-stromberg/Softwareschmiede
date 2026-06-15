# Bestandsaufnahme: Aufgabendetailansicht

Diese Analyse dokumentiert den bestehenden Projektcode zur Anforderung der Aufgabendetailansicht-Erweiterung mit Ribbon-Menü, Status-abhängigem Content-Switching und Edit-Capabilities.

## Zusammenfassung

**Was ist vorhanden:**
- Vollständig implementierter `AufgabeService` mit allen CRUD-Operationen (Create, Read, Update, Delete, Archive)
- Status-Enum `AufgabeStatus` mit allen 7 erforderlichen Status-Werten (Neu, ArbeitsverzeichnisEingerichtet, Gestartet, InArbeit, Wartend, Beendet, Archiviert)
- Status-Übergänge validiert in `AufgabeService.ValidateStatusTransition()` mit definierten erlaubten Transitionen
- `TaskDetailViewModel` mit Commands für CLI-Start/Stop und Status-Übergänge (StatusGestartetSetzenCommand, AufgabeAbschliessenCommand)
- Bestehende TaskDetailView mit CLI-Fenster-Einbettung und Protokoll-Anzeige
- `EntwicklungsprozessService` mit Methode `AbschliessenAsync()` für Aufgaben-Abschluss
- `KiAusfuehrungsService` mit Events für CLI-Status-Änderungen
- Unit-Tests für `AufgabeService` CRUD-Operationen und Status-Transitionen

**Was fehlt:**
- **Ribbon-Menü-Struktur** — TaskDetailView hat keine Ribbon-GUI, nur Button-Toolbar im Header
- **Status-abhängiges Content-Switching** — keine unterschiedliche Anzeige je nach Status (Neu, InArbeit, Beendet)
- **Edit-Modus für Status Neu** — keine Eingabefelder für Titel und AnforderungsBeschreibung im Status Neu
- **Diff-Ansicht für beendete Aufgaben** — keine Visualisierung der Änderungen im Arbeitsverzeichnis
- **SpeichernCommand** — ViewModel hat keinen Command zum Speichern von Aufgabeneigenschaften
- **LoeschenCommand** — ViewModel hat keinen Command zum Löschen mit Bestätigungsdialog
- **Toggle-Button für Info/CLI-Ansicht** — keine UI-Umschaltung zwischen Info- und CLI-Ansicht
- **ViewModel-Tests** — keine Unit-Tests für TaskDetailViewModel
- **E2E-Tests** — keine End-to-End-Tests für View-Interaktionen

## Details

- [Datenmodelle](inventory/models.md)
- [Logik-Klassen](inventory/logic.md)
- [ViewModel und Views](inventory/viewmodels.md)
- [Enums](inventory/enums.md)
- [Tests](inventory/tests.md)

## Abhängigkeiten und Schnittstellen

**Existierende Services:**
- `AufgabeService` — Bietet alle notwendigen Methoden für Status-Übergänge und CRUD-Operationen
  - `UpdateAsync()` für Speichern von Titel und AnforderungsBeschreibung vorhanden
  - `DeleteAsync()` mit Validierung vorhanden (verhindert Löschung bei aktiven Status)
  - `SetStatusAsync()` mit Transitions-Validierung vorhanden
  - `StatusSetzenAsync()` generisch (ohne Validierung) vorhanden
  - `AbschliessenAsync()` für Status → Beendet vorhanden
  - `StartenAsync()` für Status → ArbeitsverzeichnisEingerichtet vorhanden

- `EntwicklungsprozessService` — Für Entwicklungs-Workflows
  - `AbschliessenAsync()` nutzt `AufgabeService.AbschliessenAsync()`
  - `ProzessStartenAsync()` für Git-Repository-Setup vorhanden

- `KiAusfuehrungsService` — Für CLI-Prozess-Verwaltung
  - `StartCliAsync()` implementiert
  - `StopCliAsync()` implementiert
  - Event `CliProcessStatusChanged` publiziert

- `ProtokollService` — Für Protokoll-Einträge
  - `GetByAufgabeAsync()` vorhanden
  - `AddEintragAsync()` vorhanden

- `PluginSelectionService` — Für KI-Plugin-Auswahl
  - `GetAvailableKiPluginPrefixesAsync()` vorhanden
  - `ResolveDevelopmentAutomationPluginAsync()` vorhanden

**Datenmodell:**
- `Aufgabe` Klasse mit allen erforderlichen Properties
- `AufgabeStatus` Enum mit allen Status-Werten
- `Protokolleintrag` für Logeinträge
- `DiffResult` für Diff-Speicherung

**Betroffene ViewModel-Eigenschaften:**
- `AufgabeId` — wird geladen beim Setzen
- `Aufgabe` — wird mit `GetDetailAsync()` geladen
- `IsCliRunning` — wird überwacht
- `KannCliStarten`, `KannCliStoppen` — berechnet aus Status

**Bestehende View-Infrastruktur:**
- `ProcessWindowHost` Control für Fenstereinbettung
- Converter für Visibility-Binding
- Dark-Mode Brushes/Ressourcen
- Layout-Grid mit Header, Fehler, Inhalt, Statusleiste
