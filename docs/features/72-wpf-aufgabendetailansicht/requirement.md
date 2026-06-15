# Anforderungsanalyse: Aufgabendetailansicht

## Fachliche Zusammenfassung

Die Aufgabendetailansicht erweitert die bestehende `TaskDetailView` um eine Ribbon-Menü-Schnittstelle analog zur Projektdetailansicht und implementiert ein status-abhängiges Content-Switching-System. Je nach Aufgabestatus (`Neu`, `Gestartet`, `InArbeit`, `Wartend`, `Beendet`) werden unterschiedliche Inhaltsdarstellungen angezeigt: Haupteigenschaften für neue Aufgaben, eingebettetes CLI-Fenster für laufende Aufgaben und eine Diff-Ansicht der Arbeitsverzeichnisänderungen für beendete Aufgaben.

## Betroffene Klassen und Komponenten

### ViewModel
- `TaskDetailViewModel` — Erweiterung um Commands für Speichern, Löschen, Starten und Beenden
- Neue Properties: `KannSpeichern`, `KannLoeschen`, `KannStarten`, `KannBeenden` (abhängig vom Status)

### UI-Komponenten / Views
- `TaskDetailView.xaml` — Umstrukturierung:
  - Ribbon-Menü mit Gruppen „Navigation", „Aufgabe"
  - Status-abhängiges Content-Switching zwischen drei Inhalten:
    - Kachel mit Haupteigenschaften (Status: `Neu`)
    - Kachel mit eingebettetem CLI-Fenster (Status: `Gestartet`, `InArbeit`, `Wartend`)
    - Kachel mit Diff-Ansicht (Status: `Beendet`)
  - Toggle-Button zum Wechsel zwischen Info-Ansicht und CLI-Ansicht bei aktiver Aufgabe

### Services
- `AufgabeService` — Verwendung existierender `SetStatusAsync`-Methode für Status-Übergänge
- `AufgabeService` — Neue Methode `SaveAsync` (falls nicht vorhanden) zum Speichern von Aufgabeneigenschaften
- `AufgabeService` — Neue Methode `DeleteAsync` (falls nicht vorhanden) zum Löschen der Aufgabe

### Enums / Constanten
- `AufgabeStatus` — bereits vorhanden; keine Änderung erforderlich

### Tests
- Unit-Tests für ViewModel-Commands (`SpeichernCommand`, `LoeschenCommand`, `StatusGestartetSetzenCommand`, `AufgabeAbschliessenCommand`)
- E2E-Tests für View-Interaktionen

## Implementierungsansatz

### 1. ViewModel-Erweiterung

Hinzufügen zu `TaskDetailViewModel`:
- `SpeichernCommand` → ruft `AufgabeService.SaveAsync()` auf, speichert änderbare Eigenschaften (z.B. `Titel`, `AnforderungsBeschreibung`)
- `LoeschenCommand` → ruft `AufgabeService.DeleteAsync()` auf, zeigt Bestätigungsdialog
- `StatusGestartetSetzenCommand` → bereits teilweise implementiert; nutzt `SetStatusAsync(AufgabeStatus.Gestartet)`
- `AufgabeAbschliessenCommand` → bereits teilweise implementiert; nutzt `EntwicklungsprozessService.AbschliessenAsync()`
- `InfoCliToggleCommand` → Wechsel zwischen Info-Ansicht und CLI-Ansicht (neue View-Only-Logik)
- Abhängigkeitsprüfung vor Command-Ausführung (CanExecute):
  - `SpeichernCommand`: Status `Neu` oder `Gestartet` (nicht während CLI läuft)
  - `LoeschenCommand`: Nur wenn nicht `Beendet` oder `Archiviert` (Schutz für History)
  - `StatusGestartetSetzenCommand`: Nur wenn Status `Neu`
  - `AufgabeAbschliessenCommand`: Nur wenn CLI nicht läuft

### 2. View-Umstrukturierung (Ribbon + Content-Switching)

**Ribbon-Menü:**
- Nutzt bestehende `RibbonGroup` und `RibbonLargeButton`-Controls aus ProjectDetailView
- Gruppe "Navigation":
  - Button "Zurück" (ohne Änderung)
- Gruppe "Aufgabe":
  - Button "Speichern" (nur aktiv wenn edierbar)
  - Button "Löschen" (nur aktiv wenn edierbar)
  - Button "Starten" (sichtbar wenn Status `Neu`, nutzt `StatusGestartetSetzenCommand`)
  - Button "Beenden" (sichtbar wenn Status `Gestartet`, `InArbeit` oder `Wartend`, nutzt `AufgabeAbschliessenCommand`)

**Content-Switching:**
Nutzt Binding an `AufgabeStatus` mit Value-Converter:
1. **Status `Neu`:** Kachel mit Eingabefeldern für `Titel` und `AnforderungsBeschreibung`
2. **Status `Gestartet` / `InArbeit` / `Wartend`:**
   - Standard: Kachel mit CLI-Fenster (via `ProcessWindowHost`)
   - Optionaler Toggle zu Info-Ansicht (Kachel mit Aufgabeeigenschaften und Protokoll)
3. **Status `Beendet`:** Kachel mit Diff-Ansicht der Änderungen im Arbeitsverzeichnis (Platzhalter: Implementierung folgt später)

### 3. Status-Abhängige CanExecute-Logik

Commands müssen folgende Bedingungen prüfen:
```
SpeichernCommand.CanExecute: Status ∈ {Neu, Gestartet} ∧ ¬IsCliRunning
LoeschenCommand.CanExecute: Status ∉ {Beendet, Archiviert} ∧ ¬IsCliRunning
StatusGestartetSetzenCommand.CanExecute: Status == Neu
AufgabeAbschliessenCommand.CanExecute: ¬IsCliRunning ∧ Status ≠ Neu
```

### 4. Abhängigkeiten zu existierenden Komponenten

- **NavigationViewModel** / Navigationsystem: Keine neuen Anforderungen; bestehend
- **KiAusfuehrungsService**: Keine Änderungen; wird wie bisher genutzt
- **AufgabeService**: Verwendung existierender `GetDetailAsync`, `SetStatusAsync`, und ggf. neue `SaveAsync`, `DeleteAsync`

## Konfiguration

**Keine Konfigurierbarkeit erforderlich** — das Verhalten ist fest definiert:
- Ribbon-Menü-Struktur ist statisch
- Content-Switching erfolgt automatisch basierend auf `AufgabeStatus`
- Diff-Anzeige ist später konfigurierbar (derzeit Platzhalter)

## Offene Fragen

1. **Diff-Ansicht für beendete Aufgaben:** Wie werden die Änderungen visualisiert? (Git Diff? File Explorer Tree?) — Aktuell mit Platzhalter geplant.
2. **Edit-Modus:** Können Aufgabeneigenschaften nur im Status `Neu` bearbeitet werden, oder auch später?
3. **Bestätigungsdialog beim Löschen:** Welche Meldung und welche Buttons (OK/Abbrechen)?
4. **Toggle-Button für Info/CLI-Ansicht:** Sollte bei Status `InArbeit` immer sichtbar sein, auch während das CLI läuft?
5. **Pflichtfelder beim Speichern:** Müssen `Titel` und `AnforderungsBeschreibung` validiert werden?
6. **Recovery-Meldung:** Falls CLI mittels "Beenden" forciert gestoppt wird, soll ein Warndialog angezeigt werden?
