# UI-Komponenten und Styles

## Relevante vorhandene Komponenten

- `src/Softwareschmiede.App/Controls/RibbonGroup.xaml`
- `src/Softwareschmiede.App/Controls/RibbonLargeButton.xaml`
- `src/Softwareschmiede.App/Controls/RibbonSmallButton.xaml`
- `src/Softwareschmiede.App/Controls/StatusIndicatorControl.xaml`
- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`
- `src/Softwareschmiede.App/Themes/LightTheme.xaml`
- `src/Softwareschmiede.App/Themes/DarkTheme.xaml`

## Projektdetail-spezifische UI

Die Aufgabenliste in `ProjectDetailView.xaml` ist aktuell direkt als `ListBox` innerhalb einer `Border` umgesetzt. Das Item-Template zeigt:

- Aufgabentitel
- Status als Text

Es gibt keine wiederverwendbare Aufgabenlisten-Komponente fuer die Projektdetailansicht. `ActiveTasksListControl` ist fuer Seitenleiste/Dashboard konzipiert und zeigt aktive Aufgaben als Kacheln mit KI-Ausfuehrungsstatus. Es ist fuer die Projektdetailansicht fachlich nicht passend, kann aber als Referenz fuer DataTemplates, AutomationProperties und Statusanzeige dienen.

## Geeignete WPF-Bausteine

Fuer das initial zugeklappte Register bieten sich vorhandene Standard-WPF-Controls an:

- `Expander` fuer einen semantisch passenden, zugeklappten Bereich.
- Alternativ `TabControl`, wenn "Register" strikt als Tab verstanden wird. Die Anforderung sagt jedoch "standardmaessig zugeklappt", was eher zu `Expander` passt.

Die bestehende UI verwendet DynamicResource-Farben (`SurfaceBrush`, `BorderBrush`, `PrimaryTextBrush`, `SecondaryTextBrush`) und sollte diese weiterverwenden.

## Automation und Testbarkeit

Bestehende Controls setzen teils `AutomationProperties.Name`, z. B.:

- Aufgabenliste: `AufgabenListe`
- Ribbon-Button neue Aufgabe: `AufgabeNeu`
- Filter-Button: `Filter`

Fuer die neue Darstellung sollten stabile Automation-Namen eingefuehrt werden, z. B.:

- `OffeneAufgabenListe`
- `BeendeteAufgabenExpander`
- `BeendeteAufgabenListe`

Das erleichtert E2E-Tests und vermeidet fragile Textsuche.

## UI-Risiken

- Eine zweite Liste innerhalb der bestehenden Kartenstruktur muss mit der vorhandenen `ScrollViewer`-Umgebung zusammenspielen. Feste `MaxHeight`-Werte sollten bewusst gesetzt werden, damit grosse Aufgabenmengen die Projektdetailansicht nicht unbedienbar machen.
- Wenn die bestehende Filter-Kachel erhalten bleibt, muss klar sein, ob sie beide Listen, nur offene Aufgaben oder die gesamte Datenquelle beeinflusst.
