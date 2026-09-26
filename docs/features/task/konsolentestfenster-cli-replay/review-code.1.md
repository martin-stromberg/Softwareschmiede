# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### KonsolenTestViewModel.cs (KonsolenTestViewModel)

- **Logik / UI-Zustandsmaschine (schwerwiegend)** — `WiedergabeStarten` (Zeile 250): Nach regulärem Ende der Wiedergabe setzt `OnReplayExited` `IstWiedergabeAktiv = false`, wodurch `WiedergabeStartenCommand.CanExecute` (`_replaySession is not null && !IstWiedergabeAktiv`) wieder `true` liefert. Ein erneuter Klick auf „Abspielen" ruft `TerminalReplaySession.WiedergabeStarten()` auf — das ist per `_wiedergabeGestartet`-Guard dokumentiert idempotent und startet **keinen** zweiten Durchlauf. Das ViewModel setzt aber trotzdem `IstWiedergabeAktiv = true` und `StatusText = "Wiedergabe läuft."`. Ergebnis: dauerhafter Dead-Zustand — kein Chunk läuft, kein `Exited` kommt mehr (`_exitedSignaled` bereits verbraucht), der Pause-Button ist aktiviert, der Status bleibt für immer auf „Wiedergabe läuft." stehen.

  Empfehlung: Entweder einen echten Neustart in `TerminalReplaySession` implementieren (z. B. `WiedergabeNeuStarten()`: `_aktuellerChunkIndex`/`_abgespielteChunks`/`_wiedergabeGestartet`/`_exitedSignaled` zurücksetzen, Buffer resetten, neuen CTS) und im ViewModel aufrufen, oder den Abspielen-Command nach einem beendeten Durchlauf dauerhaft deaktivieren (z. B. eigenes Flag `_wiedergabeBereitsGestartet` im ViewModel in das CanExecute aufnehmen). Aktuell ist Verhalten und UI-Text inkonsistent.

- **Concurrency / UI-Thread** — `OeffneAufzeichnungAsync` (Zeile 183) ruft `await _store.LadeAsync(pfad, ct)` auf, aber `CliReplayAufzeichnungStore.LadeAsync` enthält kein einziges `await` (endet in `Task.FromResult`) und `SchreibeAsync` nutzt synchrones `BinaryWriter.Write`. Das Parsen einer bis zu 8 MB großen Aufzeichnung (u. U. tausende Chunk-Records) plus das sofortige `CliChunkQuelltextFormatter.Formatiere` für jeden Chunk in `LadeAufzeichnung` laufen komplett auf dem UI-Thread — spürbares Einfrieren des Dialogs bei großen Dateien.

  Empfehlung: Laden und Formatieren der `QuellEintraege` in `Task.Run` auslagern (die ObservableCollection erst danach auf dem UI-Thread befüllen) bzw. die Async-Methoden des Stores tatsächlich async implementieren.

- **Concurrency / Event-Abmeldung** — `LadeAufzeichnung` (Zeile 199) disposed die bisherige Session, meldet aber `session.BufferChanged`/`session.Exited` nie ab. Ein nach dem Austausch noch in-flight feuernder `BufferChanged` der alten (abgebrochenen) Session ruft `OnReplayBufferChanged` auf, das dann `PositionsText`/`AktuellerQuellEintrag` anhand der *neuen* Session/Daten aktualisiert — transient falscher Zustand der neu geladenen Aufzeichnung.

  Empfehlung: Vor `_replaySession?.Dispose()` die alte Instanz in einer lokalen Variablen halten und `alteSession.BufferChanged -= OnReplayBufferChanged; alteSession.Exited -= OnReplayExited;` abmelden.

### KiAusfuehrungsService.cs (KiAusfuehrungsService)

- **Ressourcenmanagement / Concurrency** — `_aufzeichnungen` (Zeile 23, Befüllung Zeile 270) wächst unbegrenzt: Pro gestarteter Terminal-Session verbleibt ein `CliOutputRecorder` mit bis zu `AufzeichnungByteBudget` (Standard 8 MB) Rohbytes im Speicher. Einträge werden nie entfernt — weder beim Session-Ende (bewusst, für den Export), noch beim Löschen der Aufgabe, noch bei `Dispose()`. Bei vielen Aufgaben über die App-Laufzeit summiert sich das auf ein Vielfaches von 8 MB.

  Empfehlung: Retention-Strategie ergänzen — z. B. Eintrag entfernen, wenn die zugehörige Aufgabe gelöscht wird (Hook am entsprechenden Lifecycle-Ereignis), oder die Dictionary-Größe auf die letzten N Aufzeichnungen begrenzen (älteste verwerfen). Alternativ Budget-Verbrauch über alle Recorder kumulativ deckeln.

### CliReplayAufzeichnungStore.cs (CliReplayAufzeichnungStore)

- **Fehlerbehandlung** — `LadeAsync` (Zeile 93–98): `pluginNameLength` wird nur auf `< 0` geprüft, nicht auf eine Obergrenze. `reader.ReadBytes(pluginNameLength)` allokiert sofort ein `byte[]` der angegebenen Größe — eine korrupte/bösartige Datei mit z. B. `pluginNameLength = int.MaxValue` führt zu `OutOfMemoryException` statt `InvalidDataException`. Für die Chunk-Länge existiert genau dieser Schutz bereits (Zeile 117, `length > stream.Length - stream.Position` bei seekbaren Streams) — für das Header-Feld fehlt er.

  Empfehlung: Denselben Restlängen-Check wie für Chunk-Längen auch auf `pluginNameLength` anwenden (oder eine harte Obergrenze wie z. B. 64 KB definieren).

- **Korrektheit (Serialisierung)** — `SchreibeAsync` schreibt `aufzeichnung.StartUtc.Ticks` / `EndeUtc?.Ticks` (Zeilen 28–29). `DateTimeOffset.Ticks` ist die Uhrzeit *inklusive* lokalem Offset, nicht UTC-normalisiert; `LadeAsync` rekonstruiert mit `new DateTimeOffset(ticks, TimeSpan.Zero)` als UTC. Für Recorder-Ausgaben (immer `GetUtcNow()`, Offset 0) identisch — aber die `CliOutputAufzeichnung`-API erlaubt beliebige Offsets, die dann still um den Offset verschoben werden.

  Empfehlung: `UtcTicks` statt `Ticks` schreiben (bzw. den Offset explizit mitspeichern, falls er semantisch relevant ist).

- **Namenskonvention** — Schreibseite heißt `SchreibeAsync` (Stream) vs. `SpeichernAsync` (Datei), während die Leseseite für beide Overloads einheitlich `LadeAsync` heißt. Inkonsistentes Verb-Paar (schreiben↔lesen vs. speichern↔laden).

  Empfehlung: Verben vereinheitlichen, z. B. `SpeichernAsync`/`LadeAsync` für beide Overload-Gruppen oder `SchreibeAsync`/`LeseAsync`.

### TerminalReplaySession.cs (TerminalReplaySession)

- **Fehlerbehandlung / Vertragstreue** — `Process` (Zeile 28, 60) liefert ein nie gestartetes `new Process()`: `HasExited`, `ExitCode` und `Id` werfen `InvalidOperationException` für jeden generischen `ITerminalSession`-Konsumenten. Aktuell schützt nur `TaskDetailView.TryGetProcessId` mit try/catch davor — das Interface selbst dokumentiert „Der verwaltete Prozess der Sitzung", was für die Replay-Session nicht zutrifft.

  Empfehlung: Mindestens im XML-Doc von `TerminalReplaySession.Process` festhalten, dass der Stub nicht gestartet ist und nahezu jeder Zugriff wirft; mittelfristig prüfen, ob `ITerminalSession.Process` eine nullable/alternative Abstraktion braucht.

- **Design-Konsistenz** — Die im Header gespeicherte Geometrie (`Cols`/`Rows`) ist faktisch wirkungslos: `TerminalReplaySession.Resize` liefert immer `true` ohne zu resizen, und `TerminalControl.OnSessionChanged`/`OnRenderSizeChanged` rufen `_buffer.Resize(cols, rows)` mit der Control-Geometrie direkt auf — die Aufzeichnungs-Geometrie wird beim Binden sofort überschrieben. Zusätzlich werden Resize-Ereignisse während der Aufzeichnung gar nicht erst mitgeschnitten, sodass ein Replay nie die Live-Geometrie reproduzieren kann.

  Empfehlung: Bewusst dokumentieren (XML-Doc: „die Wiedergabe nutzt die aktuelle Control-Geometrie, nicht die aufgezeichnete") — oder, falls die aufgezeichnete Geometrie relevant ist, das Rebind-Verhalten des TerminalControl für Replay-Sessions anpassen.

- **Concurrency (Pause-Granularität)** — `Pausieren()` garantiert nicht „kein weiterer Chunk", wie der XML-Doc behauptet: Kommt der Aufruf zwischen der `WartePauseGateAsync`-Prüfung und der Chunk-Anwendung an (relevant bei `ZeitrafferSchwelle = 0`, wo alle Delays 0 sind), wird genau ein in-flight Chunk trotz Pausierung noch angewendet.

  Empfehlung: Entweder Doc relativieren („best-effort auf Chunk-Granularität") oder `_istPausiert` innerhalb des `_renderLock`-Blocks vor dem Apply erneut prüfen.

- **Toter Code** — `ChunkAnzahl` (Zeile 81) wird nirgends aufgerufen (nur im eigenen Doc-Kommentar referenziert).

  Empfehlung: Entfernen oder Verwendungszweck nachziehen.

### KonsolenTestDialogView.cs (E2E-Test-View)

- **Toter Code** — `GetFehlerMeldung()` (Zeile 79) und `PausierenToggle()` (Zeile 108) werden in keinem Test aufgerufen.

  Empfehlung: Entfernen oder einen E2E-Fall ergänzen, der sie nutzt (z. B. Pause-Fortsetzen im Dialog). Hinweis: `GetPositionsText()`-Doc (Zeile 118) nennt das Format „3 / 3 Chunks", tatsächlich wird „Chunk 3/3" ausgegeben — Docstring korrigieren.

### E2E_KonsolenTestfenster.cs (End2EndTest)

- **Testqualität** — `Assert.Contains("2", dialog.GetPositionsText(), ...)` (Zeile 82) ist eine schwache Substring-Assertion: „2" trifft auch „Chunk 2/12" oder „Chunk 12/20". Der konkrete Erwartungswert ist bekannt („Chunk 2/2").

  Empfehlung: `Assert.Equal("Chunk 2/2", dialog.GetPositionsText())`.

### CliReplayAufzeichnungStoreTests.cs (CliReplayAufzeichnungStoreTests)

- **Testqualität / Lesbarkeit** — `LadeAsync_AbgeschnittenerRecordHeader_WirftInvalidDataException` (Zeile 181): `var gekuerzt = stream.ToArray()[..(int)(stream.Length + 0)]` ist ein No-Op-Slice der gesamten Länge — die Variable heißt „gekuerzt", es wird aber nichts gekürzt (die Teil-Bytes werden anschließend an `verkuerzt` *angehängt*). Umständlicher, irreführender Zwischenschritt.

  Empfehlung: Direkt `verkuerzt.Write(stream.ToArray())` verwenden oder die Variable zweckentsprechend benennen.

## Geprüfte Dateien

- `src/Softwareschmiede/Infrastructure/Terminal/CliOutputRecorder.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CliOutputAufzeichnung.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CliOutputChunkRecord.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CompositeTerminalOutputSink.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/ITerminalDiagnoseSink.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/CliReplayAufzeichnungStore.cs`
- `src/Softwareschmiede/Infrastructure/Terminal/TerminalReplaySession.cs`
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
