# Architektur-Blueprint – KI-Protokoll Auto-Scroll

**Feature-Slug:** `ki-protokoll-auto-scroll`  
**Referenz:** `../requirements/ki-protokoll-auto-scroll-requirements-analysis.md`

## Architekturziel
Deterministisches Scrollverhalten beim Einblenden und bei Inhaltsupdates, ohne Verlust manueller Leseposition.

## Komponenten
1. **Razor UI (AufgabeDetail)**
   - Rendert Streaming- und Historienprotokoll.
   - Triggert Vorher-/Nachher-Logik bei Updates.
2. **ViewModel/Code-Behind**
   - Führt Scrollzustand je Container.
   - Entscheidet, ob nach dem Rendern gescrollt wird.
3. **JS-Interop (log-scroll.js)**
   - `getMetrics(selector)` liest `scrollTop`, `scrollHeight`, `clientHeight`.
   - `scrollToEnd(selector)` setzt die Endposition.

## Zustandsmodell je Container
- `initialScrollPending`
- `isAtEndBeforeUpdate`
- `shouldScrollAfterAppend`
- `updateVersion`
- `pendingScrollVersion`

## Entscheidungslogik
1. **Einblenden:** Wenn Container sichtbar und Inhalt vorhanden, `initialScrollPending = true` und im nächsten Render `scrollToEnd`.
2. **Vor Update:** `isAtEndBeforeUpdate` mit Schwellwertregel bestimmen:
   - `scrollHeight - (scrollTop + clientHeight) <= thresholdPx`
3. **Nach Update:**
   - Wenn vorher am Ende: `scrollToEnd`.
   - Sonst: keine Scrollaktion (Position erhalten).
4. **Race-Schutz:** Scroll nur ausführen, wenn `pendingScrollVersion == updateVersion`.

## Fehlerbehandlung
- Interop-Fehler werden als Warning geloggt.
- UI darf nicht abbrechen; im Fehlerfall wird konservativ keine erzwungene Scrollaktion durchgeführt.

## Teststrategie
- Unit-Tests für Enderkennung (Schwellwertgrenzen).
- Komponenten-/UI-Tests:
  - Initiales Scrollen beim Einblenden.
  - Follow-Scroll bei Endposition.
  - Positionshalt bei Nicht-Endposition.
  - Burst-Updates und Versionsschutz.

## Qualitätsziele
- Vorhersagbares UX-Verhalten ohne Scroll-Sprünge gegen Nutzerintention.
- Deterministische Entscheidungen für alle Container.
- Robustes Verhalten bei Interop-Fehlern.
