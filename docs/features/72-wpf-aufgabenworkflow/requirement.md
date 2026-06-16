# Kundenanforderung: Aufgabenworkflow Optimierung

## Fachliche Zusammenfassung

Der Aufgabenworkflow wird vereinfacht durch Entfernung der Zwischenstatus `ArbeitsverzeichnisEingerichtet` und `InArbeit`. Eine neue Aktion „Starten" ermöglicht direkt den Übergang von Status `Neu` zu `Gestartet`, wobei gleichzeitig das Repository geklont und die CLI gestartet wird. Die Auswahl des KI-Plugins wird durch einen Dialog mit optionaler Speicherung als Projekt-Standard gesteuert. Der Wechsel des KI-Plugins wird durch optionales Beenden und Neustarten der CLI unterstützt. Damit die Abläufe fehlerfest funktionieren, werden E2E-Tests erforderlich.

## Betroffene Klassen und Komponenten

### Datenmodell
- `AufgabeStatus` (Enum) — Entfernen von `ArbeitsverzeichnisEingerichtet`, `InArbeit`; Vereinfachung auf: `Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert`
- `Aufgabe` (Entität) — Neue/geänderte Eigenschaften ggf. erforderlich für Plugin-Zuordnung auf Projekt-Ebene

### Services
- `EntwicklungsprozessService.ProzessStartenAsync` — Orchestriert den neuen kombinierten Ablauf: Klonen + CLI-Start in einem Schritt
- `PluginSelectionService` — Erweitert: Dialog-gestützte Auflösung mit Projekt-Standard-Speicherung
- `KiAusfuehrungsService` — Unterstützt Plugin-Wechsel mit Prozess-Neustarts
- `AufgabeService` — Neue Methoden für Status-Übergang und Plugin-Zuordnung

### UI-Komponenten / ViewModels
- `TaskDetailViewModel` — Neue Commands: `StartenCommand` (ersetzt zwei Schritte), `PluginAendernCommand`
- `TaskDetailView.xaml` / `TaskDetailView.xaml.cs` — Neue Buttons im Ribbon; entfernt separate CLI-Start-Action
- Neue Dialog-Komponente `PluginSelectionDialog.xaml` / `PluginSelectionDialog.xaml.cs` für Plugin-Auswahl mit Checkbox „Für alle Aufgaben des Projekts verwenden"

### Interfaces
- `IPluginSelectionDialog` — Ermöglicht Dialog-Interaktion für Plugin-Wahl mit Projekt-Speicherung

### Tests
- Unit Tests für geänderte `AufgabeStatus`-Übergänge
- E2E Tests für folgende Szenarien:
  1. Aufgabe im Status „Neu" mit „Starten" auf „Gestartet" wechseln → Repository geklont, CLI gestartet
  2. Fehlender CLI-Plugin → Dialog anzeigen, Plugin wählen, optional als Projekt-Standard speichern
  3. Nächste Aufgabe desselben Projekts → Dialog nicht anzeigen, gespeichertes Plugin verwenden
  4. Plugin-Wechsel durch „Plugin ändern"-Button → Dialog, bestehender CLI-Prozess beendet, neue CLI gestartet
  5. Aufgabendetailansicht für Status „Gestartet" ohne aktiven Prozess → CLI automatisch starten und einbetten
  6. Menüband-Elemente durch neue Aktion ersetzt (fehlende alte CLI-Start-Button)

## Implementierungsansatz

1. **Enum-Anpassung**: `AufgabeStatus` um `ArbeitsverzeichnisEingerichtet` und `InArbeit` reduzieren
   - Datenbankmigrationen erforderlich (Rückwärtskompatibilität prüfen)
   
2. **Kombinierter Start-Ablauf**:
   - `EntwicklungsprozessService.ProzessStartenAsync` wird erweitert oder umbenannt
   - Bisher separate Schritte (Klonen → Status setzen → CLI starten) erfolgen innerhalb einer Transaktion
   - Im Fehlerfall Rollback des Status und ggf. des Klonverzeichnisses
   
3. **Plugin-Dialog-Integration**:
   - `PluginSelectionService.ResolveDevelopmentAutomationPluginAsync` wird ausgebaut, um Dialog-Kontext zu übergeben
   - Dialog prüft, ob Plugin für Projekt bereits gespeichert ist; wenn nicht: Modal-Dialog anzeigen
   - Bei Bestätigung mit Checkbox: Aufruf von `PluginDefaultSettingsService.SaveProjectDefaultPluginAsync` (neu)
   
4. **Plugin-Wechsel-Logik**:
   - `TaskDetailViewModel.PluginAendernCommand` → Dialog aufrufen
   - Bei Änderung: `KiAusfuehrungsService.StopCliAsync` + `StartCliAsync` mit neuem Plugin
   - Projekt-Level Speicherung aufgehoben/neu gesetzt je nach User-Aktion
   
5. **Automatischer Prozess-Neustart**:
   - `TaskDetailViewModel.LadenAsync` prüft: Status == `Gestartet` && !isCliRunning
   - Wenn wahr: `KiAusfuehrungsService.StartCliAsync` automatisch aufrufen
   
6. **UI-Update**:
   - Neuer Command-Button „Starten" im Ribbon-Menü
   - Neuer Command-Button „Plugin ändern" im Ribbon-Menü
   - Entfernung der bisherigen Menü-Einträge für Klonen und CLI-Start

## Konfiguration

- **Projekt-Level Plugin-Speicherung**: Neue Einstellung `ProjektDefaultKiPluginPrefix` im Projekt
- **Dialog-Anzeige-Logik**: Konfigurierbar über `PluginDefaultSettingsService` (bereits vorhanden)
- **Automatischer Prozess-Neustart**: Logik in `TaskDetailViewModel.LadenAsync`, keine zusätzliche Konfiguration erforderlich

## Offene Fragen

1. **Datenbankkompatibilität**: Existierende Aufgaben in Status `ArbeitsverzeichnisEingerichtet` oder `InArbeit` — auf welchen Status sollen diese migriert werden?
   - Optionen: `Gestartet` (Annahme: alle waren vor Abschluss), `Beendet` (konservativ), manuelles Review erforderlich

2. **Plugin-Zuordnungspeicherort**: Soll die Projekt-Level-Zuordnung in `Projekt.` neu gespeichert werden oder über `PluginDefaultSettingsService` (aktuell global)?
   - Annahme: Über `PluginDefaultSettingsService` mit Projekt-ID als Scope

3. **Fehlerbehandlung bei CLI-Wechsel**: Wenn Prozess-Beendigung fehlschlägt, soll neue CLI trotzdem gestartet werden?
   - Annahme: Fehler anzeigen, Dialog offenlassen, kein Force-Kill

4. **Menüband-Elemente**: Welche bisherigen Elemente genau sollen entfernt werden (nur CLI-Start, auch Klonen)?
   - Annahme: Alle Repository-Klonen- und CLI-Start-Elemente entfernen, da durch „Starten"-Button ersetzt

5. **Recovery-Verhalten**: Wenn Aufgabe Status `Gestartet` hat, aber kein Arbeitsverzeichnis existiert — Fehler oder Neustart mit Klonen?
   - Annahme: Fehler anzeigen, Recovery erforderlich
