# Tests

## Testklassen

### `GitHubReleaseClientTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests.cs`

- `GetLatestReleaseAsync_ShouldReturnRelease_WhenAssetExists` — stabiles Release wird als `UpdateInfo` zurückgegeben (Version, `IsPrerelease=false`, `DownloadUrl`)
- `GetLatestReleaseAsync_ShouldReturnNull_WhenReleaseIsNotUsable` (Theory) — leere Liste/kein Asset/Prerelease bei ausgeschlossenen Prereleases/ungültiger Tag → `KeinTreffer`; HTTP-Fehler → `Fehlgeschlagen`
- `GetLatestReleaseAsync_ShouldReturnNull_WhenResponseIsNotAReleaseList` (Theory) — Nicht-Listen-JSON → `Fehlgeschlagen`
- `GetLatestReleaseAsync_ShouldReturnNull_WhenRequestTimesOut` — Timeout → `Fehlgeschlagen`
- Hilfsmethode `CreateSut(HttpClient, UpdateOptions?)`; `DelayingHttpHandler`

### `GitHubReleaseClientTests_Filters`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Filters.cs`

- `GetLatestReleaseAsync_FiltersByOptions` (Theory, `includePrereleases` false/true) — bei deaktivierten Prereleases gewinnt `1.2.1` stabil, bei aktivierten `1.3.0-rc.1` — belegt die heutige **inklusive** Semantik (stabile + RC konkurrieren)
- `GetLatestReleaseAsync_FiltersGitHubPrereleaseFlag` — GitHub-Flag `prerelease` klassifiziert auch stabilen Tag als Prerelease
- `GetLatestReleaseAsync_FiltersSemVerPrereleaseSuffix` — SemVer-Suffix klassifiziert auch ohne GitHub-Flag als Prerelease
- `GetLatestReleaseAsync_SkipsDraftInvalidTagAndUnusableAssetEntries` — Drafts, ungültige Tags, fehlendes/falsches/nicht-HTTPS-Asset werden übersprungen (Asset-Name case-insensitiv)
- `GetLatestReleaseAsync_PreservesPrereleaseVersion` — `v1.3.0-rc.1` → normalisierte `1.3.0-rc.1`, `IsPrerelease=true`, Asset/URL erhalten

### `GitHubReleaseClientTests_Pagination`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests_Pagination.cs`

- `GetLatestReleaseAsync_SelectsHighestAcrossPages` (Theory) — höchste zulässige SemVer über Seiten hinweg; bei `includePrereleases=false` gewinnt `1.3.0` stabil gegenüber `1.4.0-rc.1`
- `GetLatestReleaseAsync_LaterPageFailureDiscardsCandidate` — HTTP-Fehler auf Folgeseite → `Fehlgeschlagen`
- `GetLatestReleaseAsync_LaterPageJsonFailureDiscardsCandidate` — ungültiges JSON auf Folgeseite → `Fehlgeschlagen`
- `GetLatestReleaseAsync_LaterPageTimeoutDiscardsCandidate` — Timeout auf Folgeseite → `Fehlgeschlagen`
- `GetLatestReleaseAsync_RejectsPaginationCycle` — zyklische Folge-URL → `Fehlgeschlagen`
- `GetLatestReleaseAsync_RejectsDisallowedFollowUpUrls` (Theory) — fremder Host/Pfad/Repo/Schema → `Fehlgeschlagen`
- `GetLatestReleaseAsync_AllowsNumericRepositoryIdFollowUp` — `/repositories/{id}/releases` zulässig
- `GetLatestReleaseAsync_PropagatesCallerCancellation` — Caller-Cancellation propagiert als `OperationCanceledException`
- Konstanten `Page1Url`/`Page2Url` referenzieren den hartcodierten `martin-stromberg/Softwareschmiede`-Endpunkt

### `UpdateServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests.cs`

- `CheckForUpdateAsync_ShouldReturnAvailable_WhenRemoteVersionIsNewer` — neuere Remote-Version → `UpdateVerfuegbar`
- `CheckForUpdateAsync_ShouldReturnNoUpdate_WhenLookupFindsNoCandidate` — `KeinTreffer` → `KeinUpdate`
- `CheckForUpdateAsync_ShouldReturnNotCheckable_WhenLookupFails` — `Fehlgeschlagen` → `NichtPruefbar`
- `CheckForUpdateAsync_ShouldReturnNotCheckable_WhenLocalVersionIsMissing` — fehlende lokale Version → `NichtPruefbar`, kein Releaseabruf
- `StartPreparedUpdateAsync_ShouldNotShutdown_WhenScriptStartFails` — Skriptfehler → kein Shutdown

### `UpdateServiceTests_Options`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_Options.cs`

- `CheckForUpdateAsync_PropagatesCurrentOptions` — Optionswechsel führt zu neuem Abruf mit jeweils aktuellem `IncludePrereleases`; Optionsabhängige Client-Ergebnisse werden nicht gecacht
- `CheckForUpdateAsync_ExcludesPrereleaseResult_WhenOptionsDisallowPrereleases` — Defensivprüfung: Prerelease-Ergebnis bei `IncludePrereleases=false` → `NichtPruefbar`

### `UpdateServiceTests_PrereleaseChain`
Datei: `src/Softwareschmiede.Tests/Application/Services/Updates/UpdateServiceTests_PrereleaseChain.cs`

- `CheckForUpdateAsync_FromInstalledPrerelease` — echter `ApplicationVersionProvider` + echter `GitHubReleaseClient`: bei installiertem `1.3.0-rc.1` gilt `1.3.0-rc.2` (nur mit Prereleases) **und das stabile `1.3.0`** (auch ohne) als neuer — dokumentiert die zu ändernde Update-Kette
- `GetLatestReleaseAsync_LaterPageFailureDiscardsCandidate` — Folgeseiten-Fehler → `NichtPruefbar` ohne Teiltreffer
- Hilfsmethoden `CreateService`, `ReleasesJson(params string[])` (erzeugt Release-JSON, `prerelease=true` bei `-` im Tag)

### `AppEinstellungServiceTests_UpdateSettings`
Datei: `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_UpdateSettings.cs` (echte SQLite-Datei-DB)

- `UpdateSettings_PersistsAcrossScopes` — Wertepaar überlebt Context-/Scope-Grenzen
- `UpdateSettings_SaveFailureLeavesNoMixedPair` — Write-Lock-Fehler hinterlässt kein gemischtes Paar
- `GetUpdateSettingsAsync_ReadFailureDoesNotReturnDefaults` — getaggter Read schlägt kontrolliert fehl (Interceptor), keine Defaults; nach Deaktivierung wieder lesbar
- `GetUpdateSettingsAsync_ReturnsDefaults_WhenStoredValuesAreMissingOrInvalid` — fehlende/ungültige Werte → `NurPruefen`/`false`; `"7"` als Modus → Default, `"True"` → `true`
- `SetUpdateSettingsAsync_RejectsInvalidUpdateMode` — `(UpdateMode)7` → `ArgumentOutOfRangeException`, kein Write
- Hilfsmethoden `CreateContext(interceptor, timeout)`, `CreateService`

### `SettingsViewModelTests` (Update-relevante Methoden)
Datei: `src/Softwareschmiede.Tests/App/ViewModels/SettingsViewModelTests.cs` (Zeilen ~495–612)

- `UpdateSettings_DefaultsAndInvalidValues` — Defaults `NurPruefen`/Prereleases aus; ungültige Schlüssel → Defaults; `SelectedUpdateMode = null` blockiert Speichern (Fehlermeldung)
- `UpdateSettings_SaveLoadAllValues` (Theory, 6 Kombinationen Modus × bool) — Speichern publiziert `UpdateSettingsSaved` mit Snapshot, persistiert und lädt identisch zurück
- `UpdateSettings_EventFiresBeforeLaterSettingsFailure` — `UpdateSettingsSaved` feuert, auch wenn ein späterer Speicherschritt fehlschlägt
- `UpdateSettings_DiscardDoesNotPublish` — `VerwerfenCommand` lädt neu ohne `UpdateSettingsSaved` und ohne Writes

### `MainWindowViewModelTests_UpdateStartup`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateStartup.cs`

- `Constructor_DoesNotCheckUpdates` — kein Check im Konstruktor, Commands gesperrt
- `WindowReady_StartsOnceWithImmediateResult` (Theory über Modi) — einmaliger Startversuch je Modus
- `Aus_BlocksBothCommandsAndDirectInvocation` — Modus `Aus` sperrt beide Commands
- `SettingsChange_DiscardsDelayedResultAndStopsPreparation` — gespeicherte Einstellungsänderung verwirft verzögertes Ergebnis/stoppt Vorbereitung; erfasst `UpdateCheckOptions`
- `Startup_NoUpdateOrNotCheckable_DoesNotInstall` — `KeinUpdate`/`NichtPruefbar` → keine Installation
- `Startup_SafetyDeclined_DoesNotContinue` / `Startup_SafetyCancelled_DoesNotContinue` — Safety-Dialog-Abbruch
- `Startup_PrepareOrStartFailure_DoesNotShutdown` — Fehler in Vorbereitung/Start → kein Shutdown
- `Startup_ConcurrentCommandsCannotDuplicateInstall` — Gate verhindert doppelte Installation

### `MainWindowViewModelTests_UpdateSettingsReadFailure`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateSettingsReadFailure.cs`

- `UpdateSettingsReadFailure_InitialStopsBeforeReleaseRequest` (Theory über `UpdateEinstieg`: Startup/Pruefen/Starten) — Lesefehler am Initial-Read stoppt vor dem Releaseabruf
- `UpdateSettingsReadFailure_BeforePreparationStopsInstall` — Lesefehler vor Vorbereitung stoppt Installation
- `UpdateSettingsReadFailure_BeforeUpdaterStartStopsInstall` — Lesefehler vor Updater-Start stoppt Installation
- Nutzt `UpdateSettingsReadFailureInterceptor` über `ConfigureDbContext`-Haken

### `DatenbankPfadResolverTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Data/DatenbankPfadResolverTests.cs`

- `ErmittlePfad_ShouldReturnOverride_WhenTestDbPathOverrideIsSet` / `..._EvenWhenVersionJsonIndicatesProduktiv` — Test-Override hat Vorrang
- `ErmittlePfad_ShouldUseBaseDirectory_WhenVersionJsonIsMissing` — versionslos → Programmverzeichnis
- `ErmittlePfad_ShouldUseBaseDirectory_WhenTagNameIsReleaseCandidate` (Theory `v1.2.3-rc.1`, `V1.2.3-RC.1`) — RC-Tag → Programmverzeichnis
- `ErmittlePfad_ShouldUseLocalApplicationData_WhenTagNameIsProduktiv` — produktiver Tag → `%LocalAppData%`
- `ErmittlePfad_ShouldFallBackToBaseDirectory_WhenVersionJsonIsBroken` / `..._HasNoTagName` — kaputte/tagName-lose `version.json` → Programmverzeichnis

### Weitere Update-nahe Testklassen
- `ApplicationVersionProviderTests` — `version.json`-Lesung inkl. Prerelease-Erhalt und Fehlerfälle
- `UpdateVersionComparerTests` / `UpdateVersionComparerTests_SemVer` — SemVer-Parsing/-Vergleich
- `UpdatePackageServiceTests`, `UpdateScriptServiceTests`, `CliUpdateSafetyServiceTests`, `UpdateProgressViewModelTests`, `UpdateProgressDialogTests`, `UpdateFixtureHttpMessageHandlerTests` — Paket-/Skript-/Safety-/Dialog-Details

## E2E-Tests (`End2EndTest`, partial)

Registriert in `src/Softwareschmiede.Tests/E2E/MainTest.cs`: `General_UpdateFixtureSmoke_E2E` (2 Smokes), `General_UpdateStartvarianten_E2E` (7 Szenarien E-01 bis E-07).

### `E2E_UpdateFixture.cs`
- `Fixture_UsesIsolatedRealUpdatePipeline` — echte JSON-/ZIP-Pipeline über Fixture-Handler, isolierte Pfade, Stable-Auswahl, Launcher-/Shutdown-Grenze, DB-erhaltender Neustart mit RC-Auswahl (`includePrereleases: true`), unbekannte Requests scheitern ohne Fallback
- `Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization` — alle drei Lesefehler-Grenzen (Initial, BeforePreparation, BeforeUpdaterStart) mit Gate-Freigabe/-Abbruch, Persistenz des Fehlers, Timeout-Cleanup
- Hilfen: `SchliesseFixtureApp`, `AssertSequenz`, `DatenText`/`DatenBool`/`DatenZahl`, `ErmittleAppExePfadDesLaufendenProzesses`

### `E2E_UpdateSettings.cs` (Szenarien E-01 bis E-07)
- `Settings_AllModesAndPrereleasesPersist` (E-01) — alle Modus-Labels + beide Checkboxzustände über echte UI persistiert (Weg-/Zurücknavigation + echter Neustart); „Verwerfen"; ComboBox-Lesbarkeit bei Mindestfenstergröße; Neustart-Angebot RC mit `includePrereleases=true`-Protokollnachweis
- `Startup_ModesDriveUpdatePipeline` (E-02) — modusabhängige Startautomatik über UI-Speichern + echten Neustart je Modus (Aus/NurPruefen/Start), Fortschrittsdialog-Owner, Download-Gate, Installationsnachweis
- `PrereleaseCheckbox_SelectsMatchingAsset` (E-03) — Checkbox aus/ein wählt exakt Stable bzw. RC (manuell und per Startmodus-Neustart); Installationsartefakte (Download-URL, entpackte `version.json`, Skriptziel) passend zur Version
- `SavedChangesInvalidatePreviousOffer` (E-04) — gespeicherte Änderungen invalidieren Angebot/laufende Versuche
- `Startup_NoUpdateOrUncheckableRemainsUsable` (E-05) — `KeinUpdate`/`NichtPruefbar`-Varianten (fehlende/ungültige `version.json`, HTTP-/JSON-Fehler) lassen App bedienbar
- `Startup_SafetyCancelAndErrorsRemainUsable` (E-06) — Safety-Dialog-Ablehnung, Download-Abbruch, Asset-HTTP-Fehler, defektes ZIP, Launcher-Fehler
- `Startup_IsOnceAndCommandsStayBlocked` (E-07) — Einmaligkeit des Startversuchs; T-09-Lesefehler an allen drei Grenzen (automatisch/manuell) mit UI-Hinweis
- Hilfsmethoden: `OeffneSettingsGeladenAsync`, `SpeichereUpdateEinstellungenUeberUiAsync`, `PruefeUpdateUeberUiAsync`, `PruefeSettingsBedienbarAsync`, `WarteAufEreignisSeitAsync`, `WarteAufSpeichernAktiviert`, `SetzeUpdateSteuerungZurueckAsync`, `ProtokollBasis`, `EintraegeSeit`, `ZaehleEreignisSeit`, `AssertKeinUpdateNebenwirkungSeit`, `AssertiereInstallationsNachweis`, diverse `*PhaseAsync`-Phasenhelfer, `UpdateHinweisLesefehler`-Konstante in `E2E_UpdateFixture.cs`

## Hilfsmethoden / Testdoubles

### `UpdateE2EFixture`
Datei: `src/Softwareschmiede.Tests/E2E/UpdateE2EFixture.cs`

- Konstanten: `InstallierteVersion="1.2.0"`, `StableVersion="1.3.0"`, `RcVersion="1.4.0-rc.1"`, `ReleaseApiUrl` (aus `UpdateOptions`-Defaults abgeleitet)
- `ErzeugeStartUmgebung()` — Umgebungsvariablen `SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` + `SOFTWARESCHMIEDE_TEST_DB_PATH`
- `SetzeStandardAntworten(mitRcRelease)` — Release-Liste (Stable + optional RC) + beide Paket-URLs
- `SetzeUpdateEinstellungenAsync(UpdateMode, bool includePrereleases)` / `AktualisiereUpdateEinstellungenAsync(...)` — persistiert `UpdateSettings(modus, includePrereleases)` direkt über `AppEinstellungService` in der Fixture-DB
- `AktiviereLesefehler(grenze, versuch, aufFreigabeWarten, wartezeitSekunden)` / `DeaktiviereLesefehler()` — kontrollierter Settings-Lesefehler
- `OeffneGate`/`BrecheGateAb`/`SetzeGateZurueck` — dateibasierte Gates
- `LeseProtokoll`/`WarteAufEreignisAsync`/`ZaehleEreignis`/`Daten` — JSONL-Protokollzugriff
- `ErstellePaketZip(zipPfad, version, marker)` / `LesePaketExeInhalt(verzeichnis)` — Paketartefakte
- `AktualisiereSzenario(mutation)`/`SpeichereSzenario()` — Laufzeit-Szenariosteuerung

### FlaUI-Page-Object `SettingsView`
Datei: `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`

- `GetUpdateMode()` / `SetUpdateMode(label)` / `WaitForUpdateMode(label, timeout)` — „Update-Modus"-ComboBox
- `GetIncludePrereleases()` / `SetIncludePrereleases(bool)` — „Prerelease-Versionen laden"-CheckBox (Zeilen 386–398)
- `SaveSettings()` / `WaitForSettingsSaved()` / `DiscardChanges()` — Speichern/Verwerfen
- Kein Element für einen Update-Pfad vorhanden

### App-seitige Testdoubles (`Softwareschmiede.App/Services/Testing/`)
- `UpdateE2ETestConfiguration` — Konfigurationsladung/-validierung (nur mit beiden Env-Variablen, nie echter Fallback)
- `UpdateE2ETestKontext` — Laufzeitkontext (Protokoll, Gates, Szenario, Versuchszählung); implementiert `IUpdateVersuchProtokoll`
- `UpdateE2EProtokoll` + `UpdateE2EEreignisse` — JSONL-Schreiber und Ereignisnamen (`UpdateCheckStarted` trägt Datenfeld `includePrereleases`)
- `UpdateE2ESzenario`/`UpdateE2EAntwort`/`UpdateE2ELesefehler`/`UpdateE2ECliSicherheit`/`UpdateE2EProzessStart` — Szenariomodelle
- `UpdateFixtureHttpMessageHandler` — deterministischer HTTP-Transport (exakte URLs, Gates, StreamGates, Faults, kein Netzwerkfallback)
- `UpdateSettingsReadFailureInterceptor` — EF-Interceptor auf `UpdateSettingsReadTag`
- `ProtokollierenderUpdateService` — `IUpdateService`-Dekorator (schreibt `includePrereleases` ins Protokoll)
- `RecordingUpdateProcessLauncher`, `FixtureCliUpdateSafetyService` — kontrollierte Prozess-/Safety-Grenzen

### Generische Test-Helfer (`Softwareschmiede.Tests/Helpers/`)
- `StaticHttpHandler` — statische Status-/Body-Antwort für `GitHubReleaseClient`-Tests
- `RoutingHttpHandler` — URL→Antwort/-Handler-Routing inkl. `Link`-Header-Pagination (`AddPage`, `AddHandler`, `RequestedUrls`)
- `TempDirectory` — temporäres Verzeichnis für `version.json`-basierte Tests
- `TestDbContextFactory` — SQLite-Context-Erzeugung mit Interceptoren

### `MainWindowViewModelUpdateTestBase`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelUpdateTestBase.cs`

- Echter DI-Container + SQLite-Datei-DB; Mocks für `IUpdateService`, `ICliUpdateSafetyService`, `IUpdateProgressDialogService`, `IDialogService`
- `NeuesUpdate` (stabil `1.2.3`), `FertigeVorbereitung` — Standard-Testdaten
- `CreateSut()`, `SetUpdateSettingsAsync(UpdateSettings)`, `SpeichereUpdateEinstellungenUeberUiAsync(sut, modus, includePrereleases)`, `ConfigureDbContext`-Haken für Interceptoren
