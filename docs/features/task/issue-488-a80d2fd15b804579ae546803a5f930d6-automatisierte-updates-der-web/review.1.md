# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Paket/Infrastruktur

- [x] `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` — eingecheckt, `.gitignore`-Negation `!external/msTools.Updater/*.nupkg` vorhanden, `git check-ignore` bestätigt Auffindbarkeit
- [x] `NuGet.config` (Repo-Root) — lokale Quelle `external/msTools.Updater` ergänzt, nuget.org bleibt aktiv (nur `add`, kein `clear`)
- [x] `PackageReference msTools.Updater 0.11.0-rc.8` in `Softwareschmiede.StatusServer.csproj` (Zeile 23)
- [x] `PackageReference msTools.Updater 0.11.0-rc.8` + `Moq 4.20.72` in `Softwareschmiede.StatusServer.Tests.csproj` (Zeilen 23–24)

### Neue Klassen unter `server/Softwareschmiede.StatusServer/Update/`

- [x] `StatusServerUpdateOptions` — `SectionName="Update"`, `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`, `WorkingDirectory`, `SourceType`, `LocalFolderPath`, `SourceCheckStartTime`/`EndTime` (Defaults 20:00/06:00)
- [x] `StatusServerUpdateChannel` (Enum) — `Stable`, `ReleaseCandidate`
- [x] `StatusServerAutomationMode` (Enum) — `Off`, `CheckOnly`, `CheckAndInstall`
- [x] `StatusServerUpdateSettings` (record) — `Channel`, `Mode`, `ScheduledInstallTime` (`TimeOnly?`)
- [x] `StatusServerUpdateStatus` (record) — `InstalledVersion`, `AvailableVersion`, `State`, `LastCheckedAt`, `LastError`, `IsLocked`, `IsLockStale`, `ReadyToInstall`, `UpdateLogPath`
- [x] `StatusServerAutoUpdateOptionsMapper` (static) — `ApplySettings` (Enabled/AllowPrereleaseUpdates/Download+Installation-Flags/ScheduledInstallTime/SourceCheck-Intervall), `BuildSourceCheckTimeRanges` mit Über-Mitternacht-Split
- [x] `StatusServerUpdateSourceFactory` (Singleton) — `Create(settings)`: token-bewusste `AutoUpdateGithubSource` via `IHttpClientFactory`-Client `StatusServerUpdate` + `includePrereleases` aus Kanal, `AutoUpdateLocalFolderSource` bei `SourceType=LocalFolder`
- [x] `StatusServerUpdateService` (scoped) — `GetStatusAsync`, `ApplySettingsAsync` (inkl. Source-Neuaufbau), `CheckAndDownloadAsync` (Check→Download-Verkettung), `StartInstallAsync(confirmDowntime)` (Nachladen bei `UpdateAvailable` ohne Download), `ResetLockAsync` (nur bei stale Lock + Status-Korrektur), `RunWithManualOverrideAsync`-Enabled-Override
- [x] `UpdateStatusMapper` (Singleton) — Snapshot + `IInstalledVersionProvider` + Lock-Alter (`IAutoUpdatePackageStore.IsLockStale`) → DTO
- [x] `AutoUpdateApplicationExtensions` (static) — `SetInitialConfiguration` (BindConfiguration, `WithUpdateUnitName("SoftwareschmiedeStatusServerUpdate")`, `WithDownloadPath`, `WithSourceCheck(24*60, ranges)`, `PreserveFile("appsettings*.json")`, `PreserveFile("data/*")`, Quellwahl), `ApplyPersistedUpdateSettingsAsync` (Scope → `GetUpdateSettingsAsync` → Mapper → Source-Neuaufbau, Fehler geloggt+geschluckt)

### Geänderte bestehende Klassen

- [x] `ServerSettingsService` — Konstanten `UpdateChannelKey`/`UpdateAutomationModeKey`/`UpdateInstallTimeKey` + Defaults `DefaultUpdateChannel`/`DefaultUpdateMode`/`DefaultUpdateInstallTime` (03:00); `GetUpdateSettingsAsync` (korrupte/fehlende Werte → Defaults), `SetUpdateSettingsAsync` (Upsert aller drei Schlüssel unter `s_writeLock`)
- [x] `AdminEndpoints` — `GetSettings` mit `err`-Parameter lädt Update-Settings + Status; `RenderSettingsAsync` mit neuen Parametern; `PostUpdateSettings` (Validierung channel/mode/installTime, direktes Fehler-Rendering), `PostUpdateCheck` (msg/err-Redirect je Outcome), `PostUpdateInstall` (`confirmDowntime`-Pflicht → Fehlerseite; Erfolg → `RenderUpdateInstallStarted`, kein Redirect), `PostUpdateLockReset` (nur bei stale Lock) — alle vier `AdminOnlyPolicy` + `RequireAntiforgery` (Zeilen 52–55)
- [x] `AdminPagesComposer` — `RenderSettings` mit `StatusServerUpdateSettings` + `StatusServerUpdateStatus`; Sektion „Updates" (Status-Tabelle inkl. Lock-Anzeige, Kanal-/Modus-Selects, Installationszeit-Input, Buttons Speichern/Prüfen/Installieren mit Downtime-Checkbox, Lock-Reset nur bei stale Lock); `RenderUpdateInstallStarted` vorhanden
- [x] `StatusMessages` — `UpdateSettingsSaved`, `UpdateCheckDone`, `UpdateLockReset`, `UpdateCheckFailed`, `UpdateInstallFailed`, `UpdateLockResetFailed` samt deutscher Texte
- [x] `Program.cs` — `Update`-Sektion gelesen, `builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(updateOptions))` (Zeile 48), Named `HttpClient` `StatusServerUpdate` (BaseAddress api.github.com, UserAgent, Bearer aus `GitHub:Token`, Zeilen 50–59), DI-Registrierungen (Zeilen 49, 60–62), `await app.ApplyPersistedUpdateSettingsAsync()` nach DB-Init, vor `app.Run()` (Zeile 126)

### Konfiguration

- [x] `appsettings.json` — `Update`-Sektion komplett gemäß Konfigurationstabelle: `Enabled`, `HostedServicesEnabled`, `SourceCheckStartTime`/`EndTime`, `RepositoryOwner`/`Name`, `ManifestAssetName`, `WorkingDirectory`, `SourceType`, `LocalFolderPath`, `ServiceName`, `ExecutablePath`, `HealthTimeoutSeconds`, `MaxAssetBytes`, `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `StopHostAfterScriptStart`

### Release-Pipeline

- [x] `.github/actions/build-and-package/action.yml` — Schritt „Write StatusServer release metadata" schreibt `release-metadata.json` je RID (`version`, `publishedAt`, `commitSha`, `repository`, `runtimeIdentifier`) in `publish-statusserver-win`/`publish-statusserver-linux` inkl. `Test-Path`-Verify; Schritt „Create StatusServer update manifest" erzeugt `statusserver-update.json` mit SHA-256/size/assetUrl je ZIP
- [x] `.github/workflows/release.yml` — `statusserver-update.json` in `RELEASE_EXTRA_ASSET_PATHS`, im manuellen `gh release create` und im `gh release upload`
- [x] `.github/workflows/staging-ci.yml` — `statusserver-update.json` im `gh release create`

### Tests

- [x] `StatusWebApplicationFactory.ConfigureWebHost` — `Update:Enabled=false`, `Update:HostedServicesEnabled=false`, `Update:WorkingDirectory` ins Temp-Verzeichnis (Zeilen 38–40)
- [x] `Security/ServerSettingsServiceTests` — `GetUpdateSettings_WithoutPersistedValues_ReturnsDefaults`, `GetUpdateSettings_CorruptValues_FallBackToDefaults`, `SetUpdateSettings_PersistsAllKeys_AndRoundtrips`
- [x] `Update/StatusServerAutoUpdateOptionsMapperTests` — `ApplySettings_MapsModes`, `ApplySettings_MapsChannel`, `BuildSourceCheckTimeRanges_SplitsOvernight` (+ SameDay-Test)
- [x] `Update/StatusServerUpdateSourceFactoryTests` — Token-Header mit/ohne `GitHub:Token`, Kanal/`includePrereleases`, `ManifestAssetName`, LocalFolder (5 Tests)
- [x] `Update/StatusServerUpdateServiceTests` — Moq über `IAutoUpdateOrchestrator`/`IAutoUpdatePackageStore`/`IAutoUpdateStateStore`: Enabled-Override + Restore, Check→Download-Verkettung, Nachladen bei `UpdateAvailable`, `confirmDowntime`-Durchreichung, Lock-Reset nur bei stale Lock, `ApplySettings`-Source-Tausch (9 Tests)
- [x] `Pages/AdminPagesComposerTests` — `RenderSettings`-Aufrufe an neue Signatur angepasst; `RenderSettings_ShowsUpdateSection`, `RenderSettings_StaleLockAndError_ShowsResetFormAndErrorPill`, `RenderUpdateInstallStarted_ExplainsRestart`
- [x] `Auth/AdminUpdateSettingsTests` — `UpdateSettings_Post_PersistsAndAppliesToRuntimeOptions` (Redirect `?msg=update-settings-saved`, Seeder-Assert, `AutoUpdateOptions`-Singleton), `UpdateSettings_InvalidValues_RenderErrors`, `UpdateSettings_RequiresAdminRoleAndAntiforgery` (anonym→login, Überwacher→access-denied, ohne Token→400), `UpdateCheck_Post_RedirectsPerOutcome`, `UpdateInstall_WithoutDowntimeConfirmation_RendersError`, `UpdateLockReset_NoLock_RedirectsWithError`
- [x] `TestSupport/TempUpdateSource` — Temp-Verzeichnis mit `statusserver-update.json` + echtem ZIP + SHA-256 (bzw. `sha256Override` für Mismatch)
- [x] `TestSupport/UpdateTestFactory` — `LocalFolder`-Quelle + fixer `IInstalledVersionProvider` (in `Auth/AdminUpdateSettingsTests` und E2E genutzt)
- [x] `TestSupport/StatusServerDbSeeder.GetUpdateSettingsAsync` — Testhelper für Persistenz-Assertions
- [x] `E2E/StatusServerUpdateFlowTests` — `LocalFolderSource_CheckAndDownload_ReachesReadyToInstall` (Happy Path bis `ReadyToInstall` + UI-Anzeige), `LocalFolderSource_HashMismatch_CheckFails` (`?err=update-check-failed` + `LastError`), `Restart_SameDatabase_UpdateSettingsApplyOnStartup` (`DatabasePathOverride`, persistierte Werte in `AutoUpdateOptions`)

### Dokumentation

- [x] `server/README.md` — Abschnitt „Automatische Updates" (Admin-UI-Steuerung, `Update`-Konfigurationstabelle, Neustart je Plattform systemd/Windows-Dienst/IIS, geschützte Dateien, `updates/`-Workspace/Logs, Lock-Reset, `release-metadata.json`-Voraussetzung) + Hinweis in „Veröffentlichen"

## Offene Aufgaben

- [ ] `Manuelle E2E-Abnahme dokumentieren` — fehlt vollständig: Der Plan (Abschnitt „E2E-Tests (Pflicht)") verlangt eine dokumentierte manuelle Abnahme der tatsächlichen Installation mit Prozess-Neustart (StatusServer lokal bzw. als systemd-Unit/Windows-Dienst starten → Admin-Login → Kanal/Modus setzen → „Jetzt prüfen"/„Jetzt installieren" → Neustart + unverändertes `data/` + `appsettings*.json` anhand `updates/update.log`, `updates/status.json` und Screenshot der Einstellungsseite nachweisen). Kein entsprechendes Dokument/Protokoll im Repo auffindbar. Ist als Tasks-Zeile 40 bewusst auf `Offen` belassen — erfordert einen realen Server-Lauf und kann nur manuell durchgeführt werden.

## Hinweise

- `UpdateStatusMapper` nutzt `IAutoUpdatePackageStore` statt des im Plan genannten `IAutoUpdatePlatformResolver` — funktional vollständig, da `IsLockStale` nur über den PackageStore beurteilbar ist und das DTO-Feld `IsLockStale` korrekt befüllt wird.
- Die Tasks-Datei `docs/features/task/issue-488-a80d2fd15b804579ae546803a5f930d6-automatisierte-updates-der-web-tasks.md` ist bereits aktuell (39/40 Aufgaben `Erledigt`, Aufgabe 40 `Offen`) und entspricht den Prüfergebnissen — keine Änderung nötig.
- Testlauf nicht erneut ausgeführt (Review prüft nur Plan-vs-Code); Testnachweise in der Tasks-Datei beruhen auf dem vom Implementierungslauf gemeldeten grünen Stand (134 StatusServer-Tests).
