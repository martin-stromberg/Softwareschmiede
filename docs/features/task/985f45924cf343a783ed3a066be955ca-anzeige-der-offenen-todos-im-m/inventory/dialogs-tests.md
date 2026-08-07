# Dialoge und Tests

## Bestehende Dialog-Infrastruktur

`src/Softwareschmiede.App/Services/IDialogService.cs` abstrahiert UI-Dialoge fuer ViewModels. `WpfDialogService` implementiert konkrete WPF-Dialoge und oeffnet eigene Windows ueber `ShowDialog()` mit `Application.Current.MainWindow` als Owner.

Aktuell gibt es keinen Todo-spezifischen Dialog. Fuer die Anforderung ist eine Erweiterung naheliegend, z. B.:

- neues read-only ViewModel fuer offene Todos einer Aufgabe,
- neues WPF-Window oder Dialog-View fuer die Anzeige,
- neue Methode im `IDialogService`, z. B. `ShowOpenTodosDialogAsync(...)` oder synchron passend zum bestehenden Muster,
- Registrierung neuer ViewModels/Views in `App.xaml.cs`, falls Konstruktor-Injection benoetigt wird.

## Bestehende Todo-UI

`TodoListViewModel` und `TodoListView.xaml` bilden die bearbeitbare Todo-Ansicht in der Aufgabendetailansicht ab:

- neue Todos erstellen,
- Todos abhaken,
- Todos loeschen,
- `OffeneTodoCount` berechnen.

Diese UI ist fuer den neuen Dialog nur eingeschraenkt geeignet, weil die Anforderung Bearbeiten, Erledigen, Loeschen und Erstellen ausdruecklich als Nicht-Ziele ausschliesst. Wiederverwendbar sind eher `TodoViewModel` und die Darstellungslogik fuer Beschreibung/Leerzustand, nicht die Commands.

## Unit-Test-Abdeckung

Vorhandene Tests:

- `MainWindowViewModelTests` prueft Befuellen und Refresh der aktiven Aufgabenliste, Navigation, aktive Markierung, Plugin-Namen und Re-Entrancy-Schutz.
- `TodoServiceTests` prueft `GetOpenTodosAsync`, `GetAllTodosAsync` und `GetTodoCountAsync`.
- `TaskDetailViewModelTests_Todos` prueft lokale Todo-Liste, offene Anzahl, Erstellen, Loeschen, Erledigen und Abschlussblockade.

Empfohlene zusaetzliche Tests:

- `MainWindowViewModelTests`: aktive Aufgabenitems enthalten korrekte offene Todo-Anzahl.
- `MainWindowViewModelTests`: Klick-/Command fuer Todo-Dialog bekommt die richtige Aufgaben-ID.
- Neuer Dialog-ViewModel-Test: laedt nur offene Todos, sortiert stabil und zeigt Leerzustand bei `0`.
- Optional `TodoServiceTests`: Bulk-/Projektionsmethode fuer mehrere Aufgaben, falls die Planung eine solche Methode einfuehrt.

## E2E-Abdeckung

`E2E_TodoManagement.cs` prueft bereits die Todo-Detailansicht inklusive Badge `OffeneTodoCountBadge`, Erstellen, Loeschen, Abhaken und Abschlussvalidierung.

Fuer die neue Anforderung ist ein kleines E2E-Szenario sinnvoll:

- Aufgabe mit offenen Todos erzeugen.
- Aufgabe auf aktiv/wartend setzen.
- Aktive Aufgabenliste in Seitenleiste oder Dashboard oeffnen.
- Neues Todo-Count-Label per Automation-Name finden.
- Label klicken.
- Dialog zeigt nur offene Todos und nicht erledigte Todos.
- Aufgabe ohne offene Todos zeigt nachvollziehbaren Leerzustand, falls das Label bei `0` angezeigt wird.

