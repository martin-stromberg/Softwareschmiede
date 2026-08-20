# Offene Aufgaben

Erstellt am: 2026-08-20
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine (Plan-Review: Vollständig umgesetzt).

## Code-Review-Befunde

Keine (Code-Review: Keine Befunde).

## Fehlgeschlagene Tests

- [ ] `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` (Szenario `RepositoryZuweisen_MitFehlgeschlagenemStrukturabruf_ZeigtTextBoxUndSpeichertManuellenPfad_E2E`, `E2E_WorkingDirectory.cs`) — `System.InvalidOperationException` beim Auswerten eines LINQ-Query-Parameterausdrucks, innere `System.NullReferenceException` (`ExpressionTreeFuncletizer`). **Hinweis:** In zwei unabhängigen Iterationen als vorbestehendes, von dieser Anforderung unabhängiges Problem verifiziert (`repo.Id`-Zugriff ohne Null-Guard in `WaitForSavedWorkingDirectoryAsync`, Zeilen 358-359, Code-Alter laut `git blame`: 2026-07-13/2026-07-19, deutlich vor diesem Branch). Der einzige in dieser Anforderung geänderte/neue Testpfad (`RunConPtyTests`) lief in beiden Läufen dieser Sandbox gar nicht (ConPTY-Tests übersprungen) und kann den Fehler somit nicht verursacht haben. Details siehe `test-results.md`, Abschnitt "Nachverfolgung (Iteration 2)".
