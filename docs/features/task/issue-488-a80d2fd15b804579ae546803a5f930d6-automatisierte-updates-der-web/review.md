# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Paket/Infrastruktur

- [x] `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` — vorhanden, `.gitignore`-Negation `!external/msTools.Updater/*.nupkg` (Zeile 199) vorhanden
- [x] `NuGet.config` (Repo-Root) — Quelle `local-msTools.Updater` → `external/msTools.Updater`; kein `<clear/>`, nuget.org bleibt aktiv
- [x] `PackageReference msTools.Updater 0.11.0-rc.8` in `Softwareschmiede.StatusServer.csproj` (Zeile 23)
- [x] `PackageReference msTools.Updater 0.11.0-rc.8` + `Moq 4.20.72` in `Softwareschmiede.StatusServer.Tests.csproj` (Zeilen 23–24)

### Neue Klassen unter `server/Softwareschmiede.StatusServer/Update/`

- [x] `StatusServerUpdateOptions` — `SectionName="Update"`, `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`, `WorkingDirectory`, `SourceType`, `LocalFolderPath`, `SourceCheckStartTime`/`EndTime` (Defaults 20:00/06:00 über Mapper-Konstanten)
- [x] `StatusServerUpdateChannel` (Enum) — `Stable`, `ReleaseCandidate`
- [x] `StatusServerAutomationMode` (Enum) — `Off`, `CheckOnly`, `CheckAndInstall`
- [x] `StatusServerUpdateSettings` (record) — `Channel`, `Mode`, `ScheduledInstallTime` (`TimeOnly?`)
- [x] `StatusServerUpdateStatus` (record) — `InstalledVersion`, `AvailableVersion`, `State`, `LastCheckedAt`, `LastError`, `IsLocked`, `IsLockStale`, `UpdateLogPath`. **Abweichung zum Plan:** Das geplante Feld `ReadyToInstall` wurde durch den Code-Review-Befund „toter Code" entfernt — es duplizierte `State == AutoUpdateState.ReadyToInstall`, das in der UI bereits als Text gerendert wird; `UpdateLogPath` wird seit dem Fix in `AppendUpdateSection` gerendert (Composer Zeilen 286–290). Informationsgehalt und UI-Abdeckung des Planelements sind unverändert erfüllt.
- [x] `StatusServerUpdateSettingsCodec` (static) — **nicht im Plan**, aus Code-Review-Befund hinzugekommen: zentrale String↔Enum-Abbildung (`TryParseChannel`/`FormatChannel`, `TryParseMode`/`FormatMode`) und kulturinvariante `HH:mm`-Zeitverarbeitung (`TryParseInstallTime`/`FormatInstallTime`); wird von `AdminEndpoints`, `ServerSettingsService` und `AdminPagesComposer` gemeinsam genutzt
- [x] `StatusServerAutoUpdateOptionsMapper` (static) — `ApplySettings` (Enabled/AllowPrereleaseUpdates/Download+Installation-Flags/ScheduledInstallTime/SourceCheck-Intervall), `BuildSourceCheckTimeRanges` mit Über-Mitternacht-Split
- [x] `StatusServerUpdateSourceFactory` (Singleton) — `Create(settings)`: `AutoUpdateGithubSource` per Konstruktor mit `IHttpClientFactory`-Client `StatusServerUpdate` + `includePrereleases` aus Kanal; `AutoUpdateLocalFolderSource` bei `SourceType=LocalFolder`. **Erweiterung durch Code-Review-Fix:** `ReplaceSource(options, settings)` kapselt Quelltausch + Dispose des Vorgängers — wird von `ApplySettingsAsync` und `ApplyPersistedUpdateSettingsAsync` gemeinsam genutzt
- [x] `StatusServerUpdateService` (scoped) — `GetStatusAsync`, `ApplySettingsAsync`, `CheckAndDownloadAsync`, `StartInstallAsync(confirmDowntime)` mit Download-Nachladen bei `UpdateAvailable`, `ResetLockAsync` (nur bei stale Lock + Status-Korrektur). **Erweiterung durch Code-Review-Fix:** `RunWithManualOverrideAsync` serialisiert den Enabled-Override jetzt über statisches `SemaphoreSlim(1,1)` (Zeilen 20, 161–178)
- [x] `UpdateStatusMapper` (Singleton) — Snapshot + `IInstalledVersionProvider` + Lock-Alter über `IAutoUpdatePackageStore.IsLockStale` → DTO (ohne `ReadyToInstall`, s. o.)
- [x] `AutoUpdateApplicationExtensions` (static) — `SetInitialConfiguration` (`BindConfiguration`, `WithUpdateUnitName("SoftwareschmiedeStatusServerUpdate")`, `WithDownloadPath`, `WithSourceCheck(24*60, ranges)`, `PreserveFile("appsettings*.json")`, `PreserveFile("data/*")`, Quellwahl). **Erweiterung durch Code-Review-Fix:** Signatur um `databasePath`/`contentRootPath` erweitert; `PreserveDatabaseFiles` registriert einen `Data:DatabasePath` innerhalb des App-Verzeichnisses, aber außerhalb `data/*`, zusätzlich inkl. SQLite-Seitendateien (`-<suffix>-*`); `ApplyPersistedUpdateSettingsAsync` (Scope → `GetUpdateSettingsAsync` → Mapper → `ReplaceSource`, Fehler geloggt+geschluckt)

### Geänderte bestehende Klassen

- [x] `ServerSettingsService` — Konstanten `UpdateChannelKey`/`UpdateAutomationModeKey`/`UpdateInstallTimeKey` (Zeilen 24–30) + Defaults `DefaultUpdateChannel`/`DefaultUpdateMode`/`DefaultUpdateInstallTime` (03:00, Zeilen 33–39); `GetUpdateSettingsAsync` (korrupte/fehlende Werte → Defaults, Zeilen 115–136), `SetUpdateSettingsAsync` (Upsert aller drei Schlüssel unter `s_writeLock`, Zeilen 142–168) — nutzt jetzt `StatusServerUpdateSettingsCodec`
- [x] `AdminEndpoints` — `GetSettings` mit `err`-Parameter lädt Update-Settings + Status (Zeilen 264–276, 465–483); vier neue POST-Handler `PostUpdateSettings`/`PostUpdateCheck`/`PostUpdateInstall`/`PostUpdateLockReset` (Zeilen 299–391), alle `AdminOnlyPolicy` + `RequireAntiforgery` (Zeilen 52–55); Validierung über `StatusServerUpdateSettingsCodec`; `confirmDowntime`-Pflicht → direkte Fehlerseite; Erfolg → `RenderUpdateInstallStarted` (kein Redirect)
- [x] `AdminPagesComposer` — `RenderSettings` mit `StatusServerUpdateSettings` + `StatusServerUpdateStatus` (Zeilen 200–230); Sektion „Updates" (`AppendUpdateSection`, Zeilen 254–342): Status-Tabelle inkl. Lock-Anzeige und `UpdateLogPath`, Kanal-/Modus-Selects über Codec-Werte, Installationszeit-Input (`input[type=time]`, invariant formatiert), Buttons Speichern/Prüfen/Installieren mit Downtime-Checkbox, Lock-Reset-Form nur bei stale Lock; `RenderUpdateInstallStarted` (Zeilen 238–252)
- [x] `StatusMessages` — `UpdateSettingsSaved`, `UpdateCheckDone`, `UpdateLockReset`, `UpdateCheckFailed`, `UpdateInstallFailed`, `UpdateLockResetFailed` samt deutscher Texte (Zeilen 45–61, 77–82)
- [x] `Program.cs` — `Update`-Sektion gelesen (Zeilen 49–51), `Data:DatabasePath` vorab aufgelöst (Zeilen 47–48), `builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(updateOptions, databasePath, contentRoot))` (Zeilen 52–53), Named `HttpClient` `StatusServerUpdate` mit Bearer aus `GitHub:Token` (Zeilen 55–64), DI-Registrierungen (Zeilen 54, 65–67), `await app.ApplyPersistedUpdateSettingsAsync()` nach DB-Init, vor `app.Run()` (Zeile 129)

### Konfiguration

- [x] `appsettings.json` — `Update`-Sektion vollständig gemäß Konfigurationstabelle: `Enabled=false`, `HostedServicesEnabled=true`, `SourceCheckStartTime/EndTime` 20:00/06:00, `RepositoryOwner`/`RepositoryName`, `ManifestAssetName`, `WorkingDirectory`, `SourceType=Github`, `LocalFolderPath=null`, `ServiceName`/`ExecutablePath=null`, `HealthTimeoutSeconds=120`, `MaxAssetBytes=536870912`, `EnableAutomaticDownload=true`, `EnableAutomaticInstallation=false`, `StopHostAfterScriptStart=false`

### Release-Pipeline

- [x] `.github/actions/build-and-package/action.yml` — `release-metadata.json` je RID in `publish-statusserver-win`/`publish-statusserver-linux` inkl. `Test-Path`-Verify (Zeilen 108–138); Schritt „Create StatusServer update manifest" → `statusserver-update.json` (ab Zeile 209)
- [x] `.github/workflows/release.yml` — `statusserver-update.json` in `RELEASE_EXTRA_ASSET_PATHS` (Zeile 161), `gh release create` (Zeile 176) und `gh release upload` (Zeile 187)
- [x] `.github/workflows/staging-ci.yml` — `statusserver-update.json` im `gh release create` (Zeile 351)

### Tests

- [x] `StatusWebApplicationFactory.ConfigureWebHost` — `Update:Enabled=false`, `Update:HostedServicesEnabled=false`, `Update:WorkingDirectory` ins Temp-Verzeichnis (Zeilen 38–40). **Erweiterung durch Code-Review-Fix:** `DisposeAsync` löscht `_ownedDirectory` jetzt auch bei gesetztem `DatabasePathOverride` (Zeilen 44–63)
- [x] `Security/ServerSettingsServiceTests` — 7 Tests gesamt, darunter Update-Settings-Defaults/Korrupte-Werte/Roundtrip
- [x] `Update/StatusServerAutoUpdateOptionsMapperTests` — `ApplySettings_MapsModes`, `ApplySettings_MapsChannel`, `BuildSourceCheckTimeRanges_SplitsOvernight`, `BuildSourceCheckTimeRanges_SameDay_SingleRangePerDay` (4 Tests)
- [x] `Update/StatusServerUpdateSourceFactoryTests` — LocalFolder, Named-HttpClient, Auth-Header mit/ohne Token, Kanal+Manifestname (5 Tests)
- [x] `Update/StatusServerUpdateServiceTests` — Enabled-Override + Restore, Serialisierung paralleler Overrides (`CheckAndDownload_ParallelRuns_AreSerializedAndKeepEnabled`, neu aus Review-Fix), Check→Download-Verkettung, Nachladen bei `UpdateAvailable`, `confirmDowntime`-Durchreichung, Lock-Reset nur bei stale Lock, `ApplySettings`-Source-Tausch (10 Tests)
- [x] `Update/StatusServerUpdateSettingsCodecTests` — **neu aus Review-Fix:** Roundtrips aller Enum-Mitglieder, unbekannte Werte → `null`, Kulturinvarianz (`InstallTime_IsCultureInvariant` u. a. fi-FI), ungültige Zeitwerte (5 Tests)
- [x] `Update/AutoUpdateApplicationExtensionsTests` — **neu aus Review-Fix:** `PreserveDatabaseFiles`-Abdeckung (DB unter `data/` → keine Extra-Einträge, DB im App-Root → Preserve inkl. Seitendateien, DB in `data/`-Unterverzeichnis → Preserve, DB außerhalb App → keine Einträge; 4 Tests)
- [x] `StatusWebApplicationFactoryTests` — **neu aus Review-Fix:** `DisposeAsync_WithDatabasePathOverride_RemovesOwnedDirectory` (Temp-Cleanup im Override-Pfad)
- [x] `Pages/AdminPagesComposerTests` — `RenderSettings`-Aufrufe an neue Signatur angepasst; Assertions für Update-Sektion, stale-Lock-Reset-Form und `RenderUpdateInstallStarted` (11 Tests)
- [x] `Auth/AdminUpdateSettingsTests` — Persistieren+Anwenden, Validierungsfehler, AdminOnlyPolicy+Antiforgery, Check-Redirects, Install ohne `confirmDowntime` → Fehlerseite, Lock-Reset ohne Lock → err (6 Tests)
- [x] `TestSupport/TempUpdateSource` — Temp-Manifest + Fake-ZIP + echtem SHA-256 (bzw. Override für Mismatch)
- [x] `TestSupport/UpdateTestFactory` — `LocalFolder`-Quelle + fixer `IInstalledVersionProvider`
- [x] `TestSupport/StatusServerDbSeeder.GetUpdateSettingsAsync` — Persistenz-Assertions (Zeilen 190–191)
- [x] `E2E/StatusServerUpdateFlowTests` — `LocalFolderSource_CheckAndDownload_ReachesReadyToInstall`, `LocalFolderSource_HashMismatch_CheckFails`, `Restart_SameDatabase_UpdateSettingsApplyOnStartup` (3 Tests)

### Dokumentation

- [x] `server/README.md` — Abschnitt „Automatische Updates" (Zeile 103 ff.): Admin-UI-Steuerung, `Update`-Konfiguration, Neustart je Plattform, geschützte Dateien, `updates/`-Logs, Lock-Reset, `release-metadata.json`-Voraussetzung

## Offene Aufgaben

- [ ] `Manuelle E2E-Abnahme dokumentieren` — teilweise umgesetzt: Eine reale Abnahme gegen einen laufenden Kestrel-Server ist durchgeführt und in `test-results.md` („Reale End-to-End-Abnahme") dokumentiert, inkl. Artefakten unter `e2e-artifacts/` (gerenderte HTML-Seiten + `updates-status-failed.json`; Stichproben verifiziert). Abgedeckt: Happy Path bis `ReadyToInstall` (Version 1.0.0 → 9.9.9, `?msg=update-check-done`, `status.json` state=4), Persistenz der Einstellungen über echten Prozess-Neustart, Hash-Mismatch (`?err=update-check-failed`, `LastError`, state=7), Lock-Reset-Negativpfad, fehlende Downtime-Bestätigung, ungültige Werte. **Rest-Lücke:** Das im Plan geforderte Auslösen von „Jetzt installieren" mit anschließendem Installationsskript-Lauf (Binäraustausch, Dienst-/Prozess-Neustart, Backup/Restore von `data/*` + `appsettings*.json`, Workspace-Bereinigung, `updates/update.log`) wurde nicht abgenommen — Akzeptanzszenarien 4 und 5 des Plans bleiben gegen eine reale Installation unverifiziert; `update.log` existiert folglich nicht als Beleg. Umgebungsbedingt nicht in der Sandbox darstellbar (kein Service-/IIS-Lifecycle); erfordert Abnahme auf einem realen Deployment (z. B. staging) mit systemd-Unit oder Windows-Dienst.

## Hinweise

- Die seit dem ersten Review behobenen Code-Review-Befunde haben zwei Planelemente verändert, ohne Lücken zu erzeugen: `StatusServerUpdateStatus.ReadyToInstall` entfiel (redundant zu `State`), `SetInitialConfiguration`/`PreserveFile`-Abdeckung wurde um den `Data:DatabasePath`-Schutz erweitert. Zusätzlich entstand `StatusServerUpdateSettingsCodec` als nicht geplante, sinnvolle Ergänzung.
- `UpdateStatusMapper` nutzt `IAutoUpdatePackageStore` statt des im Plan genannten `IAutoUpdatePlatformResolver` (unverändert aus Iteration 1; `IsLockStale` ist nur über den PackageStore beurteilbar).
- Ob die Rest-Lücke bei Task 40 als Abnahme-Hindernis oder als auf ein Staging-Deployment zu verschiebender Schritt bewertet wird, ist eine PO-/Anwender-Entscheidung — das Review dokumentiert sie als präzise offene Aufgabe.
- Testlauf nicht erneut ausgeführt (Review prüft nur Plan-vs-Code); die Tasks-Datei verweist auf den vom Implementierungslauf gemeldeten Stand (151 StatusServer-Tests grün) plus dokumentierter realer Abnahme.
