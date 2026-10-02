# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Verifikation der Runde-1-Befunde

Alle 14 Befunde aus `review-code.1.md` wurden am echten Code verifiziert als behoben:

- **Dead-State nach Wiedergabeende:** Behoben — `KonsolenTestViewModel.WiedergabeStarten` erzeugt über `_wiedergabeBeendet` eine frische `TerminalReplaySession` (Zeile 294–302).
- **UI-Thread-Blockade beim Laden:** Behoben — Quell-Formatierung läuft in `Task.Run` (Zeile 191), Store-I/O ist echt async (`useAsync: true`, `CopyToAsync` in `CliReplayAufzeichnungStore`).
- **Event-Abmeldung beim Session-Wechsel:** Behoben — `EntsorgeReplaySession` meldet `BufferChanged`/`Exited` vor dem Dispose ab (Zeilen 281–283); zusätzlich prüfen beide Handler die Sender-Identität per `ReferenceEquals` (vor und nach dem Dispatch).
- **Unbegrenztes `_aufzeichnungen`:** Behoben — `RegistriereAufzeichnung` begrenzt die Registry via `LinkedList` auf `MaxAufzeichnungenAnzahl = 8`.
- **`pluginNameLength` ohne Obergrenze:** Behoben — Restlängen-Check in `CliReplayAufzeichnungStore.Lese` (Zeile 133).
- **`Ticks` statt `UtcTicks`:** Behoben — `SpeichernAsync` schreibt `UtcTicks` (Zeilen 32–33), Roundtrip-Test `Roundtrip_ZeitstempelMitOffset_WerdenUtcNormalisiert` belegt die Normalisierung.
- **Namensinkonsistenz Schreiben↔Lesen:** Behoben — einheitlich `SpeichernAsync`/`LadeAsync`.
- **`Process`-Stub-Vertrag:** Behoben — XML-Doc an `TerminalReplaySession.Process` dokumentiert den nicht gestarteten Stub.
- **Aufgezeichnete Geometrie wirkungslos:** Behoben — im Klassen-Kommentar von `TerminalReplaySession` dokumentiert.
- **Pause-Granularität:** Behoben — `_istPausiert` wird unter `_renderLock` gesetzt und vor dem Chunk-Apply erneut geprüft (siehe aber Befund zum Lock-Splitting unten).
- **`ChunkAnzahl` toter Code:** Behoben — entfernt.
- **Tote Methoden `GetFehlerMeldung`/`PausierenToggle`:** Behoben — beide werden im E2E-Test genutzt; Docstring des Positionsformats korrigiert.
- **Schwache Assertion `"2"`:** Behoben — `Assert.Equal("Chunk 2/2", ...)`.
- **No-Op-Slice im Store-Test:** Behoben — direktes `verkuerzt.Write(stream.ToArray())`.

## Befunde

### KonsolenTestViewModel.cs (KonsolenTestViewModel)

- **Fehlerbehandlung / fehlende Validierung** — `TryParseZeitrafferSchwelle` (Zeile 384): `double.TryParse` akzeptiert sehr große Werte (z. B. `1e13`, `999999999999`), `NaN`/`Infinity`/Negativ werden abgefangen — aber `TimeSpan.FromSeconds(sekunden)` wirft für Sekunden > ~9,2·10¹¹ eine `OverflowException`. Die Methode verletzt damit ihren Try-Vertrag (wirft statt `false` zu liefern) an drei Stellen: im Property-Setter (wird von der WPF-Bindung still geschluckt — die vorgesehene `ZeitrafferValidierungsFehler`-Meldung erscheint nicht), in `LadeAufzeichnung` → `ErzeugeReplaySession` (wird als „konnte nicht geladen werden" falsch eingeordnet) und in `WiedergabeStarten` → `ErzeugeReplaySession` im Neustart-Zweig — dort propagiert die Exception durch `RelayCommand.Execute` unbehandelt auf den UI-Thread.

  Empfehlung: Vor `TimeSpan.FromSeconds` eine Obergrenze prüfen, z. B. `if (sekunden > TimeSpan.MaxValue.TotalSeconds) return false;` (oder `FromSeconds` in try/catch → `false`).

- **Toter Code / doppelte Fallback-Ebene** — `LadeAufzeichnung` (Zeilen 209–223): Der `effektiv`-Zweig für `Cols <= 0 || Rows <= 0` ist unerreichbar — `CliReplayAufzeichnungStore.Lese` wirft für genau diesen Fall bereits eine `InvalidDataException` (Zeile 131–132), und der `CliOutputRecorder` schreibt stets positive Werte. Damit existieren drei Default-Schichten für denselben Fall (Store-Validierung, dieser Fallback auf `DefaultCols`/`DefaultRows`, `Math.Max(1, ...)` im `TerminalReplaySession`-Konstruktor) mit unterschiedlichen Ersatzwerten.

  Empfehlung: Den `effektiv`-Zweig entfernen und direkt `aufzeichnung` verwenden — die Store-Validierung ist die einzig mögliche Quelle.

- **Zustandskonsistenz nach Dispose** — `EntsorgeReplaySession` (Zeilen 273–284) setzt `_replaySession` nicht auf `null` (und `Session` bleibt auf die disposed Session gebunden). Nach `Schliessen()`/`Dispose()` zeigt das Feld weiter auf eine disposed Session, `Session` bleibt am Terminal-Control gebunden und `WiedergabeStartenCommand.CanExecute` (`_replaySession is not null && !IstWiedergabeAktiv`) liefert weiter `true`. Kombiniert mit dem Befund oben wird das erreichbar: wirft `ErzeugeReplaySession` in `LadeAufzeichnung` (Overflow aus `TryParseZeitrafferSchwelle`), verbleibt `_replaySession` auf der disposed Vorgänger-Session — ein Klick auf „Abspielen" setzt dann `IstWiedergabeAktiv = true` / „Wiedergabe läuft.", obwohl `session.WiedergabeStarten()` per `_disposed`-Guard nichts tut — der in Runde 1 behobene Dead-State auf einem Nebenpfad.

  Empfehlung: In `EntsorgeReplaySession` nach dem Dispose `_replaySession = null` setzen (und bei `Schliessen`/`Dispose` auch `Session = null`), damit `CanExecute` den disposed-Zustand korrekt abbildet.

### TerminalReplaySession.cs (TerminalReplaySession)

- **Concurrency (Lock-Splitting)** — `_istPausiert` wird unter zwei verschiedenen Locks geschrieben: `Pausieren()` setzt es unter `_renderLock` (Zeile 145), `Fortsetzen()` unter `_pauseLock` (Zeile 154). Die Invariante „`_istPausiert == true` ⇒ Gate geschlossen" ist damit nicht atomar: Bei nebenläufigem `Pausieren`/`Fortsetzen` kann `Fortsetzen` das von `Pausieren` frisch geschlossene Gate wieder öffnen, bevor `Pausieren` das Flag setzt — Ergebnis: Flag `true` bei offenem Gate. Die `while (!angewendet)`-Schleife (Zeilen 249–263) läuft dann in einer CPU-Busy-Loop (Gate sofort erfüllt, Flag verhindert den Apply). Aktuell sind alle Aufrufer (ViewModel-Toggle, `Dispose`) auf den UI-Thread serialisiert — der `volatile`-Modifier und die aufwändige Gate-Konstruktion signalisieren aber Querschnitts-Absicht; die Klasse ist öffentliche Infrastruktur.

  Empfehlung: Flag und Gate unter einem Lock konsistent halten — z. B. `_istPausiert` in `Pausieren` zuerst unter `_pauseLock` setzen (zusammen mit dem Gate-Swap) und danach `_renderLock` nur kurz als Fence nehmen, um einen in-flight Apply abzuwarten; `Fortsetzen` bleibt wie gehabt unter `_pauseLock`.

### KonsolenTestViewModelTests.cs (KonsolenTestViewModelTests)

- **Testqualität / Race** — `Wiedergabe_LaeuftBisEnde_UndSynchronisiertQuellAnsicht` (Zeilen 168–170): Mit `ZeitrafferSchwelleText = "0"` und dem synchronen `dispatcherInvoke: action => action()` läuft `OnReplayExited` inline auf dem Playback-Thread und setzt `IstWiedergabeAktiv = false` / `StatusText = "Wiedergabe beendet."` nebenläufig zum Test-Thread. Die Asserts `IstWiedergabeAktiv.Should().BeTrue()` und `StatusText.Should().Be("Wiedergabe läuft.")` hängen davon ab, dass die 2-Chunk-Wiedergabe den Assert nicht überholt — unter Last schlägt das sporadisch fehl.

  Empfehlung: Den transienten Zwischenzustand nicht synchron asserten — nur den Endzustand über `WarteBisAsync` prüfen (wie im Neustart-Test) oder beide Zustände akzeptieren.

### SettingsViewModel.cs (SettingsViewModel)

- **Namenskonvention / veraltete Dokumentation** — `KonsolenTestOeffnenCommand` (Zeile ~247): Der XML-Doc sagt „Öffnet das **modale** Konsolentestfenster" — das Fenster ist seit der Nacharbeit nicht-modal (`WpfDialogService.ShowKonsolenTestDialogAsync` nutzt `Show()`).

  Empfehlung: „nicht-modal" im Docstring korrigieren.

## Geprüfte Dateien

- `src/Softwareschmiede/Infrastructure/Terminal/CliOutputRecorder.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CliOutputAufzeichnung.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CliOutputChunkRecord.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CompositeTerminalOutputSink.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/ITerminalDiagnoseSink.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CliReplayAufzeichnungStore.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplayBuffer.cs` (Querverweis)
- `src/Softwareschmiede/Infrastructure/Terminal/AnsiSequenceParser.cs` (Diff)
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs` (Diff)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionOptions.cs` (Diff)
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalSessionService.cs` (Diff)
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Diff)
- `src/Softwareschmiede/Application/Services/CliOutputProtokollWriter.cs` (Diff)
- `src/Softwareschmiede/appsettings.json` (Diff)
- `src/Softwareschmiede.App/App.xaml.cs` (Diff)
- `src/Softwareschmiede.App/Services/IDialogService.cs` (Diff)
- `src/Softwareschmiede.App/Services/WpfDialogService.cs` (Diff)
- `src/Softwareschmiede.App/Services/CliChunkQuelltextFormatter.cs`
- `src/Softwareschmiede.App/Services/CliReplayExportService.cs`
- `src/Softwareschmiede.App/ViewModels/KonsolenTestViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/CliChunkAnzeigeEintrag.cs`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` (Diff)
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (Diff)
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml`
- `src/Softwareschmiede.App/Views/KonsolenTestDialog.xaml.cs`
- `src/Softwareschmiede.App/Views/SettingsView.xaml` (Diff)
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` (Diff)
- `src/Softwareschmiede.App/Controls/TerminalControl.cs` (Querverweis, Session-Rebind)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/CliOutputRecorderTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/CompositeTerminalOutputSinkTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/CliReplayAufzeichnungStoreTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalReplaySessionTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/AnsiSequenceParserTests.cs` (Diff)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/PseudoConsoleSessionTests.cs` (Diff)
- `src/Softwareschmiede.Tests/Infrastructure/Terminal/TerminalSessionServiceTests.cs` (Diff)
- `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceCliAufzeichnungTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/CliOutputProtokollWriterTests.cs` (Diff)
- `src/Softwareschmiede.Tests/App/ViewModels/KonsolenTestViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_CliReplayExport.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs` (Diff)
- `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests_IdePlugin.cs` (Diff)
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelUpdateTestBase.cs` (Diff)
- `src/Softwareschmiede.Tests/App/Services/CliReplayExportServiceTests.cs`
- `src/Softwareschmiede.Tests/App/Services/CliChunkQuelltextFormatterTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_KonsolenTestfenster.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ConPtyLifecycle.cs` (Diff)
- `src/Softwareschmiede.Tests/E2E/MainTest.cs` (Diff)
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/KonsolenTestDialogView.cs`
- `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs` (Diff)
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs` (Diff)
- `src/Softwareschmiede.Tests/E2E/Views/WindowExtensions.cs` (Diff)
- `src/Softwareschmiede.Tests/Helpers/TestKiAusfuehrungsServiceFactory.cs` (Diff)
- `src/Softwareschmiede.Tests/Helpers/TestTerminalSessionFactory.cs` (Diff)
