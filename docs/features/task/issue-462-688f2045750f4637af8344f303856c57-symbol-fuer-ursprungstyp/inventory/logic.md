# Logik — Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

## `WorkItemListLayout` (static)
Datei: `src/Softwareschmiede.App/ViewModels/WorkItemListLayout.cs`

Zentrale, testbare Stelle für das Label-Mapping der Auftragsart sowie für Sort-/Gruppierungsdeskriptoren der aktiven Aufgabenliste. Wird aufgerufen aus `MainWindowViewModel.MapActiveWorkItemPanelItem` (Label/Gruppenname) und `ActiveTasksListControl` (Deskriptoren).

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `NotExecuted` (`const string = "Nicht ausgeführt"`) | public | Fallback-Gruppenname im KI-Modus (Z. 13) |
| `WithoutRepository` (`const string = "Ohne Repository"`) | public | Fallback-Gruppenname im Repository-Modus (Z. 16) |
| `WithoutKind` (`const string = "Ohne Auftragsart"`) | public | Fallback-Gruppenname im Auftragsart-Modus und zugleich Rückgabewert des `KindGroupName`-Default-Arms (Z. 19) |
| `KindGroupName(WorkItemKind)` | public static | Switch-Ausdruck `WorkItemKind → string` (Z. 23–32): `Manual`→„Manuell", `Instruction`→„Anweisung", `IssuePreparation`→„Issue-Aufbereitung", `PullRequest`→„Pull Request", `Alert`→„Alert", `_`→`WithoutKind`. Dient **zweifach**: als Gruppenname im „Art"-Modus und als sichtbarer Badge-Text (`KindLabel`). **Wichtig für den Unspecified-Fall:** `KindGroupName` selbst liefert für `Unspecified`/unbekannte Werte „Ohne Auftragsart" — dass kein Badge erscheint, liegt allein an der `KindLabel`-Logik in `MapActiveWorkItemPanelItem` (s. u.), nicht an dieser Methode |
| `CreateSortDescriptors(WorkItemListMode)` | public static | Sortierkette je Modus (Z. 42–74); `Kind`-Modus: `KindSortOrder` asc, dann `SortTimestamp` desc, `Title`, `Id` |
| `CreateGroupDescriptor(WorkItemListMode)` | public static | `PropertyGroupDescription` je Modus oder `null` bei `Date` (Z. 82–89); `Kind` → `PropertyGroupDescription(nameof(ActiveWorkItemPanelItem.KindGroupName))` |
| `Configure(ListCollectionView, WorkItemListMode)` | public static | Setzt Gruppen- und Sortdeskriptoren auf die übergebene View (Z. 94–109) |

Ein Symbol-Mapping (`WorkItemKind → string?`) existiert noch nicht — `KindGroupName` ist der strukturelle Vorbild-Anchor.

## `MainWindowViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ActiveWorkItemsList` (Property) | public | `ObservableCollection<ActiveWorkItemPanelItem>` (Z. 103); wird mit `DashboardViewModel` geteilt (`_dashboardViewModel.Initialize(ActiveWorkItemsList, NavigateToWorkItem)`, Z. 321) — eine Änderung am Item-Mapping wirkt auf Seitenleiste und Dashboard zugleich |
| `RefreshActiveWorkItemsAsync(CancellationToken)` | public | Reentrancy-geschützt via `SemaphoreSlim _refreshGate` (`WaitAsync(0)` → Skip-if-busy, Z. 420); lädt `GetActiveWorkItemsAsync`, To-do-Zähler, dann `ActiveWorkItemsList.ReplaceAll(items.Select(MapActiveWorkItemPanelItem))` (Z. 425–427) |
| `RefreshActiveWorkItemsInBackground` | private | `SafeFireAndForget`-Wrapper um `RefreshActiveWorkItemsAsync`; Trigger für Event-/Timer-Aktualisierungen |
| `MapActiveWorkItemPanelItem(WorkItem, IReadOnlyDictionary<Guid,int>)` | private | Befüllt alle Panel-Item-Properties (Z. 464–514). Auftragsart relevant (Z. 495–499): `Kind = workItem.Kind`; `KindGroupName = WorkItemListLayout.KindGroupName(workItem.Kind)`; `KindLabel = workItem.Kind == WorkItemKind.Unspecified ? string.Empty : WorkItemListLayout.KindGroupName(workItem.Kind)` — d. h. der Badge-Text ist nur für `Unspecified` unterdrückt; zukünftige unbekannte Enum-Werte würden aktuell als Badge „Ohne Auftragsart" erscheinen |
| `ResolvePlugin(PluginType, string?)` | private | Liefert `(Name, Symbol)` für SCM-/KI-Glyphen (Z. 532–546): leerer Prefix → `(null, null)`; nicht auflösbarer Prefix → roher Prefix als Name + `IPlugin.FallbackSymbol` als Symbol; Moq-Mocks ohne `PluginSymbol`-Setup liefern `null` → ebenfalls Fallback-Glyph |
| `OnRunningCountChanged` / `OnRunDataChanged` | private | Event-Handler (`IRunningAutomationStatusSource.RunningCountChanged`, `WorkItemRunDataChangedNotifier.RunDataChanged`); marshallt via `_dispatcherInvoke` und triggert Refresh (Z. 555–563) |
| `OnUpdateTimerTick` | private | 5-s-`DispatcherTimer`-Refresh (Z. 565) |
| `LoadSavedWorkItemsListModeAsync` | public | Stellt persistierten `WorkItemListMode` via `WorkItemListModeBinding` wieder her (Z. 300) |
| `WorkItemListMode` (Property) | public | Delegiert an `WorkItemListModeBinding` (Z. 109); gebunden an die vier RadioButtons |

Abonnierte Events: `IRunningAutomationStatusSource.RunningCountChanged`, `WorkItemRunDataChangedNotifier.RunDataChanged`, `WorkItemListModeBinding.ValueChanged` (→ `OnPropertyChanged`), `DispatcherTimer.Tick`, `UpdateSettingsSaved`.

## `ActiveTasksListControl` (Code-Behind)
Datei: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `ItemsSourceProperty` / `ItemsSource` | public DP | Quell-`IEnumerable`; `OnItemsSourceChanged` → `CreateView` (Z. 14–19, 69–73) |
| `NavigateCommandProperty` / `NavigateCommand` | public DP | Kachel-Navigations-Command (Z. 22–27) |
| `ListModeProperty` / `ListMode` | public DP | `WorkItemListMode`, Default `Date`; `OnListModeChanged` → `WorkItemListLayout.Configure(_view, …)` (Z. 30–35, 75–82) |
| `_view` (`ListCollectionView?`) | private | Instanz-eigene Collection-View — bewusst **nicht** `CollectionViewSource.GetDefaultView`, da die ItemsSource zwischen Seitenleiste und Dashboard geteilt wird und eine gemeinsame Default-View die Gruppierung leaken würde (Z. 37–40) |
| `CreateView(object?)` | private | Erzeugt `ListCollectionView` bei `IList` und ruft `WorkItemListLayout.Configure` auf; weist die View direkt `WorkItemsItemsControl.ItemsSource` zu (Z. 84–100) |
| `OpenTodosButtonClick` | private | `e.Handled = true` — verhindert, dass der verschachtelte To-do-Button die Kachel-Navigation auslöst (Z. 102–105) |

## `NullOrEmptyToVisibilityConverter`
Datei: `src/Softwareschmiede.App/Converters/AppConverters.cs` (Z. 87–100), registriert in `App.xaml` Z. 18

`IValueConverter`: `null` oder leerer `string` → `Visibility.Collapsed`, sonst `Visible`. Steuert die Sichtbarkeit des Auftragsart-Badge-`Border`s (über `KindLabel`) und der beiden Plugin-Glyph-`TextBlock`s (über `ScmPluginSymbol`/`AiPluginSymbol`).

## Auftragsart-Herkunft (Kontext: wer `WorkItem.Kind` setzt)

| Stelle | Datei / Zeile | Zugewiesene Art |
|--------|---------------|------------------|
| `WorkItemService.CreateAsync` (manuelle Anlage) | `src/Softwareschmiede/Application/Services/WorkItemService.cs` Z. 219 | `Manual` |
| `WorkItemService.CreateFromIssueAsync` (manuelle Issue-Anlage) | Z. 248 | `Manual` |
| `WorkItemService.CreateFromIssueEventAsync` | Z. 321 | `IssuePreparation` bei `Purpose == "IssueAufbereitung"`, sonst `Instruction` |
| `WorkItemService.CreateFromPullRequestAsync` | Z. 412 | `PullRequest` |
| `WorkItemService.CreateFromAlertAsync` | Z. 542 | `Alert` |
| `ScmEventOrchestrator` (Prompt-Zustellung) | `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` Z. 804 | `task.Kind = WorkItemKind.Instruction` — Typwechsel Aufbereitung → Anweisung |
| Persistenz | `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiedeDbContext.cs` Z. 206–209 | Spalte `Aufgaben.Auftragsart`, string-Konversion, Default `Unspecified` |

Kein Änderungsbedarf an diesen Pfaden laut Anforderung (Scope-Ausschluss).

## `DashboardViewModel`
Datei: `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `ActiveWorkItemsList` (Property) | public | Geteilte `ObservableCollection<ActiveWorkItemPanelItem>`; wird in `Initialize(aktiveAufgabenListe, navigateToWorkItem)` gesetzt (Z. 71, 160) — Dashboard rendert dieselben Items wie die Seitenleiste |
| `WorkItemListMode` (Property) | public | Eigener Modus des Dashboards via `WorkItemListModeBinding.ForDashboard` (Z. 91, 108); eigener Persistenzschlüssel |

## `WorkItemListModeService` / `WorkItemListModeBinding`
Dateien: `src/Softwareschmiede.App/Services/WorkItemListModeService.cs`, `src/Softwareschmiede.App/Services/WorkItemListModeBinding.cs`

Persistieren/Restaurieren den `WorkItemListMode` je Ansicht über `AppSettingService` (`ActiveWorkItemsListModeKey` = `"aktiveaufgaben.listenmodus"`, `…DashboardKey`); Legacy-String-Mapping („Ki"/„Datum") + `Enum.TryParse`, Fallback `Date`. Kein direkter Bezug zum Badge — nur Kontext der „Art"-Gruppierung.
