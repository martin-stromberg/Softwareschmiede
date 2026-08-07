# Offene Aufgaben

Erstellt am: 2026-08-07
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Fortsetzung am: 2026-08-07
Ergebnis der Fortsetzung: Die beiden offenen Tests wurden gezielt erneut ausgefuehrt. `RunGeneralTests` scheitert aktuell bereits im bestehenden `DefaultKiPlugin`-Combobox-Ablauf; vorherige Laeufe zeigten fuer denselben Sammler UIAutomation/RPC_E_SERVERFAULT bzw. Timeout beim Hilfe-Dialog. `RunConPtyTests` scheitert weiterhin im bestehenden AutoStart-CLI-Szenario beim Warten auf `CliStoppen`. Beide Punkte liegen ausserhalb der Todo-Menue-Aenderung; es wurde deshalb kein unrelated Refactor vorgenommen.

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

- [ ] `Softwareschmiede.Tests.E2E.End2EndTest.RunConPtyTests` - `System.TimeoutException: Element wurde nicht innerhalb von 15s gefunden.`
- [ ] `Softwareschmiede.Tests.E2E.End2EndTest.RunGeneralTests` - `System.Runtime.InteropServices.COMException: Ausnahmefehler des Servers. (0x80010105 (RPC_E_SERVERFAULT))`
