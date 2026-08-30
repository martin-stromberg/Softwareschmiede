# Offene Aufgaben

Erstellt am: 2026-08-30
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine. `review.md` hat Status "Vollständig umgesetzt" (die im Plan geforderten E2E-Tests wurden im Plan-Review als technisch nicht praktikabel begründet und als akzeptierte Abweichung gewertet — GitHubPlugin wird im E2E-Test-Modus des PluginManager nicht geladen, ICliRunner ist dort nicht fakebar).

## Code-Review-Befunde

- [ ] Testmethode in `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` heißt weiterhin nach der privaten Methode `NormalizeRemoteUrlAsync`, obwohl sie nur `PullAsync` aufruft — weicht von der sonst durchgängigen Namenskonvention `<ÖffentlicheMethode>_Should...` in der Testdatei ab. Umbenennen zu `PullAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()`.

## Fehlgeschlagene Tests

Keine. Die stabile Test-Lane (`Category!=OsInterface`) lief mit 1479 von 1480 bestandenen Tests (1 plattformbedingt übersprungen) fehlerfrei durch.
