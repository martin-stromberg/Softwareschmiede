## Testklassen

### `ProtokollServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/ProtokollServiceTests.cs`

- `GetByAufgabeAsync_ShouldReturnEntriesOrderedByZeitstempel_WhenMultipleEntriesExist` — prüft chronologische Reihenfolge.
- `AddCliOutputAsync_ShouldCreateSingleEntry_WhenNoMarker` — prüft Persistierung normaler CLI-Ausgabe als `CliOutput`.
- `AddCliOutputAsync_ShouldCreateRateLimitEntry_WhenMarkerDetected` — prüft zusätzlichen `RateLimit`-Eintrag bei Marker.
- `TryParseRateLimitMarker_*`-Tests — prüfen Marker-Erkennung mit/ohne gültigen Zeitstempel.

### `CliOutputLineAccumulatorTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/CliOutputLineAccumulatorTests.cs`

- `Chunks_MitMehrerenLfZeilen_LiefertZeilenInReihenfolge` — Zeilenreihenfolge bei LF.
- `Chunks_MitGeteilterZeile_UeberChunkGrenze_LiefertEineZeile` — chunkübergreifender Zeilenaufbau.
- `Chunks_MitCrLf_ZaehltNichtDoppelt` und `Chunks_MitEinzelnemCr_FlushtProgressZeile` — Trennzeichenverhalten (`CRLF`/`CR`).
- `Flush_MitRestzeile_SpeichertUnvollstaendigeLetzteZeile` — Restzeile ohne finalen Zeilentrenner.

### `CliOutputProtokollWriterTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/CliOutputProtokollWriterTests.cs`

- `CompleteAsync_DraintAngenommeneZeilen_BevorProviderDisposedWird` — Drain-Verhalten und Persistenzreihenfolge.
- `CliOutputProtokollWriter_Persistenzfehler_BeeintraechtigtSessionNicht` — Fehlerpfad ohne Absturz.
- `CliOutputProtokollWriter_HoheAusgabe_BegrenztQueueMitBackpressure` — bounded queue/backpressure.
- `CompleteAsync_BackpressureWaerendAktivemChunk_VerliertKeineDekodiertenZeilen` — kein Zeilenverlust bei konkurrierendem Abschluss.

### `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

- `StartWithPseudoConsoleAsync_PersistiertSessionOutputAlsCliOutput` — automatische Persistenz von Session-Output.
- `StartWithPseudoConsoleAsync_ParalleleAufgaben_TrenntProtokolleNachAufgabeId` — Trennung je Aufgabe.
- `AddCliOutputAsync_RateLimitMarkerAusConPtyOutput_ErzeugtRateLimitEintrag` — Markerpfad über ConPTY/Writer.
- `StartWithPseudoConsoleAsync_RestzeileOhneZeilentrenner_PersistiertCliOutput` — Restzeilenpersistenz.
- `StartWithPseudoConsoleAsync_ProzessEndeVorReadLoopDrain_VerliertTailOutputNicht` — kein Tail-Output-Verlust beim Prozessende.

### `ProtocolLoggingServiceIntegrationTests`
Datei: `src/Softwareschmiede.Tests/ServiceIntegration/ProtocolLoggingServiceIntegrationTests.cs`

- `AddCliOutputAsync_StreichtAusgabeInProtokoll` — CLI-Ausgabe landet als `CliOutput` im Protokoll.
- `AddCliOutputAsync_MehrereZeilen_SindInReihenfolgeGespeichert` — Reihenfolge mehrerer Ausgaben.

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

- `LadeProtokolleAsync_ShouldLoadProtocols_WhenSuccessful` — Befüllung von `Protokolleintraege`.
- `LadenAsync_ShouldNotWaitForProtocols` — Protokolle werden asynchron im Hintergrund nachgeladen.
- `ShowCliPanel_IsTrue_WhenStatusGestartet` / `...WhenStatusWartend` / `...WhenAusfuehrungBeendetIst` — Sichtbarkeit der CLI-Ansicht.
- `CliStoppenCommand_LeertAktivenCliName` — CLI-Stopp aktualisiert UI-Zustand.

### `TaskDetailViewTests`
Datei: `src/Softwareschmiede.Tests/App/Views/TaskDetailViewTests.cs`

- `Xaml_CliKonsole_IstVertikalScrollbar` — prüft Struktur und Automation-Namen der CLI-Konsole.
- `TerminalScrollViewer_Clickziel_SteuertFokuspfad` — prüft Fokuslogik Klickfläche vs. ScrollBar.

## Hilfsmethoden

### `TaskDetailViewModelTestFactory`
Datei: `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`

- `Create(...)` — baut ein vollständiges `TaskDetailViewModel` mit produktionsnahen Abhängigkeiten (inkl. `ProtokollService`).
- `CreateStub()` — erstellt ein `FileExplorerViewModel`-Stub für isolierte Tests.
- `CreateAutonomAufgabeStartService(...)` — erstellt Testinstanz des Startservices.
- `CreateDefaultServiceProvider(...)` — liefert Standard-`IServiceProvider` für Rehydrierungspfade.

### Hilfsmethoden in `KiAusfuehrungsServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/KiAusfuehrungsServiceTests.cs`

- `WaitForCliOutputAsync(...)` / `WaitForProtokollEntriesAsync(...)` / `GetProtokollEntries(...)` — Polling-Helfer für asynchrone Protokollpersistenz.
- `CreateCliOutputServiceProvider()` — InMemory-ServiceProvider für CLI-Output-Tests.
- `CreateNoOpPlugin()` / `CreatePlugin(...)` — Test-Plugins für Startpfade.
- Launcher-Testdoubles (`FixedOutputPseudoConsoleProcessLauncher`, `OutputByTaskPseudoConsoleProcessLauncher`, `DelayedOutputPseudoConsoleProcessLauncher`) — kontrollierte Output-/Timing-Szenarien.

## Fehlende testspezifische Abdeckung im Anforderungsfokus

- Es gibt keine bestehenden Tests für einen Export-Command in `TaskDetailViewModel`.
- Es gibt keine bestehenden Tests für einen Save-Dialog-Pfad (`IDialogService`) mit Dateiendung `*.raw`.
- Es gibt keine bestehenden UI-Tests für einen CLI-Rohdaten-Export-Button in `TaskDetailView.xaml`.
