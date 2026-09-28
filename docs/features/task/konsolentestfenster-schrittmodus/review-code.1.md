# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### KonsolenTestViewModel.cs (KonsolenTestViewModel)

- **Fehlende Validierung von Vorbedingungen** — `SchrittVor()` (Z. 359–368) und `SchrittZurueck()` (Z. 370–379) prüfen nur `_replaySession is null`, nicht den Wiedergabe-Zustand. `RelayCommand.Execute` (ViewModelBase.cs Z. 72) wertet `CanExecute` nicht aus — ein programmatisches `Execute` (Tastatur-Shortcut, künftiger Aufrufer, Test) während unpausierter Wiedergabe mutiert die Position mitten im laufenden Durchlauf. Alle Geschwister-Handler (`WiedergabeStarten` Z. 302, `WiedergabeNeustarten` Z. 319, `WiedergabePausierenToggle` Z. 342) haben einen internen Zustands-Guard, der ihrer CanExecute-Bedingung entspricht.

  Empfehlung: In beiden Methoden den CanExecute-Zustandsteil nachziehen, z. B. `if (_replaySession is null || (IstWiedergabeAktiv && !IstPausiert)) return;` (Positionsgrenzen prüft die Session bereits über ihren Rückgabewert).

- **Fehlermeldung ohne aussagekräftigen Kontext** — `SchrittZurueck()` setzt denselben StatusText wie `SchrittVor()`: „Einzelschritt — Chunk {x}/{y} angewendet." (Z. 378). Beim Rückwärtsschritt wurde gerade ein Chunk **zurückgenommen**, nicht angewendet — die Meldung beschreibt das falsche fachliche Ereignis.

  Empfehlung: Eigenen Text verwenden, z. B. `Schritt zurück — Chunk {alt}/{n} zurückgenommen.` oder „Zustand vor Chunk {x+1} wiederhergestellt."

### TerminalReplaySession.cs (TerminalReplaySession)

- **Concurrency / dokumentierte Atomizität zu eng** — `RaiseExited(nurAmEnde: true)` (Z. 424–433): Positions-Check und `_exitedSignaled`-CAS sind unter `_renderLock` atomar gegen `SchrittZurueck` serialisiert — das `Exited?.Invoke` (Z. 442) erfolgt jedoch erst nach Lock-Freigabe. Ein in die Lücke fallender `SchrittZurueck` verlässt das Ende und re-armiert `_exitedSignaled`; das Event feuert anschließend bei Position < Ende, und ein späteres Wiedererreichen des Endes feuert erneut. Der `<param>`-Kommentar (Z. 416–419) verspricht „atomar unter `_renderLock` geprüft/gesetzt (Serialisierung gegen `SchrittZurueck`)" — das gilt nur für Flag+Check, nicht für das Signal selbst. Über die aktuelle UI-Verdrahtung praktisch unerreichbar (Schritt-Buttons bei unpausierter Schleife deaktiviert; VM-Aufrufe single-threaded), auf der öffentlichen API-Ebene aber inkonsistent. Nebenbei gehört `Interlocked.Exchange(ref _exitedSignaled, 0)` in `WiedergabeStarten` (Z. 130) in dieselbe Familie: nicht unter `_renderLock`, kann ein gerade per CAS gesetztes Flag eines nebenläufigen `SchrittVor`-End-`RaiseExited` wieder löschen (→ Doppel-`Exited` am Ende des neuen Durchlaufs).

  Empfehlung: Bewusste Entscheidung festhalten und Dokumentation präzisieren (`Exited` = „Ende wurde erreicht"-Ereignis, kein Positions-Snapshot). Falls das Event selbst atomar sein soll: Invoke unter `_renderLock` (reentrant für Same-Thread-Handler, aber Deadlock-Risiko bei cross-thread-blockierenden Handlern — dann explizit als Verbot im Event-Vertrag dokumentieren).

### TerminalReplaySessionTests.cs (TerminalReplaySessionTests)

- **Testqualität — Implementierungsdetail** — `GetPlaybackTask` (Reflection auf privates Feld `_playbackTask`, am Dateiende hinter `GetReadLoopTask`) koppelt die Tests an ein internes Feld statt an beobachtbares Verhalten. Begründet ist es (deterministischer Wartepunkt für die Re-Armierung), aber es bricht bei Umbenennung/Umstrukturierung ohne Funktionsänderung.

  Empfehlung: Entweder als bewussten Trade-off im Kommentar der Methode festhalten oder über öffentliches Verhalten warten (z. B. `WarteBisAsync` auf erfolgreichen `WiedergabeStarten`-Aufruf + `exitedCount`-Anstieg). Niedrige Priorität.

### KonsolenTestDialogView.cs (KonsolenTestDialogView)

- **Überflüssiger Code (redundante Doppelauswertung)** — `GetSelektierterQuellEintragIndex()` (Z. 201–219) liest zuerst `pattern.Selection` (komplette Selektionsliste nur für die Leer-Prüfung) und traversiert danach alle Kindzeilen erneut mit `IsSelected`-Abfrage. Die selektierten Elemente stehen bereits in `selection`; die Indexermittlung ließe sich auf einen einzigen Vergleich der Selektionselemente mit den Kindzeilen reduzieren (statt zwei getrennter UIA-Abfragen).

  Empfehlung: `selection[0]` aus `pattern.Selection` direkt gegen die Kindzeilen matchen (Referenz/`Equals`) oder die Vorauswahl streichen und nur die Kind-Traversierung mit `IsSelected` behalten.

## Positiv festgestellt (keine Befunde)

- **Schritt-Guards unter `_renderLock`:** `SchrittVor`/`SchrittZurueck` mutieren `_abgespielteChunks`, `_aktuellerChunkIndex`, Buffer und Parser-Zustand vollständig unter `_renderLock` — atomar gegenüber der Schleife.
- **In-flight Loop vs. `SchrittZurueck`:** atomar gelöst — Präfix-Kürzung und Apply konkurrieren um `_renderLock`; der Positions-Re-Check `Count != i` (Z. 348) im selben Lock verwirft veraltete Iterationszustände korrekt; das Delay wird für die neue Position frisch berechnet.
- **Pause-Gate-Zusammenspiel:** Invariante „`_istPausiert` ⇒ Gate geschlossen" über `_pauseLock`-Reihenfolge korrekt; `SchrittVor`-End-Pfad öffnet das Gate per `Fortsetzen()` und terminiert eine parkende Schleife sauber (Z. 194–201).
- **Event-Verdrahtung inkl. Abmeldung:** `BufferChanged`/`Exited` werden in `ErzeugeReplaySession` angemeldet und in `EntsorgeReplaySession` vor `Dispose` abgemeldet; Sender-Identitäts-Check in den Handlern verhindert Cross-Session-Verschmutzung.
- **ZeitrafferSchwelle-Interaktion:** Einzelschritte sind bewusst schwellen-unabhängig (dokumentiert), Fortsetzen nach Rückwärtsschritt re-armiert das Inter-Chunk-Delay korrekt.
- **Kein Deadlock:** `OutputChunk`/`Exited` werden unter bzw. nach `_renderLock` gefeuert; alle UI-Marshal-Pfade (`TerminalControl.OnBufferChanged` → `InvokeAsync`, VM → `Dispatcher.Invoke` aus Loop-Thread ohne gehaltenen Lock) sind blockfrei bzw. ohne hold-and-wait. `_pauseLock`/`_renderLock` werden nirgends verschachtelt gehalten.
- **Parser-Kohärenz:** `BaueBufferUndParserAusPraefixNeuAuf` setzt jetzt auch `_parser` zurück — behebt den latenten Inkonsistenzpunkt des alten `RebuildBufferFromReplay` (Wegwerf-Parser vs. `_parser`-Restzustand), getestet über chunk-übergreifende Escape-Sequenz.
- **E2E-Konsolidierung:** Schritt-Szenarien sind in den bestehenden Test integriert (Projektvorgabe: minimale FlaUI-Methodenzahl), `schrittPfad` wird im `finally` aufgeräumt.

## Geprüfte Dateien

- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs` (Kontext)
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs` (Kontext: `RelayCommand.Execute`-Semantik)
- `src/Softwareschmiede.App/Services/DispatcherInvokeFactory.cs` (Kontext: synchrone Dispatcher-Ausführung)
- `src/Softwareschmiede.App/Controls/TerminalControl.cs` (Kontext: BufferChanged-Marshal-Pfad)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplaySessionTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/KonsolenTestViewModelTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs`
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs`
- `docs/features/task/konsolentestfenster-schrittmodus-tasks.md`, `docs/features/task/konsolentestfenster-schrittmodus/todo.md` (Prozessdoku, keine Code-Befunde)
