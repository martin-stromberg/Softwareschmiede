# Bestandsaufnahme: Konsolentestfenster — Schrittmodus (vorwärts/rückwärts)

Bestandsaufnahme des bestehenden Konsolentestfensters (`KonsolenTestDialog`/`KonsolenTestViewModel`/`TerminalReplaySession`) und seiner Test-Infrastruktur, bezogen auf die Anforderung in `requirement.md`: Einzelschritt-Wiedergabe vorwärts (nächster Chunk, zeitstempel-unabhängig) und rückwärts (deterministischer Präfix-Rebuild `Chunks[0..n-1]`). Baut auf der Bestandsaufnahme des Vorgänger-Features `docs/features/task/konsolentestfenster-cli-replay/inventory.md` auf und wurde gegen den aktuellen Stand verifiziert — der Branch enthält die komplette Implementierung des Vorgänger-Features (Commits `eaaa502`, `e67e1fb`).

## Zusammenfassung

**Vorhanden und direkt nutzbar:**

- `TerminalReplaySession` (`src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`) spielt eine `CliOutputAufzeichnung` bereits durch den echten Renderpfad ab (`AnsiSequenceParser` → `TerminalBuffer` → `BufferChanged`) mit Pause/Fortsetzen/Zeitraffer — Positionsführung über `_aktuellerChunkIndex` + `_abgespielteChunks`, alle Locks (`_renderLock`, `_pauseLock`/`_pauseGate`, `_statusLock`) vorhanden.
- `RebuildBufferFromReplay()` (Z. 187–197) baut den `Buffer` synchron unter `_renderLock` mit einem **frischen** `AnsiSequenceParser` aus `_abgespielteChunks` neu auf — direkter Mechanismus-Vorbild für den Rückwärtsschritt.
- `AnsiSequenceParser.Reset()` (Z. 182–188) und `TerminalBuffer.Reset()` (Z. 159–178) existieren — der `_parser` der Session ist jedoch `readonly` und kumuliert Zustand (Chunk-übergreifende Escape-Sequenzen/UTF-8-Decoder), er muss beim Rückwärtsschritt ersetzt oder zurückgesetzt werden.
- `KonsolenTestViewModel` synchronisiert `PositionsText` („Chunk x/y") und `AktuellerQuellEintrag` bereits vollständig über das `BufferChanged`-Event (`OnReplayBufferChanged`, Z. 350–367) — Schritte können denselben Pfad nutzen; `ScrollIntoView` in `KonsolenTestDialog.xaml.cs` (Z. 29–33) folgt der Selektion automatisch.
- Wiedergabe-Commands (`RelayCommand` mit CanExecute + `RelayCommand.Refresh()`-Impuls in `IstWiedergabeAktiv`-Setter, Z. 121–128) als Muster für die neuen Schritt-Commands.
- Test-Infrastruktur komplett: `FakeTimeProvider` in `TerminalReplaySessionTests`/`KonsolenTestViewModelTests`, `WarteBisAsync`-Polling, synchroner `dispatcherInvoke`-Hook im ViewModel-Test, E2E-Wrapper `KonsolenTestDialogView` mit `WaitForEnabledElement`-Muster, konsolidiertes E2E-Szenario in `RunGeneralTests` (`MainTest.cs:36`).

**Nicht vorhanden (zentral für die Anforderung):**

- Keine Schritt-Methoden auf `TerminalReplaySession` (kein `SchrittVor`/`SchrittZurueck` o. ä.) und keine Schritt-Commands/-Buttons im ViewModel/Dialog.
- `WiedergabeLoopAsync` führt einen **lokalen** Schleifenindex `i` (`for (var i = 0; i < chunks.Count; i++)`, Z. 233) — `_aktuellerChunkIndex`/`_abgespielteChunks` sind nur abgeleitete Zähler; ein `Fortsetzen` nach einem Rückwärtsschritt würde die Schrittposition ignorieren und `_abgespielteChunks` desynchronisieren.
- `_abgespielteChunks` wird ausschließlich per `Add` befüllt (Z. 270) — es gibt keinen Pfad, der die Liste kürzt (für den Rückwärtsschritt nötig).
- `Exited`-/`RuntimeStatus`-Semantik ist nur für den vollständigen Schleifendurchlauf definiert (`RaiseExited`, Z. 311–327) — nicht für schrittweises Erreichen des Endes.

**Test-Ausgangszustand:** Build erfolgreich (0 Fehler, 1 bekannte Warnung `CS8602`); alle vier CI-Testlanes ausgeführt — **1989 bestanden, 0 fehlgeschlagen, 3 übersprungen** (2 ConPTY-E2E-Runner via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, 1 Nicht-Windows-Test per Design). Das konsolidierte Konsolentestfenster-E2E-Szenario ist in `RunGeneralTests` enthalten und **bestanden** (Lauf 8 m 30 s). Details und TRX-Reports: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `CliOutputAufzeichnung`, `CliOutputChunkRecord`, `CliChunkAnzeigeEintrag`, `TerminalBuffer`/`TerminalBufferSnapshot`, `TerminalReplayBuffer`, Event-Args
- [Logik](inventory/logic.md) — `TerminalReplaySession` (Schrittmodus-relevante Interna im Detail), `KonsolenTestViewModel`, `KonsolenTestDialog`, `TerminalControl`, `CliReplayAufzeichnungStore`, `CliOutputRecorder`, `CliChunkQuelltextFormatter`, `AnsiSequenceParser`, `PseudoConsoleSession` (Referenzpfad), Einstiegspunkt (`SettingsViewModel`/`WpfDialogService`), `RelayCommand`/`AsyncRelayCommand`
- [Enums](inventory/enums.md) — `CliRuntimeStatus`, `AnsiSequenceParser.State`
- [Interfaces](inventory/interfaces.md) — `ITerminalSession`, `ITerminalOutputSink`, `IDialogService`
- [Tests](inventory/tests.md) — Test-Ausgangszustand (Zeitpunkt, Commit, Umgebung, Läufe mit TRX-Nachweisen), anforderungsrelevante Testklassen und Hilfsmethoden
