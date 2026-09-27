# Umsetzungsplan: Konsolentestfenster — Schrittmodus (vorwärts/rückwärts)

## Übersicht

Das Konsolentestfenster (`KonsolenTestDialog`/`KonsolenTestViewModel`/`TerminalReplaySession`) wird um Einzelschritt-Wiedergabe ergänzt: `SchrittVor()` wendet den nächsten aufgezeichneten Chunk zeitstempel-unabhängig an, `SchrittZurueck()` stellt den gerenderten Zustand vor dem zuletzt angewendeten Chunk über einen deterministischen Präfix-Rebuild wieder her. Zentraler Umbau: Die Positionsführung in `TerminalReplaySession` wird vereinheitlicht (`_abgespielteChunks.Count` statt schleifenlokalem Index `i`), damit `Fortsetzen`/`WiedergabeStarten` an der durch Schritte veränderten Position weiterlaufen. Betroffen sind die Replay-Session, das ViewModel (zwei neue Commands + Zustandsführung), der Dialog (zwei Buttons), die E2E-Wrapper-View sowie Unit- und E2E-Tests. Keine neuen Klassen, keine Migrationen, keine Konfiguration.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Positionsquelle der Session | `_abgespielteChunks.Count` ist die einzige mutierende Positionsquelle (ausschließlich unter `_renderLock`); `_aktuellerChunkIndex` spiegelt denselben Wert für lock-freie Leser (`Volatile.Read`). `WiedergabeLoopAsync` liest die Position pro Iteration aus dem gemeinsamen Zustand statt aus einem lokalen `i`. | Schleifenpfad und Schrittpfad müssen dieselbe Position mutieren — ein schleifenlokaler Index kann von einem Rückwärtsschritt nicht korrigiert werden und würde `_abgespielteChunks` desynchronisieren (falscher Chunk an falscher Position per `Add`). Geklärt mit Anwender: Positionsführung ist zu vereinheitlichen. |
| Rückwärts-Rebuild | Naiv: `Buffer.Reset()` + `_parser.Reset()` + Re-Parse aller verbleibenden `_abgespielteChunks` **durch `_parser` selbst** (gemeinsamer privater Rebuild-Helper, auch von `RebuildBufferFromReplay` genutzt). Keine Buffer-Snapshots/Checkpoints. | Deterministisch — das Rendering ist eine reine Funktion der Chunk-Sequenz (`RebuildBufferFromReplay` ist das vorhandene Muster). O(n) pro Schritt ist für gebudgetete `.clireplay`-Mitschnitte ausreichend (Diagnosewerkzeug; keine Performance-Anforderung in der Anforderung). Durch Re-Parse über `_parser` (statt lokalem Parser wie bisher) bleiben Parser- und Bufferzustand kohärent — der bisherige Rebuild-Pfad ließ `_parser` auf einem veralteten Zustand (latente Inkonsistenz bei Rebind mitten im Lauf); `_parser` bleibt dadurch `readonly` und `AnsiSequenceParser.Reset()` (existiert genau für diesen Zweck) wird genutzt statt Neuinstanzierung. |
| Ende-Signal im Schrittmodus | `SchrittVor` auf dem letzten Chunk feuert `RaiseExited()` (gleiche Terminal-Semantik wie Schleifenende: `RuntimeStatus → Inaktiv`, `Exited` mit `ExitCode = null`) und öffnet danach das Pause-Gate (`Fortsetzen()`-Semantik). `_exitedSignaled` wird beim Verlassen des Endes zurückgesetzt — in `SchrittZurueck` (Position < Count) und bei jedem neuen Schleifenstart. | Einheitlicher Endzustand („Wiedergabe beendet.") unabhängig davon, ob das Ende per Schleife oder per Schritt erreicht wurde. Das Öffnen des Gates verhindert einen Zombie: Eine evtl. pausiert parkende `WiedergabeLoopAsync` wacht auf, sieht Position == Count und terminiert sauber (`RaiseExited` ist dann bereits signalisiert → No-Op), statt bis zum `Dispose` geparkt zu bleiben und `WiedergabeStarten` dauerhaft zu blockieren. |
| `WiedergabeStarten`-Semantik | „Zeitgesteuerte Wiedergabe ab aktueller Position"; Re-Armierung nach Schleifenende — `_wiedergabeGestartet` wird zum Lauf-Flag (CAS 0→1 beim Start, Rücksetzen im `finally` der Schleife nach `RaiseExited`; optionale Umbenennung, z. B. `_wiedergabeLoopAktiv`). | Voraussetzung für „beendet → `SchrittZurueck` → Abspielen setzt an Position fort" ohne Session-Ersatz. Idempotenz während eines laufenden Durchlaufs bleibt erhalten (CAS schlägt fehl, solange die Schleife lebt). Im ViewModel bleibt `_wiedergabeBeendet` der Treiber für „erneutes Abspielen ab 0 via frischer Session". |
| Schritt während unpausierter Wiedergabe | UI-seitig gesperrt (CanExecute `!IstWiedergabeAktiv \|\| IstPausiert`); die Session selbst bleibt permissiv — Schritte sind unter `_renderLock` atomar und die Schleife prüft die Position vor dem Anwenden erneut. | Geklärte Annahme der Anforderung (Schritt-Buttons nur aktiv, wenn die Wiedergabe nicht unpausiert läuft). Die Session muss trotzdem race-sicher bleiben, weil `Pausieren()` erst nach dem Rendern-Fence wirksam ist; die Positions-Re-Prüfung in der Schleife garantiert Konsistenz auch bei race-nahen Aufrufen. |
| `RuntimeStatus`-Zustandsmodell | Unverändert: Reines Schreiten ohne gestartete Schleife bleibt `Inaktiv`; `Laeuft` nur, solange eine Wiedergabe-Schleife lebt (inkl. Pausiert); `Inaktiv` nach `Exited`/`Dispose`. Kein neuer `CliRuntimeStatus`-Wert. | `IstWiedergabeAktiv` im ViewModel bildet „Schleife lebt" ab und steuert die bestehenden Commands konsistent („Abspielen" bleibt im Schrittmodus aktivierbar und setzt an der Schrittposition fort). Ein eigener Schritt-Statuswert hätte keine nachgefragte Semantik. |
| `OutputChunk`-Semantik im Schrittmodus | `SchrittVor` feuert `OutputChunk` (Rohbytes, vor dem Parsen — identische Reihenfolge wie die Schleife). `SchrittZurueck` feuert es nicht (kein neuer Chunk wird angewendet); nur `BufferChanged`. | Konsistenz zum Schleifenpfad — Konsumenten des Events sehen jeden tatsächlich angewendeten Chunk genau einmal pro Anwendung. |
| Visualisierung „zuletzt angewendeter Chunk" / `StatusText` | Bestehender `AktuellerQuellEintrag`-Mechanismus (`QuellEintraege[index - 1]`-Selektion + `ScrollIntoView`, bei Index 0 Selektion `null`) ist die Markierung — kein zusätzliches UI-Element. `StatusText` erhält einen eigenen Schritt-Hinweis (z. B. `„Einzelschritt — Chunk n/y angewendet."`); bei Schritt auf den letzten Chunk bleibt der `OnReplayExited`-Text „Wiedergabe beendet." maßgeblich. | Die Anforderung verlangt nur „erkennbar, welcher Chunk zuletzt angewendet wurde" — die Quell-Selektion leistet das bereits; Positionsanzeige (`Chunk x/y`) bleibt die primäre Positionsquelle. |

## Programmabläufe

### Einzelschritt vorwärts (`SchrittVor`)

1. `SchrittVorCommand` (aktiv nur bei geladener Session, nicht unpausiert laufender Wiedergabe und `AktuellerChunkIndex < Chunks.Count`) → `KonsolenTestViewModel.SchrittVor()` → `_replaySession.SchrittVor()`.
2. Session-Guards: `_disposed` → `false`; `_abgespielteChunks.Count >= _aufzeichnung.Chunks.Count` → `false` (No-Op am Ende).
3. Unter `_renderLock` in derselben Reihenfolge wie die Wiedergabe-Schleife: `OutputChunk`-Event feuern → `_abgespielteChunks.Add(chunk.Data)` → `_parser.Parse(chunk.Data)` → `Buffer.Apply` je Event → `_aktuellerChunkIndex` auf neue Position setzen (atomar mit dem `Add` im selben Lock).
4. Nach dem Lock: `BufferChanged` feuern → `OnReplayBufferChanged` im ViewModel aktualisiert `PositionsText` (`„Chunk n/y"`) und `AktuellerQuellEintrag` (Selektion + `ScrollIntoView` folgen automatisch) sowie `RelayCommand.Refresh()` für die Grenz-CanExecute der Schritt-Buttons.
5. Wurde der letzte Chunk angewendet (Position == Count): `RaiseExited()` und anschließend Gate-Öffnung (`Fortsetzen()`-Semantik), damit eine evtl. pausiert parkende `WiedergabeLoopAsync` aufwacht, das Ende erkennt und sauber terminiert.
6. Der Command-Handler setzt `StatusText` auf den Schritt-Hinweis — nur solange das Ende nicht erreicht ist (bei Ende steht der `OnReplayExited`-Text „Wiedergabe beendet." bereits; die `_dispatcherInvoke`-Ausführung ist auf dem UI-Thread synchron).
7. Rückgabewert `true` = Chunk angewendet, `false` = No-Op (Test-/Diagnose-Nachweis).

Beteiligte Klassen/Komponenten: `TerminalReplaySession`, `AnsiSequenceParser`, `TerminalBuffer`, `KonsolenTestViewModel`, `KonsolenTestDialog` (`QuellChunkListe`-Selektion), `RelayCommand`.

### Einzelschritt rückwärts (`SchrittZurueck`)

1. `SchrittZurueckCommand` (aktiv nur bei geladener Session, nicht unpausiert laufender Wiedergabe und `AktuellerChunkIndex > 0` — auch im beendeten Zustand) → `_replaySession.SchrittZurueck()`.
2. Session-Guards: `_disposed` → `false`; `_abgespielteChunks.Count == 0` → `false` (No-Op an Position 0).
3. Unter `_renderLock`: `_abgespielteChunks` um den letzten Eintrag kürzen → `_aktuellerChunkIndex` dekrementieren → `_exitedSignaled` zurücksetzen (das Ende wurde verlassen — ein späterer Durchlauf darf wieder `Exited` feuern) → gemeinsamer Rebuild-Helper: `Buffer.Reset()` + `_parser.Reset()` + Re-Parse aller verbleibenden `_abgespielteChunks` mit `Buffer.Apply` je Event (Buffer **und** Parser stehen danach exakt auf dem Präfix-Zustand).
4. Nach dem Lock: `BufferChanged` feuern → ViewModel-Synchronisation wie im Vorwärtspfad; bei Index 0 wird `AktuellerQuellEintrag` auf `null` gesetzt (kein Chunk angewendet). `OutputChunk` feuert nicht.
5. Der Command-Handler löscht ein gesetztes `_wiedergabeBeendet` (die Position hat das Ende verlassen → „Abspielen" darf die Session nicht mehr durch eine frische ersetzen, sondern setzt an der Position fort) und setzt `StatusText` auf den Schritt-Hinweis.

Beteiligte Klassen/Komponenten: `TerminalReplaySession`, `AnsiSequenceParser`, `TerminalBuffer`, `KonsolenTestViewModel`.

### Zeitgesteuerte Wiedergabe an der Schrittposition (vereinheitlichte Positionsführung)

1. `WiedergabeLoopAsync` wird von `for (var i = 0; …)` auf eine `while`-Schleife umgestellt. Pro Iteration wird unter `_renderLock` die aktuelle Position `i = _abgespielteChunks.Count` gelesen; `i >= chunks.Count` → Schleifenende.
2. Die Inter-Chunk-Pause wird aus der Position abgeleitet: `realePause = chunks[i].Offset - (i > 0 ? chunks[i-1].Offset : TimeSpan.Zero)`, `delay = min(realePause, ZeitrafferSchwelle)` — der schleifenlokale `vorherigerOffset` entfällt. `Task.Delay(delay, _timeProvider, ct)` wie bisher.
3. Anwendephase unverändert verschachtelt (Gate-Warte → `ct.ThrowIfCancellationRequested` → `_renderLock` → `_istPausiert`-Re-Check), ergänzt um einen **Positions-Re-Check**: Stimmt `_abgespielteChunks.Count` nicht mehr mit der zu Iterationsbeginn gelesenen Position überein (ein Schritt hat sie verändert), wird **nichts** angewendet und die Iteration neu begonnen — das Delay wird für die neue Position frisch berechnet. Damit wartet ein `Fortsetzen` nach einem Rückwärtsschritt die aufgezeichnete Pause des zurückgenommenen Chunks regulär erneut ab.
4. `ct.ThrowIfCancellationRequested()` am Schleifenanfang und in der Anwende-Schleife bleibt erhalten (Dispose-Semantik).
5. `finally`: wie bisher `RaiseExited()` bei nicht abgebrochenem Lauf; zusätzlich wird das Lauf-Flag (`_wiedergabeGestartet`, Semantik „Schleife läuft") auf 0 zurückgesetzt → Re-Armierung von `WiedergabeStarten`.
6. Ergebnis: `Fortsetzen()`/`WiedergabeStarten()` nach beliebigen Schritten läuft exakt an der aktuellen Schrittposition weiter — egal ob die Schritte im pausierten Zustand, ohne Start oder nach einem beendeten Durchlauf erfolgten.

Beteiligte Klassen/Komponenten: `TerminalReplaySession` (`WiedergabeLoopAsync`, `_renderLock`, `_pauseLock`/`_pauseGate`, `_playbackCts`).

### Beendet → Schritt zurück → Abspielen ab Position (Re-Arm)

1. Wiedergabe endet (Schleife oder `SchrittVor` auf letztem Chunk): `RaiseExited` → ViewModel `IstWiedergabeAktiv = false`, `_wiedergabeBeendet = true`, `StatusText = „Wiedergabe beendet."`. Session-Lauf-Flag steht nach Schleifen-`finally` auf 0.
2. `SchrittZurueck` kürzt Position, setzt `_exitedSignaled` (Session) und `_wiedergabeBeendet` (ViewModel) zurück.
3. „Abspielen" (`WiedergabeStarten`): Da `_wiedergabeBeendet` nun `false` ist, wird **keine** frische Session erzeugt — `session.WiedergabeStarten()` startet per CAS eine neue Schleife, die ab der aktuellen Position weiterläuft und am Ende erneut `Exited` feuert.
4. „Neu starten" (`WiedergabeNeustartenCommand`) bleibt unverändert der explizite Zurück-auf-0-Pfad (frische Session), ist aber wie bisher nur während aktiver Wiedergabe aktivierbar — im Beendet/Schrittmodus-Zustand übernimmt „Abspielen" bzw. das Zurückschreiten auf Position 0 die Rolle.

Beteiligte Klassen/Komponenten: `TerminalReplaySession` (`WiedergabeStarten`, `_wiedergabeGestartet`, `_exitedSignaled`), `KonsolenTestViewModel` (`_wiedergabeBeendet`, `ErsetzeReplaySessionDurchFrische`).

### UI-Synchronisation und Command-Aktivierung

1. Beide Schritt-Commands sind `RelayCommand` (Muster der bestehenden Wiedergabe-Commands); CanExecute: `_replaySession is not null && (!IstWiedergabeAktiv || IstPausiert)` plus Positionsgrenze (`AktuellerChunkIndex < QuellEintraege.Count` bzw. `> 0`).
2. `RelayCommand.Refresh()`-Impulse: `IstWiedergabeAktiv`-Setter (bestehend), `IstPausiert`-Setter (neu — Pause-Wechsel muss die Schritt-Buttons freischalten/sperren) und `OnReplayBufferChanged` (neu — Positionsgrenzen ändern CanExecute auch ohne Zustandswechsel, z. B. Ende per Schritt mit unverändertem `IstWiedergabeAktiv`).
3. `OnReplayBufferChanged` bleibt der einzige Synchronisationspfad für `PositionsText`/`AktuellerQuellEintrag` — jetzt zusätzlich mit `null`-Selektion bei Index 0.

Beteiligte Klassen/Komponenten: `KonsolenTestViewModel`, `RelayCommand`, `KonsolenTestDialog`.

## Neue Klassen

Keine — die Erweiterung baut vollständig auf `TerminalReplaySession`, dem vorhandenen `CliOutputAufzeichnung`/`CliOutputChunkRecord`-Datenmodell und den bestehenden UI-Elementen auf.

## Änderungen an bestehenden Klassen

### `TerminalReplaySession` (`src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`) — ITerminalSession-Implementierung

- **Geänderte Felder:**
  - `_wiedergabeGestartet` (`int`) — Semantik von „jemals gestartet" zu „Schleife läuft gerade": CAS 0→1 in `WiedergabeStarten`, Rücksetzen auf 0 im `finally` von `WiedergabeLoopAsync` (Re-Armierung; optionale Umbenennung, z. B. `_wiedergabeLoopAktiv`).
  - `_exitedSignaled` (`int`) — wird zusätzlich in `SchrittZurueck` (Position < Count) und in `WiedergabeStarten` bei erfolgreichem neuen Schleifenstart auf 0 zurückgesetzt.
  - `_parser` bleibt `readonly` — der Rückwärtsschritt nutzt `AnsiSequenceParser.Reset()` statt Neuinstanzierung.
- **Neue Methoden:**
  - `SchrittVor()` → `bool` — siehe Programmablauf „Einzelschritt vorwärts" (Guards, Chunk-Anwendung unter `_renderLock` in Schleifenreihenfolge, `BufferChanged`, `RaiseExited` + Gate-Öffnung bei Erreichen des Endes).
  - `SchrittZurueck()` → `bool` — siehe Programmablauf „Einzelschritt rückwärts" (Guards, Präfix-Kürzung, `_exitedSignaled`-Reset, Rebuild-Helper, `BufferChanged`).
  - `BaueBufferUndParserAusPraefixNeuAuf()` (private; muss unter `_renderLock` laufen) — `Buffer.Reset()` + `_parser.Reset()` + `Parse`/`Apply` aller `_abgespielteChunks`; gemeinsamer Kern von `RebuildBufferFromReplay` und `SchrittZurueck`.
- **Geänderte Methoden:**
  - `WiedergabeLoopAsync` — Positionsführung wie im Programmablauf beschrieben (`while` über `_abgespielteChunks.Count`, Delay aus Position abgeleitet, Positions-Re-Check vor dem Anwenden, `_aktuellerChunkIndex`-Update atomar mit `Add` im `_renderLock`, Lauf-Flag-Reset im `finally`). Abbruch-Checks, Gate- und `_istPausiert`-Semantik bleiben erhalten.
  - `RebuildBufferFromReplay` — delegiert auf `BaueBufferUndParserAusPraefixNeuAuf` (Nebeneffekt: setzt `_parser` kohärent zurück — behebt die bisherige Parser/Buffer-Inkonsistenz nach einem Rebuild mitten im Lauf).
  - `WiedergabeStarten` — CAS auf Lauf-Flag statt Einmal-Flag; setzt `_exitedSignaled` zurück; `RuntimeStatus → Laeuft` und Start von `_playbackTask` wie bisher.
- **Neue Events:** keine (`OutputChunk`, `BufferChanged`, `Exited`, `RuntimeStatusChanged` bestehend).
- **Neue Properties:** keine (`AktuellerChunkIndex`, `IstPausiert` reichen aus).

### `KonsolenTestViewModel` (`src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`) — ViewModel

- **Neue Properties (Commands):**
  - `SchrittVorCommand` (`ICommand`, `RelayCommand`) — CanExecute: `_replaySession is not null && (!IstWiedergabeAktiv || IstPausiert) && _replaySession.AktuellerChunkIndex < QuellEintraege.Count`.
  - `SchrittZurueckCommand` (`ICommand`, `RelayCommand`) — CanExecute: `_replaySession is not null && (!IstWiedergabeAktiv || IstPausiert) && _replaySession.AktuellerChunkIndex > 0`.
- **Neue Methoden:**
  - `SchrittVor()` — ruft `_replaySession.SchrittVor()`; setzt anschließend `StatusText` auf den Schritt-Hinweis, sofern die Position nicht am Ende ist (dort gilt „Wiedergabe beendet." aus `OnReplayExited`).
  - `SchrittZurueck()` — ruft `_replaySession.SchrittZurueck()`; bei Erfolg `_wiedergabeBeendet = false` (Position hat das Ende verlassen) und `StatusText` = Schritt-Hinweis.
- **Geänderte Member:**
  - `OnReplayBufferChanged` — `AktuellerQuellEintrag` wird bei Index 0 explizit auf `null` gesetzt (Rückwärtsschritt auf Anfang); zusätzlich `RelayCommand.Refresh()` für die Positionsgrenzen der Schritt-Commands.
  - `IstPausiert`-Setter — ergänzt `RelayCommand.Refresh` (CanExecute der Schritt-Commands hängt vom Pausiert-Zustand ab).
- **Unverändert:** `WiedergabeStarten`/`WiedergabeNeustarten`/`WiedergabePausierenToggle`/`LadeAufzeichnung`/`EntsorgeReplaySession`/`ErsetzeReplaySessionDurchFrische` — die bestehende `_wiedergabeBeendet`-Logik bleibt wirksam; „Abspielen" setzt bei nicht-beendeter Session an der aktuellen Position fort (durch die Re-Arm-Semantik der Session konsistent auch nach „beendet → SchrittZurueck").

### `KonsolenTestDialog` (`src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`) — Window

- **Neue Elemente:** zwei Buttons in der Werkzeugleiste (Grid.Row 0) zwischen `WiedergabePausieren` und der Zeitraffer-TextBox:
  - `Content="Schritt zurück"`, `AutomationProperties.Name="SchrittZurueck"`, `Command="{Binding SchrittZurueckCommand}"`, ToolTip im bestehenden Stil (z. B. „Zustand vor dem zuletzt angewendeten Chunk wiederherstellen").
  - `Content="Schritt vor"`, `AutomationProperties.Name="SchrittVor"`, `Command="{Binding SchrittVorCommand}"`, ToolTip (z. B. „Nächsten aufgezeichneten Chunk anwenden").
  - Stil wie Bestandsbuttons (`Padding="10,4"`, `Margin="8,0,0,0"`).
- Code-behind: keine Änderung (`OnQuellListeSelectionChanged`/`ScrollIntoView` deckt die Schritt-Selektion ab).

### `KonsolenTestDialogView` (`src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs`) — E2E-Wrapper

- **Neue Methoden:**
  - `SchrittVor()` / `SchrittZurueck()` — `WaitForEnabledElement(GetDialogWindow(), "SchrittVor"/"SchrittZurueck", Medium).AsButton().ClickInForeground()` (Muster: `PausierenToggle`, Z. 163–167).
  - `IstSchaltflaecheAktiviert(string automationName)` — Element suchen + `IsEnabled` lesen (Nachweis der Rand-Deaktivierung, z. B. `SchrittZurueck` an Position 0).
  - `GetSelektierterQuellEintragIndex()` — Index der selektierten Zeile der `QuellChunkListe` via `SelectionPattern` (`GetSelection`), `-1` bei leerer Selektion (Nachweis „zuletzt angewendeter Chunk").

### `E2E_KonsolenTestfenster` (`src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs`) — E2E-Szenario

- **Geänderte Methode:** `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` — wird um Schritt-Phasen erweitert (kein neuer FlaUI-Test; Szenario bleibt in `RunGeneralTests`, `MainTest.cs:36`). Details siehe Tests-Abschnitt.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es gibt keine neuen Eingaben; die Positionsgrenzen sind No-Op-Semantik der Session (`SchrittVor`/`SchrittZurueck` liefern `false`) plus CanExecute-Sperre der Buttons, keine Validierung im Eingabesinne.

## Konfigurationsänderungen

Keine — der Schrittmodus ist reines Laufzeit-/Interaktionsverhalten des Diagnosefensters (analog `ZeitrafferSchwelle` ohne Persistenz).

## Seiteneffekte und Risiken

- **`WiedergabeLoopAsync`-Umbau:** Die Pause-/Dispose-/Zeitraffer-Semantik darf sich nicht ändern; Bestandstests (`Pausieren_StopptChunkAnwendung_FortsetzenSetztFort`, `Wiedergabe_ZeitrafferVerkuerztRealePausen`, `Dispose_WaehrendWiedergabe_BrichtAb`, `Dispose_MitZeitrafferNull_BrichtAbStattChunksZuDraenen`, `Wiedergabe_ErzeugtGleichenBufferWieLiveSession`) sichern das ab. Der Positions-Re-Check fügt einen zusätzlichen Iterations-Neustart ein — nur wirksam, wenn ein Schritt die Position verändert hat.
- **`RebuildBufferFromReplay` setzt nun `_parser` zurück:** Bewusste Verhaltensverbesserung — der bisherige Pfad ließ `_parser` nach einem externen Rebuild-Aufruf (z. B. `TerminalControl.OnSessionChanged` bei Rebind mitten im Lauf) inkohärent zum neu aufgebauten Buffer. `TerminalControl`-Aufrufer sind unverändert.
- **`WiedergabeStarten` ist re-armierbar:** `Exited` kann über die Session-Lebensdauer mehrfach feuern (nach SchrittZurueck + neuem Durchlauf) — der ViewModel-Handler `OnReplayExited` ist idempotent. `WiedergabeStarten_ZweiterAufruf_IstNoOp` bleibt gültig (CAS schlägt fehl, solange die Schleife läuft).
- **`Exited` bei pausiert parkender Schleife:** `SchrittVor` ans Ende öffnet das Gate — die Schleife terminiert ohne `BufferChanged` für den bereits per Schritt angewendeten Chunk; `PositionsText` bleibt korrekt (Position wurde vom Schritt gesetzt).
- **`RelayCommand.Refresh()` in `OnReplayBufferChanged`/`IstPausiert`-Setter:** mehr `CommandManager.RequerySuggested`-Auslösungen — vernachlässigbarer Overhead, Standardmechanismus.
- **O(n)-Präfix-Rebuild pro Rückwärtsschritt:** bei sehr großen Mitschnitten (Zehntausende Chunks) spürbar — für das Diagnosewerkzeug akzeptiert; Snapshot-Optimierung nur bei nachgewiesenem Bedarf (nicht Teil dieses Plans).
- **`TerminalControl`, `PseudoConsoleSession`, `CliReplayAufzeichnungStore`, Export-Pfad:** unberührt (keine Interface-Änderung an `ITerminalSession`).

## Umsetzungsreihenfolge

1. **Positionsführung in `TerminalReplaySession` vereinheitlichen**
   - Voraussetzungen: Keine.
   - Beschreibung: `WiedergabeLoopAsync` auf `_abgespielteChunks.Count`-basierte `while`-Iteration umstellen (Delay aus `chunks[i-1].Offset` ableiten, Positions-Re-Check vor dem Anwenden, `_aktuellerChunkIndex` atomar im `_renderLock`); `_wiedergabeGestartet` zum Lauf-Flag machen (Reset im `finally`), `_exitedSignaled`-Reset bei neuem Start; privaten Rebuild-Helper `BaueBufferUndParserAusPraefixNeuAuf` extrahieren und `RebuildBufferFromReplay` darauf umstellen. Bestehende `TerminalReplaySessionTests` müssen danach unverändert grün bleiben.

2. **`SchrittVor()` / `SchrittZurueck()` in `TerminalReplaySession` implementieren**
   - Voraussetzungen: Schritt 1 (vereinheitlichte Positionsführung, Rebuild-Helper).
   - Beschreibung: Beide Methoden mit Guards (`_disposed`, Positionsgrenzen → `false`), Chunk-Anwendung bzw. Präfix-Rebuild unter `_renderLock`, `BufferChanged` danach; `SchrittVor` am Ende: `RaiseExited` + Gate-Öffnung; `SchrittZurueck`: `_exitedSignaled`-Reset.

3. **Unit-Tests für die Session schreiben**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Neue Tests in `TerminalReplaySessionTests` (siehe Tests-Abschnitt) mit bestehenden Helfern (`FakeTimeProvider`, `CreateAufzeichnung`, `WarteBisAsync`, `BufferAlsText`).

4. **`KonsolenTestViewModel` um Schritt-Commands erweitern**
   - Voraussetzungen: Schritt 2 (Session-Member vorhanden).
   - Beschreibung: `SchrittVorCommand`/`SchrittZurueckCommand` mit CanExecute; Handler `SchrittVor()`/`SchrittZurueck()` (StatusText, `_wiedergabeBeendet`-Clearing); `OnReplayBufferChanged` (Selektion `null` bei 0, `RelayCommand.Refresh()`); `IstPausiert`-Setter um `RelayCommand.Refresh` ergänzen.

5. **ViewModel-Tests schreiben**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: Neue Tests in `KonsolenTestViewModelTests` (siehe Tests-Abschnitt); `Commands_OhneAufzeichnung_SindInert` um die neuen Commands erweitern.

6. **Buttons in `KonsolenTestDialog.xaml` ergänzen**
   - Voraussetzungen: Schritt 4 (Commands vorhanden — Binding sonst leer).
   - Beschreibung: `Schritt zurück`/`Schritt vor` mit `AutomationProperties.Name`, Commands, ToolTips im Bestandsstil.

7. **E2E-Wrapper `KonsolenTestDialogView` erweitern**
   - Voraussetzungen: Schritt 6 (Automationsnamen existieren).
   - Beschreibung: `SchrittVor()`, `SchrittZurueck()`, `IstSchaltflaecheAktiviert`, `GetSelektierterQuellEintragIndex`.

8. **E2E-Szenario in `E2E_KonsolenTestfenster.cs` erweitern**
   - Voraussetzungen: Schritt 7.
   - Beschreibung: Bestehendes konsolidiertes Szenario um die Schritt-Phasen erweitern (siehe E2E-Tabelle); bleibt in `RunGeneralTests`.

9. **Verifikation: Build + Test-Lanes**
   - Voraussetzungen: Schritte 1–8.
   - Beschreibung: `dotnet build Softwareschmiede.slnx -c Debug`, danach `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug --filter "Category!=OsInterface"` sowie `--filter "Category=OsInterface"` (enthält das erweiterte FlaUI-Szenario) und das IntegrationTests-Projekt — alle Läufe synchron/foreground (CLAUDE.md: niemals `dotnet test` im Hintergrund).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `SchrittVor_WendetNaechstenChunkZeitunabhaengigAn` | `TerminalReplaySessionTests` | Ohne gestartete Wiedergabe: `SchrittVor` wendet genau einen Chunk an (`AktuellerChunkIndex` 0→1→2), Buffer zeigt den Inhalt, keine `Advance`-Warte nötig (große Chunk-Offsets ignorieren Zeitstempel), `RuntimeStatus` bleibt `Inaktiv`. |
| `SchrittVor_FeuertOutputChunkUndBufferChanged` | `TerminalReplaySessionTests` | `OutputChunk` liefert die Rohbytes pro Schritt (Reihenfolge), `BufferChanged` pro Schritt. |
| `SchrittVor_AmEnde_FeuertExited_IstDannNoOp` | `TerminalReplaySessionTests` | Letzter Schritt feuert `Exited` (`ExitCode null`, `RuntimeStatus → Inaktiv`); weitere `SchrittVor` liefern `false`, kein zweites `Exited`. |
| `SchrittZurueck_BautPraefixDeterministischNeuAuf` | `TerminalReplaySessionTests` | Nach zwei Schritten `SchrittZurueck`: `BufferAlsText` identisch zum Buffer einer Referenz-Session, die nur einen Schritt ausführte (Chunk 2 enthält zustandsverändernde Sequenz, z. B. `\x1b[2J`/SGR — keine Restwirkung); `AktuellerChunkIndex` 1; `OutputChunk` feuert beim Rückwärtsschritt nicht. |
| `SchrittZurueck_SetztParserZustandZurueck` | `TerminalReplaySessionTests` | Aufzeichnung mit chunk-übergreifender Escape-Sequenz (Sequenz über Chunk-Grenze geteilt): Vor–Zurück–Vor ergibt denselben Buffer wie ein ununterbrochener Durchlauf (Vergleich via `BufferAlsText` mit Referenz-Session) — Nachweis, dass `_parser` nach dem Rebuild auf dem Präfix-Zustand steht statt auf dem alten Endzustand. |
| `SchrittZurueck_BeiPosition0_IstNoOp` | `TerminalReplaySessionTests` | `SchrittZurueck` an Position 0 → `false`, kein `BufferChanged`, Buffer unverändert. |
| `Pausiert_Schritte_Fortsetzen_SetztAnSchrittpositionFort` | `TerminalReplaySessionTests` | 3 Chunks mit langen Offsets: Start → Position 1 → `Pausieren` → `SchrittZurueck` (Position 0) → `Fortsetzen` → `Advance` → Schleife wendet Chunk 1 **erneut** an (`OutputChunk`-Sequenz A, A, B, C) und läuft bis `Exited` — Nachweis der vereinheitlichten Position und der erneut abgewarteten aufgezeichneten Pause. |
| `SchrittVor_BisEndeBeiPausierterSchleife_TerminiertSauber` | `TerminalReplaySessionTests` | Lauf pausiert zwischen den Chunks → `SchrittVor` bis ans Ende → `Exited` feuert; die Schleife terminiert (Lauf-Flag frei — erneutes `WiedergabeStarten` nach `SchrittZurueck` möglich). |
| `WiedergabeStarten_NachEndeUndSchrittZurueck_SetztAnPositionFort` | `TerminalReplaySessionTests` | Vollständiger Durchlauf (`ZeitrafferSchwelle = 0`) → `Exited` → `SchrittZurueck` → `WiedergabeStarten` → Schleife wendet nur die fehlenden Chunks an und feuert erneut `Exited` (Re-Arm + `_exitedSignaled`-Reset). |
| `Schritte_NachDispose_SindNoOp` | `TerminalReplaySessionTests` | Nach `Dispose`: beide Methoden `false`, keine Events, kein Wurf. |
| `SchrittVor_SchrittZurueck_SynchronisierenPositionUndQuellAuswahl` | `KonsolenTestViewModelTests` | Nach Laden: `PositionsText` „Chunk 0/3", `AktuellerQuellEintrag = null`; zwei `SchrittVorCommand.Execute` → „Chunk 2/3", Selektion `QuellEintraege[1]`; `SchrittZurueck` → „Chunk 1/3", Selektion `[0]`; weiter zurück → „Chunk 0/3", Selektion `null` — inkl. Schritt-`StatusText`. |
| `SchrittCommands_CanExecute_NachZustand` | `KonsolenTestViewModelTests` | Geladen: Vor aktiv, Zurück inaktiv; laufend unpausiert: beide inaktiv; pausiert: beide aktiv; am Ende (per Schritt): Vor inaktiv, Zurück aktiv; nach `SchrittZurueck` vom Ende: Vor wieder aktiv. |
| `SchrittVor_BisEnde_ZeigtStatusBeendet` | `KonsolenTestViewModelTests` | Reines Schreiten bis ans Ende → `StatusText` „Wiedergabe beendet.", `_wiedergabeBeendet`-Verhalten: anschließender `SchrittZurueck` löscht den Beendet-Zustand, `WiedergabeStartenCommand` setzt danach an Position fort (keine frische Session — `BeSameAs`). |
| `Pausiert_Schritt_Fortsetzen_ViewModelEbene` | `KonsolenTestViewModelTests` | Start → Pause → `SchrittVorCommand`/`SchrittZurueckCommand` verändern Position → `WiedergabePausierenCommand` (Fortsetzen) läuft von der Schrittposition bis `Exited`. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Commands_OhneAufzeichnung_SindInert` (`KonsolenTestViewModelTests`, Z. 400) | Neue Commands ergänzen: `SchrittVorCommand`/`SchrittZurueckCommand` müssen ohne Aufzeichnung `CanExecute == false` liefern und inert ausführbar sein. |
| `TerminalReplaySessionTests` (Bestand, 9 Tests) | Keine Änderung erwartet — sie dienen als Regressionsschutz für den `WiedergabeLoopAsync`-Umbau (Position/Delay/Pause/Dispose) und die geänderte `RebuildBufferFromReplay`/`_parser`-Kohärenz; `WiedergabeStarten_ZweiterAufruf_IstNoOp` verifiziert die Idempotenz unter der neuen Lauf-Flag-Semantik. |
| `KonsolenTestViewModelTests` (Bestand) | Keine Änderung erwartet — die `null`-Selektion bei Index 0 betrifft keinen Bestandspfad (Bestandstests setzen Selektion nur bei Index > 0); `Wiedergabe_LaeuftBisEnde_UndSynchronisiertQuellAnsicht` u. a. bleiben unverändert gültig. |

### E2E-Tests (primärer Funktionsnachweis)

Das bestehende konsolidierte Szenario wird um Schritt-Phasen erweitert (kein neuer FlaUI-Test — Konsolidierung im selben Dialog-/App-Lifecycle laut Anforderung und CLAUDE.md). Neue synthetische `.clireplay` mit drei inhaltlich unterscheidbaren Chunks (`schrittPfad`, Temp-Datei, Cleanup im vorhandenen `finally`).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Laden → 2× `SchrittVor` → Position „Chunk 2/3" + Quell-Selektion auf Eintrag 1 → `SchrittZurueck` → „Chunk 1/3" + Selektion 0 → weiter zurück → „Chunk 0/3" + leere Selektion + `SchrittZurueck` deaktiviert | `E2E_KonsolenTestfenster.cs` (`KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E`, in `RunGeneralTests`) | Einzelschritt vorwärts/rückwärts ohne Start; Positionsanzeige und Quell-Chunk-Selektion spiegeln die Schrittposition; Rand-Deaktivierung an Position 0 | Benutzerfluss über die echten Buttons — Command-Verdrahtung, CanExecute → `IsEnabled`, `ScrollIntoView`-Selektion und Positionsanzeige sind nur über UI-Automation nachweisbar. |
| Pflicht | `SchrittVor` bis ans Ende → Status „Wiedergabe beendet." + `SchrittVor` deaktiviert → `SchrittZurueck` → „Chunk 2/3" → `StartWiedergabe` (Zeitraffer 0) setzt an Position fort → erneut „beendet" | dto. | Ende-Semantik per Schritt; Rückwärtsschritt nach Ende; „Abspielen" setzt an der Schrittposition fort (Re-Arm) | Der Zustandsübergang Session-ende → zurück → fortsetzen überspannt Session-Re-Armierung, ViewModel-Flags (`_wiedergabeBeendet`) und Button-Verfügbarkeit — nur E2E prüft das Zusammenspiel aller Schichten. |
| Pflicht | Auf `pausePfad` (bestehend, 3-s-Pause): `StartWiedergabe` → „Chunk 1/2" → `PausierenToggle` → `SchrittZurueck` → „Chunk 0/2" → `PausierenToggle` (Fortsetzen) → Position läuft erneut über „Chunk 1/2" bis „Wiedergabe beendet." | dto. | Zusammenspiel „pausiert → Schritte → fortsetzen": die Wiedergabe-Schleife setzt an der durch Schritte veränderten Position fort und wendet den zurückgenommenen Chunk erneut an | Kernanforderung des Features (konsistente Positionsführung); die Koordination aus Pause-Gate, Schritt und Fortsetzen ist ein Laufzeit-Zusammenspiel, das E2E am realen Dialog verifiziert. |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (`E2E_KonsolenTestfenster.cs`) | Erweiterung um die drei Schritt-Phasen im selben Dialog-Lifecycle (Reihenfolge innerhalb des Szenarios so legen, dass die `pausePfad`-Phase das Fortsetzen-Zusammenspiel mit abdeckt; `schrittPfad` als dritte Temp-Datei ergänzen). |

## Offene Punkte

Keine — die zuvor offenen Fragen der Anforderung wurden entweder mit dem Anwender geklärt (Vor-/Rückwärtsschritt, Positions-Spiegelung, Fortsetzen-Konsistenz, `_parser`-Reset) oder sind in den Designentscheidungen festgelegt (Schritt bei laufender Wiedergabe → CanExecute-Sperre; Schreiten ohne Start → erlaubt; Ende per Schritt → `Exited`; Fortsetzen → aufgezeichnete Pause gilt erneut; `OutputChunk` → nur im Vorwärtsschritt; `StatusText` → eigener Schritt-Hinweis + Quell-Selektion; `RuntimeStatus` → unverändertes Modell; Rückwärts-Performance → naiver O(n)-Rebuild).
