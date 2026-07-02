# Bestandsaufnahme: ViewModels

## `MainWindowViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`

| Eigenschaft | Typ | Sichtbarkeit | Beschreibung |
|-------------|-----|-------------|-------------|
| `Title` | `string` | public | Fenstertitel (aktuell: "Softwareschmiede", wird bei Navigation angepasst) |
| `CurrentView` | `ViewModelBase?` | public | Das aktuell angezeigte ViewModel (Navigationsinhalt) |
| `IsNavigationExpanded` | `bool` | public | Gibt an, ob die Navigation aufgeklappt ist |

| Methode / Command | Sichtbarkeit | Kurzbeschreibung |
|-------------|-------------|------------------|
| `NavigateToDashboardCommand` | public | ICommand zum Navigieren zum Dashboard |
| `NavigateToProjectListCommand` | public | ICommand zum Navigieren zur Projektliste |
| `NavigateToSettingsCommand` | public | ICommand zum Navigieren zu den Einstellungen |
| `ToggleDarkModeCommand` | public | ICommand zum Umschalten des Dark-Mode (Property nicht vorhanden) |
| `ToggleNavigationCommand` | public | ICommand zum Ein-/Ausklappen der Navigation |
| `NavigateToDashboard()` | private | Setzt `CurrentView` auf `DashboardViewModel` |
| `NavigateToProjectList()` | private | Setzt `CurrentView` auf `ProjectListViewModel` |
| `NavigateToSettings()` | private | Setzt `CurrentView` auf `SettingsViewModel` |

**Abhängigkeiten:**
- `DarkModeService`
- `IServiceProvider` (für Service-Instanziierung)
- Cached ViewModels: `DashboardViewModel`, `ProjectListViewModel`, `SettingsViewModel`

**FEHLEND (gemäß Anforderung):**
- Property `AktiveAufgaben` : `ObservableCollection<Aufgabe>`
- Property `IsDashboardVisible` : `bool` (computed, `true` wenn `CurrentView is DashboardViewModel`)
- Methode `AktiveAufgabenAktualisierenAsync(CancellationToken ct)` : `Task`
- Command `NavigateZuAufgabeCommand` : `ICommand` mit Parameter `Guid aufgabeId`

---

## `DashboardViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`

| Eigenschaft | Typ | Sichtbarkeit | Beschreibung |
|-------------|-----|-------------|-------------|
| `ProjektAnzahl` | `int` | public | Anzahl aller Projekte |
| `AktiveAufgaben` | `int` | public | Anzahl aktiver Aufgaben (Status Gestartet) — **nur Zähler, nicht Aufgaben selbst** |
| `WartendAufgaben` | `int` | public | Anzahl wartender Aufgaben (Status Wartend) |
| `IsLoading` | `bool` | public | Gibt an, ob Daten geladen werden |
| `FehlerMeldung` | `string?` | public | Fehlermeldung bei Ladefehlern |
| `RecoveryKandidaten` | `ObservableCollection<Guid>` | public | Liste von Recovery-Kandidaten (Aufgaben-IDs) |
| `HatRecoveryKandidaten` | `bool` | public | Computed Property: `RecoveryKandidaten.Count > 0` |
| `LetzteProjects` | `ObservableCollection<Projekt>` | public | Liste der zuletzt aktiven Projekte (max. 5) |

| Methode / Command | Sichtbarkeit | Kurzbeschreibung |
|-------------|-------------|------------------|
| `LadenCommand` | public | ICommand zum Neuladen der Dashboard-Daten |
| `LadenAsync(CancellationToken ct)` | private | Lädt Projekte, Recovery-Kandidaten, letzte Projekte und aktive/wartende Aufgabenzähler |

**Abhängigkeiten:**
- `ProjektService`
- `AufgabeService` (nutzt `GetAktiveUndWartendeCountAsync()`)
- `AufgabeRecoveryService`
- `ILogger<DashboardViewModel>`

**FEHLEND (gemäß Anforderung):**
- Property `AktiveAufgaben` : `ObservableCollection<Aufgabe>` (aktuell nur `int`)
- Erweiterung der `LadenAsync()`-Methode, um `AufgabeService.GetAktiveAufgabenAsync()` aufzurufen

---

## `TaskDetailViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`

| Eigenschaft | Typ | Sichtbarkeit | Beschreibung |
|-------------|-----|-------------|-------------|
| `AufgabeId` | `Guid` | public | Die ID der angezeigten Aufgabe (Setter triggert LadenAsync) |
| `Aufgabe` | `Aufgabe?` | public | Die geladene Aufgabe |
| `AufgabeTitel` | `string` | public | Computed: Titel der Aufgabe oder Platzhalter |
| `AufgabeStatus` | `AufgabeStatus` | public | Computed: Status der Aufgabe |
| `AufgabeBranchName` | `string` | public | Computed: Branch-Name oder leerer String |
| `ZurueckAction` | `Action?` | public | Callback zum Zurücknavigieren |
| `AufgabeListeAktualisierenCallback` | `Func<Task>?` | public | Callback nach dem Löschen |
| ... (weitere Properties) | | | |

**Hinweise:**
- Wird durch DataTemplate im MainWindow.xaml automatisch instantiiert, wenn der ViewModel-Typ vorhanden ist
- Zeigt Aufgabendetails, Protokoll, CLI-Status, Diff-Ergebnisse an
- Ermöglicht Bearbeitung, Start, Beendigung und Löschung von Aufgaben

---

## `ViewModelBase` (Basisklasse)
Datei: `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs`

Implementiert `INotifyPropertyChanged` mit Hilfsmethoden:
- `OnPropertyChanged(string propertyName)` - Löst PropertyChanged aus
- `SetProperty<T>(ref T field, T value, string propertyName)` - Setzt Feld und triggert PropertyChanged
- `SetFehler(ref string fehlerMeldungField, string propertyName, Exception ex)` - Fehlerbehandlung

Enthält Command-Implementierungen:
- `RelayCommand` - Synchroner Command ohne Parameter
- `RelayCommand<T>` - Synchroner Command mit Parameter
- `AsyncRelayCommand` - Asynchroner Command ohne Parameter (mit Cancellation)
- `AsyncRelayCommand<T>` - Asynchroner Command mit Parameter (mit Cancellation)
