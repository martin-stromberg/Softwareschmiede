# Offene Aufgaben

Erstellt am: 2026-11-07
Abbruchgrund: Maximale Iterationsanzahl erreicht (3 Iterationen; Anwender-Entscheidung: Stand dokumentieren statt 4. Fix-Runde)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md`: Vollständig umgesetzt.

## Code-Review-Befunde

Alle drei Befunde betreffen Restfenster derselben Race-Condition-Klasse in
`src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` /
`ZeitgesteuerterPromptStore.cs` (Lost-Update zwischen In-Memory-Plan und DB-Datensatz bei
parallelem Planen/Versand/Storno). Empfehlung des Reviewers: den gesamten Plan-Lebenszyklus
pro Aufgabe serialisieren (keyed Async-Lock, z. B. `SemaphoreSlim` pro `AufgabeId`), statt
weiterer punktueller Guards/CAS-Vergleiche.

- [ ] `UpsertWennUebereinstimmendAsync` (`ZeitgesteuerterPromptStore.cs` ~Z. 78–103) blockiert legitime Writes: Bei `erwartet == null` + vorhandenem Datensatz wird verweigert — `ersetzterEintrag == null` tritt aber auch bei Datensätzen eines in-flight-Versands oder eines in-flight-`CancelScheduledPromptAsync` auf. Folge: fehlgeschlagener Sofortversand bleibt unpersistiert bzw. veralteter Plan wird nach Neustart zugestellt. Abgedriftete Datensätze (z. B. nach fehlgeschlagenem `UpdateZielZeitpunktAsync`) lassen den CAS dauerhaft verweigern.
- [ ] Stale `neuererPlanAktiv`-Flag + unbedingte Löschpfade (`LoescheDatensatzWennAktuellAsync`, ~Z. 525–537): Flag wird vor dem Store-Aufruf gelesen; ein zwischen Lesezugriff und Delete persistierter neuer Plan wird im Mismatch-Zweig mitgelöscht. Symmetrisch kann die „kein Eintrag ⇒ Leiche"-Heuristik den Datensatz eines noch in-flight-Versands löschen, der danach fehlschlägt. `CancelScheduledPromptAsync` → `LoeschenAsync` löscht analog unbedingt. Wurzel: In-Memory-Zustand und Store-Ops sind pro Aufgabe nicht serialisiert.
- [ ] `Dispose` nicht vollständig inert (minor): In-flight-Callbacks, die ihren Eintrag vor `Dispose` entfernten, versenden danach weiter inkl. Events/Protokoll; öffentliche Methoden re-populieren eine disposed Instanz kommentarlos. Empfehlung: `_disposed`-Flag unter `_lock`, geprüft in Einstiegspunkten und vor `SendPromptAsync`.

## Usability-Befunde

Keine — `review-usability.md`: Keine Befunde.

## Fehlgeschlagene Tests

Keine — `test-results.md`: Keine Fehler (0 Fehlschläge in 2450 ausgeführten Tests;
Pflicht-E2E und reale Abnahme durch Anwender dokumentiert verzichtet, 2026-11-07).
