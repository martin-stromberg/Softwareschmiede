# Plan-Review - Anzeige offener Todos im Menue

Status: Vollständig umgesetzt

Hinweis zur Ausfuehrung: In dieser Umgebung war kein delegierbarer Unteragent fuer `/review-plan` verfuegbar. Der Review wurde lokal anhand von `plan.md`, `inventory.md`, den Detaildokumenten und dem aktuellen Implementierungsstand erstellt.

## Gepruefte Planpunkte

| Planpunkt | Bewertung | Nachweis |
|-----------|-----------|----------|
| Bulk-Abfrage fuer offene Todo-Anzahlen in `TodoService` | Erfuellt | `GetOpenTodoCountsAsync` dedupliziert IDs, liefert bei leerer Eingabe ein leeres Dictionary und zaehlt per EF-Abfrage nur `ErledigtAm == null`. |
| Aktive-Aufgaben-Abfrage bleibt schlank | Erfuellt | `GetAktiveAufgabenAsync` wurde nicht auf Todo-Includes umgestellt; die Count-Abfrage erfolgt separat im `MainWindowViewModel`. |
| `AktiveAufgabePanelItem` enthaelt Todo-Anzahl, Labeltext und Command | Erfuellt | `OffeneTodoCount`, `OffeneTodoLabelText` und `OffeneTodosAnzeigenCommand` sind vorhanden. |
| `MainWindowViewModel` laedt und mappt Todo-Anzahlen | Erfuellt | `AktiveAufgabenAktualisierenAsync` ruft die Bulk-Abfrage fuer die geladenen Aufgaben-IDs auf; fehlende Dictionary-Eintraege werden als `0` gemappt. |
| Todo-Command oeffnet den Dialog ohne Detailnavigation | Erfuellt | Das Item-Command laedt `OpenTodosDialogViewModel` fuer die konkrete Aufgabe und ruft `ShowOpenTodosDialogAsync` auf; der Todo-Button behandelt sein Click-Event, um Kachel-Navigation zu unterbinden. |
| Read-only Dialog fuer offene Todos | Erfuellt | `OpenTodosDialogViewModel` laedt ausschliesslich `GetOpenTodosAsync`; `OpenTodosDialog.xaml` zeigt Liste, Erstellungsdatum, Leerzustand und Schliessen-Button, aber keine Bearbeitungscontrols. |
| Dialogservice und DI | Erfuellt | `IDialogService` und `WpfDialogService` enthalten `ShowOpenTodosDialogAsync`; `OpenTodosDialogViewModel` ist in `App.xaml.cs` registriert. |
| Gemeinsame UI-Anzeige in Seitenleiste und Dashboard | Erfuellt | Das Todo-Element liegt im gemeinsamen `AufgabenKachelInhaltTemplate` von `ActiveTasksListControl`. |
| Tests fuer Service, Mapping/Command und Dialog-ViewModel | Erfuellt | Tests fuer Bulk-Counts, aktive Aufgaben mit Todo-Anzahl, Dialog-Command und `OpenTodosDialogViewModel` sind vorhanden. |

## Akzeptanzkriterien

| Kriterium | Status |
|-----------|--------|
| Jede aktive oder wartende Aufgabe zeigt ein Todo-Label mit Anzahl offener Todos. | Erfuellt |
| Die Anzahl basiert auf `ErledigtAm == null`; erledigte Todos werden nicht gezaehlt. | Erfuellt |
| Das Label ist anklickbar und oeffnet einen modalen read-only Dialog fuer genau diese Aufgabe. | Erfuellt |
| Der Dialog zeigt ausschliesslich offene Todos der Aufgabe. | Erfuellt |
| Bei `0` offenen Todos bleibt das Label sichtbar und der Dialog zeigt einen Leerzustand. | Erfuellt |
| Seitenleiste und Dashboard verwenden dieselbe Anzeige. | Erfuellt |
| Ein Klick auf das Todo-Label navigiert nicht versehentlich zur Aufgabendetailansicht. | Erfuellt nach Codepruefung; UI-Verifikation bleibt Teil der manuellen Validierung. |
| Die bestehende aktive-Aufgaben-Aktualisierung aktualisiert auch die Todo-Anzahl. | Erfuellt |

## Offene Aufgaben

Keine.

## Hinweise

- Der Implementierungsagent meldete erfolgreiche Builds und betroffene Tests; der vollstaendige `dotnet test Softwareschmiede.slnx` war dort wegen Timeout bzw. nicht auswertbarer Ausgabe nicht abschliessend gruen.
- Eine manuelle UI-Pruefung fuer Seitenleiste, Dashboard, Dialog und Nicht-Navigation beim Todo-Button ist weiterhin sinnvoll, weil der Plan diese explizit als UI-Validierung vorsieht.
