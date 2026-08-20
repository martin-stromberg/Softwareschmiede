# Offene Aufgaben

Erstellt am: 2026-08-19
Aktualisiert am: 2026-08-19 (Multi-Plugin-Aggregations-Zyklus)

## Status

Der zuvor hier dokumentierte geringfügige Befund (doppelter `Mock<IIdePlugin>`-Aufbau in
`TaskDetailViewModelTests_IdeAuswahl.cs`) wurde im Zuge der Multi-Plugin-Aggregations-Erweiterung
(Iteration 2) behoben — `CreateTestIdePluginMock`-Hilfsmethode wurde ergänzt und in beiden betroffenen
Tests genutzt. Verifiziert per `git diff` und Testlauf (32/32 grün).

## Bewusst nicht umgesetzter Code-Review-Befund (Orchestrator-Entscheidung, dokumentiert)

- [ ] **`TaskDetailViewModel.OeffneIdeInternAsync` — Haupt-Button-Zweig ermittelt Plugin/Einstiegspunkte technisch redundant doppelt.** Code-Review (Iteration 2 der Multi-Plugin-Aggregation) merkte an, dass `ErmittleIdeEntryPointsAsync` (Single-Plugin) und `ErmittleAggregierteIdeEinstiegspunkteAsync` (aggregiert) im Haupt-Button-Zweig für denselben Klick beide vollständig durchlaufen werden, obwohl `aggregierteEintraege[0]` nachweislich stets dem Ergebnis von `ErmittleIdeEntryPointsAsync` entspricht — mit der Empfehlung, `ErmittleIdeEntryPointsAsync` zu entfernen und stattdessen direkt `aggregierteEintraege[0]` zu verwenden.

  **Nicht umgesetzt, mit Begründung:** Diese Vereinfachung würde eine unspezifizierte Verhaltensänderung einführen. `ErmittleAggregierteIdeEinstiegspunkteAsync` schluckt seit Iteration 2 (Behebung eines anderen Befunds) Fehler einzelner Plugins pro Plugin (`try/catch` + `LogWarning`, Fortsetzung mit dem nächsten Plugin) — das ist für den Dropdown-Zweig sinnvoll (ein fehlerhaftes Plugin soll nicht die Anzeige aller anderen verhindern). `ErmittleIdeEntryPointsAsync` reicht Fehler dagegen ungefangen an den äußeren `catch`-Block durch, der eine spezifische `FehlerMeldung` anzeigt. Bei Übernahme des Befunds würde ein Fehlschlag des primären/priorisierten Plugins (z. B. Visual Studio, dessen `.sln`-Suche z. B. wegen eines Dateisystemfehlers wirft) nicht mehr als Fehler angezeigt werden — der Haupt-Button würde stattdessen **still auf das nächste kompatible Plugin ausweichen** (z. B. Visual Studio Code), da `aggregierteEintraege[0]` dann bereits das nächste erfolgreiche Plugin wäre. Das widerspricht dem in `plan.md`/`requirement.md` festgelegten Haupt-Button-Vertrag „öffnet immer direkt den ersten priorisierten Einstiegspunkt; bei 0 Einstiegspunkten wird ein Fehler angezeigt" und wäre eine unreviewte Verhaltensänderung.

  Die doppelte Ermittlung selbst ist zudem in `plan.md` unter „Seiteneffekte und Risiken" → „Doppelte Ermittlung beim Haupt-Button-Klick" bereits explizit als bewusst akzeptierter Trade-off dokumentiert (Performance-Kosten zugunsten von Einfachheit und unverändertem Haupt-Button-Verhalten).

  **Empfehlung für einen künftigen, eigenständigen Durchlauf:** Falls die Redundanz behoben werden soll, müsste `ErmittleAggregierteIdeEinstiegspunkteAsync` um eine Variante erweitert werden, die für das erste/primäre Plugin Fehler NICHT schluckt (z. B. optionaler Parameter `bool propagateFirstPluginErrors` oder eine getrennte Methode), damit der Haupt-Button-Vertrag erhalten bleibt. Dies ist eine eigene Design-Entscheidung, die nicht ohne Plan-Revision automatisiert umgesetzt werden sollte.

## Fehlgeschlagene Tests

Keine im letzten automatisierten Testlauf (`test-results.md`: 1328 gesamt, 1326 bestanden, 0 fehlgeschlagen, 2 übersprungen — inkl. eines zuvor beobachteten, isoliert bereits mehrfach als Sandbox-Timing-Flakiness verifizierten ConPTY-Tests, der in diesem Lauf gar nicht erst auffiel).
