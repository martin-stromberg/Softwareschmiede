# Tests: Aufgabenworkflow Optimierung

## Testklassen

### `AufgabeStatusTransitionTests`
Datei: `src/Softwareschmiede.Tests/Domain/Enums/AufgabeStatusTransitionTests.cs`

| Testmethode | Was wird getestet? |
|------------|-------------------|
| `TestStatusTransitions_AllowedSequence_Succeeds` | Erlaubte Übergänge: `Neu` → `ArbeitsverzeichnisEingerichtet` → `Gestartet` → `InArbeit` → `Beendet` |
| `TestStatusTransitions_WartendSequence_Succeeds` | Rate-Limit-Szenario: `InArbeit` → `Wartend` → `InArbeit` → `Beendet` |
| `TestStatusTransitions_AnyStatusToArchiviert_IsAllowed` | Übergang zu `Archiviert` ist von jedem Status erlaubt |
| `TestStatusValidation_InvalidTransition_ThrowsException` | Ungültige Übergänge werfen `InvalidStatusTransitionException` |
| `TestStatusValidation_BeendetToInArbeit_ThrowsException` | `Beendet` kann nicht zu `InArbeit` übergehen |
| `TestStatusValidation_ArchiviertToNeu_ThrowsException` | `Archiviert` kann nicht zu `Neu` übergehen |

**Abhängigkeiten:**
- `AufgabeService` — Status-Transitions mit Validierung
- Test-DB: `TestDbContextFactory.Create()`

**Bemerkungen zu Anforderung:**
- Tests sind für aktuelles Modell mit `ArbeitsverzeichnisEingerichtet` und `InArbeit` geschrieben.
- Nach Anforderung müssen neue Tests für vereinfachtes Modell hinzugefügt werden (direkt `Neu` → `Gestartet`).

---

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

| Testmethode | Was wird getestet? |
|------------|-------------------|
| `ShowEditPanel_IsTrue_WhenStatusNeu` | EditPanel nur sichtbar bei Status `Neu` |
| `ShowCliPanel_IsTrue_WhenStatusGestartet` | CliPanel sichtbar bei Status `Gestartet` |
| `ShowCliPanel_IsTrue_WhenStatusInArbeit` | CliPanel sichtbar bei Status `InArbeit` |
| `ShowCliPanel_IsTrue_WhenStatusWartend` | CliPanel sichtbar bei Status `Wartend` |
| `ShowDiffPanel_IsTrue_WhenStatusBeendet` | DiffPanel nur sichtbar bei Status `Beendet` |
| `KannSpeichern_IsTrue_WhenStatusNeuUndTitelGesetzt` | Speichern erlaubt bei Status `Neu`, Titel gesetzt, kein CLI |
| `KannSpeichern_IsFalse_WhenTitelLeer` | Speichern nicht erlaubt wenn Titel leer |
| `KannSpeichern_IsFalse_WhenStatusBeendet` | Speichern nicht erlaubt bei Status `Beendet` |

**Abhängigkeiten:**
- `AufgabeService`, `ProtokollService`, `KiAusfuehrungsService`, `EntwicklungsprozessService`, `PluginSelectionService`, `IDialogService`
- Test-DB: `TestDbContextFactory.Create()`

**Bemerkungen zu Anforderung:**
- Tests existieren für aktuelle Commands, aber keine Tests für neue `StartenCommand` oder `PluginAendernCommand`.
- Keine Tests für kombiniertes Klone + CLI-Start Szenario.
- Keine Tests für automatischen CLI-Neustart bei Status `Gestartet`.

---

### `AufgabeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`

Tests für CRUD-Operationen und Status-Verwaltung.

**Bemerkungen zu Anforderung:**
- Haupttests existieren für aktuelle Struktur.
- Nach Anforderung: Tests für vereinfachte Status-Übergänge müssen angepasst werden.

---

### `EntwicklungsprozessServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
und
`src/Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs`

Tests für Repository-Setup und Git-Operationen.

**Bemerkungen zu Anforderung:**
- Existierende Tests für `ProzessStartenAsync`.
- Nach Anforderung: Tests für kombinierte Klone + CLI-Start Methode erforderlich.

---

## E2E-Tests

### `E2E_TaskDetailNavigation`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_TaskDetailNavigation.cs`

Tests für Navigation und UI-Interaktion in Aufgabendetailansicht.

### `E2E_CreateNewTaskNavigation`
Datei: `src/Softwareschmiede.Tests/E2E/E2E_CreateNewTaskNavigation.cs`

Tests für Erstellung neuer Aufgaben.

**Bemerkungen zu Anforderung:**
- Aktuell existieren Navigations-Tests.
- Neue E2E-Tests erforderlich für:
  1. Status `Neu` → `Gestartet` mit Klone + CLI-Start
  2. Plugin-Dialog-Anzeige bei fehlendem Plugin
  3. Plugin-Standard-Speicherung pro Projekt
  4. Nächste Aufgabe verwendet gespeichertes Plugin
  5. Plugin-Wechsel mit Dialog
  6. Automatischer CLI-Neustart bei Status `Gestartet` ohne Prozess
  7. Menüband-Elemente (neue/entfernte Buttons)

---

## Hilfsmethoden

### `TaskDetailViewModelTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

Hilfsmethoden zur Erstellung von Test-Aufgaben und ViewModels für Unit-Tests.

### `TestDbContextFactory`
Hilfsmethode zur Erstellung einer Test-Datenbank mit In-Memory SQLite.

---

## Fehlende Tests nach Anforderung

| Szenario | Testtyp | Priorität |
|----------|---------|-----------|
| Aufgabe `Neu` → `Gestartet` mit kombiniertem Klone + CLI-Start | E2E/Unit | Hoch |
| Dialog-Anzeige bei fehlendem Plugin | E2E/Unit | Hoch |
| Plugin-Standard auf Projekt-Level speichern und wiederverwenden | Unit | Hoch |
| Plugin-Wechsel mit Dialog und Prozess-Neustarts | Unit | Hoch |
| Automatischer CLI-Neustart bei Status `Gestartet` ohne laufenden Prozess | Unit | Mittel |
| Menüband-Buttons: neue `StartenCommand`, neue `PluginAendernCommand` | E2E | Mittel |
| Recovery: Status `Gestartet` ohne Arbeitsverzeichnis | Unit/E2E | Mittel |
