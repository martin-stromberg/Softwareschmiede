# Umsetzungsplan: Konsolentestfenster — Wiedergabe in aufgezeichneter Terminal-Geometrie

## Übersicht

Das Konsolentestfenster soll `.clireplay`-Aufzeichnungen in der im Header gespeicherten Terminal-Geometrie (`CliOutputAufzeichnung.Cols`/`Rows`) rendern statt mit der Control-Größe; überschüssige Breite wird per horizontalem Scrollen erreichbar. Dazu erhält `ITerminalSession` ein Resize-Capability-Merkmal, `TerminalControl` überspringt bei fixierter Geometrie das Buffer-/Session-Resize und implementiert die bislang gestubbte horizontale `IScrollInfo`-Achse; der Dialog schaltet den horizontalen Scrollbalken frei und bekommt einen Geometrie-Hinweis. Die Live-Darstellung in `TaskDetailView` (`PseudoConsoleSession`, Resize-auf-Control) bleibt unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Capability-Signal für fixe Geometrie | Neue Property `bool SupportsResize` auf `ITerminalSession`; `PseudoConsoleSession` liefert `true`, `TerminalReplaySession` liefert `false` | `Resize`-Rückgabewert `false` ist nicht eindeutig — `PseudoConsoleSession.Resize` (Z. 172–188) meldet `false` bereits für ungültige Werte und fehlgeschlagene PTY-Calls (Erfolgs- statt Fähigkeitssemantik). Ein Marker-Interface wäre ein eigener Typ für ein einzelnes Bit; eine Typprüfung `session is TerminalReplaySession` koppelt das App-Control an eine konkrete Infrastructure-Klasse. Der Bool-Member passt zur bestehenden Benennung (`TerminalCapabilities.SupportsPty`) und muss nur von den beiden einzigen Implementierungen getragen werden — kein `ITerminalSession`-Testdouble existiert (am Repo verifiziert). |
| Geometrie-Quelle am Control | Keine Geometrie-Übergabe nötig — `session.Buffer` trägt die Aufzeichnungs-Geometrie bereits (Konstruktor `TerminalReplaySession` Z. 67); das Control prüft nur `SupportsResize` | `TerminalBuffer.Cols`/`Rows` sind öffentlich lesbar (Z. 50–59); `CliReplayAufzeichnungStore.LadeAsync` garantiert `> 0` (Z. 131–132). Ein `FixedCols`/`FixedRows`-Member am Interface wäre redundant. |
| Buffer bei Control breiter als Aufzeichnung | Buffer bleibt auf die aufgezeichneten Cols gepinnt — nie resizen; der rechte Rest bleibt schwarz | Replay-Fidelität: der aufgezeichnete Inhalt wurde unter genau dieser Geometrie produziert (Zeilenumbruch, absolute Cursorpositionen, Scroll-Region, `CursorMoved`-Klemmung sind reine Funktionen der Buffer-Geometrie — der Parser ist geometrie-agnostisch). Mitwachsen würde Umbruch- und Region-Semantik gegenüber der Aufzeichnung verändern. |
| Einheiten der horizontalen `IScrollInfo`-Achse | Pixel: `ExtentWidth = Buffer.Cols × _cellWidth`, `HorizontalOffset`/`SetHorizontalOffset` in Pixel; `Line*` = ±1 Zelle (`_cellWidth`), `Page*` = ±(`ViewportWidth − _cellWidth`), `MouseWheel*` = ±`MouseWheelScrollLines × _cellWidth` | `ViewportWidth` ist bereits `ActualWidth` in Pixeln (Z. 67); der `ScrollViewer` rechnet Extent↔Viewport pro Achse in derselben Einheit, die Achsen-Asymmetrie (vertikal = Zeilen wegen Scrollback-Logik) ist zulässig, da `Line*`/`Page*` vom Control selbst interpretiert werden. |
| Live-Sessions vor falscher H-Scrollbar schützen | `ExtentWidth` ist durch `CalculateCols` = `floor(ActualWidth/_cellWidth)` konstruktionsbedingt stets ≤ `ViewportWidth`; zusätzlich meldet `ExtentWidth` bei `CanHorizontallyScroll == false` konventionsgemäß `ViewportWidth` | Für `PseudoConsoleSession` kann der Extent den Viewport nie übersteigen (Floor-Arithmetik), und `TaskDetailView.xaml` behält ohnehin `HorizontalScrollBarVisibility="Disabled"` — kein Verhaltenswechsel, auch nicht über UIA-`HorizontallyScrollable`. |
| `OnRender` mit horizontalem Offset | `PushClip(Control-Bounds)` + `PushTransform(TranslateTransform(-_horizontalOffset, 0))` um den Inhalts-Block; Spalten-Culling auf den sichtbaren Spaltenbereich | Eine Transform statt Einzel-Offsets hält Hintergrund-Runs, Glyphen und Cursor konsistent; Culling vermeidet `FormattedText`-Aufbau für unsichtbare Spalten (relevant bei 220+ Cols). Offset 0 (Live-Pfad) verhält sich identisch zu heute. |
| `TerminalReplaySession.Resize`-Rückgabewert | Bleibt `=> true` (harmloser No-Op) — wird mit `SupportsResize == false` vom Control nicht mehr aufgerufen | Ein `false` wäre als „Fehlschlag" lesbar und könnte künftige Aufrufer irritieren; `true` dokumentiert „nichts zu tun". Der Stub-Vertrag in `Stubs_SindUnschaedlich` bleibt gültig und wird um die `SupportsResize`-Semantik erweitert. |
| E2E-Nachweis ohne lesbaren Terminalinhalt | Beides: (a) `AutomationProperties.Name="ReplayTerminalScrollViewer"` am Dialog-`ScrollViewer` + FlaUI `ScrollPattern` (`HorizontallyScrollable`, `HorizontalViewSize`, `HorizontalScrollPercent`, `SetScrollPercent`) als funktionaler Nachweis; (b) neues `GeometrieText`-Property + `TextBlock` (`AutomationProperties.Name="AufzeichnungGeometrie"`, `HelpText` gebunden) als Geometrie-Nachweis | `HorizontallyScrollable == true` impliziert `ExtentWidth > ViewportWidth` — nur möglich, wenn der Buffer in Aufzeichnungsbreite blieb (ein verkleinerter Buffer würde Extent ≈ Viewport liefern). `SetScrollPercent` belegt `SetHorizontalOffset` Ende-zu-Ende. Der Geometrie-Text belegt zusätzlich *welche* Geometrie aktiv ist und ist der optionale UI-Hinweis der Anforderung. `StatusText`/`PositionsText` werden **nicht** erweitert — ihre exakten Strings sind in Unit- und E2E-Tests verankert. |

## Programmabläufe

### Aufzeichnung laden und in fixierter Geometrie binden

1. `KonsolenTestViewModel.LadeAufzeichnung` erzeugt wie bisher eine `TerminalReplaySession` über `ErzeugeReplaySession` (Buffer bereits in `aufzeichnung.Cols`/`Rows`), setzt `GeometrieText` (z. B. „Aufzeichnung: 220×50") und `Session = _replaySession`.
2. `TerminalControl.OnSessionChanged` läuft: alte Session abmelden → `BufferChanged` registrieren → `session.RebuildBufferFromReplay()` → `_buffer = session.Buffer`. Wegen `session.SupportsResize == false` entfällt `_buffer.Resize(cols, rows)` (heute Z. 126) — der Buffer bleibt in Aufzeichnungs-Geometrie. `_horizontalOffset = 0`, `_isFollowingEnd = true`, `UpdateScrollInfo(followEndIfNeeded: true)`, `InvalidateVisual`.
3. Der umgebende `ScrollViewer` (jetzt `HorizontalScrollBarVisibility="Auto"`, `CanContentScroll="True"`) setzt `CanHorizontallyScroll`; mit `ExtentWidth = Cols × _cellWidth > ViewportWidth` erscheint die horizontale Scrollbar.
4. Wiedergabe/Schritte laufen unverändert über `WendeChunkAnUnterLock` → `Buffer.Apply` in der fixen Geometrie; `BaueBufferUndParserAusPraefixNeuAuf` (`Buffer.Reset()` behält die Dimensionen, Z. 159–178) arbeitet nun korrekt in der Aufzeichnungs-Geometrie.

Beteiligte Klassen/Komponenten: `KonsolenTestViewModel`, `TerminalReplaySession`, `TerminalControl`, `KonsolenTestDialog.xaml`.

### Größenänderung des Dialogfensters bei fixierter Geometrie

1. `TerminalControl.OnRenderSizeChanged`: bei `session.SupportsResize == false` entfallen `buffer.Resize(cols, rows)` und `session.Resize(cols, rows)` (Z. 310–311); `UpdateScrollInfo` und `InvalidateVisual` laufen weiter — Viewport/Extent werden neu bewertet und `_horizontalOffset` auf den neuen `ScrollableWidth`-Bereich geklemmt (Dialog verbreitert → Offset Richtung 0 korrigiert).
2. Ist die aufgezeichnete Breite kleiner als der neue Viewport, bleibt `ExtentWidth <= ViewportWidth` — keine Scrollbar, rechter Bereich bleibt schwarz.

Beteiligte Klassen/Komponenten: `TerminalControl`.

### Horizontales Scrollen (Scrollbar, Mausrad horizontal)

1. Der `ScrollViewer` ruft je nach Interaktion `SetHorizontalOffset` (Thumb-Drag), `LineLeft`/`LineRight` (Pfeilbuttons, ±`_cellWidth`), `PageLeft`/`PageRight` (±`ViewportWidth − _cellWidth`) oder `MouseWheelLeft`/`MouseWheelRight` (horizontales Mausrad, ±`MouseWheelScrollLines × _cellWidth`) auf.
2. `SetHorizontalOffset` klemmt den Pixel-Offset über `ClampOffset` auf `[0, ScrollableWidth]` (`Max(0, ExtentWidth − ViewportWidth)`), setzt `_horizontalOffset` und stößt `ScrollOwner.InvalidateScrollInfo()` + `InvalidateVisual` an.
3. `OnRender` zeichnet den schwarzen Hintergrund über die volle Control-Fläche, clippt anschließend auf die Control-Bounds, schiebt den Inhalt per `TranslateTransform(-_horizontalOffset, 0)` und iteriert nur den sichtbaren Spaltenbereich (`floor(_horizontalOffset / _cellWidth)` bis `ceil((_horizontalOffset + ActualWidth) / _cellWidth)`, geklemmt auf `snapshot.Cols`).
4. Das reguläre Mausrad scrollt weiterhin vertikal (Scrollback) — unverändert.

Beteiligte Klassen/Komponenten: `TerminalControl`, Dialog-`ScrollViewer` (`ReplayTerminalScrollViewer`).

### Live-Session in `TaskDetailView` (unveränderter Pfad)

1. `TaskDetailView.SetTerminalSession` bindet eine `PseudoConsoleSession` (`SupportsResize == true`) — `OnSessionChanged` und `OnRenderSizeChanged` führen `buffer.Resize`/`session.Resize` exakt wie heute aus.
2. `ExtentWidth = floor(ActualWidth/_cellWidth) × _cellWidth <= ViewportWidth`; der `TerminalScrollViewer` behält `HorizontalScrollBarVisibility="Disabled"` — weder Scrollbar noch geändertes Scrollverhalten.

Beteiligte Klassen/Komponenten: `PseudoConsoleSession`, `TerminalControl`, `TaskDetailView`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `ITerminalSession` (Interface) — `src/Softwareschmiede/Infrastructure/Terminal/ITerminalSession.cs`

- **Neue Eigenschaften:** `SupportsResize` (`bool`) — `true`, wenn die Session-Geometrie zur Laufzeit geändert werden darf (Control resize-t Buffer und ruft `Resize`); `false` bei fixierter Geometrie (Replay). XML-Doku muss klarstellen, dass bei `false` weder `session.Resize` noch `session.Buffer.Resize` vom Aufrufer aufgerufen werden sollen.

### `PseudoConsoleSession` — `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`

- **Neue Eigenschaften:** `SupportsResize` (`bool`, `=> true`) — Live-Sessions bleiben resize-fähig.

### `TerminalReplaySession` — `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`

- **Neue Eigenschaften:** `SupportsResize` (`bool`, `=> false`) — Geometrie ist für die Lebensdauer der Session auf `aufzeichnung.Cols`/`Rows` fixiert.
- **Klassendoku (Z. 8–14):** Umstellen auf das neue Verhalten — die Header-Geometrie ist die dauerhafte Buffer-Geometrie; das Control resized Replay-Sessions nicht (`SupportsResize`), überschüssige Breite wird per `IScrollInfo` horizontal scrollbar.
- **`Resize` (Z. 314):** unverändert `=> true`; Doku-Kommentar ergänzen, dass der No-Op ab `SupportsResize == false` vom `TerminalControl` nicht mehr aufgerufen wird.

### `TerminalControl` — `src/Softwareschmiede.App/Controls/TerminalControl.cs`

- **Neue Felder:** `_horizontalOffset` (`double`) — aktueller horizontaler Scroll-Offset in Pixel.
- **Geänderte Eigenschaften:**
  - `ExtentWidth` (Z. 61): statt `=> ViewportWidth` jetzt `CanHorizontallyScroll && _buffer != null` → `Math.Max(ViewportWidth, _buffer.Cols * _cellWidth)`, sonst `ViewportWidth`. (Für Live-Sessions liegt `Cols × _cellWidth` konstruktionsbedingt ≤ `ViewportWidth`.)
  - `HorizontalOffset` (Z. 73): statt `=> 0` jetzt `=> _horizontalOffset`.
- **Geänderte Methoden:**
  - `OnSessionChanged` (Z. 94–132): `_buffer.Resize(cols, rows)` (Z. 126) nur noch bei `session.SupportsResize`; `cols`/`rows`-Berechnung nur im Resize-Zweig nötig. `_horizontalOffset = 0` beim Sessionwechsel und im `session == null`-Zweig zurücksetzen.
  - `OnRenderSizeChanged` (Z. 301–317): `buffer.Resize` + `session.Resize` nur bei `session.SupportsResize`; `UpdateScrollInfo` + `InvalidateVisual` laufen in beiden Fällen (Viewport-Wechsel, Offset-Klemmung).
  - `OnRender` (Z. 152–222): nach dem schwarzen Hintergrund `PushClip` (Control-Bounds) + `PushTransform` (`- _horizontalOffset`); Spalten-Culling — Hintergrund-Runs, Glyphen- und Cursor-Schleifen nur über den sichtbaren Spaltenbereich; `Pop` beider am Ende. Vertikale Logik (`visibleStart`, `_isFollowingEnd`) unverändert.
  - `MeasureOverride` (Z. 350–356): bei unendlicher `availableSize.Width` die gewünschte Breite aus `(_buffer?.Cols ?? CalculateCols()) * _cellWidth` ableiten statt allein aus dem `220 × 8`-Fallback (unter `CanContentScroll`+`IScrollInfo` misst der Host mit Viewport-Constraint — der Infinite-Pfad ist Fallback, z. B. in scrollenden Nicht-`IScrollInfo`-Kontexten).
  - `UpdateScrollInfo` (Z. 446–466): zusätzlich `_horizontalOffset = ClampOffset(_horizontalOffset, ScrollableWidth)` (neuer privater Ausdruck `ScrollableWidth = Math.Max(0, ExtentWidth − ViewportWidth)`).
- **Neu implementierte `IScrollInfo`-Member:** `SetHorizontalOffset` (Z. 415–417 — klemmen, `ScrollOwner?.InvalidateScrollInfo()`, `InvalidateVisual()`), `LineLeft`/`LineRight` (±`_cellWidth`), `PageLeft`/`PageRight` (±`ViewportWidth − _cellWidth`), `MouseWheelLeft`/`MouseWheelRight` (±`MouseWheelScrollLines × _cellWidth`).

### `KonsolenTestViewModel` — `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`

- **Neue Eigenschaften:** `GeometrieText` (`string`, init `""`) — Anzeigetext der aufgezeichneten Geometrie, Format „Aufzeichnung: {Cols}×{Rows}".
- **Geänderte Methoden:** `LadeAufzeichnung` setzt `GeometrieText` aus `aufzeichnung.Cols`/`Rows`; `ErsetzeReplaySessionDurchFrische` setzt ihn aus `_aufzeichnung` erneut; `EntsorgeReplaySession` setzt ihn auf `""` zurück (konsistenter Zustand bei „keine Aufzeichnung geladen").

### `KonsolenTestDialog.xaml` — `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`

- `ScrollViewer` um `ReplayTerminal` (Z. 136–143): `HorizontalScrollBarVisibility` von `Disabled` auf `Auto`; zusätzlich `x:Name="ReplayTerminalScrollViewer"` + `AutomationProperties.Name="ReplayTerminalScrollViewer"` (E2E-Adressierung — das Element trägt heute keinen Automation-Namen).
- Werkzeugleiste (`WrapPanel`, Z. 26–97): neuer `TextBlock` `Text="{Binding GeometrieText}"` mit `AutomationProperties.Name="AufzeichnungGeometrie"` + `AutomationProperties.HelpText="{Binding GeometrieText}"` — nach dem Muster von `WiedergabeStatus`/`WiedergabePosition` (Z. 77–90).

### Explizit unverändert

- `TaskDetailView.xaml` / `.xaml.cs` — `HorizontalScrollBarVisibility="Disabled"` am `TerminalScrollViewer` bleibt; Live-Pfad (`PseudoConsoleSession`) unverändert.
- `TerminalBuffer` (`Resize`, `Reset`), `AnsiSequenceParser`, `CliReplayAufzeichnungStore`, `CliOutputAufzeichnung`, `CliOutputRecorder` — keine Änderung.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine. Neue Offsets werden über den bestehenden `ClampOffset`-Helper geklemmt; die Aufzeichnungs-Geometrie `> 0` ist durch die `LadeAsync`-Validierung des Stores garantiert.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`ITerminalSession`-Vertrag:** wird um `SupportsResize` erweitert — betrifft nur die beiden Produktiv-Implementierungen; im Testprojekt existiert kein eigenes `ITerminalSession`-Testdouble (verifiziert), daher kein Kompilier-Bruch.
- **Live-Pfad `TaskDetailView`:** `PseudoConsoleSession.SupportsResize == true` hält `OnSessionChanged`/`OnRenderSizeChanged` identisch; `ExtentWidth ≤ ViewportWidth` durch Floor-Arithmetik — selbst falls der Host `CanHorizontallyScroll` setzt, kann keine horizontale Scrollbar entstehen (zusätzlich `Disabled` in der View).
- **`OnRender`-Änderungen:** mit `_horizontalOffset = 0` (Live, schmale Aufzeichnungen) ist die Transform eine Identität — optisch keine Änderung; `PushClip` begrenzt Neuzeichnung auf die Control-Bounds.
- **`MeasureOverride`-Infinite-Pfad:** liefert jetzt Buffer-Breite statt `220×8`-Fallback — nur in Kontexten ohne Viewport-Constraint wirksam; im produktiven Einsatz (`CanContentScroll`) unverändert.
- **`ErsetzeReplaySessionDurchFrische`:** `GeometrieText` geht über `EntsorgeReplaySession` kurz auf `""` und wird anschließend wieder gesetzt — visuell unerheblich (gleicher Wert).
- **Verifikations-Besonderheit (Umgebung):** Der **Debug**-Build des App-Projekts ist derzeit durch eine laufende `Softwareschmiede.App.exe` (PID 36992, aus Repo-`bin\Debug` gestartet) und Visual Studio gesperrt (MSB3027) — der Prozess darf **nicht** beendet werden (Self-Hosting-Regel). Verifikation erfolgt in `-c Release`; für E2E-Läufe muss `SOFTWARESCHMIEDE_E2E_APP_PATH` auf die **Release**-App.exe zeigen, da `ResolveAppExePath` (`WpfTestBase.cs` Z. 941–985) sonst die veraltete Debug-App.exe bevorzugt und das Feature nicht getestet würde. Alle `dotnet test`-Aufrufe synchron/im Vordergrund und mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`.

## Umsetzungsreihenfolge

1. **`SupportsResize` am `ITerminalSession`-Vertrag und beiden Implementierungen**
   - Voraussetzungen: Keine.
   - Beschreibung: `bool SupportsResize { get; }` ans Interface (mit XML-Doku zum erweiterten Vertrag); `PseudoConsoleSession.SupportsResize => true`; `TerminalReplaySession.SupportsResize => false` inkl. aktualisierter Klassendoku (Z. 8–14) und `Resize`-Doku-Kommentar.

2. **`TerminalControl`: Resize-Guards + horizontale `IScrollInfo`-Achse + Render-Offset**
   - Voraussetzungen: Schritt 1 (Property existiert am Vertrag).
   - Beschreibung: `_horizontalOffset`-Feld; `SupportsResize`-Guard in `OnSessionChanged`/`OnRenderSizeChanged`; `ExtentWidth`/`HorizontalOffset` echt; `SetHorizontalOffset`, `Line*`/`Page*`/`MouseWheel*` horizontal; `ScrollableWidth`; `UpdateScrollInfo`-Klemmung; `OnRender` mit `PushClip`/`PushTransform`/Spalten-Culling; `MeasureOverride`-Buffer-Breite im Infinite-Pfad; Offset-Reset beim Sessionwechsel.

3. **`KonsolenTestViewModel.GeometrieText`**
   - Voraussetzungen: Keine (unabhängig von 1–2; `_aufzeichnung` liegt bereits vor).
   - Beschreibung: Property + `SetProperty`-Pattern wie die übrigen Anzeige-Properties; Setzen in `LadeAufzeichnung`/`ErsetzeReplaySessionDurchFrische`, Zurücksetzen in `EntsorgeReplaySession`.

4. **`KonsolenTestDialog.xaml`: horizontale Scrollbar + Automation-Namen + Geometrie-Anzeige**
   - Voraussetzungen: Schritt 2 (ohne echte `ExtentWidth` bliebe die Scrollbar wirkungslos), Schritt 3 (Binding-Quelle).
   - Beschreibung: `HorizontalScrollBarVisibility="Auto"`, `x:Name`/`AutomationProperties.Name="ReplayTerminalScrollViewer"`, neuer `TextBlock AufzeichnungGeometrie` in der Werkzeugleiste.

5. **Unit-Tests anpassen und ergänzen**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `TerminalReplaySessionTests.Stubs_SindUnschaedlich` um `SupportsResize == false` erweitern (Vertrag-Kommentar anpassen); neue Geometrie-Invarianz-Tests in `TerminalReplaySessionTests`; neue Fixierte-Geometrie-/Horizontal-Scroll-Tests in `TerminalControlTests` (Referenzmuster: `ScrollViewerLayout_CanContentScroll_…`, `RunOnSta`, `IScrollInfo`-Cast, ggf. `CanHorizontallyScroll = true` setzen wie ein echter Host); `GeometrieText`-Assert in `KonsolenTestViewModelTests`; `SupportsResize == true`-Assert in `PseudoConsoleSessionTests`.

6. **E2E-Szenario erweitern (konsolidiert, kein neuer FlaUI-Test)**
   - Voraussetzungen: Schritte 4 (Automation-Namen) und 5.
   - Beschreibung: `KonsolenTestDialogView` um `GetGeometrieText()` und ScrollPattern-Leser/Setzer am `ReplayTerminalScrollViewer` erweitern; in `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` eine Phase mit breiter Aufzeichnung (220×50) — `HorizontallyScrollable`, `SetScrollPercent` wirkt — und schmaler Aufzeichnung (60×20) — nicht scrollbar; Temp-Dateien im `finally` aufräumen.

7. **Hilfe-Dokumentation aktualisieren**
   - Voraussetzungen: Schritte 1–4 (verbindliche Semantik).
   - Beschreibung: `docs/help/terminal/api.md` (Eigenschaftentabelle `ITerminalSession` um `SupportsResize`; `TerminalControl`-Scrollabschnitt „Kein horizontaler UI-Scroll" und Abschnitt „Größenänderungen"; `TerminalReplaySession`-Stub-Tabelle Z. 847; `KonsolenTestViewModel`-Member-Tabelle um `GeometrieText`), `docs/help/terminal/ablauf-technisch.md` (Z. 319/325 — Replay-Bindung resized nicht mehr), `docs/help/terminal/beschreibung.md` (Z. 109 — Wiedergabe nutzt jetzt die Aufzeichnungs-Geometrie), `docs/help/terminal/business-rules.md` (Z. 88 — Geometrie-Regel umschreiben).

8. **Verifikation**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: `dotnet build Softwareschmiede.slnx -c Release` (Debug ist derzeit durch laufende App-Instanz/VS gesperrt — nicht beenden, siehe Seiteneffekte); stabile Lane `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Release --filter "Category!=OsInterface"`; OsInterface-Lane (enthält das erweiterte Konsolentestfenster-Szenario) mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` **und** `SOFTWARESCHMIEDE_E2E_APP_PATH=<Release-App.exe>` — synchron, niemals im Hintergrund; abschließend `dotnet format Softwareschmiede.slnx --verify-no-changes`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `OnSessionChanged_FixierteGeometrie_BehaeltAufzeichnungsGroesse` | `TerminalControlTests` | Replay-Session (`Cols`/`Rows` deutlich größer als die arrangierte Control-Größe, z. B. 220×50 bei 160×48) gebunden → `session.Buffer.Cols`/`Rows` bleiben 220/50 (kein Control-Resize) |
| `OnRenderSizeChanged_FixierteGeometrie_ResizedNicht` | `TerminalControlTests` | Re-Arrange auf andere Größe → Buffer-Dimensionen unverändert; `ExtentWidth` bleibt `Cols × _cellWidth` |
| `ScrollInfo_FixierteGeometrie_HorizontalesScrollen` | `TerminalControlTests` | `CanHorizontallyScroll = true` gesetzt → `ExtentWidth ≈ 220 × Zellbreite > ViewportWidth`; `SetHorizontalOffset` klemmt `[0, ExtentWidth − ViewportWidth]`; `LineRight`/`PageRight`/`MouseWheelRight` verschieben den Offset erwartbar; `HorizontalOffset` wirkt nicht auf `VerticalOffset` |
| `ScrollInfo_LiveSession_KeinHorizontalerScrollbereich` | `TerminalControlTests` | Regressions-Nachweis: `PseudoConsoleSession` → `ExtentWidth <= ViewportWidth`, `SetHorizontalOffset(100)` bleibt auf 0 geklemmt, `HorizontalOffset == 0` |
| `Geometrie_BleibtUeberSchritteUndRebuild_Fixiert` | `TerminalReplaySessionTests` | `Buffer.Cols`/`Rows` == Aufzeichnungs-Header nach `SchrittVor`, `SchrittZurueck` und `RebuildBufferFromReplay` (deckt die `Buffer.Reset()`-Dimensionserhaltung ab) |
| `SupportsResize_LiveTrue` (oder Assert in bestehendem `IsPseudoTerminal`-Test) | `PseudoConsoleSessionTests` | `PseudoConsoleSession.SupportsResize == true` — Gegenpol des neuen Vertrags |
| `AufzeichnungOeffnen_SetztGeometrieText` | `KonsolenTestViewModelTests` | Nach Laden enthält `GeometrieText` die Aufzeichnungs-Geometrie („80×24" der Fixture); nach `Schliessen` wieder leer |
| `CreateReplaySession(cols, rows, chunks)`-Helfer | `TerminalControlTests` | Baut `CliOutputAufzeichnung` + `TerminalReplaySession` (`FakeTimeProvider`) in beliebiger Geometrie — analog `CreateSession` für Live-Sessions |
| `GetGeometrieText()` | `KonsolenTestDialogView` | Liest `AufzeichnungGeometrie` per HelpText (Muster `GetStatusText`/`GetPositionsText`) |
| `GetReplayScrollPattern()` / `IstHorizontalScrollbar()` / `WarteAufHorizontalScrollFaellig()` / `SetzeHorizontalScrollProzent(double)` / `GetHorizontalScrollPercent()` | `KonsolenTestDialogView` | Findet `ReplayTerminalScrollViewer` per Automation-Name, liest `Patterns.Scroll` (`HorizontallyScrollable`, `HorizontalViewSize`, `HorizontalScrollPercent`), scrollt per `SetScrollPercent`; Warte-Methoden mit Polling wie die bestehenden `WarteAuf*`-Helfer (Layout/Extent-Aktualisierung ist asynchron) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TerminalReplaySessionTests.Stubs_SindUnschaedlich` (Z. 193–224) | Der dokumentierte No-Op-Vertrag wird um das neue Capability-Merkmal erweitert: `SupportsResize.Should().BeFalse()` ergänzen, Doku-Kommentar von „Resize ist ein Stub" auf die Fixiert-Geometrie-Semantik umstellen (die `Resize => true`-Asserts bleiben gültig) |
| `E2E/E2E_KonsolenTestfenster.cs` `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` | Neue Phase(n) für Geometrie/horizontales Scrollen: zwei zusätzliche `.clireplay`-Fixtures (220×50 „E2E-Breit", 60×20 „E2E-Schmal") inkl. Aufräumen im `finally` — gleiche Methode, kein neuer FlaUI-Test (Konsolidierungsregel) |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Breite Aufzeichnung (220×50) laden → Geometrie-Anzeige enthält „220"; `ScrollPattern` am `ReplayTerminalScrollViewer` meldet `HorizontallyScrollable == true`/`HorizontalViewSize < 100`; `SetScrollPercent(50)` verschiebt `HorizontalScrollPercent` > 0 | `E2E_KonsolenTestfenster.cs`, `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (aus `RunGeneralTests`, `MainTest.cs` Z. 36) | Replay rendert mit aufgezeichneter Geometrie statt Control-Größe; Overflow ist horizontal scrollbar | Der gerenderte Terminalinhalt ist per UI-Automation nicht lesbar (Kommentar Z. 127–129) — der Nachweis der fixierten Buffer-Geometrie kann nur über den ScrollViewer-Extent/ScrollPattern und das Geometrie-Element laufen; `HorizontallyScrollable` impliziert `ExtentWidth > ViewportWidth` und damit einen nicht verkleinerten Buffer |
| Pflicht | Schmale Aufzeichnung (60×20) laden → `HorizontallyScrollable == false` | dieselbe Methode | Kein unerwünschter horizontaler Scrollbalken, wenn die Aufzeichnung in den Viewport passt | Randfall nur über den echten Layout-/ScrollViewer-Pfad beobachtbar |
| Pflicht (Regression) | Live-Pfad `TaskDetailView` unverändert — durch bestehende Szenarien in `RunGeneralTests`/`RunConPtyTests` mitgedeckt (keine neue Assertion nötig; `ScrollInfo_LiveSession_KeinHorizontalerScrollbereich` deckt die Control-Logik auf Unit-Ebene ab) | bestehende Runner | Live-Sessions bleiben resize-fähig, kein horizontaler Balken | Bereits vorhandene Abdeckung; die Unit-Tests sichern die neue Verzweigung |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` (`End2EndTest`, `E2E_KonsolenTestfenster.cs`) | Erweiterung um die Geometrie-/Horizontal-Scroll-Phase (siehe oben) |

## Offene Punkte

Keine.
