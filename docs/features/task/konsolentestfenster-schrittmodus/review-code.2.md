# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TerminalReplaySession.cs (TerminalReplaySession)

- **Concurrency / Zustandsmaschine** — `SchrittVor`-Endpfad (Z. 205–212): `RaiseExited` feuert `Exited`, danach weckt `Fortsetzen()` eine evtl. pausiert parkende Wiedergabe-Schleife, damit sie `Position == Count` erkennt und terminiert. Die Termination ist jedoch rein positionsbasiert (`i >= chunks.Count`, Z. 327) — zwischen `TrySetResult()` und dem Auflesen der Position unter `_renderLock` liegt ein unbeschränktes Scheduling-Fenster. Fällt ein `SchrittZurueck` in dieses Fenster, liegt die Position unter `Count`, die Schleife bricht **nicht** ab, sondern nimmt die zeitgesteuerte Wiedergabe an der zurückgesetzten Position wieder auf: Chunks werden mit realem Delay angewendet, am Ende feuert `Exited` erneut. Der VM-Zustand ist zu diesem Zeitpunkt bereits „inaktiv" (`IstWiedergabeAktiv = false` durch das erste `Exited`): Der Pause-Button ist deaktiviert (die Geister-Wiedergabe ist nicht anhaltbar) und beide Schritt-Buttons sind aktiv, während die Schleife die Position mutiert — genau die Mutation mitten im Lauf, die die internen Guards verhindern sollen. Menschlich kaum treffbar (Sub-ms-Fenster), aber prinzipiell unbeschränkt und für programmatische Aufrufer (Tests, UI-Automatisierung, künftige Konsumenten) deterministisch erreichbar.

  Empfehlung: Dediziertes Terminations-Flag statt rein positioneller Termination — z. B. `_schleifeBeendenAngefordert`, das der `SchrittVor`-Endpfad unter `_renderLock` setzt (vor `Fortsetzen()`), die Schleife atomar mit der Position unter `_renderLock` prüft (`if (_schleifeBeendenAngefordert || i >= chunks.Count) break;`) und `WiedergabeStarten` am selben Lock-Punkt wie den `_exitedSignaled`-Reset zurücksetzt. `SchrittZurueck` darf es **nicht** löschen (der Lauf ist beendet; Fortsetzen ist Aufgabe von `WiedergabeStarten`) — nur so wird das Aufweck-Fenster geschlossen statt nur verschoben. `_exitedSignaled` selbst ist als Flag ungeeignet, weil `SchrittZurueck` es re-armiert (Z. 235).

- **Doppelter Code (niedrige Priorität)** — Die Chunk-Anwendereihenfolge ist dupliziert: `SchrittVor` (Z. 194–199) vs. Schleifen-Apply (Z. 362–367) — jeweils `OutputChunk?.Invoke` → `_abgespielteChunks.Add` → `_parser.Parse`/`Buffer.Apply` → `_aktuellerChunkIndex`-Write. Die identische Reihenfolge ist korrektheitsrelevant (Kommentar „Dieselbe Anwendereihenfolge wie die Wiedergabe-Schleife" verweist darauf), bei einer Änderung an nur einer Stelle divergieren die Pfade still.

  Empfehlung: In eine private Methode `WendeChunkAnUnterLock(int position)` auslagern (Vertrag: Aufruf nur unter `_renderLock`) und an beiden Stellen aufrufen; Schleifen-spezifische Vorprüfungen (`_istPausiert`, Positions-Re-Check) bleiben in der Schleife.

## Verifizierte Runde-1-Korrekturen (keine Befunde)

- **Interne Guards** in `SchrittVor`/`SchrittZurueck` (VM, Z. 374, 388): `IstWiedergabeAktiv && !IstPausiert` → No-Op, deckt den `RelayCommand.Execute`-Semantik-Verstoß ab; mit `SchrittCommands_Execute_WaehrendLaufenderWiedergabe_SindNoOp` getestet.
- **`_exitedSignaled`-Reset in `WiedergabeStarten` unter `_renderLock`** (Z. 138–141): serialisiert gegen `RaiseExited(nurAmEnde: true)` und `SchrittZurueck`; das in Runde 1 benannte CAS-Reset-Fenster ist geschlossen (Flag-Reset im `finally` der Schleife erfolgt erst nach `RaiseExited` — ein neuer Start kann das Flag daher keinem noch ausstehenden Signal der alten Schleife „unter den Füßen wegziehen", das dessen CAS bereits setzte).
- **Deadlock-Begründung für `Exited`-Invoke nach Lock-Freigabe (Z. 431–437) ist korrekt:** `OnReplayExited` marshalt synchron per `Dispatcher.Invoke` (`DispatcherInvokeFactory` Z. 16). Ein Invoke unter `_renderLock` auf dem Schleifen-Thread plus zeitgleich blockierendem UI-Thread in `SchrittZurueck`/`Pausieren` wäre ein klassischer Deadlock. `Exited` ist als Flanken-Ereignis („Ende wurde erreicht", kein Positions-Snapshot) konsistent dokumentiert (Z. 105–110) — passt zur bewusst nicht-atomaren Semantik.
- **Statustext „Schritt zurück — Chunk n/y zurückgenommen."** (Z. 400) benennt korrekt den zurückgenommenen Chunk (alte Position = 1-basierte `#`-Zeile); `#`-Spalte 1-basiert (`Index = i + 1`, Z. 268; `CliChunkAnzeigeEintrag.Index`-Doc aktualisiert; kein weiterer `Index`-Konsument mit 0-basierter Annahme).
- **`GetSelektierterQuellEintragIndex`** auf einfache Kind-Traversierung mit `IsSelected` reduziert (DialogView Z. 198–213); **`GetPlaybackTask`-Reflection** als bewusster Trade-off mit korrekter Begründung dokumentiert (öffentliches Verhalten bietet keinen Wartepunkt auf Schleifen-Termination).
- **`WiedergabeNeustartenCommand`**: CanExecute um `AktuellerChunkIndex > 0` erweitert, Handler-Guard äquivalent nachgezogen (Z. 325–327), `IstWiedergabeAktiv = true` im neuen Schrittmodus-Pfad gesetzt (Z. 334) — ohne das wäre der VM-Zustand inkonsistent zur laufenden neuen Wiedergabe.
- **WrapPanel-Toolbar** (XAML Z. 26): behebt das Abschneiden von PositionsText/„Schließen".

## Positiv festgestellt (keine Befunde)

- **Lock-Ordnung sauber:** `SchrittVor`/`SchrittZurueck` halten `_renderLock` nie beim Aufruf von `Fortsetzen()`/`WartePauseGateAsync` (kein `_pauseLock`-unter-`_renderLock`-Nesting); einziges Nesting ist `Pausieren` (`_pauseLock` → `_renderLock`-Fence) — keine umgekehrte Reihenfolge im Code.
- **Event-Verdrahtung inkl. Abmeldung:** `BufferChanged`/`Exited` werden in `ErzeugeReplaySession` angemeldet und in `EntsorgeReplaySession` vor `Dispose` abgemeldet; Sender-Identität wird doppelt geprüft (vor und innerhalb des Dispatcher-Blocks), in-flight Events der Vorgänger-Session können den neuen Zustand nicht verschmutzen.
- **`_wiedergabeBeendet`-Kohärenz:** wird in `OnReplayExited`, `LadeAufzeichnung`, `ErsetzeReplaySessionDurchFrische` gesetzt/gelöscht und jetzt auch in `SchrittZurueck` (Z. 397) — „Abspielen" nach „beendet → zurück" setzt korrekt auf derselben Session an der Schrittposition fort statt eine frische zu erzeugen (durch `SchrittVor_BisEnde_ZeigtStatusBeendet` mit `BeSameAs`-Assert belegt).
- **Re-Armierung der Schleife:** `_wiedergabeLoopAktiv`-Reset steht im `finally` **nach** `RaiseExited` — ein darin synchron durch `Dispatcher.Invoke` blockierter `Exited`-Handler kann daher die Re-Armierung nicht überholen; CAS-Fail eines zu frühen `WiedergabeStarten` ist funktional harmlos, weil die alte Schleife ohnehin an der Position fortsetzt.
- **`OnReplayBufferChanged`**: `AktuellerQuellEintrag` wird bei Position 0 jetzt auf `null` gesetzt (kein stale Selection) und `RelayCommand.Refresh()` triggert die CanExecute-Neuauswertung der Schritt-Commands auch ohne Zustandswechsel.
- **Testqualität:** Neue Unit-Tests sind AAA-strukturiert, prüfen beobachtbares Verhalten (Buffer-Text, Event-Sequenzen, ExitCode null); der Determinismus der FakeTimeProvider-Sequenzierung (wiederholte `Advance`-Polls statt Einzel-Advance) ist kommentiert und korrekt begründet; E2E-Phasen sind in den bestehenden FlaUI-Test konsolidiert (Projektvorgabe minimale Methodenzahl), `schrittPfad` wird im `finally` aufgeräumt.
- **Build:** `dotnet build Softwareschmiede.slnx -c Debug` — 0 Fehler, 0 Warnungen.

## Geprüfte Dateien

- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs` (Kontext)
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs` (Kontext: `RelayCommand.Execute`-Semantik, `SetProperty`-Callback)
- `src/Softwareschmiede.App/Services/DispatcherInvokeFactory.cs` (Kontext: synchrone Dispatcher-Ausführung — Deadlock-Begründung)
- `src/Softwareschmiede.App/Controls/TerminalControl.cs` (Kontext: `BufferChanged`-Marshal-Pfad `InvokeAsync`)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplaySessionTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/KonsolenTestViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs`
- `docs/features/task/konsolentestfenster-schrittmodus-tasks.md`, `docs/features/task/konsolentestfenster-schrittmodus/todo.md` (Prozessdoku, keine Code-Befunde)
