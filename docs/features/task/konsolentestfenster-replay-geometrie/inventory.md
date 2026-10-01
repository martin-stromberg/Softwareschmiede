# Bestandsaufnahme: Konsolentestfenster — Wiedergabe in aufgezeichneter Terminal-Geometrie

Bestandsaufnahme des Konsolentestfensters (`KonsolenTestDialog`/`KonsolenTestViewModel`/`TerminalReplaySession`) und des `TerminalControl` (inkl. `IScrollInfo`), bezogen auf die Anforderung in `requirement.md`: `.clireplay`-Wiedergabe soll mit der aufgezeichneten Header-Geometrie (`CliOutputAufzeichnung.Cols`/`Rows`) rendern statt mit der Control-Größe; überschüssige Breite muss horizontal scrollbar sein; die Live-Darstellung in `TaskDetailView` darf sich nicht ändern. Baut auf der Bestandsaufnahme des Vorgänger-Features `konsolentestfenster-schrittmodus` auf (im HEAD-Commit `6d43f59` aus dem Arbeitsbaum entfernt, aber über `git show HEAD~1:...` nachvollziehbar) und wurde gegen den aktuellen Stand verifiziert.

## Zusammenfassung

**Vorhanden und direkt nutzbar:**

- `TerminalReplaySession` erzeugt ihren `Buffer` bereits im Konstruktor mit der Aufzeichnungs-Geometrie `Max(1, aufzeichnung.Cols)`/`Max(1, aufzeichnung.Rows)` (`TerminalReplaySession.cs` Z. 67) und verfügt über den Schrittmodus (`SchrittVor`/`SchrittZurueck`, Z. 253/292) samt deterministischem Präfix-Rebuild (`BaueBufferUndParserAusPraefixNeuAuf`, Z. 494–501).
- `CliReplayAufzeichnungStore.LadeAsync` garantiert `Cols > 0`/`Rows > 0` (Validierung Z. 131–132) — die aufgezeichnete Geometrie ist immer vorhanden und gültig. Referenz-Aufzeichnung im Arbeitsbaum (untracked): `cli-replay-a255276dd4e848039bab82ed51400729.clireplay` — **220×50, 486051 Chunks, `IstVollstaendig=false`, Plugin „Devin CLI"** (Header ausgelesen und verifiziert).
- `TerminalControl` implementiert `IScrollInfo` bereits vollständig für **vertikales** Scrollen (`_verticalOffset`, `_extentHeight`, `UpdateScrollInfo` Z. 446–466, `SetVerticalOffset` mit Alternate-Screen-Klemmung Z. 420–437) — das Muster ist für die horizontale Achse direkt übertragbar. `ScrollOwner`-Verdrahtung durch den umgebenden `ScrollViewer` (`CanContentScroll="True"` in beiden Views) funktioniert.
- `TerminalBuffer` exponiert `Cols`/`Rows` als öffentliche Getter (Z. 50–59) — das Control kann die Buffer-Geometrie lesen.
- Test-Infrastruktur komplett: WPF-Unit-Tests via `RunOnSta` (`WpfUnitTestHelpers`), `TerminalControl` bereits in einem echten `ScrollViewer` arrangierbar (`ScrollViewerLayout_CanContentScroll_BegrenztViewportAufSichtbareHoehe`), `FakeTimeProvider`, E2E-Wrapper `KonsolenTestDialogView`, konsolidiertes E2E-Szenario in `RunGeneralTests` (`MainTest.cs` Z. 36).

**Nicht vorhanden (zentral für die Anforderung):**

- Kein Capability-Merkmal am `ITerminalSession`-Vertrag, mit dem `TerminalControl` eine fixe Geometrie erkennen könnte: kein `IsGeometryFixed`/`SupportsResize`/`FixedCols`-Member und kein separates Interface. `Resize`-Rückgabewert `false` ist **nicht** eindeutig — `PseudoConsoleSession.Resize` (Z. 172–188) liefert `false` bereits für ungültige Werte und fehlgeschlagene PTY-Calls; `TerminalReplaySession.Resize` liefert immer `true` (No-Op-Stub, Z. 314).
- Horizontales Scrollen ist in `TerminalControl` komplett stubbed: `CanHorizontallyScroll` (Z. 58, default `false`), `ExtentWidth => ViewportWidth` (Z. 61), `HorizontalOffset => 0` (Z. 73), `LineLeft`/`LineRight` (Z. 373–380), `PageLeft`/`PageRight` (Z. 389–396), `MouseWheelLeft`/`MouseWheelRight` (Z. 405–412), `SetHorizontalOffset` (Z. 415–417) — alle leer. `OnRender` zeichnet alle X-Koordinaten ohne horizontalen Offset (`c * _cellWidth` Z. 210, Hintergrund-Runs Z. 187, Cursor Z. 218–220).
- `TerminalControl` erzwingt die Control-Geometrie an zwei Stellen: `OnSessionChanged` → `_buffer.Resize(cols, rows)` (Z. 126, nach `RebuildBufferFromReplay` Z. 124) und `OnRenderSizeChanged` → `buffer.Resize` + `session.Resize` (Z. 310–311). Genau dieses Verhalten zerstört die Aufzeichnungs-Geometrie.
- `TerminalBuffer.Reset()` (Z. 159–178) erhält `_cols`/`_rows` — ein einmal vom Control verkleinerter Buffer bleibt auch nach `RebuildBufferFromReplay`/`SchrittZurueck` in der falschen Geometrie (verifiziert; die Anforderung beschreibt das korrekt).
- `KonsolenTestDialog.xaml` Z. 136–143: `ScrollViewer` um `ReplayTerminal` hat `HorizontalScrollBarVisibility="Disabled"` — horizontaler Scrollbalken ist derzeit unmöglich.
- Kein UI-/Geometrie-Hinweis im Dialog (StatusText/PositionsText enthalten keine Geometrie-Angabe).
- `TempReplayDiagnoseTests.cs` (in der Anforderung als „untracked, vor Commit zu entfernen" erwähnt) **existiert nicht** im Arbeitsbaum — nichts zu entfernen.

**Test-Ausgangszustand:** Voller Debug-Build **nicht möglich** — MSB3027/MSB3021, weil eine laufende `Softwareschmiede.App.exe` (PID 36992, gestartet aus dem Repo-`bin\Debug`) und Visual Studio (`devenv.exe`, PID 18668) `Softwareschmiede.dll` und `Softwareschmiede.Plugin.Contracts.dll` im App-Ausgabeverzeichnis sperren (Infrastrukturproblem, kein Test-/Codefehler; der Prozess wurde **nicht** beendet — Self-Hosting-Regel). Ausweich-**Release**-Build erfolgreich (0 Fehler, 1 bekannte Warnung `CS8602`); alle vier CI-Testlanes in Release ausgeführt — **2010 entdeckte Tests: 2007 bestanden, 0 fehlgeschlagen, 3 übersprungen** (2 ConPTY-E2E-Runner via `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, 1 Nicht-Windows-Test per Design). `RunGeneralTests` (8 m 37 s) enthielt das konsolidierte Konsolentestfenster-Szenario und **bestand** — gestartet wurde dabei die vorhandene Debug-App.exe (`ResolveAppExePath` bevorzugt Debug, `WpfTestBase.cs` Z. 960–974; die Debug-Binaries vom 01.10. 06:26 entsprechen HEAD). Details und TRX-Reports: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `CliOutputAufzeichnung`, `CliOutputChunkRecord`, `CliChunkAnzeigeEintrag`, `TerminalBuffer`/`TerminalBufferSnapshot`, `TerminalSessionOptions`
- [Logik](inventory/logic.md) — `TerminalControl` (Geometrie/IScrollInfo im Detail), `TerminalReplaySession`, `KonsolenTestViewModel`, `KonsolenTestDialog`, `PseudoConsoleSession` (Referenzpfad), `CliReplayAufzeichnungStore`, `CliOutputRecorder`, `AnsiSequenceParser`, `TaskDetailView`-Nutzung, Einstiegspunkt, `RelayCommand`/`DispatcherInvokeFactory`
- [Enums](inventory/enums.md) — `CliRuntimeStatus`, `AnsiSequenceParser.State`, `ScrollBarVisibility` (WPF, verwendete Werte)
- [Interfaces](inventory/interfaces.md) — `ITerminalSession` (Resize-Vertrag), `ITerminalOutputSink`, `IDialogService`, `IScrollInfo` (WPF-Vertrag und Umsetzungsstand)
- [Tests](inventory/tests.md) — Test-Ausgangszustand (Zeitpunkt, Commit, Umgebung, Build-Sperre, vier Läufe mit TRX-Nachweisen), anforderungsrelevante Testklassen und Hilfsmethoden
