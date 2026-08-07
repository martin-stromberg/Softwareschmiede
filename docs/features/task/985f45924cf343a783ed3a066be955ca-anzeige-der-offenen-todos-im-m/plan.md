# Umsetzungsplan - Anzeige offener Todos im Menue

## Zielbild

In der gemeinsamen aktiven Aufgabenliste wird fuer jede aktive oder wartende Aufgabe ein anklickbares Todo-Label angezeigt. Das Label zeigt die Anzahl offener Todos dieser Aufgabe. Ein Klick oeffnet einen read-only Dialog mit den offenen Todos der ausgewaehlten Aufgabe. Aufgaben ohne offene Todos zeigen weiterhin ein anklickbares Label mit `0`; der Dialog zeigt dann einen klaren Leerzustand.

Die Anzeige bleibt ueber die bestehende Aktualisierung der aktiven Aufgabenliste konsistent: initial, bei View-Wechseln, bei Laufstatusaenderungen und im bestehenden 5-Sekunden-Refresh. Eine zusaetzliche Live-Kopplung an die bearbeitbare Todo-Detailansicht ist nicht Bestandteil dieser Umsetzung.

## Entscheidungen

- Das Label ist auch bei `0` offenen Todos sichtbar und anklickbar, weil die Anforderung eine Anzahl je Aufgabe verlangt und der Dialog einen Leerzustand fordert.
- Der neue Dialog ist rein lesend. Er bietet keine Aktionen zum Erstellen, Abhaken, Bearbeiten oder Loeschen von Todos.
- Im Dialog werden Todo-Beschreibung und Erstellungsdatum angezeigt. Weitere Metadaten werden nicht eingefuehrt, weil das vorhandene Todo-Modell fuer offene Todos keine Prioritaet oder Faelligkeit enthaelt.
- Die Aktualisierung im Menue nutzt den bestehenden Refresh-Zyklus. Es wird kein neues Event-System fuer Todo-Aenderungen eingefuehrt.
- Der Dialog wird modal ueber die bestehende WPF-Dialog-Infrastruktur geoeffnet.

## Umsetzungsschritte

### 1. Daten fuer offene Todo-Anzahl bereitstellen

1. `src/Softwareschmiede/Application/Services/TodoService.cs` um eine Bulk-Abfrage fuer offene Todo-Anzahlen erweitern, z. B.:
   - `Task<IReadOnlyDictionary<Guid, int>> GetOpenTodoCountsAsync(IEnumerable<Guid> aufgabeIds, CancellationToken ct = default)`
   - Eingabe-IDs deduplizieren und bei leerer Eingabe ein leeres Dictionary zurueckgeben.
   - Per EF-Abfrage `Todos` nach `AufgabeId` und `ErledigtAm == null` filtern, nach `AufgabeId` gruppieren und zaehlen.
2. `src/Softwareschmiede/Application/Services/AufgabeService.cs` fuer `GetAktiveAufgabenAsync` nicht auf `Include(a => a.Todos)` umstellen. Die bestehende Aufgabe-Abfrage bleibt schlank; die Todo-Anzahl wird im UI-Mapping separat per Bulk-Abfrage geladen.
3. `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs` um folgende Properties erweitern:
   - `int OffeneTodoCount`
   - `string OffeneTodoLabelText`, z. B. `"0 Todos"`, `"1 Todo"`, `"3 Todos"`
   - `ICommand OffeneTodosAnzeigenCommand`

### 2. MainWindowViewModel anbinden

1. `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` um eine `TodoService`-Dependency erweitern.
2. In `AktiveAufgabenAktualisierenAsync` nach `GetAktiveAufgabenAsync` die IDs der geladenen Aufgaben sammeln und mit `TodoService.GetOpenTodoCountsAsync` die offenen Todo-Anzahlen laden.
3. `MapAktiveAufgabePanelItem` so erweitern, dass die jeweilige Anzahl aus dem Dictionary ins `AktiveAufgabePanelItem` uebernommen wird. Nicht vorhandene Dictionary-Eintraege bedeuten `0`.
4. Ein neues Command im Item erzeugen, das auf eine private Methode im `MainWindowViewModel` verweist, z. B. `OffeneTodosDialogOeffnenAsync(Guid aufgabeId)`.
5. Der Command darf keine Navigation zur Aufgabendetailansicht ausloesen. Fehler beim Dialogoeffnen werden analog zu bestehenden Hintergrundfehlern geloggt.

### 3. Read-only Dialog fuer offene Todos erstellen

1. Neues ViewModel unter `src/Softwareschmiede.App/ViewModels/`, z. B. `OpenTodosDialogViewModel`:
   - Konstruktor mit `TodoService` und `ILogger<OpenTodosDialogViewModel>`.
   - Properties fuer `AufgabeId`, `AufgabenTitel`, `ObservableCollection<TodoViewModel>` oder ein kleines read-only Item-ViewModel.
   - `IsEmpty` bzw. `HasOpenTodos` fuer den Leerzustand.
   - `LoadAsync(Guid aufgabeId, string aufgabenTitel, CancellationToken ct)` laedt ausschliesslich `TodoService.GetOpenTodosAsync`.
2. Neues Window unter `src/Softwareschmiede.App/Views/`, z. B. `OpenTodosDialog.xaml` und Code-behind:
   - Titel mit Aufgabenbezug.
   - Liste der offenen Todos, sortiert wie `GetOpenTodosAsync`.
   - Leerzustand: "Keine offenen Todos."
   - Schliessen-Button.
   - Keine Bearbeitungscontrols, keine Checkboxen, keine Delete-/Create-Commands.
3. `src/Softwareschmiede.App/App.xaml.cs` um DI-Registrierung fuer das neue ViewModel und ggf. das Window erweitern.
4. `src/Softwareschmiede.App/Services/IDialogService.cs` um eine Methode fuer den Dialog erweitern, z. B.:
   - `Task ShowOpenTodosDialogAsync(OpenTodosDialogViewModel viewModel, CancellationToken ct = default);`
5. `src/Softwareschmiede.App/Services/WpfDialogService.cs` implementiert die Methode mit `Application.Current.Dispatcher.InvokeAsync`, setzt `Owner = Application.Current.MainWindow` und ruft `ShowDialog()`.

### 4. UI-Label in ActiveTasksListControl ergaenzen

1. `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` im gemeinsamen `AufgabenKachelInhaltTemplate` um ein interaktives Todo-Element erweitern.
2. Als WPF-Element einen kleinen `Button` im bestehenden Layout verwenden, obwohl die Anforderung fachlich von Label spricht. Das erfuellt die Klickbarkeit sauber, verhindert Probleme mit `MouseBinding` in der vollflaechig klickbaren Dashboard-Kachel und bleibt per Automation testbar.
3. Button-Inhalt an `OffeneTodoLabelText` binden.
4. Button-Command an `OffeneTodosAnzeigenCommand` binden.
5. Einen stabilen Automation-Namen setzen, z. B. `OffeneTodos:{Titel}`; HelpText kann die Anzahl enthalten.
6. Styling kompakt halten:
   - transparente oder dezente Oberflaeche passend zur Kachel,
   - `Cursor="Hand"`,
   - kein Layoutsprung bei unterschiedlichen Zahlen,
   - Texttrimming bzw. feste Mindestbreite fuer kompakte Seitenleiste.

### 5. Kollisionsfreie Interaktion sicherstellen

1. Im Dashboard-Template liegt die Navigation aktuell als `MouseBinding` auf dem Border. Der neue Todo-Button muss das Click-Event selbst behandeln, damit sein Command vor der Border-Navigation greift.
2. Falls WPF trotz Button eine Border-Navigation ausloest, das vollflaechige Template auf eine explizite Button- oder Command-Struktur umbauen, bei der der Todo-Button ausgenommen ist.
3. Seitenleisten-Navigationsbutton unveraendert lassen.

### 6. Tests

1. `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs` erweitern:
   - aktive Aufgabenitems enthalten die korrekte offene Todo-Anzahl,
   - erledigte Todos werden nicht gezaehlt,
   - Aufgaben ohne offene Todos erhalten `0`,
   - Ausfuehren des Item-Commands oeffnet den Dialog fuer die richtige Aufgabe.
2. Neuen Unit-Test fuer `OpenTodosDialogViewModel` anlegen:
   - laedt nur offene Todos,
   - setzt Leerzustand bei `0`,
   - uebernimmt Aufgaben-ID und Titel.
3. `src/Softwareschmiede.Tests/Application/Services/TodoServiceTests.cs` um die Bulk-Count-Methode erweitern:
   - mehrere Aufgaben,
   - erledigte Todos ignorieren,
   - unbekannte/leere IDs liefern keine falschen Eintraege.
4. Optionaler E2E-Test in der bestehenden Todo-E2E-Struktur:
   - aktive Aufgabe mit offenen und erledigten Todos vorbereiten,
   - Todo-Label in aktiver Aufgabenliste finden,
   - Dialog oeffnen,
   - offene Todos sichtbar, erledigte Todos nicht sichtbar,
   - `0`-Fall zeigt Leerzustand.

## Akzeptanzkriterien

- Jede aktive oder wartende Aufgabe in der aktiven Aufgabenliste zeigt ein Todo-Label mit der Anzahl offener Todos.
- Die Anzahl basiert auf `ErledigtAm == null`; erledigte Todos werden nicht gezaehlt.
- Das Label ist anklickbar und oeffnet einen modalen read-only Dialog fuer genau diese Aufgabe.
- Der Dialog zeigt ausschliesslich offene Todos der Aufgabe.
- Bei `0` offenen Todos bleibt das Label sichtbar und der Dialog zeigt einen nachvollziehbaren Leerzustand.
- Seitenleiste und Dashboard verwenden dieselbe Anzeige, weil die Erweiterung im gemeinsamen `ActiveTasksListControl` liegt.
- Ein Klick auf das Todo-Label navigiert nicht versehentlich zur Aufgabendetailansicht.
- Die bestehende aktive-Aufgaben-Aktualisierung aktualisiert auch die Todo-Anzahl.

## Risiken und Gegenmassnahmen

- Risiko: Ein Button innerhalb der vollflaechig klickbaren Dashboard-Kachel loest zusaetzlich die Kachel-Navigation aus.
  Gegenmassnahme: Interaktion nach Implementierung gezielt testen und bei Bedarf das Dashboard-Template so umbauen, dass Navigation nicht ueber einen uebergeordneten `MouseBinding`-Handler konkurriert.
- Risiko: N+1-Abfragen fuer Todo-Counts bei bis zu 20 aktiven Aufgaben.
  Gegenmassnahme: Bulk-Count-Methode in `TodoService` nutzen.
- Risiko: Wiederverwendung der bestehenden `TodoListView` wuerde Bearbeitungsfunktionen in den Dialog ziehen.
  Gegenmassnahme: eigenes read-only Dialog-ViewModel und Window verwenden.
- Risiko: Sofortige Todo-Aenderungen aus der Detailansicht erscheinen erst beim naechsten Refresh.
  Gegenmassnahme: Dieses Verhalten ist bewusst akzeptiert und entspricht der bestehenden Aktualisierungsmechanik.

## Validierung

- `dotnet test`
- Bei UI-Aenderungen zusaetzlich die Anwendung starten und manuell pruefen:
  - aktive Aufgabenliste in Seitenleiste,
  - aktive Aufgabenliste im Dashboard,
  - Dialog mit offenen Todos,
  - Dialog-Leerzustand bei `0`,
  - keine unbeabsichtigte Navigation beim Klick auf das Todo-Label.

## Offene Punkte

Keine.
