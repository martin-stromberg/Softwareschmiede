# Übersetzte Anforderung: Konsolentestfenster — Wiedergabe in aufgezeichneter Terminal-Geometrie

**Quelle:** Aufgabe `task/konsolentestfenster-replay-geometrie`
**Branch:** `task/konsolentestfenster-replay-geometrie`

## Fachliche Zusammenfassung

Das Konsolentestfenster (`KonsolenTestDialog`) soll `.clireplay`-Aufzeichnungen mit der im Aufzeichnungs-Header gespeicherten Terminal-Geometrie (`CliOutputAufzeichnung.Cols`/`Rows`) rendern statt mit der aktuellen Control-Größe. Heute erzwingt `TerminalControl` beim Binden und bei jeder Größenänderung ein `TerminalBuffer.Resize` auf die Control-Geometrie (~69 Zellen bei Standarddialog-Breite), wodurch für breitere Aufzeichnungen (verifiziert: 220×50 bei einer echten Devin-Aufzeichnung) Zeilen umbrechen, absolute Cursorpositionierungen (`ESC[18;120H`) falsch landen und das Layout komplett divergiert. Ist die aufgezeichnete Breite größer als der sichtbare Bereich, muss der überschüssige Inhalt per horizontalem Scrollen erreichbar sein (Terminal-Semantik: abschneiden/scrollen statt umbrechen). Die Live-Session-Darstellung in `TaskDetailView` darf nicht beeinträchtigt werden — dort bleibt Resize-auf-Control das korrekte Verhalten.

## Betroffene Klassen und Komponenten

### Bestehende, voraussichtlich zu ändernde Artefakte

- `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs` — der Vertrag braucht ein Mittel, mit dem `TerminalControl` Sessions mit fixierter Geometrie erkennt (siehe Offene Fragen: `Resize`-Rückgabewert `false`, neuer Capability-Member wie `IsGeometryFixed`/`SupportsResize` bzw. Property für die fixe Geometrie, oder separates Interface — Entscheidung in der Planung).
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs` — `Resize` ist derzeit ein No-Op-Stub (`=> true`, Zeile 314) mit unklarer Semantik; der Buffer wird im Konstruktor bereits mit `aufzeichnung.Cols`/`Rows` angelegt (Zeile 67). Die Klassendoku (Zeilen 8–14) beschreibt explizit das heutige Verhalten („die Wiedergabe nutzt die aktuelle Control-Geometrie") und ist entsprechend zu aktualisieren. `BaueBufferUndParserAusPraefixNeuAuf` (Zeile 494–501) ruft `Buffer.Reset()` auf — `Reset` erhält die Dimensionen, sodass ein einmal vom Control verkleinerter Buffer auch nach `RebuildBufferFromReplay`/`SchrittZurueck` in der falschen Geometrie bleibt; der Fix muss daher das Control-seitige Resize verhindern, nicht den Buffer nachträglich korrigieren.
- `src/Softwareschmiede.App/Controls/TerminalControl.cs` — Kern der Änderung:
  - `OnSessionChanged` (Zeilen 94–132): `_buffer.Resize(cols, rows)` (Zeile 126) darf bei fixierter Geometrie nicht aufgerufen werden — der Buffer behält die Aufzeichnungsgröße.
  - `OnRenderSizeChanged` (Zeilen 301–317): `buffer.Resize(cols, rows)` + `session.Resize(cols, rows)` (Zeilen 310–311) bei fixierter Geometrie überspringen.
  - `IScrollInfo`-Implementierung für horizontalen Bildlauf ist derzeit komplett stubbed: `CanHorizontallyScroll` (Zeile 58), `ExtentWidth => ViewportWidth` (Zeile 61), `HorizontalOffset => 0` (Zeile 73), `LineLeft`/`LineRight` (Zeilen 373–380), `PageLeft`/`PageRight` (Zeilen 389–396), `MouseWheelLeft`/`MouseWheelRight` (Zeilen 405–412), `SetHorizontalOffset` (Zeilen 415–417). Für fixe Geometrie müssen `ExtentWidth` (Buffer-Cols × `_cellWidth`), `HorizontalOffset` und die Scroll-Methoden real implementiert werden.
  - `OnRender` (Zeilen 152–222): die X-Koordinaten (`c * _cellWidth`, Zeile 210; Hintergrund-Runs Zeile 187; Cursor Zeilen 218–220) müssen den horizontalen Offset berücksichtigen (bzw. `PushTransform`/Clip) — und idealerweise nur den sichtbaren Spaltenbereich zeichnen.
  - `MeasureOverride` (Zeilen 350–356): unter einem horizontal scrollenden `ScrollViewer` ist `availableSize.Width` unendlich — für fixe Geometrie muss die gewünschte Breite aus den Buffer-Spalten kommen (aktuell fällt `CalculateCols` bei `ActualWidth == 0` auf `220 * 8` zurück, Zeile 339).
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml` — der `ScrollViewer` um `ReplayTerminal` hat `HorizontalScrollBarVisibility="Disabled"` (Zeilen 136–143); für die Wiedergabe-Ansicht ist `Auto` (oder `Visible`) erforderlich.
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs` — `Session`-Bindung (Zeilen 98–103, gesetzt in `LadeAufzeichnung` Zeile 255 und `ErsetzeReplaySessionDurchFrische` Zeile 345) bleibt der Eintragspunkt; optional kann die aufgezeichnete Geometrie über eine neue Property für einen UI-Hinweis exponiert werden (nicht verpflichtend, siehe Anforderung).

### Explizit unverändert zu lassende Artefakte

- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` — `TerminalScrollViewer`/`TerminalConsole` (Zeilen 487–496, `HorizontalScrollBarVisibility="Disabled"`): Live-Sessions (`PseudoConsoleSession`) bleiben resize-fähig, das Verhalten dort ist korrekt und darf nicht beeinträchtigt werden (inkl. kein horizontaler Scrollbar für Live-Sessions).
- `src/Softwareschmiede/Domain/Terminal/TerminalBuffer.cs` — `Resize` (Zeilen 183–231) selbst bleibt unverändert; es wird für Replay-Sessions schlicht nicht mehr aufgerufen.
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` — `Resize` (Zeilen 167–188) bleibt der Referenzpfad für echte PTYs.

### Datenmodell

- `CliOutputAufzeichnung.Cols`/`Rows` (`src/Softwareschmiede/Infrastructure/Terminal/CliOutputAufzeichnung.cs` Zeilen 16–20) liefern die aufgezeichnete Geometrie bereits; `CliReplayAufzeichnungStore.LadeAsync` garantiert Werte > 0 (vgl. Kommentar in `KonsolenTestViewModel` Zeilen 235–236). Keine Formatänderung nötig (Annahme).

### Tests

- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplaySessionTests.cs` — der Test an Zeile 220–221 dokumentiert den No-Op-Vertrag (`Resize` gibt immer `true` zurück); je nach gewählter Semantik anzupassen bzw. durch Tests der Fixiert-Geometrie-Semantik zu ersetzen.
- `src/Softwareschmiede.Tests/App/Controls/TerminalControlTests.cs` (+ `TerminalControlTests.ClipboardPaste.cs`) — neue Unit-Tests: Bei einer Session mit fixierter Geometrie darf `OnSessionChanged`/`OnRenderSizeChanged` den Buffer nicht verkleinern (Buffer-Cols bleiben die aufgezeichneten), `ExtentWidth` muss den Buffer-Inhalt abbilden, `SetHorizontalOffset`/`LineLeft`/`LineRight` verschieben den sichtbaren Bereich. (Das Testprojekt kann WPF-Controls bereits hosten — bestehende `TerminalControlTests` sind der Referenzrahmen.)
- E2E-Pflicht aus der Anforderung: das bestehende Konsolentestfenster-Szenario `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` in `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs` (Zeile 30, aufgerufen aus `End2EndTest.RunGeneralTests` in `src/Softwareschmiede.Tests/E2E/MainTest.cs` Zeile 36) ist zu erweitern — **kein neuer FlaUI-Test** (Konsolidierungsregel). Zu beachten: der gerenderte Terminalinhalt ist über UI-Automation nicht lesbar (Kommentar Zeilen 127–129) — der Nachweis muss daher über andere Beobachtbare laufen, z. B. Sichtbarkeit/Extent der horizontalen Scrollbar am `ScrollViewer` des Dialogs, ScrollPattern-Verhalten oder ein Geometrie-Hinweistext (siehe Offene Fragen). Eine synthetische Aufzeichnung mit deutlich mehr Cols als die Dialogbreite (analog den vorhandenen `Cols = 80`-Fixtures, z. B. 200+) genügt.
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TempReplayDiagnoseTests.cs` — temporärer Diagnose-Test aus der Voranalyse (untracked); vor dem Commit zu entfernen.

## Implementierungsansatz

- **Fixierte Geometrie signalisieren:** `TerminalControl` muss beim Binden (`OnSessionChanged`) und bei Größenänderungen (`OnRenderSizeChanged`) unterscheiden können, ob die Session resize-fähig ist. Die Anforderung nennt als Kandidaten `Resize`-Rückgabewert `false` oder einen neuen Capability-Member — die Entscheidung ist Teil der Planung (siehe Offene Fragen). Für `TerminalReplaySession` gilt danach: Geometrie = `CliOutputAufzeichnung.Cols`/`Rows`, fix für die Lebensdauer der Session.
- **Buffer-Geometrie beibehalten:** Bei fixierter Geometrie entfällt `_buffer.Resize(...)` in beiden Hooks; der Buffer verbleibt in der Aufzeichnungsgröße, `RebuildBufferFromReplay` und `SchrittZurueck` (`Buffer.Reset` + Re-Parse, Zeilen 494–501) arbeiten dann in der korrekten Geometrie.
- **Horizontales Scrollen:** `TerminalControl` implementiert `IScrollInfo` bereits für vertikales Scrollen (`_verticalOffset`, `_extentHeight`, `UpdateScrollInfo` Zeilen 446–466). Analog sind `_horizontalOffset` und `ExtentWidth = buffer.Cols * _cellWidth` zu ergänzen, `SetHorizontalOffset`/`LineLeft`/`LineRight`/`PageLeft`/`PageRight`/`MouseWheelLeft`/`MouseWheelRight` zu implementieren und `OnRender` um den Offset zu verschieben; bei aktivem `IScrollInfo`-Host meldet `CanHorizontallyScroll` Verfügbarkeit. Nur bei fixierter Geometrie mit `ExtentWidth > ViewportWidth` entsteht ein bedienbarer horizontaler Scrollbalken; bei Live-Sessions bleibt `ExtentWidth == ViewportWidth` (kein Verhaltenswechsel, auch wenn in `TaskDetailView` die Scrollbar ohnehin `Disabled` ist).
- **UI-Hinweis (optional laut Anforderung):** Falls es sich sauber ergibt, Hinweis in UI oder Doku, dass die Wiedergabe die Original-Geometrie nutzt — z. B. Geometrie-Angabe im `StatusText`/`PositionsText`-Bereich oder einem eigenen TextElement mit `AutomationProperties.Name` (würde zugleich die E2E-Beobachtbarkeit verbessern — Annahme).
- **Renderpfad unverändert:** Die Wiedergabe läuft weiterhin chunkweise über `AnsiSequenceParser` → `TerminalBuffer` → `BufferChanged` (`TerminalReplaySession.WiedergabeLoopAsync`/`WendeChunkAnUnterLock`, Zeilen 374–489); an der Verarbeitungslogik der Chunks ändert sich nichts — nur die Buffer-Geometrie und die Darstellung.

## Konfiguration

Kein Konfigurationsbedarf: Die Geometrie ist eine Eigenschaft der geladenen `.clireplay`-Datei (Header `Cols`/`Rows`) und wird pro Aufzeichnung fix übernommen — weder anwendungs- noch benutzerweit einstellbar. Die bestehenden Laufzeitparameter des Fensters (Zeitraffer-Schwelle) bleiben davon unberührt.

## Offene Fragen

1. **Capability-Mechanismus:** Wie signalisiert eine `ITerminalSession` fixierte Geometrie? Optionen: (a) `Resize` gibt `false` zurück und `TerminalControl` interpretiert das als „Geometrie fix" — Achtung Semantik-Kollision: `PseudoConsoleSession.Resize` liefert `false` bereits für ungültige Werte/fehlgeschlagene PTY-Calls (Zeilen 174–175, 182), ein `false` wäre also nicht eindeutig „fixiert"; (b) neuer Member am Interface (z. B. `bool IsGeometryFixed`, `SupportsResize`, oder `FixedCols`/`FixedRows`-Properties); (c) separates Marker-/Capability-Interface. Zu klären in der Planung; (b) wirkt am saubersten, erweitert aber den öffentlichen Vertrag (Annahme).
2. **Fixierte Zeilen vs. nur Spalten:** Die Anforderung betont die Breite; für absolute Cursorpositionierungen und Scroll-Region-Semantik ist auch die Zeilenzahl der Aufzeichnung maßgeblich (`CliOutputAufzeichnung.Rows`) — Annahme: beide Dimensionen werden fixiert (entspricht der Aufzeichnungs-Header-Semantik), vertikales Scrollen über den bestehenden Scrollback/Extent-Mechanismus bleibt wie gehabt.
3. **E2E-Nachweisbarkeit:** Der gerenderte Inhalt ist per FlaUI nicht lesbar. Welcher beobachtbare Nachweis soll die fixe Geometrie belegen — sichtbarer horizontaler Scrollbar/ScrollExtent am Dialog-`ScrollViewer`, ein dediziertes Geometrie-TextElement, oder beides? (Beeinflusst, ob der optionale UI-Hinweis implementiert wird.)
4. **Aufgezeichnete Geometrie kleiner als der Viewport:** Kein Problem erwartet (rechter Bereich bleibt schwarz, kein Umbruch nötig) — in der Planung nur zu verifizieren, dass `MeasureOverride`/Arrange diesen Fall sauber abbilden (Annahme).
5. **Vertikale Geometrie-Differenzen:** Bei aufgezeichneten `Rows` > sichtbaren Zeilen greift der bestehende Scrollback/Vertical-Scroll-Mechanismus; bei `Rows` < sichtbarer Höhe bleibt der Rest leer — keine gesonderte Anforderung erkennbar (Annahme).
