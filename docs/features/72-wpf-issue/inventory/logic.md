# Logikklassen und Services

## `AufgabeService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetByProjektAsync` | public | Gibt alle aktiven (nicht archivierten) Aufgaben eines Projekts zurück |
| `GetArchiviertByProjektAsync` | public | Gibt alle archivierten Aufgaben eines Projekts zurück |
| `GetAktiveUndWartendeCountAsync` | public | Gibt Anzahl aktiver (Gestartet) und wartender (Wartend) Aufgaben zurück |
| `GetByIdAsync` | public | Gibt eine Aufgabe anhand ihrer ID zurück |
| `GetDetailAsync` | public | Gibt eine Aufgabe mit IssueReferenz und Protokolleinträgen zurück |
| `GetLatestDiffResultIdAsync` | public | Gibt die ID des zuletzt generierten Diff-Ergebnisses zurück |
| `GetLatestDiffResultIdForFileAsync` | public | Gibt dateispezifische DiffResult-ID zurück |
| `CreateAsync` | public | **Erstellt eine neue Aufgabe mit Status `Neu`** |
| `CreateFromIssueAsync` | public | **Erstellt eine neue Aufgabe aus einem Issue mit `IssueReferenz`** |
| `UpdateAsync` | public | Aktualisiert Titel, Beschreibung und KI-Plugin-Prefix |
| `DeleteAsync` | public | Löscht eine Aufgabe |
| `VerwerfenAsync` | public | Verwirft eine neue Aufgabe durch Archivieren oder Löschen |
| `ArchivierenAsync` | public | Archiviert eine beendete Aufgabe |
| `StartenAsync` | public | Startet eine Aufgabe: Status → Gestartet, Branch und Arbeitsverzeichnis setzen |
| `SavePromptVorschlagAsync` | public | Speichert einen Vorschlagsprompt und optionalen Ausführungszeitpunkt |
| `ClearPromptVorschlagAsync` | public | Entfernt den gespeicherten Vorschlagsprompt |
| `AbschliessenAsync` | public | Schließt eine Aufgabe ab: Status → Beendet |
| `SetStatusAsync` | public | Setzt Status mit Validierung der erlaubten Übergänge |
| `StatusSetzenAsync` | public | Setzt Status ohne Transitions-Validierung |
| `UpdateHeartbeatAsync` | public | Aktualisiert LastHeartbeatUtc |
| `GetHeartbeatAgeMinutesAsync` | public | Gibt Minuten seit letztem Heartbeat zurück |

**Abhängigkeiten:**
- `SoftwareschmiededDbContext` (für DB-Zugriff)
- `ILogger<AufgabeService>` (für Logging)

**Hinweis:** Die Methode `CreateFromIssueAsync` ist bereits implementiert und erstellt automatisch eine `IssueReferenz` mit allen erforderlichen Feldern aus dem `Issue` Value Object.

---

## `ProjectDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`

**Eigenschaften (relevante Auswahl):**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `ProjektId` | `Guid` | Die Projekt-ID, deren Details angezeigt werden |
| `Projekt` | `Projekt?` | Das geladene Projekt |
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | `string?` | Fehlermeldung bei Ladefehlern |
| `Aufgaben` | `ObservableCollection<Aufgabe>` | Liste der Aufgaben des Projekts |
| `GefilterteAufgaben` | `ObservableCollection<Aufgabe>` | Gefilterte Aufgaben nach `AufgabenFilter` |
| `ProjektName` | `string` | Bearbeitbarer Projektname |
| `ProjektBeschreibung` | `string?` | Bearbeitbare Projektbeschreibung |
| `SelectedRepository` | `GitRepository?` | Ausgewähltes Repository |
| `AufgabenFilter` | `AufgabenFilterTyp` | Aktueller Aufgabenfilter |
| `IsFilterOverlayVisible` | `bool` | Gibt an, ob das Filter-Overlay sichtbar ist |
| `IsNeuanlage` | `bool` | Gibt an, ob die Ansicht im Neuanlage-Modus ist |

**Commands:**

| Command | Beschreibung |
|---------|-------------|
| `LadenCommand` | Lädt das Projekt neu |
| `AufgabeErstellenCommand` | Erstellt eine neue Aufgabe für das Projekt |
| `AufgabeOeffnenCommand` | Öffnet eine Aufgabe im Detail |
| `ZurueckCommand` | Navigiert zurück zur Projektübersicht |
| `SpeichernCommand` | Speichert Projektänderungen |
| `LoeschenCommand` | Löscht das Projekt |
| `FilterCommand` | Öffnet das Filter-Overlay |
| `RepositoryZuweisenCommand` | Öffnet Repository-Zuweisungs-Dialog |
| `RepositoryOeffnenCommand` | Öffnet Repository im Browser |

**Callbacks:**

- `ZurueckAction` — wird aufgerufen, wenn Nutzer zur Listenansicht zurückkehren möchte
- `ProjektListeAktualisierenCallback` — wird nach Erstellen/Löschen eines Projekts aufgerufen
- `NavigateToTaskViewCallback` — wird aufgerufen, um zur Aufgabendetailansicht zu navigieren
- `NavigateBackToProjectCallback` — wird aufgerufen, um von Aufgabendetailansicht zurück zu navigieren

**Abhängigkeiten:**
- `ProjektService`
- `AufgabeService`
- `IServiceProvider`
- `IDialogService`
- `ILogger<ProjectDetailViewModel>`

**Hinweis:** Die Klasse enthält bereits `Aufgaben` und `GefilterteAufgaben` Collections. Es gibt keine separaten `IssueVorschlaege`-Collections oder `LadenIssuesAsync`-Methode.

---

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

**Eigenschaften (relevante Auswahl):**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `AufgabeId` | `Guid` | ID der angezeigten Aufgabe |
| `Aufgabe` | `Aufgabe?` | Die geladene Aufgabe |
| `AufgabeTitel` | `string` | Titel der Aufgabe (Read-Only) |
| `AufgabeStatus` | `AufgabeStatus` | Status der Aufgabe (Read-Only) |
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | `string?` | Fehlermeldung bei Fehlern |
| `IsCliRunning` | `bool` | Gibt an, ob ein CLI-Prozess läuft |
| `KannCliStoppen` | `bool` | Gibt an, ob laufender CLI-Prozess gestoppt werden kann (= `IsCliRunning`) |
| `SelectedKiPluginPrefix` | `string?` | Gewähltes KI-Plugin (Prefix) |
| `OptionalCliParameters` | `string?` | Optionale Parameter für CLI-Start |
| `EmbeddedWindowHandle` | `IntPtr` | Handle des eingebetteten CLI-Fensters |
| `Protokolleintraege` | `ObservableCollection<Protokolleintrag>` | Protokolleinträge der Aufgabe |
| `VerfuegbareKiPlugins` | `ObservableCollection<string>` | Verfügbare KI-Plugin-Prefixe |
| `IsInfoViewVisible` | `bool` | Steuert Sichtbarkeit zwischen Info-Panel und CLI-Fenster |
| `EditTitel` | `string?` | Editable Kopie von `Aufgabe.Titel` |
| `EditAnforderungsBeschreibung` | `string?` | Editable Kopie von `Aufgabe.AnforderungsBeschreibung` |
| `ShowEditPanel` | `bool` | True wenn Status == Neu |
| `ShowCliPanel` | `bool` | True wenn Status ∈ {Gestartet, Wartend} |
| `ShowDiffPanel` | `bool` | True wenn Status == Beendet |
| `KannSpeichern` | `bool` | CanExecute für SpeichernCommand |
| `KannLoeschen` | `bool` | CanExecute für LoeschenCommand |

**Commands:**

| Command | Beschreibung |
|---------|-------------|
| `LadenCommand` | Lädt die Aufgabe |
| `CliStoppenCommand` | Stoppt den CLI-Prozess |
| `StartenCommand` | Startet die Aufgabe (kombiniertes Klonen, Plugin-Auflösung, CLI-Start) |
| `PluginAendernCommand` | Wechselt KI-Plugin bei laufender CLI |
| `AufgabeAbschliessenCommand` | Schließt die Aufgabe ab (Status: Beendet) |
| `SpeichernCommand` | Speichert Titel und AnforderungsBeschreibung |
| `LoeschenCommand` | Löscht die Aufgabe nach Bestätigungsdialog |
| `InfoCliToggleCommand` | Toggled IsInfoViewVisible |
| `ZurueckCommand` | Navigiert zurück zur vorherigen Ansicht |

**Events:**

- `CliProzessGestartet` — wird gefeuert wenn ein CLI-Prozess gestartet wurde und Handle verfügbar ist

**Methoden:**

| Methode | Beschreibung |
|---------|-------------|
| `GetRunningProcess()` | Gibt den laufenden CLI-Prozess zurück, oder null |
| `SetCliWindowHandle(IntPtr)` | Speichert das bekannte HWND des CLI-Fensters |
| `GetCliWindowHandle()` | Gibt das gespeicherte HWND des CLI-Fensters zurück |

**Abhängigkeiten:**
- `AufgabeService`
- `ProtokollService`
- `KiAusfuehrungsService`
- `EntwicklungsprozessService`
- `PluginSelectionService`
- `IDialogService`
- `ILogger<TaskDetailViewModel>`

**Hinweis:** Es gibt keine Eigenschaften/Commands für Issue-Verwaltung wie `CanAssignIssue`, `CurrentIssueReferenz`, `IssueZuweisenAsync` oder `IssueBrowserOeffnenCommand`.
