# Logik

## `MainWindowViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` (`sealed`, `IDisposable`)

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `AktiveAufgabenListe` | public `ObservableCollection<AktiveAufgabePanelItem>` | Geteilte Datenquelle für Seitenleiste und Dashboard (Z. 100) |
| `NavigateZuAufgabeCommand` | public `ICommand` (`RelayCommand<Guid>`) | Navigiert zur TaskDetailViewModel einer Aufgabe (Z. 127, 170) |
| `AktiveAufgabenAktualisierenAsync(ct)` | public `Task` | Lädt `_aufgabeService.GetAktiveAufgabenAsync` + `TodoService.GetOpenTodoCountsAsync`, ersetzt die Liste via `ReplaceAll(...Select(MapAktiveAufgabePanelItem))`; Re-Entrancy-Schutz per `_refreshGate`-Semaphore; Fehler → `LogWarning` (Z. 297–320) |
| `MapAktiveAufgabePanelItem(aufgabe, offeneTodoCounts)` | private | Erzeugt `AktiveAufgabePanelItem` inkl. `ScmPluginName`/`KiPluginName` via `ResolvePluginName`, `IsAktiv` via `GetAktiveAufgabeId()`, `HasScheduledPrompt` via `PromptZeitVersandService.GetScheduledPromptStatus`, To-do-Command (Z. 340–369) |
| `ResolvePluginName(pluginType, pluginPrefix)` | private `string?` | Löst Prefix über `IPluginManager` (`GetSourceCodeManagementPlugins`/`GetDevelopmentAutomationPlugins`, Vergleich `OrdinalIgnoreCase`) zu `PluginName` auf; Fallback: roher Prefix; `null` bei leerem Prefix (Z. 381–394) |
| `NavigateZuAufgabe(aufgabeId)` | private | Erzeugt `TaskDetailViewModel`, setzt `ZurueckAction=NavigateToDashboard`, `CurrentView`, `AufgabeId` (Z. 322–338) |
| `GetAktiveAufgabeId()` | private `Guid?` | Liefert die ID der aktuell offenen `TaskDetailViewModel`-Aufgabe (Z. 396–401) |
| `OffeneTodosDialogOeffnenAsync` | private `Task` | Öffnet `OpenTodosDialogViewModel` via `IDialogService` (Z. 371–379) |
| `NavigateToDashboard()` | private | Teilt `AktiveAufgabenListe` + `NavigateZuAufgabe` via `DashboardViewModel.Initialize` (Z. 195–204) |
| `AktiveAufgabenImHintergrundAktualisieren()` | private | `SafeFireAndForget`-Hülle (Z. 418–421) |

Abonnierte Events: `_runningStatusSource.RunningCountChanged` → `OnRunningCountChanged` → Refresh (Z. 174, 403–406); `_laufdatenChangedNotifier.LaufdatenChanged` → Refresh (Z. 176–179, 408–411); `_scmNotifier.Changed` → `RefreshScmCountAsync` (Z. 175, 262–279); `DispatcherTimer _aktualisierungsTimer` (5 s) → `OnAktualisierungsTimerTick` → Refresh (Z. 181–186, 413–416); `CurrentView`-Setter → Refresh (Z. 60–64).

Publizierte Events: keine eigenen (nur `INotifyPropertyChanged` via `ViewModelBase`).

Weitere relevante Konstruktor-Details: `_pluginManager = serviceProvider.GetService<IPluginManager>()` (Z. 156 — optional, kann `null` sein); Optionale Dienste via `MainWindowOptionaleDienste`-Record (`src/Softwareschmiede.App/ViewModels/MainWindowOptionaleDienste.cs`, u. a. `DispatcherInvoke`, `LaufdatenChangedNotifier`).

**Nicht vorhanden:** Modus-Zustand/Commands für die Listenanzeige, Symbol-Auflösung, Gruppierung.

## `DashboardViewModel`

Datei: `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs` (`sealed`)

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `AktiveAufgabenListe` | public `ObservableCollection<AktiveAufgabePanelItem>` (private setter) | Wird von `MainWindowViewModel` per `Initialize` gesetzt — dieselbe Instanz (Z. 69–73) |
| `Initialize(aktiveAufgabenListe, navigateZuAufgabeAction)` | public | Übernimmt die geteilte Liste und den Navigationsdelegaten (Z. 142–146) |
| `NavigateZuAufgabeCommand` | public `ICommand` | Delegiert an `NavigateZuAufgabeAction` (Z. 79, 97) |
| `LadenAsync` | private | Lädt Statistiken/Recovery-Kandidaten; berührt `AktiveAufgabenListe` nicht (Z. 101–137) |

## `ActiveTasksListControl` (Code-Behind)

Datei: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OffeneTodosButton_Click` | private | `e.Handled = true` — verhindert, dass der To-do-Klick die Kachel-Navigation (MouseBinding auf dem Border) auslöst (Z. 62–65) |

## `KiAusfuehrungsStatusConverter`

Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs` (Z. 115–185), `sealed`, `IValueConverter`

- `Convert(value, …)`: akzeptiert `Aufgabe` oder `AktiveAufgabePanelItem`, mappt auf internes `StatusDaten`-Record; Reihenfolge der Auswertung:
  1. Pausiert (`PausiertBisUtc` in Zukunft) → `"⏸ Pausiert (noch {restzeit})"` (Format `d\.hh\:mm\:ss` ≥ 1 Tag, sonst `hh\:mm\:ss`)
  2. `HasScheduledPrompt` → `"⏳ Prompt in Wartestellung"`
  3. `AusfuehrungsStatus != Aktiv` → `"✓ Bereit"`
  4. `AufgabeLaufAktivitaet.IstAktiv(AktiveRunId, LastHeartbeatUtc, …)` → `LaufStatus == WartetAufEingabe` ? `"⏸ Wartet"` : `"▶ Läuft"`
  5. `Status == Wartend` → `"⏸ Wartet"`
  6. sonst `"✓ Bereit"`
- `ConvertBack` → `NotSupportedException`.
- **Nicht vorhanden:** Symbol-/Glyph-Variante oder `ConverterParameter`-Steuerung; die Glyphen sind Teil des Volltexts.
- Wird an drei Stellen des Kachel-Inhaltstemplates gebunden (sichtbarer Text, `HelpText`, `StatusUebergangsAnimation.Status`) — siehe [ui.md](ui.md).

## `StatusUebergangsAnimation`

Datei: `src/Softwareschmiede.App/Behaviors/StatusUebergangsAnimation.cs` (`static`, Attached Behavior)

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `Status` (Attached DP) | public | `string`-Property; Änderung triggert `OnStatusChanged` (Z. 23–37) |
| `OnStatusChanged` | private | **Bricht ab, wenn `element.DataContext is not Aufgabe`** (Z. 44) — im Kachel-Template ist der DataContext `AktiveAufgabePanelItem`, die Animation greift dort also faktisch nicht; bei erkannter Statusänderung (über `StatusAenderungsErkennung` pro `aufgabe.Id`) Opacity-Fade 0,3→1,0 in 250 ms (Z. 39–62) |
| `RegistriereUnloadedBereinigung` | private | `Unloaded`-Handler entfernt den Status-Eintrag aus der Erkennung (Z. 64–71) |

## `AufgabeService` (relevante Ausschnitte)

Datei: `src/Softwareschmiede/Application/Services/AufgabeService.cs` (`sealed`, implementiert `IAktiveAufgabenService`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAktiveAufgabenAsync(ct)` | public | `AsNoTracking`, `Include(Projekt)`, `Include(GitRepository)`, Filter `IstAktivOderWartendPredicate` (Status `Gestartet`/`Wartend`, Z. 20–21 via `AufgabeStatusExtensions.AktivOderWartendStatus`), `OrderByDescending(LetzterCliStartUtc ?? ErstellungsDatum)`, `ThenBy(Titel)`, `ThenBy(Id)`, `Take(20)` (Z. 1073–1085) |
| `UpdateAsync` | public | Setzt u. a. `KiPluginPrefix` (laut Anforderungsanalyse; Aufruf in Tests: `UpdateAsync(id, titel, beschreibung, "Softwareschmiede.Codex")`) |

Publizierte Events: keine direkten — Laufdaten-Änderungen werden über `AufgabeLaufdatenChangedNotifier` signalisiert.

## `TodoService` (relevanter Ausschnitt)

Datei: `src/Softwareschmiede/Application/Services/TodoService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOpenTodoCountsAsync(aufgabeIds, ct)` | public | Gruppiert offene To-dos (`ErledigtAm == null`) nach `AufgabeId`, liefert `IReadOnlyDictionary<Guid,int>` (Z. 100–113) |

## `AufgabeLaufdatenChangedNotifier`

Datei: `src/Softwareschmiede/Application/Services/AufgabeLaufdatenChangedNotifier.cs`

UI-neutraler Notifier: `event Action<Guid>? LaufdatenChanged` + `NotifyLaufdatenChanged(aufgabeId)`. Publiziert von Schreibpfaden mit Laufdaten-Änderungen, abonniert vom `MainWindowViewModel`.

## `PluginManager`

Datei: `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs` (`sealed`, `IPluginManager`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DiscoverPlugins()` | private | Lazy über `EnsureInitialized`; lädt `*.dll` aus `AppContext.BaseDirectory/plugins` (TopDirectoryOnly + Unterordner) via `AssemblyLoadContext.Default.LoadFromAssemblyPath` (Z. 174); Filter `IsAllowedInTestMode` (Z. 115–124) erlaubt im Test-Modus nur LocalDirectory, KiSimulator, ClaudeCli, Codex, Devin, GitHubCopilot |
| `GetSourceCodeManagementPlugins()` | public | `IReadOnlyList<IGitPlugin>` (Z. 37–42) |
| `GetDevelopmentAutomationPlugins()` | public | `IReadOnlyList<IKiPlugin>` (Z. 44–49) |
| `GetDefaultDevelopmentAutomationPlugin()` | public | Priorisiert `CliKiPluginBase.ProviderDateiPraefix == "copilot"`, dann alphabetisch (Z. 60–69) |
| `RegisterBuiltInIdePlugins()` | private | Registriert `VisualStudioIdePlugin`/`VisualStudioCodeIdePlugin` ohne DLL (Z. 164–168) |

## `ScmE2EPluginManager` (Test-Modus-Ersatz)

Datei: `src/Softwareschmiede.App/Services/Testing/ScmE2EPluginManager.cs` (`internal sealed`, `IPluginManager`)

Ersetzt im SCM-E2E-Modus den Plugin-Manager: liefert eine fixture-gesteuerte `GitHubPlugin`-Instanz als einziges SCM-Plugin und filtert KI-Plugins auf `Softwareschmiede.KiSimulator` (szenarioabhängig auch leer).

## `ObservableCollectionExtensions.ReplaceAll`

Datei: `src/Softwareschmiede.App/Extensions/ObservableCollectionExtensions.cs`

`ReplaceAll<T>(collection, items)` = `Clear()` + Add-Loop; wird von `AktiveAufgabenAktualisierenAsync` genutzt (komplettes Collection-Replace → volle UI-Aktualisierung).
