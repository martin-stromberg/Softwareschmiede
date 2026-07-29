# Offene Aufgaben

Erstellt am: 2026-07-29
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] ViewModel-/View-Tests aus dem Plan ergaenzen: PR-Tab verfuegbar, PRs und Workflow-Runs werden geladen und angezeigt, Leer-/Fehlerzustaende und Aktualisierung nach PR-Erstellung.
- [ ] Persistenztests fuer mehrere PRs pro Aufgabe und Cascade Delete von Aufgabe zu PRs beziehungsweise PRs zu Workflow-Runs ergaenzen.
- [ ] GitHubPlugin-Testabdeckung fuer Sanitizing der neuen PR-Fehlerpfade und alle Merge-/Bypass-Fehlerpfade vervollstaendigen.
- [ ] Monitoring-Testabdeckung fuer Pre-Merge-Fehlschlag, Auto-Abschluss bei deaktivierter Einstellung, Blockierung bei Berechtigungs-/Bypass-Fehlern und Post-Merge-Fehlerphase vervollstaendigen.
- [ ] PR-spezifischen Ladezustand und Abruffehlerzustand fuer das Laden der PR-Liste in der UI ergaenzen.
- [ ] Post-Merge-Fallback ueber Zielbranch-Runs mit zeitlicher und SHA-Zuordnung umsetzen oder fachlich als bewusst nicht umgesetzt dokumentieren.
- [ ] Vollstaendigen `dotnet test`-Lauf erfolgreich nachweisen oder die bekannten Timeout-Ursachen separat dokumentieren.

## Code-Review-Befunde

- [ ] Idempotenzproblem bei nicht gemergten Erfolgsresultaten beheben: Nach `ApprovalOnly` oder `AutoMerge` mit offenem PR darf das Monitoring bei unveraendert erfolgreichem Pre-Merge-Zustand nicht in jedem Poll erneut `CompletePullRequestAsync` ausfuehren und neue Protokolleintraege schreiben.
- [ ] Zwei-Durchlauf-Monitoring-Test fuer `ApprovalOnly` ergaenzen, der sicherstellt, dass nach persistierter Phase `Approved` kein erneuter Abschlussversuch erfolgt, solange der PR offen und bereits genehmigt ist.
- [ ] Zwei-Durchlauf-Monitoring-Test fuer `AutoMerge` ergaenzen, der sicherstellt, dass nach `WaitingForMerge` nur weiter gepollt und nicht erneut `gh pr merge --auto` ausgefuehrt wird.

## Fehlgeschlagene Tests

Keine.
