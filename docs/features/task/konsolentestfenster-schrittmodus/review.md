# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### `TerminalReplaySession` (`src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`)

- [x] Feld `_wiedergabeLoopAktiv` (`int`, Z. 36) — vom Plan erlaubte Umbenennung von `_wiedergabeGestartet`; Semantik „Schleife läuft": CAS 0→1 in `WiedergabeStarten` (Z. 126), Rücksetzen auf 0 im `finally` der Schleife (Z. 384) — Re-Armierung vorhanden.
- [x] Feld `_exitedSignaled` (`int`) — zusätzliche Rücksetzung in `SchrittZurueck` (Z. 224) und bei erfolgreichem neuen Schleifenstart in `WiedergabeStarten` (Z. 130).
- [x] `_parser` bleibt `readonly` (Z. 20); Rückwärtsschritt/Rebuild nutzen `AnsiSequenceParser.Reset()` statt Neuinstanzierung.
- [x] Methode `SchrittVor()` → `bool` (public, Z. 170–204) — Guards (`_disposed`, Position == Count → `false`, Z. 172/179), Chunk-Anwendung unter `_renderLock` in Schleifenreihenfolge (`OutputChunk` → `Add` → `Parse`/`Apply` → `_aktuellerChunkIndex` atomar, Z. 183–188), `BufferChanged` nach dem Lock (Z. 192), bei Erreichen des Endes `RaiseExited` + Gate-Öffnung via `Fortsetzen()` (Z. 194–201).
- [x] Methode `SchrittZurueck()` → `bool` (public, Z. 211–230) — Guards (`_disposed`, Position 0 → `false`), Präfix-Kürzung + Index-Dekrement + `_exitedSignaled`-Reset + Helper-Rebuild unter `_renderLock` (Z. 216–226), `BufferChanged` danach (Z. 228), kein `OutputChunk`.
- [x] Methode `BaueBufferUndParserAusPraefixNeuAuf()` (private, Z. 391–398) — `Buffer.Reset()` + `_parser.Reset()` + Re-Parse aller `_abgespielteChunks` durch `_parser`; Aufrufe ausschließlich unter `_renderLock` (`RebuildBufferFromReplay` Z. 259–262, `SchrittZurueck` Z. 225).
- [x] `WiedergabeLoopAsync` umgebaut (Z. 293–386): `while` über `_abgespielteChunks.Count` unter `_renderLock` gelesen (Z. 311–315), Delay aus `chunks[i].Offset - chunks[i-1].Offset` abgeleitet, `vorherigerOffset` entfernt (Z. 319–322), Positions-Re-Check vor dem Anwenden mit Iterations-Neustart (Z. 348–349, 361–362), `_aktuellerChunkIndex` atomar mit `Add` im `_renderLock` (Z. 353–356), Abbruch-Checks und `_istPausiert`-Semantik erhalten (Z. 308, 338, 341).
- [x] `RebuildBufferFromReplay` delegiert auf `BaueBufferUndParserAusPraefixNeuAuf` (Z. 257–263) — `_parser` wird jetzt kohärent zurückgesetzt.
- [x] `WiedergabeStarten` re-armierbar (Z. 122–133): CAS auf Lauf-Flag, `_exitedSignaled`-Reset, `RuntimeStatus → Laeuft`, `_playbackTask`-Start.
- [x] Ende-Signal im Schrittmodus: `SchrittVor` auf letztem Chunk feuert `RaiseExited` (Z. 196) und öffnet das Pause-Gate (Z. 200) — parkende Schleife terminiert sauber.

### `KonsolenTestViewModel` (`src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`)

- [x] `SchrittVorCommand` (`RelayCommand`, Z. 60–62) — CanExecute: `_replaySession is not null && (!IstWiedergabeAktiv || IstPausiert) && AktuellerChunkIndex < QuellEintraege.Count`.
- [x] `SchrittZurueckCommand` (`RelayCommand`, Z. 63–65) — CanExecute analog mit `AktuellerChunkIndex > 0`.
- [x] Methode `SchrittVor()` (private, Z. 359–368) — Session-Aufruf; `StatusText`-Schritt-Hinweis nur solange Position < Ende (am Ende gilt „Wiedergabe beendet." aus `OnReplayExited`).
- [x] Methode `SchrittZurueck()` (private, Z. 370–379) — Session-Aufruf; bei Erfolg `_wiedergabeBeendet = false` (Z. 377) + `StatusText`-Schritt-Hinweis.
- [x] `OnReplayBufferChanged` erweitert (Z. 387–408): `AktuellerQuellEintrag = null` bei Index 0 (Z. 401–403) + `RelayCommand.Refresh()` (Z. 406).
- [x] `IstPausiert`-Setter ergänzt `RelayCommand.Refresh` (Z. 149); `IstWiedergabeAktiv`-Setter hat Refresh bereits (Z. 139).
- [x] Unverändert wie geplant: `WiedergabeStarten`/`WiedergabeNeustarten`/`WiedergabePausierenToggle`/`LadeAufzeichnung`/`EntsorgeReplaySession`/`ErsetzeReplaySessionDurchFrische` inkl. `_wiedergabeBeendet`-Logik.

### `KonsolenTestDialog` (`src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`)

- [x] Button „Schritt zurück" (Z. 54–59): `AutomationProperties.Name="SchrittZurueck"`, `Command="{Binding SchrittZurueckCommand}"`, ToolTip, `Padding="10,4"`, `Margin="8,0,0,0"`.
- [x] Button „Schritt vor" (Z. 60–65): `AutomationProperties.Name="SchrittVor"`, `Command="{Binding SchrittVorCommand}"`, ToolTip, Bestandsstil.
- [x] Position: beide Buttons in der Werkzeugleiste (Grid.Row 0) zwischen `WiedergabePausieren` und der Zeitraffer-TextBox.
- [x] Code-behind unverändert (Bestands-Selektionsmechanismus deckt Schritte ab).

### `KonsolenTestDialogView` (`src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs`)

- [x] `SchrittVor()` (Z. 173–177) / `SchrittZurueck()` (Z. 183–187) — `WaitForEnabledElement(..., Medium).AsButton().ClickInForeground()` (Muster `PausierenToggle`).
- [x] `IstSchaltflaecheAktiviert(string automationName)` (Z. 193–197) — Element suchen + `IsEnabled` lesen.
- [x] `GetSelektierterQuellEintragIndex()` (Z. 201–218) — `Patterns.Selection.PatternOrDefault` + `Selection.TryGetValue`, `-1` bei leerer Selektion (dokumentierte Abweichung von `GetSelection()`, FlaUI-API-Einschränkung).

### `E2E_KonsolenTestfenster` (`src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs`)

- [x] `KonsolenTestfenster_OeffnetLaedtAufzeichnungUndSpieltAb_E2E` um Schritt-Phasen erweitert — kein neuer FlaUI-Test, bleibt in `RunGeneralTests` (`MainTest.cs:36`).
- [x] Neue 3-Chunk-Datei `schrittPfad` (Z. 34, 88–106), Cleanup im `finally` (Z. 275–276).
- [x] Phase 1 — Schritte ohne Start: Position „Chunk 0/3"→„2/3"→„1/3"→„0/3", Quell-Selektion 1/0/−1, `SchrittZurueck` an Position 0 deaktiviert (Z. 125–153).
- [x] Phase 2 — Ende per Schritt + Re-Arm: „Wiedergabe beendet.", `SchrittVor` am Ende deaktiviert, `SchrittZurueck` → „Chunk 2/3", `StartWiedergabe` setzt an Position fort → erneut beendet (Z. 155–171).
- [x] Phase 3 — pausiert → Schritt zurück → fortsetzen auf `pausePfad`: „Chunk 1/2" → Pause → `SchrittZurueck` → „Chunk 0/2" → Fortsetzen läuft erneut über „Chunk 1/2" bis beendet; Schritt-Buttons während unpausierter Wiedergabe deaktiviert (Z. 213–238).

### Unit-Tests `TerminalReplaySessionTests` (`src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplaySessionTests.cs`)

- [x] `SchrittVor_WendetNaechstenChunkZeitunabhaengigAn` (Z. 329)
- [x] `SchrittVor_FeuertOutputChunkUndBufferChanged` (Z. 352)
- [x] `SchrittVor_AmEnde_FeuertExited_IstDannNoOp` (Z. 376)
- [x] `SchrittZurueck_BautPraefixDeterministischNeuAuf` (Z. 407)
- [x] `SchrittZurueck_SetztParserZustandZurueck` (Z. 437 — chunk-übergreifende Escape-Sequenz)
- [x] `SchrittZurueck_BeiPosition0_IstNoOp` (Z. 462)
- [x] `Pausiert_Schritte_Fortsetzen_SetztAnSchrittpositionFort` (Z. 480 — `OutputChunk`-Sequenz A,A,B,C)
- [x] `SchrittVor_BisEndeBeiPausierterSchleife_TerminiertSauber` (Z. 536)
- [x] `WiedergabeStarten_NachEndeUndSchrittZurueck_SetztAnPositionFort` (Z. 583)
- [x] `Schritte_NachDispose_SindNoOp` (Z. 620)
- [x] Bestandstests unverändert erhalten (Regressionsschutz für den Schleifen-Umbau).

### Unit-Tests `KonsolenTestViewModelTests` (`src/Softwareschmiede.Tests/App/ViewModels/KonsolenTestViewModelTests.cs`)

- [x] `SchrittVor_SchrittZurueck_SynchronisierenPositionUndQuellAuswahl` (Z. 435)
- [x] `SchrittCommands_CanExecute_NachZustand` (Z. 468 — alle Zustände: geladen/unpausiert/pausiert/Ende/zurück vom Ende)
- [x] `SchrittVor_BisEnde_ZeigtStatusBeendet` (Z. 524 — inkl. `BeSameAs`-Nachweis „keine frische Session")
- [x] `Pausiert_Schritt_Fortsetzen_ViewModelEbene` (Z. 557)
- [x] `Commands_OhneAufzeichnung_SindInert` um beide neue Commands erweitert (Z. 419–420, 424–425)

## Hinweise

- **Dokumentierte Planabweichungen verifiziert:**
  - `_wiedergabeGestartet` → `_wiedergabeLoopAktiv`: vom Plan explizit erlaubt („optionale Umbenennung"), korrekt umgesetzt.
  - `RaiseExited(bool nurAmEnde)` (Z. 420–448): Ergänzung über den Plan hinaus — Position und Signal-Flag werden atomar unter `_renderLock` geprüft, sodass ein Rückwärtsschritt zwischen Schleifenende und `finally` kein spurious `Exited` auslöst. Vergleich mit dem Ausgangsstand (`99fe3db`) bestätigt: Der Fehlerpfad feuert weiterhin `Exited` (`nurAmEnde: !fehler` — bei `fehler == true` unverändert bedingungslos), die Semantik ist erhalten.
  - `ISelectionPattern.Selection.TryGetValue` statt `GetSelection()` im E2E-Wrapper: FlaUI-API-Einschränkung, funktional gleichwertig (zusätzlich Fallback über `SelectionItem.IsSelected` je Zeile).
- **Tasks-Datei:** `docs/features/task/konsolentestfenster-schrittmodus-tasks.md` ist bereits aktuell — alle 15 Tasks stehen auf „Erledigt" und sämtliche genannten Testnachweise wurden im Code verifiziert (existieren und decken die jeweilige Aufgabe ab). Keine Aktualisierung erforderlich.
- **Build-Verifikation:** `dotnet build src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj -c Debug` im Review erfolgreich ausgeführt — 0 Fehler, 0 Warnungen (kompiliert Softwareschmiede + App + Tests transitiv). Keine `dotnet test`-Läufe im Review wiederholt; die in Task 15 dokumentierten Lane-Ergebnisse stammen aus dem Implementierungslauf.
