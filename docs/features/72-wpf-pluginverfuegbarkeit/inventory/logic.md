# Bestandsaufnahme: Services und Logik

## `RepositoryAssignViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LadenAsync(CancellationToken)` | public async | Lädt alle verfügbaren Repositories vom ProjektService; befüllt `VerfuegbareRepositories` |
| (Konstruktor) | public | Initialisiert ViewModel mit `ProjektService` und Logger; erzeugt BestaetigenCommand und AbbrechenCommand |

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `VerfuegbareRepositories` | `ObservableCollection<GitRepository>` | Repositories zur Anzeige in der ListBox |
| `SelectedRepository` | `GitRepository?` | Ausgewähltes Repository (Two-Way Binding) |
| `IsLoading` | `bool` | Gibt an, ob Daten geladen werden |
| `BestaetigenCommand` | `ICommand` | RelayCommand für Bestätigung; CanExecute nur wenn Repository ausgewählt |
| `AbbrechenCommand` | `ICommand` | RelayCommand für Abbruch |

Abonnierte Events:
- Keine direkten Event-Abonnements

Publizierte Events:
- `CloseRequested` — wird ausgelöst nach Bestätigung oder Abbruch mit bool-Parameter (true = bestätigt, false = abgebrochen)

**Abhängigkeiten:**
- `ProjektService` — lädt Repositories
- `ILogger<RepositoryAssignViewModel>` — Logging

## `ProjektService`
Datei: `src/Softwareschmiede.Application/Services/ProjektService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllRepositoriesAsync(CancellationToken)` | public async | Gibt alle Repositories sortiert nach Name zurück |
| `AddRepositoryAsync(Guid, string, string, string, CancellationToken)` | public async | Fügt Repository zu Projekt hinzu (String-Overload mit URL + Name) |
| `AddRepositoryAsync(Guid, string, IReadOnlyDictionary<string,string>, CancellationToken)` | public async | Fügt Repository zu Projekt hinzu (Dictionary-Overload mit Feldwerten) |
| `RemoveRepositoryAsync(Guid, CancellationToken)` | public async | Entfernt Repository aus Projekt |
| `GetDetailAsync(Guid, CancellationToken)` | public async | Lädt Projekt mit Repositories und Aufgaben |
| `SaveRepositoryStartKonfigurationAsync(Guid, string, bool, CancellationToken)` | public async | Speichert Startkonfiguration für Repository |
| `GetRepositoryStartKonfigurationAsync(Guid, CancellationToken)` | public async | Lädt Startkonfiguration eines Repositories |

**Private Hilfsmethoden:**
- `ValidateRequiredFields(string, IReadOnlyDictionary<string,string>)` — validiert Plugin-spezifische Pflichtfelder
- `ResolveRepositoryUrl(string, IReadOnlyDictionary<string,string>)` — ermittelt Repository-URL je nach Plugin-Typ
- `ResolveRepositoryName(string, IReadOnlyDictionary<string,string>, string)` — ermittelt oder leitet Repository-Namen ab
- `NormalizeFieldValues(IReadOnlyDictionary<string,string>)` — normalisiert Eingabefelder (Trim, Null-Check)
- `NormalizeRequiredValue(string, string)` — normalisiert erforderliche Werte

**Abhängigkeiten:**
- `SoftwareschmiededDbContext` — Datenbankzugriff
- `ILogger<ProjektService>` — Logging

**Plugin-Typ-Unterstützung:**
- `LocalDirectoryPlugin` — verwendet SourceDirectory als RepositoryUrl
- `GitHub` / `Softwareschmiede.GitHub` — erfordert RepositoryUrl und RepositoryName

## `IPluginManager`
Datei: `src/Softwareschmiede/Domain/Interfaces/IPluginManager.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSourceCodeManagementPlugins()` | public | Gibt Liste aller verfügbaren IGitPlugin-Implementierungen zurück |
| `GetDevelopmentAutomationPlugins()` | public | Gibt Liste aller verfügbaren IKiPlugin-Implementierungen zurück |
| `GetDefaultSourceCodeManagementPlugin()` | public | Gibt Standard-SCM-Plugin zurück (wirft Exception wenn keine vorhanden) |
| `GetDefaultDevelopmentAutomationPlugin()` | public | Gibt Standard-KI-Plugin zurück (mit Priorität für Copilot) |

## `PluginManager`
Datei: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`

**Implementierung von IPluginManager**

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| (Konstruktor) | public | Initialisiert Manager mit DI-Container, Logger und Plugin-Verzeichnis |
| `EnsureInitialized()` | private | Thread-sicheres Lazy-Loading der Plugins (Double-Checked Locking) |
| `DiscoverPlugins()` | private | Durchsucht Plugin-Verzeichnis und lädt DLLs |
| `LoadPluginsFromDll(string)` | private | Lädt Plugins aus einzelner DLL mit Fehlerbehandlung |
| `TryCreateAndRegister(Type, string)` | private | Instantiiert Plugin via ActivatorUtilities und registriert nach Typ |

**Besonderheiten:**
- Lazy Initialization: Plugins werden erst beim ersten Aufruf geladen
- Thread-sicher durch Double-Checked Locking
- Test-Modus: Filtert Plugins basierend auf Umgebungsvariable `SOFTWARESCHMIEDE_TEST_DB_PATH`
- Fehlerbehandlung: Ungültige DLLs werden protokolliert und übersprungen

**Abhängigkeiten:**
- `IServiceProvider` — DI-Container für Plugin-Instantiierung
- `ILogger<PluginManager>` — Logging
