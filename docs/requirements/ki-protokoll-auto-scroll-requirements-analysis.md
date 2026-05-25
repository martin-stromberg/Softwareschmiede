# Anforderungsanalyse – KI-Protokoll Auto-Scroll

**Feature-Slug:** `ki-protokoll-auto-scroll`

## Zielbild
Das KI-Protokoll verhält sich beim Scrollen vorhersagbar und nutzerzentriert:
- Beim Einblenden sofort zum neuesten Eintrag.
- Bei neuem Inhalt nur dann automatisch nach unten, wenn der Anwender zuvor am Ende war.
- Bei manuellem Hochscrollen bleibt die Leseposition stabil.

## Scope
### In Scope
- Initiales Scrollen ans Ende beim Einblenden.
- Bedingtes Auto-Scroll bei neuem Inhalt (abhängig vom Vorher-Zustand).
- Erhalt der Position bei nicht-endständiger Scrollposition.
- Verhalten pro Container (Streaming und Historie) getrennt.

### Out of Scope
- Backend- oder Persistenzänderungen.
- Redesign der gesamten Protokoll-UI.
- Virtualisierung/Paging.

## Funktionale Anforderungen
1. **FR-1 Initiales End-Scroll:** Beim Sichtbarwerden eines Protokollcontainers wird an das Ende gescrollt.
2. **FR-2 Follow-Scroll bei Endposition:** Wird Inhalt angehängt und war die Scrollposition vor dem Update am Ende, wird nach dem Update wieder ans Ende gescrollt.
3. **FR-3 Positionsschutz:** Wird Inhalt eingefügt und war die Scrollposition vor dem Update nicht am Ende, bleibt die aktuelle Position erhalten.
4. **FR-4 Vorher-Zustand erfassen:** Vor jedem Inhaltsupdate wird der Zustand „am Ende/nicht am Ende“ je Container ermittelt.
5. **FR-5 Container-Isolation:** Streaming- und Historienprotokoll werden unabhängig voneinander bewertet.

## Nicht-funktionale Anforderungen
- **NFR-1 Reaktivität:** Scrollentscheidung und Aktion ohne wahrnehmbares Nachziehen (Ziel < 100 ms nach Render-Update).
- **NFR-2 Determinismus:** Enderkennung erfolgt über reproduzierbare Toleranzschwelle (z. B. 16 px).
- **NFR-3 Robustheit:** JS-Interop-/DOM-Fehler dürfen die Bedienbarkeit nicht beeinträchtigen.
- **NFR-4 Testbarkeit:** Kernfälle müssen automatisiert testbar sein.

## Akzeptanzkriterien
1. Beim Einblenden eines Containers mit Inhalt wird genau ein Scroll ans Ende ausgeführt.
2. Nach dem initialen Scroll liegt die Distanz zum Ende innerhalb der Toleranzschwelle.
3. Bei neuem Inhalt und vorheriger Endposition erfolgt erneut Scroll ans Ende.
4. Bei neuem Inhalt und vorheriger Nicht-Endposition erfolgt kein erzwungener Sprung ans Ende.
5. Bei manueller Leserposition oberhalb des Endes bleibt die Position stabil.
6. Das Verhalten gilt identisch für Streaming- und Historiencontainer.
7. Bei schnellen Folgeupdates verhindert die Logik veraltete Scrollentscheidungen.
8. Bei Interop-Fehlern bleibt die UI funktionsfähig; Fehler werden protokolliert.

## Risiken und Annahmen
- Selektoren der Zielcontainer bleiben stabil.
- Inhalte werden append-basiert ergänzt.
- Ein ungeeigneter Schwellwert kann Fehlentscheidungen „am Ende/nicht am Ende“ verursachen.

## Fachliches Domänenmodell
- **ScrollContainer**: Repräsentiert einen scrollbaren Protokollbereich.
- **ScrollState**: Enthält Zustand wie `isAtEndBeforeUpdate`, `initialScrollPending`, `shouldScrollAfterAppend`.
- **ContentUpdateEvent**: Signalisiert das Anfügen neuer Inhalte.
- **ScrollDecision**: Ergebnisregel für `scrollToEnd` oder Positionshalt.
