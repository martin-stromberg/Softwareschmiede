# Review: Prereleases bei Updates

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Offene Eintraege: Task 58 (teilweise — dedizierter RC-Paketpfad-Unit-Test in `UpdatePackageServiceTests` fehlt, fachlich durch E2E abgedeckt), Tasks 68/69 (Ausfuehrungsnachweise — werden in Lifecycle-Schritt 10 erbracht).

Gegenstand: Umsetzungsreview des Plans [plan.md](plan.md) gegen den tatsaechlichen Stand unter `src/` (Branch `task/5c1a753c335f441294221c1e260a87c3-prereleases`).

Methode: Vollstaendige Quellcode-Sichtung der produktiven und testseitigen Dateien; Abgleich jedes Planelements und aller 69 Tasks aus [../5c1a753c335f441294221c1e260a87c3-prereleases-tasks.md](../5c1a753c335f441294221c1e260a87c3-prereleases-tasks.md).

Einschraenkung dieser Review: Per Vorgabe wurden **keine Builds und keine Tests ausgefuehrt** und kein Produktivcode geaendert. Alle Aussagen beruhen auf Quellcode-Inspektion. Vorhandener Testcode belegt die vorgesehene Abdeckung, nicht deren erfolgreichen Lauf. Die Ausfuehrungsnachweise (Tasks 68/69) fehlen damit vollstaendig; die Abnahme gemaess Plan-Abschnitt "Ausfuehrung und Abnahme" ist noch nicht moeglich.

## Gelesene Dokumente

- `plan.md` (Designentscheidungen, Programmablaeufe, neue/geaenderte Klassen, Validierungsregeln, Testvoraussetzungen 1-13, neue Tests, Regressionstabelle T-08, E2E-Tabelle E-01 bis E-07, Ausfuehrung/Abnahme, Befund-Nachverfolgung)
- `inventory.md` samt Detailinventaren (`settings.md`, `update-pipeline.md`, `startup-flow.md`, `tests.md`)
- `5c1a753c335f441294221c1e260a87c3-prereleases-tasks.md` (69 Tasks)

## Inspizierte Quelldateien (Auswahl)

Produktivcode:

- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` – `UpdateMode` (Werte 0/1/2), `UpdateSettings`, `UpdateCheckOptions`, `UpdateInfo.IsPrerelease`, `UpdateCheckStatus`
- `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` – Optionssignaturen fuer `CheckForUpdateAsync` und `GetLatestReleaseAsync`
- `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs` – `_checkGate`, Optionsweitergabe, defensiver Prerelease-Ausschluss, Shutdown erst nach `StartScriptAsync`
- `src/Softwareschmiede/Application/Services/Updates/SemanticUpdateVersion.cs` – SemVer-Parsing/-Praezedenz
- `src/Softwareschmiede/Application/Services/Updates/UpdateVersionComparer.cs` – `TryParse`/`Normalize`/`IsNewer` auf SemVer-Vertrag
- `src/Softwareschmiede/Application/Services/Updates/ApplicationVersionProvider.cs`
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` – `UpdateModeKey`/`IncludePrereleasesKey`, `UpdateSettingsReadTag`, `GetUpdateSettingsAsync`, `SetUpdateSettingsAsync`
- `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs` – Pagination, Filter, Timeout, Cancellation
- `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs`, `UpdateScriptService.cs`
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` – Startkennzeichen, `_updateGate`, Generation, `LeseUpdateSettingsAsync` mit frischem Scope, `InstalliereUpdateAsync`, Event-Abmeldung in `Dispose`
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` – Update-Properties, `UpdateSettingsSaved`
- `src/Softwareschmiede.App/Views/MainWindow.xaml(.cs)`, `SettingsView.xaml`, `UpdateProgressDialog.xaml`, `App.xaml.cs`, `Services/WpfUpdateProgressDialogService.cs`
- `src/Softwareschmiede.App/Services/Testing/*` – `UpdateE2ETestConfiguration`, `UpdateE2ETestKontext`, `UpdateFixtureHttpMessageHandler`, `RecordingUpdateProcessLauncher`, `RecordingApplicationShutdownService`, `FixtureCliUpdateSafetyService`, `UpdateSettingsReadFailureInterceptor`, `ProtokollierenderUpdateService`, `UpdateE2EProtokoll`

Testcode:

- `src/Softwareschmiede.Tests/Application/Services/AppEinstellungServiceTests_UpdateSettings.cs`
- `src/Softwareschmiede.Tests/Application/Services/Updates/*` (Comparer-, Provider-, Service-, Options-, Chain-, Package-, Script-, Safety-, ProgressTests)
- `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests(.cs|_Filters.cs|_Pagination.cs)`
- `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests_UpdateStartup.cs`, `MainWindowViewModelTests_UpdateSettingsReadFailure.cs`, `SettingsViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/Services/Testing/UpdateFixtureHttpMessageHandlerTests.cs`, `App/Views/UpdateProgressDialogTests.cs`
- `src/Softwareschmiede.Tests/E2E/*` (`E2E_UpdateSettings.cs`, `E2E_UpdateFixture.cs`, `UpdateE2EFixture.cs`, `MainTest.cs`, `WpfTestBase.cs`, `Views/*`)

## Befunde je Planelement

### Datenmodell und Persistenz

- `UpdateMode` mit `Aus = 0`, `NurPruefen = 1`, `BeiProgrammstartPruefenUndAusfuehren = 2` sowie die unveraenderlichen Records `UpdateSettings` und `UpdateCheckOptions` sind in `UpdateModels.cs` umgesetzt; `UpdateInfo` fuehrt `IsPrerelease` verbindlich.
- `AppEinstellungService` liest beide Schluessel (`updates.mode`, `updates.includePrereleases`) in einer einzigen getaggten Abfrage (`UpdateSettings.Read`), mappt fehlende/ungueltige Werte auf die Defaults `NurPruefen`/`false` und laesst DB-Lesefehler propagieren (T-09-Voraussetzung). `SetUpdateSettingsAsync` validiert den Enum und schreibt beide Werte atomar in einem `SaveChangesAsync`.
- Abdeckung: `AppEinstellungServiceTests_UpdateSettings` (5 Tests inkl. Scope-Roundtrip, kein gemischtes Paar bei Fehler, Lesefehler ohne Defaults via Interceptor).

### Settings-UI und Hauptfenster

- `SettingsViewModel` exponiert `SelectedUpdateMode`, `IncludePrereleases`, `UpdateModusOptionen` (drei Labels) und feuert `UpdateSettingsSaved` direkt nach dem erfolgreichen Update-Speichern – auch wenn ein spaeterer Speicherschritt fehlschlaegt (durch `UpdateSettings_EventFiresBeforeLaterSettingsFailure` belegt).
- `SettingsView.xaml` enthaelt ComboBox (`Update-Modus`) und CheckBox (`Prerelease-Versionen laden`) mit geforderten Automation-Namen.
- `MainWindowViewModel` abonniert das Event einmalig am gecachten `SettingsViewModel`, meldet es in `Dispose` ab, erhoeht bei geaenderten Werten die Generation, cancelt den laufenden Ablauf und invalidiert das Angebot. `KannUpdatePruefen`/`KannUpdateStarten` verlangen geladene Settings und `Modus != Aus`; die Methoden selbst pruefen den Modus zusaetzlich (P-01-Guard auch bei umgangenem `CanExecute`). `MainWindow.xaml` bindet Sichtbarkeit/ToolTip und den `UpdateHinweis` (`AutomationId="UpdateHinweis"`).

### Versionslogik und Releasequelle

- `SemanticUpdateVersion` implementiert vollstaendige SemVer-Praezedenz (numerisch < nichtnumerisch, ordinal case-sensitiv, kuerzere identische Folge < Verlaengerung, Stable > eigene Prereleases, Metadaten ohne Einfluss) und strenge Validierung (keine leeren Identifier, keine fuehrenden Nullen, exakt `X.Y.Z`). `UpdateVersionComparer` arbeitet auf diesem Vertrag; `Normalize` erhaelt Suffix und Metadaten, `TryParse` akzeptiert fuehrendes `v`/`V`.
- `ApplicationVersionProvider` liefert den suffixerhaltenden Vertrag aus `version.json`; `GitHubReleaseClient` setzt `UpdateInfo.Version` per `Normalize`; `UpdatePackageService` legt `extracted/<version>` an (RC-Pfad im E2E nachgewiesen).
- `GitHubReleaseClient` ruft `releases?per_page=100` ab, folgt `Link`-Headern mit `rel=next`, verweigert bereits besuchte und fremde Folge-URLs (gleicher Host + Repository-Releases-Pfad), begrenzt alle Seiten mit einem gemeinsamen Timeout, filtert Drafts/ungueltige Tags/Kanal/Assets und waehlt die hoechste zulaessige Version ueber alle Seiten. Jeder Seitenfehler (HTTP, JSON, Timeout, ungueltige Pagination) ergibt `null` statt Teiltreffer; Caller-Cancellation propagiert als `OperationCanceledException`.
- `UpdateService` reicht Optionen durch und schliesst ein als Prerelease klassifiziertes Client-Ergebnis bei deaktivierten Prereleases defensiv aus (`NichtPruefbar`). Ein `CachedResult` existiert nicht mehr; jede Pruefung geht durch `_checkGate` zum Client, sodass Optionswechsel kein altes Ergebnis liefern koennen (`CheckForUpdateAsync_PropagatesCurrentOptions`).

### Startfluss und Installation

- Kein Updateabruf im ViewModel-Konstruktor; `App.xaml.cs` weist `Application.Current.MainWindow` vor `Show()` zu; `MainWindow.xaml.cs` verdrahtet einen sich selbst abmeldenden `ContentRendered`-Handler, der `InitializeUpdatesAfterWindowReadyAsync` beobachtet startet.
- `InitializeUpdatesAfterWindowReadyAsync` setzt das Startkennzeichen per `Interlocked.Exchange` vor dem ersten `await` (Einmaligkeit), nutzt das gemeinsame nicht wartende `_updateGate`, liest Settings im frischen Scope, stoppt bei `Aus` vor jedem Abruf und installiert nur im Startmodus automatisch.
- `UpdateStartenAsync` prueft erneut mit aktuellen Optionen statt ein altes Angebot zu installieren; `InstalliereUpdateAsync` ist der gemeinsame Kern mit CLI-Sicherheitsabfrage, echtem Fortschrittsdialog (Owner = `Application.MainWindow` via `WpfUpdateProgressDialogService`), erneutem Settings-Abgleich unmittelbar vor `PrepareUpdateAsync` und vor `StartPreparedUpdateAsync` (T-09-Grenzen) sowie Generations-/Token-Pruefung nach jedem `await`.
- Abbruch und Fehler landen sichtbar im Dialog bzw. `UpdateHinweis`, Gate/Busy werden im `finally` freigegeben; `Shutdown` ruft ausschliesslich `UpdateService` nach erfolgreichem `StartScriptAsync` auf – kein zweiter Shutdown im ViewModel.
- `GetUpdateSettingsAsync`-Lesefehler erzeugen an allen drei Lesegrenzen sichtbare Fehler-/Nicht-pruefbar-Zustaende ohne Ersatzwerte, Retry oder Folgeschritte (Initial: kein Releaseabruf; BeforePreparation: kein Asset/Prepare; BeforeUpdaterStart: kein `MarkUpdaterStarting`/Start/Shutdown).

### Testinfrastruktur und E2E

- `UpdateE2ETestConfiguration` aktiviert die Testumgebung nur mit beiden Variablen (`SOFTWARESCHMIEDE_UPDATE_TEST_CONFIG` UND `SOFTWARESCHMIEDE_TEST_DB_PATH`) und bricht mit Diagnose ab statt auf Produktion zurueckzufallen. `App.ConfigureServices` ersetzt nur Transport-, Launcher-, Shutdown- und Safety-Grenzen; UpdateService, Release-Auswahl, Paket-/Skriptpipeline und ViewModels bleiben produktiv (letzte Registrierung gewinnt).
- `UpdateFixtureHttpMessageHandler` bildet Szenario-Antworten inkl. `Link`-Headern, Statusfehlern, Antwort-Gates und Stream-Gates (`GateBlockierterStream`) ab; unbekannte Requests scheitern ohne Netzwerkfallback (`UpdateFixtureHttpMessageHandlerTests`, 7 Tests).
- `UpdateE2EFixture` legt eine isolierte Tempwurzel an (`installed/version.json` = 1.2.0, SQLite-DB, `scenario.json`, Stable-/RC-ZIPs mit Markern, Gates, `events.jsonl`); das JSONL-Protokoll traegt monotone `Seq` und alle geforderten Ereignisse inkl. `DatabaseInitializationCompleted`, `WindowReady`, `StartupUpdateCompleted`, `UpdateAttempt*`, `Preparation*`, `UpdateProcessStartRecorded`, `ShutdownRequested`, `UpdateSettingsReadReached/Failed/Disabled`, `CliSafetyChecked`, `UnknownHttpRequest`.
- `UpdateSettingsReadFailureInterceptor` ist ein EF-`DbCommandInterceptor`, der ausschliesslich `UpdateSettings.Read`-getaggte Abfragen betrifft, versuch-/grenzbezogen zaehlt, begrenzt/cancelbar auf Gate-Freigabe wartet und bis zur Deaktivierung aktiv bleibt.
- `WpfTestBase.LaunchApp` nimmt prozessbezogene Umgebungsvariablen (inkl. DB-Pfad-Override und App-Log-Diagnose); `RestartAppPreservingDatabase` schliesst nur das eigene Fenster regulaer, wartet den Prozess-Exit ab und erneuert die UIA-Handles ohne DB-/Build-Artefakt-Aenderung.
- Alle sieben Pflichtszenarien E-01 bis E-07 sind in `E2E_UpdateSettings.cs` implementiert (Methoden `Settings_AllModesAndPrereleasesPersist`, `Startup_ModesDriveUpdatePipeline`, `PrereleaseCheckbox_SelectsMatchingAsset`, `SavedChangesInvalidatePreviousOffer`, `Startup_NoUpdateOrUncheckableRemainsUsable`, `Startup_SafetyCancelAndErrorsRemainUsable`, `Startup_IsOnceAndCommandsStayBlocked`) und in `MainTest.RunGeneralTests` hinter den beiden Fixture-Smokes konsolidiert. Negative Aussagen stehen hinter Protokoll-Abschlussmarkern bzw. kontrolliert blockierten Schritten (Phasen-Basen via `ProtokollBasis`/`EintraegeSeit`). T-09 ist sowohl im Fixture-Smoke (`Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization`) als auch als automatische und manuelle Varianten in E-06 an allen drei Lesegrenzen abgedeckt.

## Taskstatus (1-69)

- Erledigt (Quellcode- und Testabdeckungsnachweis vorhanden): 1-57, 59-67.
- Teilweise: 58 – `UpdatePackageServiceTests` deckt die geforderten Regressionsbereiche vollstaendig ab, aber ein dedizierter Unit-Test fuer den RC-Paketpfad existiert dort nicht; der RC-Pfad wird end-to-end ueber `AssertiereInstallationsNachweis` (`extracted/1.4.0-rc.1` + `version.json`) in E-03 und im Fixture-Smoke nachgewiesen. `UpdateScriptServiceTests` ist vollstaendig vorhanden.
- Offen: 68 (kein Build-/Testlauf ausgefuehrt – per Vorgabe) und 69 (`test-results.md` existiert nicht; ohne Laeufe nicht erstellbar).

Die detaillierte Zuordnung steht in der aktualisierten [Tasks-Datei](../5c1a753c335f441294221c1e260a87c3-prereleases-tasks.md).

## Abweichungen und fehlende Nachweise

1. **Ausfuehrungsnachweise fehlen komplett (Tasks 68/69).** Weder `dotnet build Softwareschmiede.slnx` noch die regulaere (`Category!=OsInterface`) oder OS-/E2E-Spur (`Category=OsInterface`) wurden ausgefuehrt; `test-results.md` fehlt. Der Plan verlangt erfolgreiche Pflicht-E2Es und gruene Regressionen fuer die Abnahme – diese ist daher noch nicht erteilt. Vorhandener Testcode ersetzt den Lauf nicht.
2. **Kein dedizierter RC-Paketpfad-Unit-Test (Task 58).** Die T-08-Regressionstabelle nennt explizit "RC-Paketpfad mit passender `version.json` ergaenzen" in `UpdatePackageServiceTests`; dort nutzen alle Faelle `1.2.3`. Fachlich gedeckt ist der RC-Pfad ueber E2E (`AssertiereInstallationsNachweis` prueft `extracted/<version>` inkl. `1.4.0-rc.1` und Paketinhalt) – als Planziel-Punkttest nur teilweise erfuellt.
3. **Testnamen weichen teils von den Zielnamen ab**, was der Plan ausdruecklich erlaubt ("Testnamen sind Zielnamen; Parameterfaelle duerfen ohne Abdeckungsverlust zusammengefasst werden"). Konkret: `Startup_SafetyDeclinedOrCancelled_DoesNotContinue` ist in `Startup_SafetyDeclined_DoesNotContinue` + `Startup_SafetyCancelled_DoesNotContinue` aufgeteilt; die frueheren `UpdateStartenCommand_Should*`-Tests existieren nicht mehr unter diesem Namen, ihre Szenarien sind in den `automatisch`-parametrisierten Theories erhalten und automatisch gespiegelt; `TryParse_ShouldAcceptOnlyStableSemVerTags` wurde wie geplant zu `TryParse_ShouldAcceptSemVerTags` geaendert. Kein Abdeckungsverlust erkennbar.
4. **`CachedResult`-Aufgabe (Task 23) ist durch vollstaendige Entfernung geloest.** Kein Cache mehr im `UpdateService`; die vom Plan geforderte Optionswechsel-Sicherheit wird durch `CheckForUpdateAsync_PropagatesCurrentOptions` belegt. Eine separate Dokumentation der Verbrauchersuche liegt nicht vor, ist aber durch das Fehlen jeglicher Cache-Referenz unter `src/` praktisch nachgewiesen.
5. **T-09-Cleanup/Recovery ist umfassend abgedeckt:** Interceptor-Fehler bis Deaktivierung aktiv, Gate-Freigabe/-Abbruch/-Timeout, sichtbare Fehler, keine Folgeschritte, erfolgreiche manuelle Folgepruefung – in ViewModel-Tests (`MainWindowViewModelTests_UpdateSettingsReadFailure`, 3 parametrisierte Tests), Fixture-Smoke und E-06. Kein Restbefund.

## Fazit

Die Implementierung ist quellcode-seitig vollstaendig und plan-konform: saemtliche Architektur-, Verhaltens- und Testinfrastruktur-Anforderungen sowie alle sieben Pflicht-E2E-Szenarien sind mit konkreten Code- und Testbelegen nachweisbar. Einziger fachlicher Teilbefund ist der fehlende dedizierte RC-Paketpfad-Test in `UpdatePackageServiceTests` (Task 58, durch E2E abgedeckt). Die Abnahme gemaess Plan bleibt ausstehend, weil Build- und Testausfuehrung (Tasks 68/69) in dieser Review bewusst nicht erfolgten und `test-results.md` fehlt.
