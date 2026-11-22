# UI / XAML — Symbol für Auftragsart auf den Aufgabenkacheln (Issue #462)

## `ActiveTasksListControl.xaml` — Kachel-Templates
Datei: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`

Einziges Rendering der Aufgabenkachel; geteilt von Seitenleiste (`MainWindow.xaml`) und Dashboard (`DashboardView.xaml`) — ein Eingriff hier deckt beide Listen ab.

### `WorkItemsTileContentTemplate` (Z. 6–84) — Kachelinhalt
Drei Zeilen: `Title`-`TextBlock` (Z. 8–12), `RepositoryName`-`TextBlock` (Z. 13–17), horizontale **Symbolzeile** (`StackPanel`, Z. 18–71) mit vier Kindern in dieser Reihenfolge:

| Element | Binding | Sichtbarkeit / Format |
|---------|---------|----------------------|
| **Auftragsart-Badge** (`Border`, Z. 19–31) | `BorderBrush`/`BorderThickness`/`CornerRadius="3"`/`Padding="6,1"`/`Margin="0,0,8,0"`; enthält **einen** `TextBlock` mit `Text="{Binding KindLabel}"`, `FontSize="10"`, `Foreground={DynamicResource SecondaryTextBrush}` | `AutomationProperties.AutomationId="{Binding Title, StringFormat=ActiveWorkItemKind_{0}}"`, `AutomationProperties.HelpText="{Binding KindLabel, StringFormat=Auftragsart: {0}}"`, `ToolTip="{Binding KindLabel, StringFormat=Auftragsart: {0}}"`, `Visibility="{Binding KindLabel, Converter={StaticResource NullOrEmptyToVisibilityConverter}}"` — steht **als erstes** Element in der Symbolzeile |
| SCM-Glyph (`TextBlock`, Z. 32–39) | `Text="{Binding ScmPluginSymbol}"`, `FontSize="13"`, `SecondaryTextBrush`, `Margin="0,0,8,0"` | `ToolTip`/`HelpText` = „SCM: {ScmPluginName}", `AutomationId="ActiveWorkItemScmPlugin_{Title}"`, `Visibility` via `NullOrEmptyToVisibilityConverter` auf `ScmPluginSymbol` |
| KI-Glyph (`TextBlock`, Z. 40–47) | `Text="{Binding AiPluginSymbol}"`, `FontSize="13"`, `SecondaryTextBrush`, `Margin="0,0,8,0"` | `ToolTip`/`HelpText` = „KI: {AiPluginName}", `AutomationId="ActiveWorkItemAiPlugin_{Title}"`, `Visibility` via `NullOrEmptyToVisibilityConverter` auf `AiPluginSymbol` |
| Status-Glyph (`TextBlock`, Z. 48–56) | `Text`/`ToolTip`/`HelpText` via `AiExecutionStatusSymbolConverter`/`AiExecutionStatusConverter` auf `{Binding .}` | `AutomationId="ActiveWorkItemStatusGlyph_{Title}"`, `AutomationProperties.Name="WorkItemStatus:{Title}"`, `StatusTransitionAnimation.Status` (Behavior) — bleibt laut Scope unverändert |
| To-do-Button (`Button`, Z. 57–70) | `Content="{Binding OpenTodoLabelText}"`, `Command` + `Click="OpenTodosButtonClick"` | `AutomationProperties.Name="OpenTodos:{Title}"` — unverändert |

UIA-Anker für die Pausiert-Abblendung: leerer `TextBlock` mit `AutomationProperties.Name="WorkItemCard:{Title}"` und `HelpText="Active:{IsActive};Paused:{IsPaused}"` (Z. 75–82, MultiBinding).

### `WorkItemsTileTemplate` (Z. 86–137) — Kachelrahmen
Vollflächiger `Button` (`AutomationProperties.Name="WorkItemNavigate:{Title}"`, `NavigateCommand` + `CommandParameter={Binding Id}`) mit `ControlTemplate`: `TileBorder` (`AutomationProperties.Name="WorkItem:{Title}"`), `DataTrigger` auf `IsActive` (Accent-Rahmen) und `IsPaused` (Opacity 0.55), Hover-/Pressed-Feedback.

### `ItemsControl` + `GroupStyle` (Z. 140–165)
`x:Name="WorkItemsItemsControl"`. `GroupStyle.ContainerStyle` setzt `AutomationProperties.Name="WorkItemsGroup:{Name}"` am `GroupItem` (E2E-Anker); `HeaderTemplate` rendert den Gruppennamen als **Klartext**-`TextBlock` (`FontSize="12"`, `SemiBold`, `SecondaryTextBrush`, Z. 155–160) — Gruppenköpfe haben kein Symbol und sollen laut Scope auch keines bekommen.

## Verwendungsstellen des Controls

| Stelle | Zeilen | AutomationId | Bindings |
|--------|--------|--------------|----------|
| `src/Softwareschmiede.App/Views/MainWindow.xaml` | 171–174 | `ActiveWorkItemsListMenu` | `ItemsSource="{Binding ActiveWorkItemsList}"`, `NavigateCommand="{Binding NavigateToWorkItemCommand}"`, `ListMode="{Binding WorkItemListMode}"`; sichtbar nur wenn `!IsDashboardVisible` (Z. 170, `InverseBoolToVisibilityConverter`) |
| `src/Softwareschmiede.App/Views/DashboardView.xaml` | 95–98 | `ActiveWorkItemsListDashboard` | identische Bindings; immer sichtbar auf dem Dashboard |

## Modus-Umschalter (Kontext „Art"-Gruppierung)

| Ansicht | Zeilen | RadioButtons |
|---------|--------|--------------|
| `MainWindow.xaml` | 136–162 | `WorkItemsMode:Date` (Content „Datum"), `WorkItemsMode:Repository`, `WorkItemsMode:AI` (Content „KI"), `WorkItemsMode:Kind` (Content „Art", Tooltip „Gruppiert nach Auftragsart (manuell, Anweisung, Issue-Aufbereitung, Pull Request, Alert)"); verborgen bei eingeklappt Navigation (`IsNavigationExpanded`) |
| `DashboardView.xaml` | 69–91 | `DashboardWorkItemsMode:*` — gleiche vier Modi, eigene `GroupName="DashboardWorkItemMode"` |

## Ressourcen

- `NullOrEmptyToVisibilityConverter`: in `src/Softwareschmiede.App/App.xaml` Z. 18 als `StaticResource` registriert — steuert Badge-`Border` (`KindLabel`) und beide Plugin-Glyph-`TextBlock`s.
- `EnumToBoolConverter`: RadioButton-`IsChecked` ↔ `WorkItemListMode` mit `ConverterParameter`.
- `SecondaryTextBrush`/`PrimaryTextBrush`/`BorderBrush`/`SurfaceBrush`/`AccentBrush`: Theme-`DynamicResource`s.
