# ViewModels

## `ProjectDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`

### Properties

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `ProjektId` | `Guid` | Projekt-ID (setzt automatisch Laden aus) |
| `Projekt` | `Projekt?` | Geladenes Projekt-Objekt |
| `IsLoading` | `bool` | Gibt an, ob Daten werden geladen |
| `FehlerMeldung` | `string?` | Fehlermeldung bei Ladefehlern |
| `SelectedTaskViewModel` | `ViewModelBase?` | **DERZEIT: Inline TaskDetailViewModel (wird entfernt)** |
| `Aufgaben` | `ObservableCollection<Aufgabe>` | Liste der Aufgaben des Projekts |
| `ProjektName` | `string` | Bearbeitbarer Projektname (Two-Way-Binding) |
| `ProjektBeschreibung` | `string?` | Bearbeitbare Projektbeschreibung |
| `SelectedRepository` | `GitRepository?` | Ausgewähltes Repository |
| `AufgabenFilter` | `AufgabenFilterTyp` | Filter (Alle, Aktiv, Archiviert) |
| `IsFilterOverlayVisible` | `bool` | Sichtbarkeit des Filter-Overlays |
| `IsNeuanlage` | `bool` | Gibt an, ob kein persistiertes Projekt existiert (ProjektId == Guid.Empty) |

### Commands

| Command | Typ | Beschreibung |
|---------|-----|-------------|
| `LadenCommand` | `AsyncRelayCommand` | Lädt das Projekt und seine Aufgaben |
| `AufgabeErstellenCommand` | `AsyncRelayCommand` | **Erstellt neue Aufgabe mit Status "Neu"** (Zeile 176-178) |
| `AufgabeOeffnenCommand` | `RelayCommand<Guid>` | Öffnet Aufgabe (ID als Parameter) – **wird angepasst** |
| `ZurueckCommand` | `RelayCommand` | Navigiert zurück zur Projektliste |
| `SpeichernCommand` | `AsyncRelayCommand` | Speichert Projekt-Änderungen |
| `LoeschenCommand` | `AsyncRelayCommand` | Löscht Projekt |
| `FilterCommand` | `RelayCommand` | Toggelt Filter-Overlay |
| `RepositoryZuweisenCommand` | `AsyncRelayCommand` | Öffnet Repository-Zuweisungs-Dialog |
| `RepositoryOeffnenCommand` | `RelayCommand` | Öffnet Repository im Browser |

### Callbacks

| Callback | Typ | Beschreibung |
|----------|-----|-------------|
| `ZurueckAction` | `Action?` | Wird aufgerufen zum Zurück-Navigieren (Zeile 24) |
| `ProjektListeAktualisierenCallback` | `Func<Task>?` | Wird nach Projekt-CRUD aufgerufen (Zeile 27) |

### Methoden (Privat)

| Methode | Beschreibung |
|---------|-------------|
| `LadenAsync(ct)` | Lädt Projekt und Aufgaben (GetDetailAsync, GetByProjektAsync) |
| `AufgabeErstellenAsync(ct)` | Erstellt Aufgabe via `AufgabeService.CreateAsync()` |
| `OeffneAufgabe(id)` | **DERZEIT: Erstellt TaskDetailViewModel inline und setzt SelectedTaskViewModel** (Zeile 391-404) |
| `ProjektSpeichernAsync(ct)` | Speichert oder aktualisiert Projekt |
| `ProjektLoeschenAsync(ct)` | Löscht Projekt nach Bestätigung |
| `RepositoryZuweisenAsync(ct)` | Zeigt Repository-Dialog und fügt Repository hinzu |
| `RepositoryOeffnen()` | Öffnet Repository-URL im Browser |

**Bemerkung zu OeffneAufgabe:**
Derzeit (Zeile 391-404) wird `TaskDetailViewModel` hier erstellt und auf `SelectedTaskViewModel` gesetzt:
```csharp
private void OeffneAufgabe(Guid id)
{
    var vm = _serviceProvider.GetRequiredService<TaskDetailViewModel>();
    vm.ZurueckAction = () => SelectedTaskViewModel = null;
    vm.AufgabeListeAktualisierenCallback = async () => { ... };
    vm.AufgabeId = id;
    SelectedTaskViewModel = vm;
}
```
Diese Logik muss **entfernt** werden, um zur separaten Navigation zu wechseln.

---

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

### Properties

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `AufgabeId` | `Guid` | Aufgaben-ID (setzt automatisch Laden aus) |
| `Aufgabe` | `Aufgabe?` | Geladene Aufgabe |
| `AufgabeTitel` | `string` (Readonly) | Aufgaben-Titel (aus Aufgabe.Titel oder "(wird geladen…)") |
| `AufgabeStatus` | `AufgabeStatus` (Readonly) | Aufgaben-Status |
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | `string?` | Fehlermeldung |
| `IsCliRunning` | `bool` | Gibt an, ob CLI läuft |
| `KannCliStarten` | `bool` (Readonly) | CanExecute für CliStartenCommand: Status ∈ {Gestartet, InArbeit, Wartend} && !IsCliRunning && KiPluginPrefix gesetzt |
| `KannCliStoppen` | `bool` (Readonly) | CanExecute für CliStoppenCommand: IsCliRunning |
| `SelectedKiPluginPrefix` | `string?` | Ausgewähltes KI-Plugin (Dropdown) |
| `OptionalCliParameters` | `string?` | Optionale CLI-Parameter |
| `EmbeddedWindowHandle` | `IntPtr` | Handle des eingebetteten CLI-Fensters |
| `Protokolleintraege` | `ObservableCollection<Protokolleintrag>` | Protokoll der KI-Ausführungen |
| `VerfuegbareKiPlugins` | `ObservableCollection<string>` | Verfügbare KI-Plugin-Prefixe |
| `IsInfoViewVisible` | `bool` | Sichtbarkeit zwischen Info-Panel und CLI |
| `EditTitel` | `string?` | Editable Kopie von Aufgabe.Titel |
| `EditAnforderungsBeschreibung` | `string?` | Editable Kopie von Aufgabe.AnforderungsBeschreibung |
| `ShowEditPanel` | `bool` (Readonly) | True wenn Status == Neu |
| `ShowCliPanel` | `bool` (Readonly) | True wenn Status ∈ {Gestartet, InArbeit, Wartend} |
| `ShowDiffPanel` | `bool` (Readonly) | True wenn Status == Beendet |
| `KannSpeichern` | `bool` (Readonly) | Status ∈ {Neu, Gestartet} && !IsCliRunning && EditTitel nicht leer |
| `KannLoeschen` | `bool` (Readonly) | Status ∉ {Beendet, Archiviert} && Aufgabe != null && !IsCliRunning |

### Commands

| Command | Typ | Beschreibung |
|---------|-----|-------------|
| `LadenCommand` | `AsyncRelayCommand` | Lädt Aufgabe mit Details und Protokoll |
| `CliStartenCommand` | `AsyncRelayCommand` | Startet CLI-Prozess |
| `CliStoppenCommand` | `AsyncRelayCommand` | Stoppt CLI-Prozess |
| `StatusGestartetSetzenCommand` | `AsyncRelayCommand` | Setzt Status auf "Gestartet" |
| `AufgabeAbschliessenCommand` | `AsyncRelayCommand` | Beendet Aufgabe (Status: Beendet) |
| `SpeichernCommand` | `AsyncRelayCommand` | **Speichert Titel und Beschreibung** via `AufgabeService.UpdateAsync()` |
| `LoeschenCommand` | `AsyncRelayCommand` | Löscht Aufgabe nach Bestätigung |
| `InfoCliToggleCommand` | `RelayCommand` | Toggelt zwischen Info-Panel und CLI |
| `ZurueckCommand` | `RelayCommand` | Navigiert zurück |

### Callbacks

| Callback | Typ | Beschreibung |
|----------|-----|-------------|
| `ZurueckAction` | `Action?` | Wird aufgerufen zum Zurück-Navigieren (Zeile 42) |
| `AufgabeListeAktualisierenCallback` | `Func<Task>?` | Wird nach Aufgaben-CRUD aufgerufen (Zeile 45) |

### Events

| Event | Beschreibung |
|-------|-------------|
| `CliProzessGestartet` | Wird ausgelöst, wenn CLI-Prozess startet (Parameter: Process) |

### Methoden (Privat)

| Methode | Beschreibung |
|---------|-------------|
| `LadenAsync(ct)` | Lädt Aufgabe via `GetDetailAsync()`, CLI-Status, Protokoll und verfügbare Plugins |
| `LadeVerfuegbarePluginsAsync(ct)` | Lädt KI-Plugin-Prefixe via PluginSelectionService |
| `CliStartenAsync(ct)` | Startet CLI via `KiAusfuehrungsService.StartCliAsync()` |
| `CliStoppenAsync(ct)` | Stoppt CLI via `KiAusfuehrungsService.StopCliAsync()` |
| `StatusGestartetSetzenAsync(ct)` | Setzt Status auf "Gestartet" |
| `AufgabeAbschliessenAsync(ct)` | Beendet Aufgabe via `EntwicklungsprozessService.AbschliessenAsync()` |
| `SpeichernAsync(ct)` | **Aktualisiert Titel und Beschreibung via `AufgabeService.UpdateAsync()`** |
| `LoeschenAsync(ct)` | Löscht Aufgabe nach Dialog-Bestätigung |
| `InfoCliToggle()` | Toggelt IsInfoViewVisible |
| `OnCliProcessStatusChanged(aufgabeId, status)` | Event-Handler für CLI-Status-Änderungen |

**Status-abhängiges Content-Switching:**
Die View nutzt `ShowEditPanel`, `ShowCliPanel` und `ShowDiffPanel` Properties, um zwischen verschiedenen UI-Panels zu wechseln basierend auf dem Aufgaben-Status.

---

## Beziehungen zwischen ViewModels

```
ProjectDetailViewModel
  ├─ Erstellt TaskDetailViewModel via AufgabeOeffnenCommand
  │  └─ vm.ZurueckAction = () => SelectedTaskViewModel = null
  │  └─ vm.AufgabeListeAktualisierenCallback = Reload Aufgaben
  └─ Setzt SelectedTaskViewModel (triggers Inline-View-Rendering)

TaskDetailViewModel
  ├─ Speichert via AufgabeService.UpdateAsync()
  ├─ Ruft ZurueckAction auf zum Zurück-Navigieren
  └─ Speichert neue Aufgabe mit Status "Neu"
```

**Anpassungen für separate Navigation erforderlich:**
- ProjectDetailViewModel sollte **nicht** mehr TaskDetailViewModel erstellen
- Stattdessen sollte Navigation über ein zentrales NavigationService/RootViewModel gehen
