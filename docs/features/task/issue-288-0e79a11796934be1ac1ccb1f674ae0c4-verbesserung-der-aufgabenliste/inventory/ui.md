# UI / XAML-Struktur

## `ActiveTasksListControl`

Dateien: `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`, `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs`

Wiederverwendbares `UserControl` zur Darstellung aktiver Aufgaben als Kachelliste (Kommentar in `.xaml.cs:8`: "Seitenleiste und Dashboard").

### Dependency Properties (`.xaml.cs`)

| Property | Typ | Standard | Zweck |
|----------|-----|----------|-------|
| `ItemsSource` | `IEnumerable?` | `null` | Die anzuzeigenden Aufgaben (`.xaml.cs:12–17, 36–40`) |
| `NavigateCommand` | `ICommand?` | `null` | Navigations-Command, erhält `AktiveAufgabePanelItem.Id` als Parameter (`.xaml.cs:20–25, 43–47`) |
| `ShowNavigationButton` | `bool` | `true` | `true` → Template mit "→"-Button; `false` → vollflächig klickbare Kachel (`.xaml.cs:28–33, 50–54`) |

### DataTemplates (`.xaml`)

- **`AufgabenKachelInhaltTemplate`** (Zeile 6–65): Gemeinsamer Kachelinhalt, `StackPanel` mit sechs Elementen:
  1. `TextBlock` `Titel` (SemiBold, 13)
  2. `TextBlock` `ProjektName` (FontSize 11)
  3. `TextBlock` `SCM: {ScmPluginName}` (FontSize 10, `NullOrEmptyToVisibilityConverter`)
  4. `TextBlock` `KI: {KiPluginName}` (FontSize 10, `AutomationId="AktiveAufgabeKiPlugin_{Titel}"`)
  5. `TextBlock` Status-Volltext via `KiAusfuehrungsStatusConverter` (`AutomationProperties.Name="AufgabeStatus:{Titel}"`, `HelpText` = derselbe Status-Text, `behaviors:StatusUebergangsAnimation.Status` angebunden)
  6. Leerer `TextBlock` als UIA-Marker (`Name="AufgabeKachel:{Titel}"`, `HelpText="Aktiv:{IsAktiv};Pausiert:{IstPausiert}"` per MultiBinding) — Kommentar (Zeile 38–40) dokumentiert ausdrücklich, dass der Kachel-`Border` keinen `AutomationPeer` erzeugt und daher dieses Element den UIA-Anker trägt
  7. `Button` `OffeneTodos:{Titel}` (`Content=OffeneTodoLabelText`, `Command=OffeneTodosAnzeigenCommand`, `Click="OffeneTodosButton_Click"` → `e.Handled = true`, `.xaml.cs:62–65`)
- **`AufgabenKachelMitNavigationButtonTemplate`** (Zeile 67–112): `Border` (`Name="Aufgabe:{Titel}"`, `SurfaceBrush`-Hintergrund, `DataTrigger IsAktiv` → `AccentBrush`-Rand, `DataTrigger IstPausiert` → `Opacity 0.55`) mit zweispaltigem `Grid`: Inhalt + "→"-`Button` (`AutomationProperties.Name="AufgabeNavigieren:{Titel}"`, `Command={Binding NavigateCommand, RelativeSource=UserControl}`, `CommandParameter={Binding Id}`).
- **`AufgabenKachelVollflaechigKlickbarTemplate`** (Zeile 114–144): Gleiches Border-Styling plus `Cursor="Hand"` und `Border.InputBindings` → `MouseBinding MouseAction="LeftClick"` → `NavigateCommand` + `Id`. Kein separater Button. **Hinweis:** kein UIA-klickbares Element (Border erzeugt keinen AutomationPeer) — für E2E gibt es aktuell keinen adressierbaren Klick-Anker auf der Kachel selbst.

### ItemsControl (`.xaml:147–158`)

`ItemsControl ItemsSource={Binding ItemsSource, RelativeSource=UserControl}`; `Style.Triggers`-`DataTrigger` auf `ShowNavigationButton=False` wechselt `ItemTemplate` zwischen `AufgabenKachelMitNavigationButtonTemplate` (Default) und `AufgabenKachelVollflaechigKlickbarTemplate`. **Kein `GroupStyle`, keine `CollectionViewSource`/Gruppierung vorhanden.**

## `MainWindow` (Seitenleiste)

Datei: `src/Softwareschmiede.App/Views/MainWindow.xaml`

- Seitenleiste = `Border Grid.Column="0"` mit `Width={Binding IsNavigationExpanded, Converter=BoolToWidthConverter}` (aufgeklappt 240, eingeklappt 48).
- Navigations-Buttons mit Unicode-Glyphen: "☰" Toggle (Z. 56), "⊞ Dashboard" (Z. 75), "📁 Projekte" (Z. 89), "⇥ SCM-Ereignisse" (Z. 101), "⚙ Einstellungen" (Z. 116).
- Sektions-Titel "Aktive Aufgaben": einzelner `TextBlock` (Zeile 127–131), nur sichtbar, wenn `IsDashboardVisible == false` (`InverseBoolToVisibilityConverter`). **Keine Buttons in der Titelleiste vorhanden.**
- `ScrollViewer` (Zeile 134–140) enthält `ActiveTasksListControl` mit `ItemsSource={Binding AktiveAufgabenListe}`, `NavigateCommand={Binding NavigateZuAufgabeCommand}` — `ShowNavigationButton` nicht gesetzt → Standard `true` → Template mit "→"-Button. Auch nur sichtbar, wenn Dashboard nicht angezeigt wird.
- Fußzeile: `CurrentVersion` (`AutomationId="AppVersionText"`), Update-Buttons, `UpdateHinweis`.

## `DashboardView`

Datei: `src/Softwareschmiede.App/Views/DashboardView.xaml`

- Sektion "Aktive Aufgaben" (Zeile 60–71): `TextBlock`-Überschrift + `ActiveTasksListControl` mit `ItemsSource={Binding AktiveAufgabenListe}`, `NavigateCommand={Binding NavigateZuAufgabeCommand}` und **`ShowNavigationButton="False"`** → vollflächig-klickbares Template bereits aktiv.
- Dieselbe `AktiveAufgabenListe`-Instanz wie die Seitenleiste (geteilt über `DashboardViewModel.Initialize`, siehe [logic.md](logic.md)).

## Verwendete UI-Konventionen

- Symbole werden als Unicode-Glyphen in `TextBlock`s gerendert (☰ ⊞ 📁 ⇥ ⚙ ⇧ ⟳ ▶ ⏸ ✓ ⏳).
- Farben über `DynamicResource`-Brushes: `BackgroundBrush`, `SurfaceBrush`, `BorderBrush`, `AccentBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`, `SuccessBrush`, `WarningBrush`, `ErrorBrush`, `SidebarBackgroundBrush`.
