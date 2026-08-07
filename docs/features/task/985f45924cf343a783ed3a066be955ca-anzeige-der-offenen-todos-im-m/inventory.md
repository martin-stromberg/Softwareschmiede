# Bestandsaufnahme - Anzeige offener Todos im Menue

## Kurzfazit

Die aktive Aufgabenliste wird zentral ueber `ActiveTasksListControl` dargestellt und sowohl in der Seitenleiste als auch im Dashboard verwendet. Die Datenquelle ist `MainWindowViewModel.AktiveAufgabenListe`; sie wird alle 5 Sekunden, bei Laufstatusaenderungen und bei View-Wechseln aus `IAktiveAufgabenService.GetAktiveAufgabenAsync` neu aufgebaut.

Todo-Daten sind bereits persistent vorhanden. `TodoService` bietet sowohl `GetTodoCountAsync` als auch `GetOpenTodosAsync`; offene Todos sind fachlich durch `ErledigtAm == null` definiert. Aktuell werden diese Werte aber nicht in die aktive Aufgabenliste geladen und `AktiveAufgabePanelItem` enthaelt keine Todo-bezogenen Properties oder Commands.

Fuer das geforderte anklickbare Label braucht die Umsetzung voraussichtlich:

- Erweiterung des Panel-Items um Anzahl offener Todos und ein Command/Callback zum Oeffnen der offenen Todos.
- Erweiterung der aktiven Aufgaben-Abfrage oder des Mapping-Schritts um Todo-Zaehler.
- UI-Erweiterung in `ActiveTasksListControl.xaml`, damit Seitenleiste und Dashboard konsistent dieselbe Anzeige erhalten.
- Einen neuen read-only Dialog oder eine Dialogservice-Erweiterung fuer offene Todos einer Aufgabe.
- Unit-Tests fuer Mapping/Refresh/Command und Service-Abfragen, optional E2E fuer Label und Dialog.

## Detaildokumente

- [UI und Navigation](inventory/ui-navigation.md)
- [Datenmodell und Services](inventory/data-services.md)
- [Dialoge und Tests](inventory/dialogs-tests.md)

## Betroffene Kernstellen

| Bereich | Datei | Relevanz |
|---------|-------|----------|
| Aktive Aufgaben UI | `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` | Gemeinsames Template fuer Seitenleiste und Dashboard; hier muss das Label sichtbar werden. |
| Aktive Aufgaben Item | `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs` | Enthalt aktuell Titel, Projekt, Plugin- und Laufstatusdaten, aber keine Todo-Anzahl. |
| Aktive Aufgaben Refresh | `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` | Baut `AktiveAufgabenListe` aus `Aufgabe`-Entities und steuert Navigation. |
| Aktive Aufgaben Service | `src/Softwareschmiede/Application/Services/AufgabeService.cs` | Liefert aktive/wartende Aufgaben; laedt aktuell Projekt und Repository, aber keine Todo-Zaehler. |
| Todo-Abfragen | `src/Softwareschmiede/Application/Services/TodoService.cs` | Vorhandene Abfragen fuer offene Todos und Count koennen wiederverwendet werden. |
| Todo-Domain | `src/Softwareschmiede/Domain/Entities/Todo.cs` | `ErledigtAm == null` bzw. `IstOffen` definiert offene Todos. |
| Dialogservice | `src/Softwareschmiede.App/Services/IDialogService.cs` und `WpfDialogService.cs` | Bisher gibt es keinen Todo-Dialog; bestehendes Muster fuer eigene Dialoge ist vorhanden. |
| Todo-Detail-UI | `src/Softwareschmiede.App/Views/TodoListView.xaml` | Bearbeitbare Todo-Liste in der Aufgabendetailansicht; fuer den neuen Dialog vermutlich nicht direkt passend, aber als UI-Referenz nutzbar. |

## Offene Entscheidungen fuer die Planung

- Das Requirement fragt, ob das Label bei `0` offenen Todos sichtbar und anklickbar sein soll. Bestehende Todo-Badge-Logik in der Detailansicht blendet den Badge bei `0` aus; fuer die neue Anforderung ist das aber nicht eindeutig entschieden.
- Das Fenster soll laut Nicht-Zielen read-only bleiben. Bestehende `TodoListView` ist bearbeitbar und sollte nicht unveraendert als Dialog wiederverwendet werden.
- Live-Aktualisierung im Menue kann zunaechst ueber den bestehenden 5-Sekunden-Refresh erfolgen. Sofortige Aktualisierung nach Todo-Aenderungen aus der Detailansicht ist aktuell nur lokal in `TodoListViewModel` vorhanden und nicht mit der aktiven Aufgabenliste gekoppelt.

