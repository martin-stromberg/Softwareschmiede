# Offene Aufgaben

Erstellt am: 2026-06-16
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` ist vollständig umgesetzt.

## Code-Review-Befunde

- [ ] `ProjectListViewModel.cs`: `_currentProjectDetailViewModel` wird beim Navigieren zur separaten Aufgabenansicht absichtlich von der automatischen Dispose-Logik des `DetailViewModel`-Setters ausgenommen, damit es bei `KehreZuProjectZurueck()` wiederverwendet werden kann. Wird die Detailansicht jedoch direkt aus der Aufgabenansicht geschlossen (z. B. über `SchliesseDetailCommand`), wird das zurückgehaltene `ProjectDetailViewModel` (inklusive seines `CancellationTokenSource`) nie disposed und nie auf `null` gesetzt — ein Ressourcen-Leak, der durch den neuen Navigationsfluss dieses Features aktiv auslösbar ist.

## Fehlgeschlagene Tests

- [ ] `FaviconHammerPickSvgTests.FaviconHammerPickSvg_ShouldExistInWwwroot` — Datei `favicon-hammer-pick.svg` fehlt im Verzeichnis `src/Softwareschmiede/wwwroot/`. Vorbestehend, nachweislich unabhängig von diesem Feature (verifiziert per `git stash`); keine Beziehung zur Aufgabendetailansicht-Navigation.
- [ ] `FaviconHammerPickSvgTests.FaviconHammerPickSvg_ShouldContainRequiredMarkers` — Abhängig vom obigen Test, gleiche Ursache.
