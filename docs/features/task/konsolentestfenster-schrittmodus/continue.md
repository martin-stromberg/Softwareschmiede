# Offene Aufgaben

Erstellt am: 2026-09-24
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt den Status „Vollständig umgesetzt".

## Code-Review-Befunde

- [ ] `TerminalReplaySession.WiedergabeStarten` (Z. ~138–139): Rest-Race im CAS-Fail-Pfad. Das `_schleifeBeendenAngefordert`-Flag erzeugt den Zustand „Schleife lebt (`_wiedergabeLoopAktiv`=1), ist aber zum Abbruch verurteilt". Ein `WiedergabeStarten` in diesem Fenster returnt wirkungslos (CAS schlägt fehl), die Schleife stirbt danach lautlos — bei Position < Count feuert `RaiseExited(nurAmEnde: true)` nichts. UI-Sequenz: pausiert → `SchrittVor` bis Ende → `SchrittZurueck` → „Abspielen". Folge: VM dauerhaft `IstWiedergabeAktiv=true`, „Abspielen"/Schritt-Buttons deaktiviert (auflösbar nur via „Neu starten"/Neuladen/Schließen). Fenster: ms bis `ZeitrafferSchwelle`. Empfehlung laut `review-code.md`: CAS-Fail-Pfad unter `_renderLock` entscheiden — Flag gesetzt → Termination widerrufen; Flag bereits konsumiert → Neustart via `_playbackTask.ContinueWith` nach Task-Abschluss. Deterministischer FakeTimeProvider-Test skizziert im Review.

## Usability-Befunde

Keine — `review-usability.md` trägt den Status „Keine Befunde".

## Fehlgeschlagene Tests

Keine — `test-results.md` trägt den Status „Keine Fehler" (2009 Tests, 2006 bestanden, 0 fehlgeschlagen, 3 übersprungen). Übersprungen: `RunConPtyTests` und `InitialisierungsskriptAusfuehrung` (ConPTY-Sandbox-Limitation — in interaktiver Session/Visual Studio ohne Skip-Variable nachzuholen), `Oeffne_AufNichtWindows_...` (Plattform).
