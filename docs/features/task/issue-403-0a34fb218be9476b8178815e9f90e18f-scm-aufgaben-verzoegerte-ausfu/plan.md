# Umsetzungsplan: Verzögerte Ausführung per Keyword (Issue #403)

## Zielbild

Ein SCM-Ereignis (Beschreibung oder Kommentar) kann eine Verzögerungs-Direktive
enthalten. Die Anwendung erkennt sie bei der Ereignisaufnahme im selben Text wie den
Trigger, stellt die Zustellung bis zum Zielzeitpunkt zurück, versieht Ereignis und
Aufgabe mit einem persistierten Wartezustand und blockiert den Auto-Shutdown, solange
die verzögerte Ausführung aussteht.

## Klärungen aus Issue #403 (Kommentar vom 2026-11-17, verbindlich)

- **OP1 → konfigurierbares Keyword:** Die Direktive nutzt ein eigenes, in den
  Einstellungen konfigurierbares Keyword (neuer Schlüssel `scm.delay.keyword`,
  Standard `#verzögert`), analog `scm.trigger.keyword`.
- **OP2 → Shutdown wird blockiert (Lesart a):** Ein verzögert ausstehendes Ereignis
  zählt als ausstehende Automatisierung; `AutoShutdownOrchestrator` darf den Countdown
  nicht armieren, solange mindestens ein Ereignis im Status `Verzögert` wartet.
- **OP3 → Kontext des Triggers:** Die Direktive wirkt nur bei der Aufnahme und muss im
  selben Text stehen wie der Trigger. Kein nachträgliches Verzögern laufender
  Verarbeitungen.
- **OP4 → max. 32 Tage:** Relative und monatliche Zielzeitpunkte dürfen höchstens
  32 Tage in der Zukunft liegen. Persistenz ist ohnehin gegeben
  (`ScmIncomingEvent.NextAttemptUtc`, `Aufgabe.PausiertBisUtc` sind DB-Felder); der
  Timer übersteht Neustart und Shutdown.
- **Zusatz → `m<N>`-Formel:** `m<N>` mit N = 1..31 benennt einen Kalendertag im Monat
  (lokale Zeitzone, Tageszeit = Ereigniszeit). Ist der Tag im laufenden Monat bereits
  erreicht oder existiert er dort nicht (z. B. `m31` im Februar), gilt der nächste
  Monat mit diesem Tag. Auch hier gilt die 32-Tage-Obergrenze.

## Syntax der Direktive

`<delayKeyword> <angabe>` frei stehend im Ereignistext (gleiche
Wortgrenzen-/Zitat-Regel wie der Trigger):

| Angabe   | Bedeutung                                            |
|----------|------------------------------------------------------|
| `<n>min` | n Minuten ab Ereignisaufnahme                        |
| `<n>h`   | n Stunden                                            |
| `<n>t` / `<n>d` | n Tage (`t` = Tage, `d` = days, beide zulässig) |
| `<n>w`   | n Wochen                                             |
| `m<N>`   | N-ter Kalendertag des Monats (siehe oben)            |

Beispiele: `#verzögert 4h`, `#verzögert 30min`, `#verzögert 2t`, `#verzögert m1`.

Ungültig: Keyword ohne Angabe, unbekannte Einheit, `m0`/`m32+`, Angabe über 32 Tage,
nicht-positive Dauer. Erste frei stehende Direktive im Text gewinnt; Groß-/Kleinschreibung
des Keywords wird ignoriert.

## Umsetzung

1. **`ScmDelayDirective` (neu, `Application/Services`):** statischer Parser.
   `TryParse(body, keyword, nowLocal) → None | Deferred(targetUtc) | Invalid(grund)`.
   Konstanten `DefaultDelayKeyword = "#verzögert"`, `MaxDelay = 32 Tage`.

2. **Einstellung `scm.delay.keyword`:** `AppEinstellungService.ScmDelayKeywordKey` +
   `GetScmDelayKeywordAsync` (Leerwert → Standard). UI: `SettingsViewModel`/
   `SettingsView.xaml` erhalten ein eigenes Textfeld samt Validierung, sichtbar
   sobald die SCM-Überwachung aktiv ist (gilt für beide Kontomodi, da die Direktive
   aus bereits akzeptierten Triggertexten gelesen wird).

3. **Aufnahme (`ScmEventMonitoringService` + `ScmEventStore.AcceptBatchAsync`):**
   Monitoring liest das Delay-Keyword einmal pro Zyklus und reicht es als neuen
   Parameter an `AcceptBatchAsync`. Dort wird pro neu angenommenem Ereignis
   (Trigger-Prüfung bestanden) die Direktive ausgewertet:
   - **Deferred:** `ScmIncomingEvent.Status = "Verzögert"`,
     `NextAttemptUtc = Ziel`, `Reason = "Verzögert bis <lokal>"`. Zusätzlich wird eine
     `ScmOutgoingAction` mit Purpose `waiting` und deterministischer ID
     (`ActionId(item.Id, "waiting")`) angelegt; der vorhandene Dispatcher sendet sie
     beim nächsten Poll als Empfangskommentar mit Verzögerungshinweis, ohne die
     Zustellung auszulösen (Orchestrator-Zeile „due receipt/waiting response" bricht
     vor `NextAttemptUtc` ab).
   - **Task-Pause:** Liegt die Aufgabe in Status Neu/Gestartet/Wartend, wird
     `PausiertBisUtc = max(PausiertBisUtc, Ziel)` gesetzt (Semantik von
     `AufgabeService.SetPauseAsync`, hier transaktionsgleich mit `TimeProvider` und
     Protokolleintrag „Aufgabe pausiert bis … (verzögerte SCM-Ausführung)"). So ist der
     Wartezustand in der Aufgabenliste sichtbar und blockiert Start/Recovery.
   - **Invalid:** Das Ereignis läuft normal (keine Verzögerung); eine `waiting`-Aktion
     mit Erklärungstext quittiert die verworfene Direktive remote.
   - **None:** unverändert.

4. **Wiederaufnahme:** Die Poll-Auswahl (`NextAttemptUtc <= now`) stellt zum
   Zielzeitpunkt regulär zu; die Pause ist dann abgelaufen. App-Neustart/Shutdown
   überdauert der Zustand persistiert.

5. **Shutdown-Blockade (`LaufendeAutomatisierungenZaehlung`):** Zusätzlich zu den
   `Laeuft`-Aufgaben zählt jeder Datensatz `ScmIncomingEvents.Status == "Verzögert"`
   als eine ausstehende Automatisierung (per `SoftwareschmiededDbContext` im
   bestehenden Scope). Damit armiert der Auto-Shutdown-Countdown nicht, solange eine
   verzögerte Ausführung aussteht. Interface-/Klassen-Doku wird angepasst.

6. **Abbruch (`ScmEventOrchestrator.CancelAsync`):** bleibt möglich; hebt das
   Ereignis endgültig auf. Setzte das Ereignis zuvor die Aufgabenpause
   (`PausiertBisUtc == ehemaliges NextAttemptUtc`), wird die Pause mit aufgehoben,
   damit kein verwaister Pausenzustand zurückbleibt.

7. **Anzeige:** `ScmEventQueueViewModel` zeigt den Status `Verzögert` im
   Wartend-/Alle-Filter; StatusText-Fallback ist bereits identisch. Grund zeigt den
   Zielzeitpunkt bzw. den Pausenhinweis.

8. **Tests:**
   - `ScmDelayDirectiveTests` (Unit): alle Einheiten, `m<N>`-Monatslogik
     (vor/ nach dem Tag, Februar-Kanten, Jahreswechsel), 32-Tage-Deckel,
     frei stehendes Keyword, Mehrfachvorkommen, ungültige Angaben.
   - `AppEinstellungServiceTests_ScmMonitoring`: Lesen/Fallback des neuen Schlüssels.
   - Store-/Monitoring-Integrationstests: Direktive erzeugt `Verzögert` +
     `NextAttemptUtc` + `waiting`-Outbox + Aufgabenpause; ungültige Direktive
     erzeugt normale Aufnahme + Hinweis-Aktion; keine Direktive unverändert.
   - `LaufendeAutomatisierungenZaehlungTests`: verzögertes Ereignis zählt als
     blockierende Automatisierung.
   - Dispatch-Test: vor Zielzeitpunkt keine Zustellung, danach regulär.

9. **Dokumentation:** `docs/help/` (SCM-Ereignisse: Direktivensyntax, Wirkung auf
   Aufgabe/Shutdown, Obergrenze), `docs/RELEASE_NOTES.md`, README-Hinweis.

## Akzeptanzszenarien

- Kommentar `$Softwareschmiede #verzögert 2h` auf verknüpftem Issue → Ereignis-Status
  `Verzögert`, kein CLI-Start; Aufgabe zeigt „Pausiert" mit Endzeit; Bestätigungs-
  kommentar mit Zielzeitpunkt; Auto-Shutdown armiert nicht; nach 2 h (im Test per
  `TimeProvider`) reguläre Zustellung.
- `#verzögert m1` am 20. eines Monats → Ziel = 1. des Folgemonats; am 1. → Folgemonat.
- `#verzögert 40t` oder `#verzögert xyz` → sofortige normale Verarbeitung plus
  Hinweiskommentar zur ungültigen Direktive.
- App-Neustart während der Verzögerung → Zustellung erfolgt dennoch zum/nach dem
  Zielzeitpunkt (persistierter `NextAttemptUtc`).
- Abbruch während der Verzögerung → keine Zustellung, Aufgabenpause aufgehoben.

## Offene Punkte

Keine — alle Klärungen durch den Anwender erfolgt (siehe oben).
