# Code-Review - Anzeige offener Todos im Menue

Status: Keine Befunde

Hinweis zur Ausfuehrung: In dieser Umgebung war kein delegierbarer Unteragent fuer `/review-code` verfuegbar. Der Code-Review wurde lokal anhand des aktuellen Diffs, der betroffenen Implementierung und der Tests ausgefuehrt.

## Befunde

Keine.

## Gepruefte Bereiche

- `TodoService.GetOpenTodoCountsAsync` dedupliziert Eingabe-IDs, liefert bei leerer Eingabe ein leeres Ergebnis und zaehlt offene Todos ueber `ErledigtAm == null` per gruppierter EF-Abfrage (`src/Softwareschmiede/Application/Services/TodoService.cs:100`, `src/Softwareschmiede/Application/Services/TodoService.cs:110`).
- `MainWindowViewModel` laedt die offenen Todo-Anzahlen nach der aktiven Aufgabenliste per Bulk-Abfrage und mappt fehlende Eintraege als `0` in die Panel-Items (`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:256`, `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:291`).
- Das neue Item-Command laedt ein `OpenTodosDialogViewModel` fuer die konkrete Aufgabe und oeffnet den Dialog ueber den bestehenden Dialogservice (`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:320`, `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs:327`).
- Das gemeinsame `ActiveTasksListControl` zeigt den Todo-Button im geteilten Aufgaben-Template und behandelt das Click-Event, damit der Button-Klick nicht als Kachel-Navigation weitergereicht wird (`src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml:37`, `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml.cs:62`).
- `OpenTodosDialogViewModel` laedt ausschliesslich offene Todos und aktualisiert Leerzustands-Properties; der Dialog zeigt Liste oder Leerzustand read-only ohne Bearbeitungscontrols (`src/Softwareschmiede.App/ViewModels/OpenTodosDialogViewModel.cs:54`, `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml:46`, `src/Softwareschmiede.App/Views/OpenTodosDialog.xaml:86`).
- `WpfDialogService.ShowOpenTodosDialogAsync` oeffnet den Dialog modal auf dem WPF-Dispatcher und setzt das Hauptfenster als Owner (`src/Softwareschmiede.App/Services/WpfDialogService.cs:94`).

## Validierung

- `dotnet build Softwareschmiede.slnx --no-restore` bestanden, 0 Warnungen, 0 Fehler.
- `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build --filter "FullyQualifiedName~TodoServiceTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~OpenTodosDialogViewModelTests"` bestanden: 44/44 Tests.

## Restrisiko

Eine manuelle UI-Pruefung der echten WPF-Interaktion in Seitenleiste und Dashboard wurde in diesem Review-Schritt nicht ausgefuehrt. Der relevante Codepfad ist unit-getestet und der XAML-Build ist gruen; die finale UI-Validierung bleibt Teil des Test-/Abnahmeschritts.
