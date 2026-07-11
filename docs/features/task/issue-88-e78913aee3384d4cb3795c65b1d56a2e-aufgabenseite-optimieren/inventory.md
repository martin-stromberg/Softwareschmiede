# Bestandsaufnahme: Aufgabenseite optimieren

Analysiert wurden die WPF-Aufgabendetailansicht, die zugehoerigen ViewModels, Aufgaben-/CLI-Services, Plugin-Contracts und vorhandene Tests bezogen auf Titelanzeige, Fusszeilen-CLI-Kontext, Info-Ansicht und Start von Aufgaben ohne Issue-Bezug.

## Zusammenfassung

- `MainWindow` bindet den Fenstertitel an `MainWindowViewModel.Title`; beim Navigieren zur Aufgabendetailansicht wird der Titel aktuell nicht auf den Aufgabentitel gesetzt.
- `TaskDetailViewModel` stellt `AufgabeTitel`, `CliStatusText`, `IsInfoViewVisible` und Start-/Stop-/Info-Kommandos bereit; der Fusszeilentext beschreibt derzeit den CLI-Laufzeitstatus, nicht den Namen des ausgefuehrten CLI-Plugins.
- `TaskDetailView.xaml` enthaelt eine Info-Ansicht nur innerhalb des CLI-Panels fuer gestartete/wartende Aufgaben; fuer neue und beendete Aufgaben gibt es keine gemeinsame Ansichtsleiste mit separatem `Info`-Button.
- `Aufgabe` enthaelt bereits optionale Beziehungen zu `GitRepository` und `IssueReferenz`; die Startlogik erzeugt Branch-Namen auch ohne Issue-Nummer.
- `EntwicklungsprozessService` loest Repository und SCM-Plugin bei fehlender direkter Aufgaben-Repository-Zuordnung ueber den Projektkontext auf und erstellt `issue.md` aus der Aufgabenbeschreibung.
- Es existieren Tests fuer `TaskDetailViewModel`, `MainWindowViewModel`, `EntwicklungsprozessService`, `KiAusfuehrungsService` und E2E-Navigation; direkte Tests fuer Aufgabentitel im Fenstertitel, CLI-Pluginnamen in der Fusszeile und Info-Button in allen Detailzustanden sind im Bestand nicht offensichtlich vorhanden.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
