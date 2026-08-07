# Datenmodell und Services

## Domainmodell

`src/Softwareschmiede/Domain/Entities/Aufgabe.cs` hat eine Navigation `List<Todo> Todos`.

`src/Softwareschmiede/Domain/Entities/Todo.cs` definiert:

- `AufgabeId`
- `Beschreibung`
- `ErledigtAm`
- `IstOffen => ErledigtAm is null`
- `ErstellungsDatum`

Im EF-Modell ist `Aufgabe` zu `Todo` als Cascade-Beziehung konfiguriert. `ErstellungsDatum` und `ErledigtAm` werden fuer SQLite als Unix-Millisekunden gespeichert.

## TodoService

`src/Softwareschmiede/Application/Services/TodoService.cs` bietet bereits die fuer diese Anforderung wichtigen Abfragen:

- `GetOpenTodosAsync(Guid aufgabeId, CancellationToken ct)` gibt offene Todos einer Aufgabe sortiert nach `ErstellungsDatum` zurueck.
- `GetAllTodosAsync(Guid aufgabeId, CancellationToken ct)` wird fuer die bestehende Detailansicht genutzt.
- `GetTodoCountAsync(Guid aufgabeId, CancellationToken ct)` zaehlt offene Todos.

Wichtig: Die EF-Abfragen nutzen explizit `ErledigtAm == null`, weil `Todo.IstOffen` nicht nach SQL uebersetzbar ist.

## Aktive Aufgaben Service

`AufgabeService.GetAktiveAufgabenAsync` liefert aktive oder wartende Aufgaben:

- Filter: `AufgabeStatusExtensions.AktivOderWartendStatus.Contains(a.Status)`
- Includes: `Projekt`, `GitRepository`
- Sortierung: letzter CLI-Start bzw. Erstellungsdatum, dann Titel und ID
- Limit: `Take(20)`

Aktuell werden keine `Todos` included und keine Todo-Zaehler projiziert. Fuer die Umsetzung gibt es zwei realistische Wege:

- `GetAktiveAufgabenAsync` um `Include(a => a.Todos)` erweitern und im UI-Mapping zaehlen.
- Eine eigene Projektion bzw. DTO fuer aktive Aufgaben einfuehren, die den offenen Todo-Count direkt aus SQL mitliefert.

Die Projektion ist performanter und vermeidet unnoetiges Laden aller Todos, waere aber eine staerkere Vertragsaenderung an `IAktiveAufgabenService`.

## Aktualisierungskonsistenz

`MainWindowViewModel` aktualisiert die aktive Aufgabenliste:

- beim Konstruktorstart ueber Dashboard-Navigation,
- bei `CurrentView`-Wechseln,
- alle 5 Sekunden per `DispatcherTimer`,
- bei `RunningCountChanged`.

Todo-Aenderungen in `TodoListViewModel` aktualisieren bisher nur dessen lokale `OffeneTodoCount`. Sie loesen keinen expliziten Refresh der aktiven Aufgabenliste aus. Die neue Anzeige waere ohne weitere Kopplung spaetestens nach dem Timer konsistent.

