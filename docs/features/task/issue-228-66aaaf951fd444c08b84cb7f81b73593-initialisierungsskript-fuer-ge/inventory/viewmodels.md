# ViewModels

## `ProjectDetailViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`

**Relevante Properties (Auszug):**

| Property | Typ | Beschreibung | Getter | Setter |
|----------|-----|-------------|--------|--------|
| `ProjektId` | `Guid` | Die Projekt-ID, deren Details angezeigt werden | public | public |
| `Projekt` | `Projekt?` | Das geladene Projekt | public | private |
| `SelectedRepository` | `GitRepository?` | Ausgewähltes Repository | public | public |
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden | public | private |
| `FehlerMeldung` | `string?` | Fehlermeldung bei Ladefehlern | public | private |
| `Aufgaben` | `ObservableCollection<Aufgabe>` | Liste der Aufgaben des Projekts | public | - |
| `GefilterteAufgaben` | `ObservableCollection<Aufgabe>` | Gefilterte Aufgaben | public | - |
| `SelectedRepositorySourceBranchName` | `string?` | Aktuell konfigurierter Basis-Branch | public | public |
| `IsEditingSourceBranch` | `bool` | Gibt an, ob der Basis-Branch bearbeitet wird | public | private |
| `AvailableSourceBranchesForEdit` | `ObservableCollection<string>` | Verfügbare Branches für Bearbeitung | public | private |

**Relevante Methoden (Auszug):**

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LadenAsync(ct)` | `private` | Lädt Projekt und Aufgaben; ruft `LadenOffeneAnforderungenAsync()` auf |
| `RepositoryZuweisenAsync(ct)` | `private` | Dialog zum Zuweisen eines neuen Repositories |
| `EditSourceBranchAsync(ct)` | `private` | Lädt verfügbare Remote-Branches und aktiviert Bearbeitungsmodus |
| `SaveSourceBranchAsync(ct)` | `private` | Speichert geänderten Basis-Branch über `_projektService.UpdateRepositorySourceBranchAsync()` |
| `CancelSourceBranchEdit()` | `private` | Bricht Bearbeitung ab und verwirft Änderungen |
| `LadenOffeneAnforderungenAsync(ct)` | `private` | Lädt Issues/Alerts von Remote-Repository über `IGitPlugin` |

**Abhängigkeiten:**
- `ProjektService` — für Projekt-CRUD-Operationen
- `AufgabeService` — für Aufgaben-Verwaltung
- `IServiceProvider` — für ViewModels-Instanziierung
- `IDialogService` — für Dialog-Anzeige
- `IPluginManager` — für Plugin-Zugriff (besonders für SCM-Plugins)
- `ILogger<ProjectDetailViewModel>` — für Protokollierung

**Bemerkungen:**
- ViewModel zeigt Projekt mit zugeordneten Repositories und Aufgaben an
- Unterstützt Repository-Auswahl mit Basis-Branch-Verwaltung
- Lädt Remote-Branches über SCM-Plugin (siehe `EditSourceBranchAsync` Zeile 584)
- Hat bereits Pattern für asynchrones Laden von Remote-Daten (Branches, Issues)
- Fehlerbehandlung wird über Exceptions abgefangen und `SetFehler(ex)` aufgerufen
- **Fehlende Properties für Initialisierungsskript:**
  - `InitialisierungsskriptSuggestionen: IEnumerable<string>`
  - `SelectedInitialisierungsskript: string?`
  - `IsEditingInitialisierungsskript: bool`
  - `SaveInitialisierungsskriptAsync(): Task`

