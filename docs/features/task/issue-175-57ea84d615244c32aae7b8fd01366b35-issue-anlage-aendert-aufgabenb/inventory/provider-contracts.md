# Provider-Vertraege und Rueckgabedaten

## IIssueCreateProvider

`IIssueCreateProvider` stellt bereit:

- `CanCreateIssueAsync(string repositoryId, CancellationToken ct)`
- `CreateIssueAsync(string repositoryId, IssueCreateRequest request, CancellationToken ct)`

`IssueCreateDialogViewModel` verwendet diese Schnittstelle direkt. Die neue Checkbox muss den Provider-Vertrag nicht erweitern, weil sie nur lokale Anwendungspersistenz betrifft.

## IssueCreateRequest

`IssueCreateRequest` enthaelt:

- `Title`
- `Body`

Der Request enthaelt den lokal im Dialog eingegebenen Issue-Text. Dieser Text ist verfuegbar, bevor der Provider antwortet.

## IssueCreateResult und Issue

`IssueCreateResult` enthaelt bei Erfolg ein `Issue`. `Issue` enthaelt:

- `Nummer`
- `Titel`
- `Body`
- `Labels`
- `Milestone`
- `IssueUrl`

Der erfolgreich angelegte Provider-Stand wird im Dialog als `CreatedIssue` abgelegt. Bestehende Tests verwenden den Provider-Return als Quelle fuer die lokale `IssueReferenz`.

## Quelle fuer die Aufgabenbeschreibung

Die Anforderung laesst offen, ob die Aufgabenbeschreibung mit dem lokal gesendeten Body oder mit dem vom Provider zurueckgegebenen Body aktualisiert werden soll. Aus dem Bestand ergeben sich diese Hinweise:

- `CreateFromIssueAsync` und `CreateFromAlertAsync` verwenden den `Issue.Body` aus Provider-/Result-Objekten als Aufgabenbeschreibung.
- `IssueReferenz.Body` wird ebenfalls aus `Issue.Body` befuellt.
- `IssueCreateDialogViewModel` hat den lokal gesendeten Body noch als `Body`, aber `TaskDetailViewModel` bekommt ueber `IDialogService` aktuell nur `Issue?`.

Bestandsnahe Entscheidung: Primaer `createdIssue.Body` verwenden. Falls `createdIssue.Body` leer oder `null` ist, sollte die Planung entscheiden, ob leer uebernommen wird oder ob der lokal gesendete Dialog-Body als Fallback benoetigt wird.

## Provider-spezifische Normalisierung

Der Vertrag erlaubt, dass Provider einen anderen `Issue.Body` zurueckgeben als der Request enthielt. Die Bestandsaufnahme hat keine separate Normalisierungsschicht gefunden, die nach der Rueckgabe angewendet wird. Wenn Provider normalisieren, ist `Issue.Body` die einzige bestaetigte Quelle im bestehenden Contract.
