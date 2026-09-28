← [Zurück zur Übersicht](index.md)

# Terminal-Integration — Business Rules

## Automatischer Rohbyte-Mitschnitt (immer-an)

**Beschreibung:** Die CLI-Aufzeichnung läuft automatisch bei jedem Terminal-Session-Start mit — ein späteres opt-in wäre für die Diagnose von Rendering-Fehlern zu spät, weil der Fehler dann bereits aufgetreten ist, bevor die Aufzeichnung begonnen hätte.

**Bedingungen:**
- `TerminalSessionOptions.AufzeichnungByteBudget > 0` (Default 8 MB) → Mitschnitt aktiv.
- `AufzeichnungByteBudget <= 0` → bewusster Opt-out: es wird weder ein `CliOutputRecorder` noch eine `CompositeTerminalOutputSink` erzeugt; der `CliOutputProtokollWriter` wird direkt als `outputSink` übergeben.

**Verhalten:**
- Mitschnitt läuft ab Session-Erzeugung mit (die Composite-Senke existiert vor dem ersten Chunk) → keine Race-Bedingung für frühe Chunks, die eine nachträgliche `OutputChunk`-Event-Registrierung hätte.
- Der Mitschnitt bleibt auch nach dem Session-Ende abrufbar (`KiAusfuehrungsService.GetCliAufzeichnung`).
- Ein Neustart derselben Aufgabe ersetzt den bisherigen Mitschnitt.

**Umsetzung:** `KiAusfuehrungsService.StartTerminalSessionAsync` (Recorder-Erzeugung + Composite), `CliOutputRecorder`, `CompositeTerminalOutputSink`.

## Budget-Überschreitung: intaktes Präfix statt Ringpuffer-Verwerfen

**Beschreibung:** Wird das Aufzeichnungs-Budget überschritten, stoppt die Aufnahme und das bisherige intakte Präfix bleibt erhalten — es werden **keine** ältesten Chunks verworfen (anders als der `TerminalReplayBuffer`, der für den UI-Neuaufbau ein Ringpuffer-Modell nutzt).

**Bedingungen:**
- `bufferedBytes + chunk.Length > byteBudget` beim `OnOutputChunk`-Aufruf.

**Verhalten:**
- Erste Überschreitung: `IstVollstaendig = false`, Log-Information, Chunk wird nicht mehr gespeichert.
- Alle folgenden Chunks werden verworfen; der Session-Betrieb (Protokoll, Rendering) läuft unverändert weiter.
- Ein Replay braucht den Session-Anfang für den Parser-Zustand — ein verworfener Anfang würde den Neuaufbau korrumpieren. Ein abgeschnittenes Ende ist für die Diagnose brauchbarer als ein fehlender Anfang.

**Umsetzung:** `CliOutputRecorder.OnOutputChunk` — das `IstVollstaendig`-Flag wandert in die exportierte `.clireplay`-Datei und wird im Konsolentestfenster als Hinweisband angezeigt.

## Retention: nur die letzten 8 Aufzeichnungen

**Beschreibung:** Die Aufzeichnungs-Registry im `KiAusfuehrungsService` ist auf die zuletzt gestarteten Aufgaben begrenzt, damit der Speicherverbrauch nicht unbegrenzt mit der Zahl der Sessions wächst (ein Eintrag kann bis zu `AufzeichnungByteBudget` Bytes halten).

**Bedingungen:**
- `KiAusfuehrungsService.MaxAufzeichnungenAnzahl = 8`.

**Verhalten:**
- Beim 9. Eintrag wird der älteste Mitschnitt verworfen (LRU über die Registrierungsreihenfolge).
- Ein Neustart derselben Aufgabe zählt als jüngster Eintrag — der alte Mitschnitt der Aufgabe wird ersetzt.
- `GetCliAufzeichnung` für eine verdrängte Aufgabe liefert `null` → der Export meldet „keine Aufzeichnung vorhanden".

**Umsetzung:** `KiAusfuehrungsService.RegistriereAufzeichnung` (`ConcurrentDictionary` + `LinkedList` unter `_aufzeichnungenLock`).

## `[Terminal-Diagnose]`-Marker gehören nicht in den Mitschnitt

**Beschreibung:** Die Markerzeilen des Pipe-Fallback-Preflights sind keine echte CLI-Ausgabe — ein byte-exakter Mitschnitt, der sie enthielte, würde beim Replay gefälschte Ausgabe anzeigen.

**Bedingungen:**
- `TerminalSessionService.WriteDiagnosis` schreibt eine `[Terminal-Diagnose]`-Markerzeile an die Session-Output-Senke.

**Verhalten:**
- Senke implementiert `ITerminalDiagnoseSink` → Marker geht über `OnDiagnoseChunk` nur an die inneren Diagnose-Senken (`CliOutputProtokollWriter` → Aufgabenprotokoll).
- Senke ohne das Interface → Marker geht weiter über `OnOutputChunk` (Rückwärtskompatibilität für einfache Senken/Mocks).
- `CliOutputRecorder` implementiert `ITerminalDiagnoseSink` bewusst nicht → keine Artefakt-Zeilen im Mitschnitt.

**Umsetzung:** `ITerminalDiagnoseSink`, `CompositeTerminalOutputSink.OnDiagnoseChunk`, `CliOutputProtokollWriter.OnDiagnoseChunk` (leitet auf denselben Accumulator-Pfad), `TerminalSessionService.WriteDiagnosis` (Routing-Entscheid).

## Zeitraffer-Semantik der Wiedergabe

**Beschreibung:** Das Konsolentestfenster spielt Aufzeichnungen zeitreal ab, verkürzt aber lange Leerzeiten über eine einstellbare Schwelle.

**Bedingungen:**
- `TerminalReplaySession.ZeitrafferSchwelle` (über `ZeitrafferSchwelleText` im UI, Sekunden als Dezimalzahl ≥ 0).

**Verhalten:**
- Wartezeit vor jedem Chunk: `min(realePause seit letztem Chunk, ZeitrafferSchwelle)` — kürzere Pausen bleiben zeitreal, längere werden auf die Schwelle gedeckelt.
- `ZeitrafferSchwelle = 0` → maximale Geschwindigkeit (alle Pausen entfallen).
- Änderung wirkt sofort auf alle folgenden Pausen; ungültige Eingabe → `FehlerMeldung`, die zuletzt gültige Schwelle bleibt aktiv.
- Die Pause beginnt vor dem Chunk — Pause-Gate und Chunk-Anwendung sind gegen `Pausieren()` abgesichert, sodass nach dem Pausieren kein weiterer Chunk mehr angewendet wird.

**Umsetzung:** `TerminalReplaySession.WiedergabeLoopAsync` (`Task.Delay(delay, _timeProvider, ct)`), `KonsolenTestViewModel.TryParseZeitrafferSchwelle`.

## Wiedergabe nutzt immer den echten Renderpfad

**Beschreibung:** Das Konsolentestfenster besitzt keinen eigenen Renderer — die `TerminalReplaySession` füttert die aufgezeichneten Chunks durch denselben `AnsiSequenceParser` → `TerminalBuffer` → `TerminalControl`-Pfad wie `PseudoConsoleSession.ReadLoopAsync`, in derselben Reihenfolge (`OutputChunk` → Parse → `Buffer.Apply` unter Render-Lock → `BufferChanged`).

**Bedingungen:**
- Aufzeichnung geladen und `WiedergabeStarten` aufgerufen.

**Verhalten:**
- Gleiche Event-Sequenz wie der Live-Pfad → ein Replay reproduziert den tatsächlichen Anzeigezustand, inkl. aller (damaligen) Parser-Verhaltensweisen.
- `RebuildBufferFromReplay` baut den Buffer aus den **bis dahin abgespielten** Chunks neu auf — die Control-Bindung beim Laden/Neustart zeigt den korrekten Zwischenstand, keinen vermischten Zustand.
- Nach dem letzten Chunk: `RuntimeStatus = Inaktiv`, `Exited` mit `ExitCode = null`; erneutes Abspielen erzeugt über das ViewModel eine frische Session ab Position 0 — solange der Beendet-Zustand gilt (`_wiedergabeBeendet`). `WiedergabeStarten` der Session selbst ist re-armierbar (Lauf-Flag `_wiedergabeLoopAktiv`, Rücksetzen im Schleifen-`finally`): Wurde das Ende per `SchrittZurueck` verlassen, startet dieselbe Session einen neuen Durchlauf ab der aktuellen Position und feuert `Exited` erneut.
- Terminal-Geometrie: die Header-Werte `Cols`/`Rows` bestimmen nur die initiale Buffer-Größe; die Wiedergabe nutzt die aktuelle Fenstergröße (Resize-Ereignisse werden nicht aufgezeichnet und nicht reproduziert).

**Umsetzung:** `TerminalReplaySession`, `KonsolenTestViewModel.ErsetzeReplaySessionDurchFrische` (Neustart durch frische Session statt Zustands-Reset der laufenden).

## Einzelschritt-Wiedergabe und deterministischer Rückwärts-Rebuild

**Beschreibung:** Das Konsolentestfenster erlaubt Einzelschritte durch die Aufzeichnung — vorwärts ohne Zeitbezug, rückwärts über einen vollständigen Neuaufbau aus dem verbleibenden Chunk-Präfix. Damit lässt sich exakt lokalisieren, welcher aufgezeichnete Block einen Darstellungsfehler erzeugt.

**Bedingungen:**
- `SchrittVor`/`SchrittZurueck` stehen nur bei geladener Aufzeichnung und nicht unpausiert laufender Wiedergabe zur Verfügung (UI-CanExecute plus interne Guards — `RelayCommand.Execute` wertet `CanExecute` nicht aus); die Session-Methoden selbst sind zusätzlich race-sicher unter `_renderLock`.
- An den Positionsgrenzen (0 bzw. `Chunks.Count`) und nach `Dispose` liefern beide `false` (No-Op).

**Verhalten:**
- `SchrittVor` wendet exakt einen Chunk an — ohne Inter-Chunk-Pause und ohne `ZeitrafferSchwelle`-Wirkung — und feuert `OutputChunk` + `BufferChanged` in derselben Reihenfolge wie die Wiedergabe-Schleife (`WendeChunkAnUnterLock`).
- `SchrittZurueck` kürzt die abgespielten Chunks um einen und baut Buffer **und** Parser-Zustand aus dem Präfix neu auf (`BaueBufferUndParserAusPraefixNeuAuf`: `Buffer.Reset()` + `_parser.Reset()` + Re-Parse). Der Neuaufbau ist deterministisch, weil das Rendering eine reine Funktion der Chunk-Sequenz ist — O(n) pro Schritt ist für budgetbegrenzte Mitschnitte akzeptabel (Diagnosewerkzeug).
- Position ist `_abgespielteChunks.Count` unter `_renderLock` — die Wiedergabe-Schleife liest dieselbe Quelle, daher setzen „Fortsetzen"/„Abspielen" exakt an der Schrittposition fort und warten die aufgezeichnete Pause eines zurückgenommenen Chunks regulär erneut ab.
- `SchrittVor` auf dem letzten Chunk feuert `Exited` (gleiche Ende-Semantik wie das Schleifenende) und beendet eine evtl. pausiert parkende Schleife über das Terminations-Flag `_schleifeBeendenAngefordert` — nicht positionsbasiert, damit kein „Geister-Replay" entsteht, wenn ein `SchrittZurueck` die Position zwischen Gate-Öffnung und Positions-Lesen wieder senkt.
- `SchrittZurueck` setzt `_exitedSignaled` zurück (das Ende wurde verlassen → `Exited` darf erneut feuern), löscht im ViewModel `_wiedergabeBeendet` (→ „Abspielen" setzt an der Position fort statt eine frische Session zu erzeugen) und feuert kein `OutputChunk`.
- Reines Schreiten ohne gestartete Schleife lässt `RuntimeStatus = Inaktiv` — es gibt keinen eigenen Schritt-Status.

**Umsetzung:** `TerminalReplaySession.SchrittVor`/`SchrittZurueck`/`BaueBufferUndParserAusPraefixNeuAuf`/`WendeChunkAnUnterLock`, `RaiseExited(nurAmEnde)` (atomare Positions-Prüfung + Signal unter `_renderLock`, Event-Invoke nach Lock-Freigabe), `KonsolenTestViewModel.SchrittVor`/`SchrittZurueck` (StatusText `„Einzelschritt — Chunk n/y angewendet."` / `„Schritt zurück — Chunk n/y zurückgenommen."`).
