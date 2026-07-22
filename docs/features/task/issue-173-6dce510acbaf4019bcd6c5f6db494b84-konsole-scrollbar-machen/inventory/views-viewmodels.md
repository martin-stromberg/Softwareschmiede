# Views und ViewModels

## TaskDetailView.xaml.cs

Das Code-behind bindet die aktive `PseudoConsoleSession` an `TerminalConsole`.

Relevante Stellen:

- `TaskDetailView.xaml.cs`: Im Konstruktor wird `DataContextChanged` abonniert, weil die View bei ViewModel-Wechseln gleichen Typs wiederverwendet wird.
- `OnDataContextChanged(...)`: Registriert `PseudoConsoleSessionGestartet`, `CliGestoppt` und `PromptVorlageGesendet` am neuen `TaskDetailViewModel`.
- `SetTerminalSession(PseudoConsoleSession? session)`: Setzt `TerminalConsole.Session = session` und schreibt die Prozess-ID in `AutomationProperties.HelpText`.

Die Session-Bindung ist imperativ und nicht als XAML-Binding umgesetzt. Eine Scroll-Loesung im `TerminalControl` sollte diese Bindung unveraendert weiterverwenden koennen.

## TaskDetailViewModel.cs

Das ViewModel verwaltet CLI-Ansicht, Start/Stop, Statusanzeige und Session-Events.

Relevante Stellen:

- `TaskDetailViewModel.cs:28`: `DetailAnsicht.Cli`.
- `TaskDetailViewModel.cs:323`: `IsCliViewSelected` steuert die Sichtbarkeit der CLI-Ansicht.
- `TaskDetailViewModel.cs:480`: Event `PseudoConsoleSessionGestartet`.
- `TaskDetailViewModel.cs:483`: Event `CliGestoppt`.
- `TaskDetailViewModel.cs:548-551`: Commands fuer Ansichtswechsel.
- `TaskDetailViewModel.cs:1177-1194`: `WaehleAnsicht(...)` aktualisiert Sichtbarkeitsproperties.
- `TaskDetailViewModel.cs:1408-1428`: CLI-Start, `StartWithPseudoConsoleAsync`, Statussession anhängen und `PseudoConsoleSessionGestartet` feuern.
- `TaskDetailViewModel.cs:1459-1495`: Laufzeitstatus der CLI fuer die Fusszeile.

## Bewertung

Das ViewModel besitzt keinen eigenen Ausgabetext und keinen UI-Scrollzustand. Die Terminalausgabe laeuft ueber `PseudoConsoleSession.Buffer`. Eine Umsetzung der Scrollbarkeit sollte deshalb moeglichst nicht in `TaskDetailViewModel` verankert werden, ausser es wird bewusst ein scrollbezogener UI-Zustand benoetigt, der bei Ansichtswechseln erhalten bleiben soll.

Die vorhandene Architektur trennt:

- ViewModel: Start/Stop, Auswahl, Status.
- Code-behind: Session an Control binden.
- `TerminalControl`: Rendering und Eingaben.
- `PseudoConsoleSession`/`TerminalBuffer`: Outputdaten.

Diese Trennung spricht dafuer, Scrollback-Daten im Domain-Buffer bereitzustellen und die eigentliche Scroll-Interaktion im App-Control bzw. in der View zu halten.
