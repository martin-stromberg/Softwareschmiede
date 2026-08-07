# Code-Review - Anzeige offener Todos im Menue

Status: Keine Befunde

Iteration: 2

Hinweis zur Ausfuehrung: In dieser Umgebung war kein delegierbarer Unteragent fuer `/review-code` verfuegbar. Der Code-Review wurde lokal anhand des aktuellen Diffs, der relevanten Implementierung, der Plan-/Review-Artefakte und der fokussierten Tests ausgefuehrt. Diese Iteration folgt auf eine Implementierungsbewertung ohne Codeaenderungen; die Plan-Review steht weiterhin auf `Vollständig umgesetzt`.

## Befunde

Keine.

## Gepruefte Bereiche

- `TodoService.GetOpenTodoCountsAsync` dedupliziert Eingabe-IDs, behandelt leere Eingaben und zaehlt offene Todos per gruppierter EF-Abfrage nur mit `ErledigtAm == null` (`src/Softwareschmiede/Application/Services/TodoService.cs:100`, `src/Softwareschmiede/Application/Services/TodoService.cs:105`, `src/Softwareschmiede/Application/Services/TodoService.cs:111`).
- `MainWindowViewModel` laedt die offenen Todo-Anzahlen per Bulk-Abfrage nach der aktiven Aufgabenliste und mappt die Werte in die Panel-Items; fehlende Dictionary-Eintraege werden als `0` behandelt (`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:256`, `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:291`).
- Das neue Item-Command oeffnet den Dialog fuer die konkrete Aufgabe ueber das geladene `OpenTodosDialogViewModel` und den bestehenden Dialogservice (`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:297`, `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:320`, `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:327`).
- Das gemeinsame `ActiveTasksListControl` zeigt den Todo-Button im geteilten Template und markiert den Button-Klick als behandelt, damit keine Kachel-Navigation mit ausgeloest wird (`src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml:37`, `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml:38`, `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs:62`, `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs:64`).
- `OpenTodosDialogViewModel` laedt ausschliesslich offene Todos, aktualisiert die Leerzustands-Properties und der Dialog zeigt Liste oder Leerzustand read-only ohne Bearbeitungscontrols (`src/Softwareschmiede.App/ViewModels/OpenTodosDialogViewModel.cs:46`, `src/Softwareschmiede.App/ViewModels/OpenTodosDialogViewModel.cs:54`, `src/Softwareschmiede.App/ViewModels/OpenTodosDialogViewModel.cs:61`, `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml:36`, `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml:86`).
- `WpfDialogService.ShowOpenTodosDialogAsync` oeffnet den neuen Dialog modal auf dem WPF-Dispatcher und setzt das Hauptfenster als Owner (`src/Softwareschmiede.App/Services/WpfDialogService.cs:94`, `src/Softwareschmiede.App/Services/WpfDialogService.cs:106`).
- Direkte `MainWindowViewModel`-Konstruktoraufrufe in den Tests sind an die neue `TodoService`-Dependency angepasst; produktiv erfolgt die Aufloesung ueber DI.

## Validierung

- `dotnet build Softwareschmiede.slnx --no-restore` bestanden: 0 Warnungen, 0 Fehler.
- `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build --filter "FullyQualifiedName~TodoServiceTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~OpenTodosDialogViewModelTests"` bestanden: 44/44 Tests.

## Restrisiko

Eine manuelle UI-Pruefung der echten WPF-Interaktion in Seitenleiste und Dashboard wurde in diesem Review-Schritt nicht ausgefuehrt. Der relevante Codepfad ist unit-getestet und der XAML-Build ist gruen; die finale UI-Validierung bleibt Teil des Test-/Abnahmeschritts.
