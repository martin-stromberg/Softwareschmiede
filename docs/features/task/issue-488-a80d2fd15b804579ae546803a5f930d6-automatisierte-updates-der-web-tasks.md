# Tasks: Automatisierte Updates der Status-Webanwendung mit msTools.Updater (Issue #488)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Paket/Infrastruktur | `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` ins Repo kopieren (aus NuGet-Cache oder FinanceManager) | Offen | — |
| 2 | Paket/Infrastruktur | `NuGet.config` im Repo-Root anlegen (Quelle `external/msTools.Updater`, nuget.org bleibt) | Offen | — |
| 3 | Paket/Infrastruktur | `PackageReference msTools.Updater 0.11.0-rc.8` in `Softwareschmiede.StatusServer.csproj` eintragen | Offen | — |
| 4 | Paket/Infrastruktur | `PackageReference msTools.Updater 0.11.0-rc.8` + `Moq` in `Softwareschmiede.StatusServer.Tests.csproj` eintragen | Offen | — |
| 5 | Konfiguration | `Update/StatusServerUpdateOptions.cs` anlegen (SectionName `Update`, Repository/Manifest/WorkingDirectory/SourceType/SourceCheck-Fenster) | Offen | — |
| 6 | Konfiguration | `Update`-Sektion in `appsettings.json` ergänzen (Defaults gemäß Plan) | Offen | — |
| 7 | Persistenz | `Update/StatusServerUpdateChannel.cs` (Enum `Stable`/`ReleaseCandidate`) anlegen | Offen | — |
| 8 | Persistenz | `Update/StatusServerAutomationMode.cs` (Enum `Off`/`CheckOnly`/`CheckAndInstall`) anlegen | Offen | — |
| 9 | Persistenz | `Update/StatusServerUpdateSettings.cs` (record: Channel, Mode, ScheduledInstallTime) anlegen | Offen | — |
| 10 | Persistenz | `ServerSettingsService` um `UpdateChannelKey`/`UpdateAutomationModeKey`/`UpdateInstallTimeKey` + `GetUpdateSettingsAsync`/`SetUpdateSettingsAsync` erweitern | Offen | — |
| 11 | Update-Logik | `Update/StatusServerAutoUpdateOptionsMapper.cs` anlegen (`ApplySettings`, `BuildSourceCheckTimeRanges` mit Über-Mitternacht-Split) | Offen | — |
| 12 | Update-Logik | `Update/StatusServerUpdateSourceFactory.cs` anlegen (token-aware `AutoUpdateGithubSource` via `IHttpClientFactory`, `AutoUpdateLocalFolderSource`) | Offen | — |
| 13 | Update-Logik | `Update/StatusServerUpdateStatus.cs` (Status-DTO) anlegen | Offen | — |
| 14 | Update-Logik | `Update/UpdateStatusMapper.cs` anlegen (Snapshot + `IInstalledVersionProvider` → DTO) | Offen | — |
| 15 | Update-Logik | `Update/StatusServerUpdateService.cs` anlegen (`GetStatusAsync`, `ApplySettingsAsync`, `CheckAndDownloadAsync`, `StartInstallAsync`, `ResetLockAsync`, Enabled-Override) | Offen | — |
| 16 | Update-Logik | `Update/AutoUpdateApplicationExtensions.cs` anlegen (`SetInitialConfiguration` inkl. `PreserveFile("appsettings*.json")` + `PreserveFile("data/*")`, `ApplyPersistedUpdateSettingsAsync`) | Offen | — |
| 17 | Update-Logik | `Program.cs`: `UseAutoUpdate`-Aufruf, Named `HttpClient` `StatusServerUpdate` (Bearer aus `GitHub:Token`), DI-Registrierungen, `ApplyPersistedUpdateSettingsAsync` vor `app.Run()` | Offen | — |
| 18 | Admin-UI | `StatusMessages` um Update-Schlüssel erweitern (`update-settings-saved`, `update-check-done`, `update-lock-reset`, `update-check-failed`, `update-install-failed`, `update-lock-reset-failed`) | Offen | — |
| 19 | Admin-UI | `AdminEndpoints`: `GET /admin/settings` um `err`-Param + Update-Settings/-Status erweitern; `RenderSettingsAsync`-Signatur anpassen | Offen | — |
| 20 | Admin-UI | `AdminEndpoints`: `POST /admin/settings/update` (Speichern Kanal/Modus/Installationszeit, Validierung, PRG) | Offen | — |
| 21 | Admin-UI | `AdminEndpoints`: `POST /admin/settings/update/check` (manueller Check+Download, PRG msg/err) | Offen | — |
| 22 | Admin-UI | `AdminEndpoints`: `POST /admin/settings/update/install` (Downtime-Checkbox-Pflicht, Ergebnisseite oder err-Redirect) | Offen | — |
| 23 | Admin-UI | `AdminEndpoints`: `POST /admin/settings/update/lock-reset` (nur bei stale Lock) | Offen | — |
| 24 | Admin-UI | `AdminPagesComposer.RenderSettings`: Sektion „Updates" (Statusanzeige, Kanal-/Modus-/Zeit-Formular, Aktionsbuttons, Lock-Reset-Button) | Offen | — |
| 25 | Admin-UI | `AdminPagesComposer.RenderUpdateInstallStarted` anlegen (Ergebnisseite „Server startet neu") | Offen | — |
| 26 | Pipeline | `build-and-package/action.yml`: `release-metadata.json` je RID in `publish-statusserver-win`/`publish-statusserver-linux` schreiben + Verify | Offen | — |
| 27 | Pipeline | `build-and-package/action.yml`: Schritt „Create StatusServer update manifest" → `statusserver-update.json` (beide Assets, SHA-256/size/assetUrl) | Offen | — |
| 28 | Pipeline | `release.yml`: `statusserver-update.json` in `RELEASE_EXTRA_ASSET_PATHS`, manuellem `gh release create` und `gh release upload` ergänzen | Offen | — |
| 29 | Pipeline | `staging-ci.yml`: `statusserver-update.json` im `gh release create` ergänzen | Offen | — |
| 30 | Dokumentation | `server/README.md`: Abschnitt „Automatische Updates" (Admin-UI, `Update`-Konfiguration, Neustart je Plattform, geschützte Dateien, `updates/`-Logs, Lock-Reset) | Offen | — |
| 31 | Tests | `StatusWebApplicationFactory.ConfigureWebHost`: `Update:Enabled=false`, `Update:HostedServicesEnabled=false`, `Update:WorkingDirectory` ins Temp-Verzeichnis setzen | Offen | — |
| 32 | Tests | `ServerSettingsServiceTests` um Update-Settings-Defaults/Roundtrip/Korrupte-Werte erweitern | Offen | — |
| 33 | Tests | `StatusServerAutoUpdateOptionsMapperTests` anlegen (Modus-/Kanal-Mapping, Zeitfenster-Split) | Offen | — |
| 34 | Tests | `StatusServerUpdateSourceFactoryTests` anlegen (Token-Header, `includePrereleases`, Manifestname) | Offen | — |
| 35 | Tests | `StatusServerUpdateServiceTests` anlegen (Moq über Bibliotheks-Interfaces: Override, Check→Download, Install, Lock-Reset) | Offen | — |
| 36 | Tests | `AdminPagesComposerTests` anpassen (neue `RenderSettings`-Signatur) + Assertions für Update-Sektion ergänzen | Offen | — |
| 37 | Tests | `Auth/AdminUpdateSettingsTests.cs` anlegen (Persistieren+Anwenden, Validierung, AdminOnlyPolicy, Antiforgery) | Offen | — |
| 38 | Tests | `TestSupport/TempUpdateSource.cs` anlegen (Temp-Manifest + ZIP + SHA-256 für LocalFolder-Tests) | Offen | — |
| 39 | E2E-Tests | `E2E/StatusServerUpdateFlowTests.cs` anlegen (Happy Path bis `ReadyToInstall` via LocalFolder, Hash-Mismatch-Fehler, Neustart-Persistenz via `DatabasePathOverride`) | Offen | — |
| 40 | E2E-Tests | Manuelle E2E-Abnahme dokumentieren (lokaler Server-Start, Admin-Login, manuelle Prüfung/Installation, `update.log`/`status.json`/Screenshot-Nachweis, Datenerhalt `data/`+`appsettings*.json`) | Offen | — |
