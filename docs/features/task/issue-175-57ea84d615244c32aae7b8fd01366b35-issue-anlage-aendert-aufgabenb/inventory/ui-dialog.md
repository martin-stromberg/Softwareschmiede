# UI- und Dialogfluss

## IssueCreateDialogViewModel

`IssueCreateDialogViewModel` verwaltet den Dialog zur Issue-Anlage. Relevante Bestandteile:

- `Title` und `Body` sind editierbare Dialogfelder.
- `Initialize(...)` setzt `Title` auf den Aufgabentitel und `Body` auf die bestehende Aufgabenbeschreibung oder leer.
- `SelectedTemplate` ersetzt den Body per `ComposeTemplateBody(...)`.
- `ErstellenAsync(...)` ruft `IIssueCreateProvider.CreateIssueAsync(...)` mit `new IssueCreateRequest(Title!.Trim(), Body ?? string.Empty)` auf.
- Bei Erfolg wird `CreatedIssue = result.Issue` gesetzt und `CloseRequested(..., true)` ausgeloest.
- Bei Provider-Fehlern bleibt der Dialog offen und `CreatedIssue` bleibt `null`.

Es gibt aktuell keinen State fuer "Aufgabenbeschreibung nach Issue-Anlage aktualisieren". Der passende Ort ist eine neue boolsche Property im Dialog-ViewModel, z. B. `UpdateTaskDescriptionAfterCreate`, initial `false`.

## IssueCreateDialog.xaml

Der Dialog hat diese Struktur:

- Header und Fehleranzeige.
- Titel-Eingabe.
- Template-Auswahl und KI-Ausfuellhilfe.
- Mehrzeilige Beschreibung (`IssueBeschreibung`).
- Statusmeldungen und Buttons `Abbrechen`/`Anlegen`.

Die Checkbox kann fachlich nahe an der Beschreibung oder im unteren Aktionsbereich stehen. Ein sinnvoller Automation-Name waere `AufgabenbeschreibungNachIssueAnlageAktualisieren`.

## WpfDialogService und IDialogService

`IDialogService.ShowIssueCreateDialogAsync(...)` gibt aktuell `Task<Issue?>` zurueck. `WpfDialogService` erzeugt `IssueCreateDialog`, zeigt ihn modal und gibt bei Erfolg `viewModel.CreatedIssue` zurueck.

Optionen fuer die Umsetzung:

- Minimalinvasiv: `TaskDetailViewModel` liest nach Rueckgabe von `ShowIssueCreateDialogAsync(dialogVm, ct)` den boolschen State direkt aus `dialogVm`.
- Expliziter: Neue Value-Object-Rueckgabe, z. B. `IssueCreateDialogResult(Issue Issue, bool UpdateTaskDescription)`, und Anpassung von `IDialogService`, `WpfDialogService` sowie Tests.

Die explizite Rueckgabe ist sauberer fuer Tests und vermeidet implizite Kopplung an den ViewModel-State nach Dialogschluss.

## TaskDetailViewModel UI-Aktualisierung

`TaskDetailViewModel` setzt beim Laden:

- `EditAnforderungsBeschreibung` fuer das Edit-Panel.
- `Aufgabe` fuer die Anzeige `Aufgabe.AnforderungsBeschreibung`.

Nach erfolgreicher Issue-Anlage ruft `IssueAnlegenAsync` bereits `LadenAsync(ct)` auf. Wenn die Beschreibung im gleichen Ablauf vor diesem Reload gespeichert wird, erscheint sie in der Detailansicht ohne zusaetzlichen UI-Mechanismus.

## Betroffene UI-Tests

Vorhandene Tests in `IssueCreateDialogViewModelTests` pruefen Initialisierung, Provider-Aufruf, Fehlerpfade, Cancellation und doppelte Ausfuehrung. Neue Tests sollten pruefen:

- Default der neuen Option ist `false`.
- Property ist setzbar und bleibt ueber Submit erhalten.
- Provider-Fehler schliesst den Dialog nicht und loest keine Beschreibungsaenderung im uebergeordneten Flow aus.

Vorhandene Tests in `TaskDetailViewModelTests` pruefen die Issue-Anlage ab Zeile 1630, inklusive erfolgreicher Persistenz, Abbruch, lokaler Fehler und paralleler Issue-Zuordnung. Diese Tests sind der richtige Ort fuer den End-to-End-ViewModel-Flow.
