# TaskDetailView, TaskDetailViewModel und TerminalControl

## ViewModel

`TaskDetailViewModel` haelt `ProtokollService`, `KiAusfuehrungsService` und `EntwicklungsprozessService` als Abhaengigkeiten (`src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs:496`, `:498`, `:499`, `:500`). Es stellt `ObservableCollection<Protokolleintrag> Protokolleintraege` fuer die Info-Ansicht bereit (`TaskDetailViewModel.cs:226`).

Beim Laden:

- holt es die Aufgabe,
- prueft laufende CLI,
- bindet ggf. die vorhandene `PseudoConsoleSession`,
- laedt Protokolleintraege ueber `GetByAufgabeAsync`,
- fuellt die `Protokolleintraege`-Collection (`TaskDetailViewModel.cs:566`, `:580`, `:588`, `:598`, `:600`).

Wichtig: Die Collection wird beim Laden aktualisiert. Es gibt aktuell keinen beobachteten Mechanismus, der neue `CliOutput`-Eintraege live in diese Liste streamt. Fuer die Akzeptanz "nach Abschluss oder Unterbrechung nachvollziehbar" reicht Persistenz; fuer sofortige Sichtbarkeit in der Info-Protokollliste waere ein zusaetzlicher Refresh/Eventpfad noetig.

## Start und Session-Bindung

Der normale Start aus dem ViewModel ruft `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync` (`TaskDetailViewModel.cs:1300`). Nach dem Start holt es die `PseudoConsoleSession` und feuert `PseudoConsoleSessionGestartet` (`:1310`, `:1314`).

Beim CLI-Neustart/Pluginwechsel ruft das ViewModel direkt `KiAusfuehrungsService.StartWithPseudoConsoleAsync` und feuert ebenfalls `PseudoConsoleSessionGestartet` (`TaskDetailViewModel.cs:1415`, `:1428`).

Promptvorlagen werden ueber `session.WritePromptAsync` in die laufende Session geschrieben (`TaskDetailViewModel.cs:667`, `:675`). Das ist Eingabe, nicht Ausgabe; die Anforderung betrifft die automatisch entstehende CLI-Ausgabe.

## View und TerminalControl

`TaskDetailView.xaml.cs` synchronisiert DataContext-Wechsel mit `TerminalConsole.Session`. Bei neuer Session setzt `SetTerminalSession` die Session am Control und schreibt die Prozess-ID in `AutomationProperties.HelpText` (`src/Softwareschmiede.App/Views/TaskDetailView.xaml.cs:40`, `:55`, `:95`, `:97`).

`TaskDetailView.xaml` zeigt:

- die Protokollliste in der Info-Ansicht (`src/Softwareschmiede.App/Views/TaskDetailView.xaml:352`, `:357`, `:381`)
- das `TerminalControl` als eigenstaendiges Control in der CLI-Ansicht (`TaskDetailView.xaml:395`)
- den CLI-Status in der Fusszeile (`TaskDetailView.xaml:438`)

`TerminalControl` ist ein reiner Renderer. Es abonniert `PseudoConsoleSession.BufferChanged`, ruft `InvalidateVisual` auf und rendert den `TerminalBuffer` (`src/Softwareschmiede.App/Controls/TerminalControl.cs:13`, `:82`, `:88`, `:102`). Tastatur- und Zwischenablageeingaben schreibt es in `Session.InputStream` und markiert Input-Aktivitaet (`TerminalControl.cs:185`, `:209`, `:223`, `:228`, `:294`).

## Schlussfolgerung fuer Implementierung

Die UI ist nicht der richtige primaere Ort fuer vollstaendige Protokollierung: Eine Aufgabe kann weiterlaufen, waehrend keine View gebunden ist. Die Persistenz muss in der Session bzw. im Service-Lifecycle liegen, bevor Ausgabe vom UI-Lebenszyklus abhaengt. Die UI kann spaeter nur optional aktualisiert werden.

