# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Gegenstand: Umsetzungsreview (Iteration 2) des Plans [plan.md](plan.md) gegen den aktuellen Stand unter `src/` (Branch `task/5c1a753c335f441294221c1e260a87c3-prereleases`, HEAD `9bb3585`). Die Befunde der Iteration-1-Reviews (`review.1.md`, `review-code.1.md`, `review-usability.1.md`) wurden in den Commits `1714e2a` und `850a1cc` behoben und sind im aktuellen Stand verifiziert.

Einschraenkung dieser Review: Per Vorgabe wurden **keine Builds und keine Tests ausgefuehrt** und kein Produktivcode geaendert. Alle Aussagen beruhen auf Quellcode-Inspektion; die zwischenzeitlich erbrachten Ausfuehrungsnachweise wurden anhand des committed `test-results.md` (Commit `9bb3585`) geprueft, nicht erneut ausgefuehrt.

## Umgesetzte Planelemente

### Neue Klassen

- [x] `UpdateMode` (Enum, `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`, Zeilen 30-40) — feste Werte `Aus = 0`, `NurPruefen = 1`, `BeiProgrammstartPruefenUndAusfuehren = 2`
- [x] `UpdateSettings` (unveraenderliches Record, `UpdateModels.cs:45`) — gemeinsam geladenes Modus-/Prerelease-Paar
- [x] `UpdateCheckOptions` (unveraenderliches Record, `UpdateModels.cs:49`) — Optionsvertrag fuer Service und Releaseclient
- [x] `SemanticUpdateVersion` (Value Object, `src/Softwareschmiede/Application/Services/Updates/SemanticUpdateVersion.cs`) — Kernversion, Prerelease-Identifier, Metadaten, volle SemVer-Praezedenz; keine neue NuGet-Abhaengigkeit
- [x] `UpdateE2ETestConfiguration` (`src/Softwareschmiede.App/Services/Testing/UpdateE2ETestConfiguration.cs`) — Szenariodatei, Testwurzel, Protokoll; aktiviert nur mit beiden Variablen (`SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` UND `SOFTWARESCHMIEDE_TEST_DB_PATH`), bricht mit Diagnose ab statt Fallback
- [x] `UpdateFixtureHttpMessageHandler` (`src/Softwareschmiede.App/Services/Testing/UpdateFixtureHttpMessageHandler.cs`) — JSON-Seiten/ZIP-Streams, `Link`-Header, Fehlerfaelle, Antwort- und Stream-Gates (`GateBlockierterStream`), unbekannte Requests scheitern ohne Netzwerkfallback
- [x] `RecordingUpdateProcessLauncher` (`src/Softwareschmiede.App/Services/Testing/RecordingUpdateProcessLauncher.cs`) — protokolliert Skriptpfad/Arbeitsverzeichnis/Argumente/Elevation, steuerbarer Erfolg/Fehler/Ausnahme, kein Prozessstart
- [x] `RecordingApplicationShutdownService` (`src/Softwareschmiede.App/Services/Testing/RecordingApplicationShutdownService.cs`) — protokolliert `ShutdownRequested`, laesst Testfenster offen
- [x] `FixtureCliUpdateSafetyService` (`src/Softwareschmiede.App/Services/Testing/FixtureCliUpdateSafetyService.cs`) — szenariogesteuerte riskante Aufgaben fuer echte Ja/Nein-Dialoge ohne ConPTY
- [x] `UpdateSettingsReadFailureInterceptor` (`src/Softwareschmiede.App/Services/Testing/UpdateSettingsReadFailureInterceptor.cs`) — EF-`DbCommandInterceptor` nur auf `UpdateSettings.Read`-getaggte Abfragen; deaktivierbar, `SettingsReadReached`-Ereignis, versuch-/grenzbezogene Ordinalzaehlung, begrenzte/cancelbare Gate-Freigabe, Fehler bleibt bis Deaktivierung aktiv
- [x] `UpdateE2EFixture` (`src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`) — eindeutige Tempwurzel mit `installed/version.json` (1.2.0), eigener SQLite-DB, `scenario.json`, Stable-`1.3.0`-/RC-`1.4.0-rc.1`-ZIPs mit Markern und getrennten URLs, Gates, `events.jsonl`, Lesefehler-/Gate-Steuerung, Fixture-Cleanup

### Geaenderte bestehende Klassen

- [x] `AppEinstellungService` (`src/Softwareschmiede/Application/Services/AppEinstellungService.cs`) — `UpdateModeKey`/`IncludePrereleasesKey`/`UpdateSettingsReadTag`; `GetUpdateSettingsAsync` liest beide Werte in einer getaggten Abfrage mit Defaults `NurPruefen`/`false` und propagiert Lesefehler (T-09); `SetUpdateSettingsAsync` validiert per `Enum.IsDefined` und schreibt beide Werte in einem `SaveChangesAsync`
- [x] `SettingsViewModel` (`src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs`) — `SelectedUpdateMode`, `IncludePrereleases`, `UpdateModusOptionen` (drei exakte Labels); `LadenAsync` liest `GetUpdateSettingsAsync`; `SpeichernAsync` validiert das Label, ruft `SetUpdateSettingsAsync` und feuert `UpdateSettingsSaved` direkt danach (auch bei spaeterem Fremdfehler); `VerwerfenAsync` feuert kein Event
- [x] `SettingsView.xaml` (`src/Softwareschmiede.App/Views/SettingsView.xaml:295-310`) — ComboBox `AutomationProperties.Name="Update-Modus"` an `UpdateModusOptionen` mit umbrechendem ItemTemplate; CheckBox `AutomationProperties.Name="Prerelease-Versionen laden"` mit TwoWay-Bindung
- [x] `UpdateInterfaces.cs` — explizite Signaturen `CheckForUpdateAsync(UpdateCheckOptions, ct)` und `GetLatestReleaseAsync(UpdateCheckOptions, ct)`; alle Aufrufer/Mocks umgestellt, keine Legacy-Adapter
- [x] `UpdateModels.cs` — `UpdateInfo.IsPrerelease` als verbindliche Klassifikation
- [x] `UpdateService` (`src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`) — `_checkGate`-Semaphore, Optionsweitergabe, defensiver Prerelease-Ausschluss (`latest.IsPrerelease && !options.IncludePrereleases` -> `NichtPruefbar`), fehlende lokale Version -> `NichtPruefbar` ohne Releaseabruf, Caller-Cancellation propagiert; `StartPreparedUpdateAsync` ruft `Shutdown` erst nach erfolgreichem `StartScriptAsync`; kein `CachedResult` mehr (Repo-weit keine Referenz)
- [x] `UpdateVersionComparer` (`UpdateVersionComparer.cs`) — `TryParse` liefert `SemanticUpdateVersion`, `Normalize` entfernt Rand-Leerzeichen/`v`-Praefix und erhaelt Suffix+Metadaten, `IsNewer` semantisch
- [x] `ApplicationVersionProvider` (`ApplicationVersionProvider.cs`) — liefert suffixerhaltende Version aus `version.json`; Basispfad-Konstruktor fuer E2E vorhanden
- [x] `GitHubReleaseClient` (`src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`) — `releases?per_page=100`, `Link`/`rel=next`-Verfolgung, besuchte URLs und unzulaessige Folge-URLs (gleicher Host + Releases-Pfad) abgelehnt, gemeinsames Timeout ueber alle Seiten, `draft`-DTO-Feld, Draft-/Tag-/Kanal-/Assetfilter mit absoluter HTTPS-URL, hoechste zulaessige SemVer ueber alle Seiten (gleiche Praezedenz -> erste Fundstelle), jeder Seitenfehler -> `null`, Caller-Cancellation propagiert als `OperationCanceledException`
- [x] `MainWindowViewModel` (`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`) — keine Konstruktorpruefung; `InitializeUpdatesAfterWindowReadyAsync` mit `Interlocked.Exchange`-Startkennzeichen vor erstem `await`; nicht wartendes `_updateGate` fuer Start/Pruefen/Starten; `LeseUpdateSettingsAsync` in frischem DI-Scope; `Aus`-Guard in allen drei Eintrittspunkten vor Releaseabruf; Generation (`IstUpdateVersuchAktuell`), `_updateAblaufCts`, erneutes Lesen/Vergleichen vor `PrepareUpdateAsync` und `StartPreparedUpdateAsync`; `InstalliereUpdateAsync` als gemeinsamer Kern (CLI-Sicherheit, Bestaetigungsdialog, echter Fortschrittsdialog); `NavigateToSettings` abonniert `UpdateSettingsSaved` einmalig am gecachten VM, `Dispose` meldet ab und cancelt; `KannUpdatePruefen`/`KannUpdateStarten` verlangen geladene Settings + `Modus != Aus`; Lesefehler an allen drei Grenzen -> sichtbarer `UpdateHinweis`, Angebot invalidiert, Dialog-Fehlerzustand schliessbar, keine Folgeschritte; `ApplyUpdateCheckResult` zeigt `NichtPruefbar`-/manuelle `KeinUpdate`-Meldung sichtbar
- [x] `MainWindow.xaml.cs` (`src/Softwareschmiede.App/Views/MainWindow.xaml.cs:43-51`) — einmaliger, sich selbst abmeldender `ContentRendered`-Handler mit beobachteter Ausfuehrung via `SafeFireAndForget`
- [x] `App.xaml.cs` (`src/Softwareschmiede.App/App.xaml.cs:140-144`) — `Application.Current.MainWindow` explizit vor `Show()` zugewiesen; E2E-Registrierung (Interceptor, dedizierter `HttpClient`, Basispfad-/Launcher-/Shutdown-/Safety-Adapter, `ProtokollierenderUpdateService`) nur bei aktivem Testkontext, letzte Registrierung gewinnt
- [x] `MainWindow.xaml` — Update-Start-Button sichtbar nur bei Angebot (`UpdateVerfuegbar`), Tooltip mit `VerfuegbaresUpdate.Version`, Pruef-Button bleibt sichtbar/deaktiviert, `UpdateHinweis`-TextBlock mit `AutomationId="UpdateHinweis"` und Sichtbarkeitsbindung
- [x] `WpfUpdateProgressDialogService` (`WpfUpdateProgressDialogService.cs`) — echter Owner `Application.Current.MainWindow`, Dialogverwaltung
- [x] `UpdateProgressDialog`/`UpdateProgressViewModel` — Automation-Merkmale (`UpdatePhase`, `UpdateFortschrittMeldung`, `UpdateFortschritt`, `UpdateAbbrechen`), `SetError`/`MarkUpdaterStarting`-Terminalzustaende schliessbar, `OnClosing` blockiert bei `CanClose == false`, spaete Fortschrittsreports ueberschreiben den Terminalzustand nicht
- [x] `UpdatePackageService`/`UpdateScriptService` — explizite Basispfad-Konstruktoren fuer die Testwurzel; produktiver Ablauf (Download, Entpacken, Validierung, Skripterzeugung, Launcher) unveraendert

### Testvoraussetzungen 1-13

- [x] `UpdateE2ETestKontext` — pro Request/Read neu gelesenes Szenario, dateibasierte Gates, versuchsbezogene Ordinalzaehlung (`BeginneVersuch`/`BeendeVersuch` vor Gate-Freigabe), `UpdateAttemptCompleted`/`StartupUpdateCompleted`-Abschlussmarker
- [x] JSONL-Protokoll (`UpdateE2EProtokoll`/`UpdateE2EEreignisse`) mit monotoner Seq, Szenario-/Prozess-ID und allen geforderten Ereignissen inkl. `UpdateSettingsReadReached`/`Failed`/`FailureDisabled`, `CliSafetyChecked`, `UnknownHttpRequest`; Runner-Anfuegen und Append-Retry
- [x] `WpfTestBase` — prozessbezogene Launch-Umgebungsvariablen, `RestartAppPreservingDatabase` (regulaeres Schliessen, Exit-Abwarten, neue UIA-Handles, `ensureDatabaseDeleted: false`), `SOFTWARESCHMIEDE_E2E_APP_PATH`-Override, App-Log-Diagnose
- [x] FlaUI-Views: `Views/SettingsView.cs` (Modus-/Checkbox-Getter/Setter, `WaitForUpdateMode`, `SaveSettings`/`WaitForSettingsSaved`, `DiscardChanges`), `Views/Dialogs/UpdateProgressDialogView.cs` (Phase, Fortschritt, Abbrechen, `WaitForErrorState`, Close), `Views/Dialogs/UpdateSafetyDialogView.cs` (echte IDYES/IDNO-Bedienung), `Views/MenuView.cs` (poll-/retry-feste Navigation, Angebotsversion, Enabled/Visible-Zustaende, `GetUpdateHinweis`/`WaitForUpdateHinweis`)

### Neue Tests

- [x] `SettingsViewModelTests` — `UpdateSettings_DefaultsAndInvalidValues`, `UpdateSettings_SaveLoadAllValues` (3 Modi x 2 Checkboxzustaende), `UpdateSettings_DiscardDoesNotPublish`, `UpdateSettings_EventFiresBeforeLaterSettingsFailure`
- [x] `AppEinstellungServiceTests_UpdateSettings` — `UpdateSettings_PersistsAcrossScopes`, `UpdateSettings_SaveFailureLeavesNoMixedPair`, `GetUpdateSettingsAsync_ReadFailureDoesNotReturnDefaults` (Interceptor, echter SQLite-Scope-Roundtrip), `GetUpdateSettingsAsync_ReturnsDefaults_WhenStoredValuesAreMissingOrInvalid`, `SetUpdateSettingsAsync_RejectsInvalidUpdateMode`
- [x] `MainWindowViewModelTests_UpdateSettingsReadFailure` — `UpdateSettingsReadFailure_InitialStopsBeforeReleaseRequest` (Startautomatik/manuellPruefen/manuellInstallieren), `UpdateSettingsReadFailure_BeforePreparationStopsInstall` und `_BeforeUpdaterStartStopsInstall` (je automatisch/manuell; null Folgeschritte, Dialog-Fehler sichtbar/schliessbar, Gate/Busy frei, Wiederherstellung ueber echte Settings-UI mit genau einer Folgepruefung ohne Auto-Installation)
- [x] `UpdateVersionComparerTests_SemVer` — `Normalize_PreservesPrereleaseAndMetadata`, `Compare_PrereleasePrecedence`, `Compare_PrereleaseIdentifierRules`, `Compare_MetadataDoesNotAffectPrecedence`, `TryParse_RejectsInvalidIdentifiers`, `TryParse_ExposesPrereleaseIdentifiersAndMetadata`, `Normalize_ThrowsFormatException_WhenVersionIsInvalid`
- [x] `ApplicationVersionProviderTests.GetInstalledVersionAsync_PreservesPrerelease` — echte `version.json` mit Suffix/Metadaten; Stable-/Fehlerfaelle erhalten
- [x] `GitHubReleaseClientTests_Filters.GetLatestReleaseAsync_PreservesPrereleaseVersion` — `v1.3.0-rc.1`-Tag -> `UpdateInfo.Version` mit Suffix
- [x] `UpdateServiceTests_PrereleaseChain.CheckForUpdateAsync_FromInstalledPrerelease` — echter Provider + echter Client: `rc.2`/`1.3.0` neuer als `1.3.0-rc.1`, `rc.1`/`beta.2`/`1.2.9` nicht; RC nur mit Checkbox
- [x] `GitHubReleaseClientTests_Filters` — `FiltersByOptions`, `FiltersGitHubPrereleaseFlag`, `FiltersSemVerPrereleaseSuffix`, `SkipsDraftInvalidTagAndUnusableAssetEntries`
- [x] `GitHubReleaseClientTests_Pagination` — `SelectsHighestAcrossPages` (beide Kanaele, unsortierte Folgeseite), `LaterPageFailureDiscardsCandidate`, `LaterPageJsonFailureDiscardsCandidate`, `LaterPageTimeoutDiscardsCandidate`, `RejectsPaginationCycle`, `RejectsDisallowedFollowUpUrls`, `PropagatesCallerCancellation`; Serviceebene in `UpdateServiceTests_PrereleaseChain`
- [x] `UpdateServiceTests_Options` — `CheckForUpdateAsync_PropagatesCurrentOptions` (`true -> false` ohne altes RC), `CheckForUpdateAsync_ExcludesPrereleaseResult_WhenOptionsDisallowPrereleases`
- [x] `MainWindowViewModelTests_UpdateStartup` — `Constructor_DoesNotCheckUpdates`, `WindowReady_StartsOnceWithImmediateResult` (alle Modi, sofortiges Ergebnis, Einmaligkeit), `Aus_BlocksBothCommandsAndDirectInvocation`, `SettingsChange_DiscardsDelayedResultAndStopsPreparation`, `Startup_NoUpdateOrNotCheckable_DoesNotInstall`, `Startup_SafetyDeclined_DoesNotContinue`/`Startup_SafetyCancelled_DoesNotContinue`/`Startup_PrepareOrStartFailure_DoesNotShutdown` (je automatisch/manuell), `Startup_ConcurrentCommandsCannotDuplicateInstall`
- [x] `UpdateFixtureHttpMessageHandlerTests` — 7 Tests inkl. unbekannter URL ohne Netzwerkfallback, Gates, Wurzel-Pfadpruefung
- [x] `E2E_UpdateFixture.cs` — `Fixture_UsesIsolatedRealUpdatePipeline` (echte JSON-/ZIP-Kette, Testpfad-Isolation, Launcher-/Shutdown-Grenze, DB-erhaltender Neustart mit RC-Auswahl, `UnknownHttpRequest`) und `Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization` (alle drei Grenzen, Freigabe/Abbruch/Timeout, Fehlerpersistenz bis Deaktivierung, Wiederherstellung)
- [x] `UpdatePackageServiceTests.PreparePackageAsync_ShouldUsePrereleaseVersionPath_WhenPackageIsValid` — dedizierter RC-Paketpfad-Test (`updates/extracted/1.4.0-rc.1` mit Paket-`version.json`), seit Commit `1714e2a` (Iteration-1-Befund behoben)
- [x] `UpdateScriptServiceTests` — vollstaendig (4 Tests: Skripterzeugung, Launcher-Argumente, pwsh-Fallback, Launcherfehler ohne Installation)

### Betroffene bestehende Tests (T-08)

- [x] `UpdateVersionComparerTests` — `IsNewer_ShouldCompareSemVerValues` auf SemVer-Erwartung, `TryParse_ShouldAcceptSemVerTags` umbenannt/erweitert
- [x] `GitHubReleaseClientTests` und alle `IUpdateReleaseClient`/`IUpdateService`-Mocks auf Listen-Responses und Optionssignatur umgestellt (kein `/latest`-Einzelobjekt)
- [x] Bisheriger Konstruktor-Prueftest durch `Constructor_DoesNotCheckUpdates` + Bereitschaftstests ersetzt; manuelle Sicherheits-/Abbruch-/Fortschrittstests automatisch gespiegelt
- [x] `CliUpdateSafetyServiceTests`, `UpdateProgressViewModelTests` (5 Tests), `UpdateProgressDialogTests` erhalten; `SettingsViewModelTests`, `E2E_SettingsFeatureFlags`, `E2E_VersionAnzeige`, `E2E_ViewPattern`, `MainTest.cs` konsolidiert

### E2E-Pflichtszenarien (`E2E_UpdateSettings.cs`, Partial `End2EndTest`, in `MainTest.RunGeneralTests` hinter den Fixture-Smokes konsolidiert)

- [x] E-01 `Settings_AllModesAndPrereleasesPersist` — drei exakte Labels, beide Checkboxzustaende, Verwerfen, echte DB-erhaltende Neustarts, Minimal-Fenstergroesse
- [x] E-02 `Startup_ModesDriveUpdatePipeline` — alle drei Modi mit UI-Speichern/Neustart; `Aus` Nullabruf + deaktiviertes Pruefen/verborgenes Installieren; Startmodus mit Owner-nachgewiesenem Fortschrittsdialog, blockiertem Download, genau einem Launcher-Erfolg vor einem Shutdown
- [x] E-03 `PrereleaseCheckbox_SelectsMatchingAsset` — Stable/RC je Checkboxzustand manuell und im Startmodus nach Neustart, exklusive Asset-URLs, Installationsnachweis
- [x] E-04 `SavedChangesInvalidatePreviousOffer` — RC-Angebot -> Checkbox aus (Angebot weg ohne neuen Request), Stable-Neuinstallation, blockierter laufender Request + `Aus`-Speichern (spaete Antwort verworfen, kein Download), Neustart-Nullabruf
- [x] E-05 `Startup_NoUpdateOrUncheckableRemainsUsable` — gleiche/aeltere Version, fehlende/ungueltige lokale `version.json`, Release-HTTP-/JSON-Fehler; `NichtPruefbar`-Hinweis, keine Folgeschritte, App bedienbar
- [x] E-06 `Startup_SafetyCancelAndErrorsRemainUsable` — echte Ja/Nein-Sicherheitsdialoge, blockierter Download + Abbruch, Asset-HTTP-/ZIP-Fehler, Launcherfehler ohne Shutdown; danach alle drei T-09-Lesefehler-Grenzen automatisch und manuell mit Gate-Freigabe, null Folgeschritten und erfolgreicher Folgepruefung
- [x] E-07 `Startup_IsOnceAndCommandsStayBlocked` — blockierter ZIP-Stream, gesperrte Commands, kein zweiter Ablauf, genau eine Vorbereitung/Uebergabe/Shutdown, unveraenderte Zaehler bei Navigation/erneutem Rendern

### Verifikation (Tasks 68/69)

- [x] Voller Build und beide Testspuren ausgefuehrt — dokumentiert in `test-results.md` (Commit `9bb3585`): Build erfolgreich (App-Projekt per Shadow-Output wegen gesperrtem `bin` durch laufende App-Instanz, keine Prozesse beendet), regulaere Spur 1627/0 Fehler/1 uebersprungen, OS-/E2E-Spur mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` ausgefuehrt (47/48; einziger Fehlschlag feature-unabhaengiger FlaUI-Timing-Flake, isoliert gruen), alle Update-Pflicht-E2Es und Fixture-Smokes bestanden
- [x] `test-results.md` vorhanden und dokumentiert Build, beide Spuren, Ueberspringungen, E-01 bis E-07 samt Varianten, Fixture-Smokes, Fehleranalysen und Fallback-Matrix

## Hinweise

- Diese Review hat per Vorgabe keine Builds/Tests ausgefuehrt; der `Erledigt`-Status der Tasks 68/69 beruht auf dem committed `test-results.md`. Werden die Laeufe spaeter erneut ausgefuehrt, sollte `test-results.md` entsprechend fortgeschrieben werden.
- Der dokumentierte Restbefund der OS-Spur (`E2E_RepositoryInitialisierungConfigTests.InitialisierungsskriptKonfiguration`) ist ein feature-unabhaengiger FlaUI-Timing-Flake und im Isolationslauf gruen — kein Plan-Element dieses Features.
- Asset-Download-URLs werden seit `1714e2a` auf HTTPS eingeschraenkt (Plan forderte "absolut und HTTP(S)"); das ist eine beabsichtigte Verschaerfung aus der Iteration-1-Review.
- `CachedResult` wurde vollstaendig entfernt statt optionsgebunden invalidiert — der Plan liess beide Varianten zu; die Optionswechsel-Sicherheit ist per `CheckForUpdateAsync_PropagatesCurrentOptions` belegt.
- Testnamen weichen teils von den Zielnamen ab (vom Plan ausdruecklich erlaubt): `Startup_SafetyDeclinedOrCancelled_DoesNotContinue` ist in zwei parametrisierte Theories aufgeteilt; die frueheren `UpdateStartenCommand_Should*`-Tests sind in den `automatisch`-parametrisierten Theories ohne Abdeckungsverlust erhalten.
- `MainWindow.xaml` baut den `UpdateHinweis` ohne `AutomationProperties.Name` (nur `AutomationId`): Der gebundene Text bleibt als UIA-Name lesbar; die FlaUI-Helfer adressieren ueber die AutomationId — entspricht der Plan-Formulierung "sichtbarer automatisierbarer `UpdateHinweis`".
