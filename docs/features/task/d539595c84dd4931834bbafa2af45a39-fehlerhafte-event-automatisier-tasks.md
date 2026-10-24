# Tasks: Versionsbereiche und ehrliche Fehlermeldung in der SCM-CLI-Erkennung

Plan: [`d539595c84dd4931834bbafa2af45a39-fehlerhafte-event-automatisier/plan.md`](d539595c84dd4931834bbafa2af45a39-fehlerhafte-event-automatisier/plan.md)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---|---|---|---|
| 1 | Neue Typen | `ScmCliPromptProfile`-Katalog mit `ScmCliVersionRange`, `ScmCliPromptMatch`, `ScmCliRecognition`, `ScmCliRecognitionState` in `src/Softwareschmiede/Application/Services/ScmCliPromptProfile.cs` anlegen; Profile für Codex, ClaudeCli, Devin (Versionsbereiche) und KiSimulator (Banner-only) | Offen | — |
| 2 | Neue Typen | `ScmCliWaitReason` in `src/Softwareschmiede/Application/Services/ScmCliWaitReason.cs` anlegen (`UnknownReady`, `ConfirmableMarker`, `Describe(ScmCliRecognition)`, `IsConfirmable`) | Offen | — |
| 3 | Adapter | `IScmPromptSessionAdapter` um `CliRecognition` erweitern; `ScmPromptSessionAdapter`: `_recognizedCli` → `_cliRecognition`, `RecognizeStartup` auf Profil-/Bereichserkennung mit Versionsextraktion umstellen, `RefreshScreen`-Gate differenzieren | Offen | — |
| 4 | Adapter | `CheckExchangeAccessCoreAsync`: Erkennungsbedingung auf `State != Supported` (+ `_formatUserConfirmed` bei Empfehlung Offenem Punkt 2) umstellen, Reason aus `ScmCliWaitReason.Describe`; `ConfirmReady` an neue Erkennungszustände anpassen (Entscheidung Offener Punkt 2 umsetzen) | Offen | — |
| 5 | Orchestrator | `ScmEventOrchestrator.DispatchCoreAsync`: Warte-Grund-Verzweigung um `CliRecognition`-Fall erweitern; `ConfirmReadyAsync`-Fehlertext um konkreten Erkennungsgrund ergänzen | Offen | — |
| 6 | ViewModel | `ScmEventQueueItem.CanConfirmReady` auf `ScmCliWaitReason.IsConfirmable(Reason)` umstellen | Offen | — |
| 7 | Test-Infrastruktur | `ScmKiProfile.Devin` um optionalen `banner`-Parameter erweitern (`ScmDispatchFixture.cs`) | Offen | — |
| 8 | Tests (Integration) | `ScmEventCliExchangeAccessTests(.cs/.PendingProbe.cs)`: In-Range-Banner ergänzen; neue Theorien `UnsupportedVersion_ReportsHonestReason`, `UnparseableVersion_ReportsVersionNotDetermined`, `UnknownCli_ReportsHonestReason` | Offen | — |
| 9 | Tests (Integration) | `ScmEventDispatchTests_Devin`: `DevinTask_WithSupportedPatchVersion_DeliversEvent` und `DevinTask_WithUnsupportedVersion_WaitsWithHonestReason` ergänzen | Offen | — |
| 10 | Tests (Unit) | `ScmEventOrchestratorTests`: `UnknownCliFormat_WaitsWithoutConfirmOption` und `ConfirmReady_UnsupportedVersion_*` je Entscheidung; neue `ScmCliPromptProfileTests` für Bereichsgrenzen/Token-Parsing | Offen | — |
| 11 | Test-Infrastruktur (Plugin) | Fixture-Gate `unknown-banner` in `KiSimulatorPlugin.cs` (unterdrückt `SCM_SIMULATOR_PROTOCOL_V1`-Startzeile nur im Fixture-Modus) | Offen | — |
| 12 | E2E | `E2E_ScmEvents.cs`: neuen Abschnitt im bestehenden Test — ehrlicher Grund bei nicht erkanntem Banner, `ScmEventConfirmReady` nicht sichtbar, Zustellung nach regulärem CLI-Neustart; bestehende `unknown-ready`-Assertions verifizieren | Offen | — |
| 13 | Dokumentation | `docs/help/plugins/github-ereignisse/`: `architektur.md` (Z. 20) und `ablauf-technisch.md` (Z. 35) auf Versionsbereiche umstellen; `troubleshooting.md` neuen Fehlerfall ergänzen; `beschreibung.md`/`installation.md`/`ablauf-anwender.md` Formulierungen zu „unbekannte Versionen" angleichen | Offen | — |
| 14 | Verifikation | `dotnet build Softwareschmiede.slnx -c Debug`; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test` beider Testprojekte (`Category!=OsInterface`), danach OsInterface-Lanes (synchron, nicht im Hintergrund); `dotnet format Softwareschmiede.slnx --verify-no-changes` | Offen | — |
