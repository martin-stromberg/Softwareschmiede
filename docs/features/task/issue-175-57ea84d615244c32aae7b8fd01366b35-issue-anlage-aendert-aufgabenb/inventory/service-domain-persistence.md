# Service, Domain und Persistenz

## Domainmodell

`Aufgabe` enthaelt die Aufgabenbeschreibung in `AnforderungsBeschreibung`. Sie ist nullable und wird in der UI als editierbarer Text und Anzeigeinhalt verwendet.

`IssueReferenz` speichert die lokale Referenz auf das externe Issue mit eigenem `Body`. Damit koennen Aufgabenbeschreibung und Issue-Body aktuell bewusst auseinanderlaufen.

## AufgabeService

Relevante Methoden:

- `CreateAsync(...)` erstellt eine Aufgabe mit `AnforderungsBeschreibung`.
- `CreateFromIssueAsync(...)` setzt `AnforderungsBeschreibung = issue.Body` und legt eine `IssueReferenz` an.
- `CreateFromAlertAsync(...)` setzt `AnforderungsBeschreibung` bevorzugt auf `createdIssue.Body`.
- `UpdateAsync(...)` aktualisiert Titel, Beschreibung und KI-Plugin-Prefix.
- `UpdateIssueReferenzAsync(...)` setzt oder entfernt eine Issue-Referenz und ueberschreibt vorhandene Referenzen.
- `TryAssignIssueReferenzIfNoneAsync(...)` legt eine Issue-Referenz nur dann an, wenn noch keine existiert; parallele Zuordnung wird als `false` gemeldet.

Fuer die neue Anforderung ist `TryAssignIssueReferenzIfNoneAsync` der kritischste Bestandspfad, weil `TaskDetailViewModel.IssueAnlegenAsync` damit verhindert, dass eine parallel gesetzte Issue-Referenz ueberschrieben wird.

## Persistenzoptionen

### Getrennte Aufrufe

Ablauf:

1. `TryAssignIssueReferenzIfNoneAsync(...)`
2. Bei aktivierter Option `UpdateAsync(id, titel, neueBeschreibung, kiPluginPrefix, ct)`
3. `LadenAsync(ct)`

Vorteil: Wenig neue Service-API.

Nachteil: Zwischen Issue-Referenz und Beschreibung gibt es einen zweiten Save. Wenn Schritt 2 fehlschlaegt, ist das Issue lokal zugeordnet, aber die Beschreibung nicht aktualisiert.

### Kombinierte Service-Methode

Moegliche Methode:

`TryAssignIssueReferenzIfNoneAsync(Guid id, Issue issue, bool updateAnforderungsBeschreibung, string? anforderungsBeschreibung, CancellationToken ct = default)`

oder eine neue explizite Methode, z. B. `TryAssignIssueReferenzAndUpdateDescriptionIfNoneAsync(...)`.

Vorteil: IssueReferenz und Aufgabenbeschreibung koennen in einem `SaveChangesAsync` persistiert werden. Der bestehende Race-Schutz gegen parallele Issue-Zuordnung bleibt zentral im Service.

Nachteil: Bestehende Methode erhaelt mehr Verantwortung; Tests muessen die optionale Beschreibung sauber abdecken.

## Empfehlung fuer Planung

Eine kombinierte Service-Operation ist fachlich robuster, weil der neue Ablauf "Issue wurde erstellt, lokale Zuordnung und optionale Aufgabenbeschreibung aktualisieren" in einer lokalen Persistenztransaktion behandelt. Das externe Issue kann trotzdem nicht zurueckgerollt werden; lokale Fehler muessen sichtbar gemeldet werden.

## UI-Reload

Nach lokal erfolgreicher Persistenz reicht der vorhandene `await LadenAsync(ct)` in `TaskDetailViewModel.IssueAnlegenAsync`, weil `GetDetailAsync(...)` die Aufgabe neu aus der Datenbank laedt und die gebundenen Properties aktualisiert.

## Migrationen

Keine Datenbankmigration erforderlich, sofern nur `Aufgabe.AnforderungsBeschreibung` und `IssueReferenz.Body` verwendet werden. Es wird kein neues persistiertes Feld benoetigt, wenn die Checkbox pro Dialoglauf gilt und nicht als Praeferenz gespeichert wird.
