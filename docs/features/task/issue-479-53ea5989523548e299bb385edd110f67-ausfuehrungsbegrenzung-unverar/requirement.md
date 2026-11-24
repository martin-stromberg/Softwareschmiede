# Übersetzte Anforderung: Ausführungsbegrenzung — unverarbeitbare hängende Ereignisse halten Startplatz dauerhaft belegt

Quelle: GitHub-Issue #479 (martin-stromberg/Softwareschmiede)

## Fachliche Zusammenfassung

Die Ausführungsbegrenzung (`ScmEventExecutionLimitService`) soll einen Startplatz einer am verifizierten Eingabeprompt wartenden bzw. pausierten Aufgabe nur dann weiterhin als belegt zählen, wenn ein offenes `ScmIncomingEvent` dieser Aufgabe (a) tatsächlich zustellbar ist und (b) seine Verarbeitung einen neuen Startplatz erfordert. Aktuell zählt jedes nicht-terminale Ereignis (alle Stati außer `Verarbeitet`/`Abgebrochen`) als platzbelegend — inklusive der ausschließlich manuell klärbaren Endstati `VerarbeitungUngeklärt` und `ZustellungUngewiss` sowie `Wartend`-Ereignisse, deren Zustellung an die eigene lebende Sitzung gar keinen neuen Startplatz benötigt. Folge: dauerhaft belegte Plätze und eine dauerhaft gestaute Ereignisqueue.

Zusätzlich sollen strukturell unzustellbare Ereignisse (z. B. auf beendete/archivierte Aufgaben oder mit fehlender Repository-Zuordnung) einen definierten Endzustand erhalten statt ewig `Wartend` zu bleiben.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Belegungsbewertung eines CLI-Startplatzes (`ScmEventExecutionLimitService.GetOccupancyAsync`) je Aufgabe, sowie der Endzustand strukturell unzustellbarer `ScmIncomingEvent`-Einträge.
- **Auslösende Ereignisse:** Jede Occupancy-Bewertung im Admission-Gate (`CheckAndReserveStartSlotAsync` im `ScmEventOrchestrator`) sowie die Zustellungspfad-Entscheidungen des Orchestrators; daneben die Statusverwaltung von `ScmIncomingEvent`-Einträgen.
- **Erwartete Wirkung:** Eine am verifizierten Prompt wartende Aufgabe mit nur manuell zu klärendem oder nicht platzbedürftigem Ereignis gibt ihren Startplatz frei; die Ereignisqueue fließt wieder. Während tatsächlich laufender Zustellung/Verarbeitung (In-Flight) bleibt der Platz weiterhin belegt — kein Platz wird doppelt vergeben. Strukturell unzustellbare Ereignisse erhalten einen klaren, abschließbaren Endzustand.
- **Umgang mit fehlenden Objekten:** Ereignisse, deren Aufgabe beendet/archiviert ist oder denen die Repository-Zuordnung fehlt, werden verworfen (definierter Endzustand statt ewig `Wartend`).
- **Scope-Ausschlüsse:** Keine Änderung am globalen Startplatz-Limit-Mechanismus selbst (begrenzt werden weiterhin nur Starts, nie laufende Prozesse); keine Änderung an der Ereignis-Serialisierung (`NonBlocking`-Vorgängerprüfung) — diese ist bereits korrekt.

## Betroffene Klassen und Komponenten

- `ScmEventExecutionLimitService` — Methode `GetOccupancyAsync` (Pending-Abfrage auf `ScmIncomingEvents`, ~Z. 218–224)
- `ScmIncomingEventStatus` — Konstanten/Statusmengen, insb. `NonBlocking`; die Pending-Abfrage nutzt aktuell String-Literale `"Verarbeitet"`/`"Abgebrochen"` statt Konstanten
- `ScmEventOrchestrator` — `CheckAndReserveStartSlotAsync`-Aufrufstellen (~Z. 233/261 und ~Z. 725/750), Vorgängerprüfung (~Z. 194), ggf. Endzustand-Setzung für unzustellbare Ereignisse
- `ScmTaskCompletionMonitoringService` — nutzt `NonBlocking` bereits für Abschlussprüfung (Referenz, vermutlich unverändert)
- `WorkItemService` — Kontext: Aufgabe-Abschluss leert Branch-/Klonpfad-Felder
- `ScmIncomingEvent`-Datenmodell — `Status`, `NextAttemptUtc`, `BlockReason`
- Tests: `ScmEventExecutionLimitServiceTests`, `ScmEventDispatchTests_Ordering`

## Implementierungsansatz

- Die Pending-Abfrage in `GetOccupancyAsync` wird von „Status ≠ Verarbeitet/Abgebrochen" auf eine engere Menge „zustellbar UND platzbedürftig" umgestellt (Kandidaten laut Issue: die In-Flight-Stati `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung`; strittig: `Aufgenommen`, `Verzögert`, `Wartend`).
- String-Literale durch Konstanten aus `ScmIncomingEventStatus` ersetzen.
- Entscheidung treffen: `NonBlocking`-Menge wiederverwenden (Konsistenz) vs. eigene Statusmenge (Entkopplung).
- Für strukturell unzustellbare `Wartend`-Ereignisse einen definierten Endzustand festlegen (Optionen: eigener terminaler Status, `Abgebrochen` mit Grund, oder bewusst manuell klärbar).

## Konfiguration

Keine neue Konfiguration vorgesehen — bestehende Limits (`plugins.maxparallel.*`) bleiben unverändert.

## Offene Fragen

Aus dem Issue übernommen (vor Umsetzung festzulegen; Empfehlungen jeweils im Issue ableitbar):

1. Welche Ereignis-Stati konkret als „zustellbar und platzbedürftig" gelten — Kandidaten: `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung`; strittig: `Aufgenommen`, `Verzögert`, `Wartend`.
2. Ob `Wartend` mit `NextAttemptUtc = long.MaxValue` weiterhin bewusst platzbelegend bleiben soll.
3. Ob die Platz-Logik `ScmIncomingEventStatus.NonBlocking` wiederverwenden oder eine eigene Statusmenge definieren soll.
4. Gewünschte Semantik des Endzustands strukturell unzustellbarer Ereignisse.
