# Tasks: Konsolentestfenster — Wiedergabe in aufgezeichneter Terminal-Geometrie

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `ITerminalSession.SupportsResize` (`bool`) mit XML-Doku ergänzen | Offen | — |
| 2 | Logik | `PseudoConsoleSession.SupportsResize => true` implementieren | Offen | — |
| 3 | Logik | `TerminalReplaySession.SupportsResize => false` implementieren; Klassendoku (Z. 8–14) und `Resize`-Doku auf Fixiert-Geometrie-Semantik umstellen | Offen | — |
| 4 | Logik | `TerminalControl`: `_horizontalOffset`-Feld; `ExtentWidth` (Buffer-Cols × `_cellWidth`, Gate auf `CanHorizontallyScroll`) und `HorizontalOffset` echt implementieren; `ScrollableWidth`-Ausdruck ergänzen | Offen | — |
| 5 | Logik | `TerminalControl`: `SetHorizontalOffset` (klemmen + `InvalidateScrollInfo` + `InvalidateVisual`), `LineLeft`/`LineRight` (±`_cellWidth`), `PageLeft`/`PageRight` (±`ViewportWidth − _cellWidth`), `MouseWheelLeft`/`MouseWheelRight` (±3 Zellen) implementieren | Offen | — |
| 6 | Logik | `TerminalControl.OnSessionChanged`: `_buffer.Resize` nur bei `SupportsResize`; `_horizontalOffset` beim Wechsel und bei `null` zurücksetzen | Offen | — |
| 7 | Logik | `TerminalControl.OnRenderSizeChanged`: `buffer.Resize`/`session.Resize` nur bei `SupportsResize`; `UpdateScrollInfo`/`InvalidateVisual` beibehalten | Offen | — |
| 8 | Logik | `TerminalControl.OnRender`: `PushClip` + `PushTransform(-_horizontalOffset)` um den Inhalts-Block; Spalten-Culling auf sichtbaren Bereich | Offen | — |
| 9 | Logik | `TerminalControl.MeasureOverride`: Infinite-Breite aus `_buffer?.Cols` ableiten; `UpdateScrollInfo` um `_horizontalOffset`-Klemmung erweitern | Offen | — |
| 10 | UI | `KonsolenTestViewModel`: `GeometrieText`-Property („Aufzeichnung: {Cols}×{Rows}"); setzen in `LadeAufzeichnung`/`ErsetzeReplaySessionDurchFrische`, zurücksetzen in `EntsorgeReplaySession` | Offen | — |
| 11 | UI | `KonsolenTestDialog.xaml`: `HorizontalScrollBarVisibility="Auto"` am `ReplayTerminal`-ScrollViewer; `x:Name`/`AutomationProperties.Name="ReplayTerminalScrollViewer"`; `TextBlock AufzeichnungGeometrie` (Text + HelpText gebunden) in der Werkzeugleiste | Offen | — |
| 12 | Tests | `TerminalReplaySessionTests.Stubs_SindUnschaedlich`: `SupportsResize == false`-Assert + Kommentar-Anpassung; neuen Test `Geometrie_BleibtUeberSchritteUndRebuild_Fixiert` ergänzen | Offen | — |
| 13 | Tests | `TerminalControlTests`: `CreateReplaySession`-Helfer + Tests `OnSessionChanged_FixierteGeometrie_BehaeltAufzeichnungsGroesse`, `OnRenderSizeChanged_FixierteGeometrie_ResizedNicht`, `ScrollInfo_FixierteGeometrie_HorizontalesScrollen`, `ScrollInfo_LiveSession_KeinHorizontalerScrollbereich` | Offen | — |
| 14 | Tests | `PseudoConsoleSessionTests`: `SupportsResize == true`-Assert ergänzen | Offen | — |
| 15 | Tests | `KonsolenTestViewModelTests`: `AufzeichnungOeffnen_SetztGeometrieText` (Setzen beim Laden, Leeren beim Schließen) | Offen | — |
| 16 | E2E-Tests | `KonsolenTestDialogView`: `GetGeometrieText()` + ScrollPattern-Leser/Setzer (`IstHorizontalScrollbar`, `WarteAufHorizontalScrollFaellig`, `SetzeHorizontalScrollProzent`, `GetHorizontalScrollPercent`) am `ReplayTerminalScrollViewer` | Offen | — |
| 17 | E2E-Tests | `E2E_KonsolenTestfenster`: Phase in `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` ergänzen — 220×50-Aufzeichnung (scrollable + `SetScrollPercent` wirkt + Geometrie-Text) und 60×20-Aufzeichnung (nicht scrollable); Temp-Dateien im `finally` aufräumen | Offen | — |
| 18 | Dokumentation | `docs/help/terminal/api.md` aktualisieren (`ITerminalSession`-Tabelle `SupportsResize`; `TerminalControl`-Scroll-/Resize-Abschnitte; `TerminalReplaySession`-Stub-Tabelle; `KonsolenTestViewModel`-Tabelle `GeometrieText`) | Offen | — |
| 19 | Dokumentation | `docs/help/terminal/ablauf-technisch.md` (Z. 319/325), `beschreibung.md` (Z. 109) und `business-rules.md` (Z. 88) auf fixierte Replay-Geometrie umstellen | Offen | — |
| 20 | Verifikation | Release-Build + beide Testlanes (`SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`; OsInterface mit `SOFTWARESCHMIEDE_E2E_APP_PATH` auf die Release-App.exe); `dotnet format Softwareschmiede.slnx --verify-no-changes` | Offen | — |
