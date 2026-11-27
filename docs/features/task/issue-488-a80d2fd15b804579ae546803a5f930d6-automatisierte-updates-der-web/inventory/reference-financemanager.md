# Referenz: FinanceManager-Integration und reale Deployments

FinanceManager (`D:\Repositories\FinanceManager\Current`) ist die nächstliegende Referenz für einen ASP.NET-Core-Konsumenten von msTools.Updater (Web-/Server-Host, Blazor). Rezepte und VideoPlayer liegen lokal nur als Deployment-Backups vor (kein Quellcode), liefern aber reale Laufzeit-Artefakte.

## NuGet-Einbindung

- `NuGet.config` (Repo-Wurzel): `<add key="local-msTools.Updater" value="external/msTools.Updater" />` (plus `local-msTools` für `msTools.Web.Blazor`).
- `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` eingecheckt.
- `<PackageReference Include="msTools.Updater" Version="0.11.0-rc.8" />` in `FinanceManager.Web/FinanceManager.Web.csproj` (Zeile 68) sowie den Testprojekten.

## Registrierung (`FinanceManager.Web/ProgramExtensions.cs`)

| Ort | Inhalt |
|-----|--------|
| Z. 262 | `builder.Services.AddSingleton(TimeProvider.System);` |
| Z. 263–267 | `UpdateOptions` aus Sektion `Updates` lesen; `builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(updateOptions));` |
| Z. 268–272 | `IUpdateOrchestrator → UpdateOrchestratorAdapter` (scoped), `IUpdateSettingsStore → UpdateSettingsStore`, `IInstalledReleaseMetadataProvider → InstalledReleaseMetadataProvider`, `UpdateStatusMapper`, `IUpdateServiceCatalog` (Singletons) |
| Z. 570–595 `SetInitialConfiguration` | `cfg.BindConfiguration("Updates")`; `cfg.WithUpdateUnitName("FinanceManagerUpdate")`; `cfg.WithDownloadPath(updateOptions.WorkingDirectory)`; `cfg.WithSourceCheck(24*60 Minuten, BuildSourceCheckTimeRanges(start, end))`; `SourceType == "LocalFolder"` → `UseLocalFolderSource(path, manifestAssetName)`, sonst `UseGithubSource(owner, name, manifestAssetName)` |
| Z. 894–906 `ApplyPersistedUpdateSettings` | Wird **vor** `app.Run()` auf `WebApplication` aufgerufen: lädt persistierte Settings via `IUpdateSettingsStore` und wendet sie mit `ApplyToOptions` auf den `AutoUpdateOptions`-Singleton an — sonst würden Checker/Scheduler mit appsettings-Defaults statt Admin-Einstellungen laufen. Fehler werden geloggt und geschluckt (korrupte settings.json blockiert den Start nicht) |

## Eigene Schichten um die Bibliothek (`FinanceManager.Web/Services/Updates/`)

| Datei | Inhalt |
|-------|--------|
| `UpdateOptions.cs` | Host-seitige `Updates`-Sektion: `SourceCheckStartTime/EndTime` (Default 20:00–06:00), `RepositoryOwner` (`martin-stromberg`), `RepositoryName` (`FinanceManager`), `ManifestAssetName` (`update.json`), `WorkingDirectory` (`updates`), `SourceType` (`Github`/`LocalFolder`), `LocalFolderPath` |
| `UpdateSettingsStore.cs` | Persistiert Admin-Settings als `settings.json` unter `IAutoUpdatePackageStore.RootDirectory` (atomar via `JsonFileStore.WriteAtomicAsync`); normalisiert/clamped Werte (`MinHealthTimeoutSeconds`/`MaxHealthTimeoutSeconds`); liest Legacy-Format (`windowsServiceName`/`linuxServiceName`) migrierend; `ApplyToOptions` delegiert an `AutoUpdateOptionsMapper` |
| `AutoUpdateOptionsMapper.cs` | `ApplySettings`: schreibt `Enabled`, `SourceCheck.Interval` (24×60), `TimeRanges` (pro Wochentag, über-Mitternacht-Fenster in zwei Ranges gespalten), `ServiceName`, `ExecutablePath`, `DownloadPath`, `HealthTimeoutSeconds`, `ScheduledInstallTime`, `AllowPrereleaseUpdates = settings.IncludePrereleases`; `EnableAutomaticInstallation = ScheduledInstallTime.HasValue`; ersetzt `options.Source` bei `AutoUpdateGithubSource` zur Laufzeit neu via `Create(...)` (alte Instanz disposed). `ToSettingsDto`/`ReadSourceCheckWindow` für die Rückrichtung |
| `UpdateOrchestratorAdapter.cs` | Implementiert FM-eigenes `IUpdateOrchestrator` über `IAutoUpdateOrchestrator` + `AutoUpdateStatusService` + `IAutoUpdatePackageStore` + `AutoUpdateOptions`. Kernpunkte: `RunWithManualOverrideAsync` setzt `Enabled` temporär auf `true`, damit manuelle „Jetzt prüfen/installieren"-Aktionen auch bei deaktivierter Automation laufen; `CheckAsync` verkettet Check→Download; `StartInstallAsync` lädt ggf. erst nach und ruft `InstallAsync(confirmDowntime, force:false)`; `ResetLockAsync` resettet stale Locks (`IsLockStale` über `HealthTimeoutSeconds`, `DeleteLockAsync`, Status-Korrektur) mit typisierter `UpdateLockResetException`; `ValidateLockCleanupAsync` loggt verbliebene Locks nur als Warning |
| `UpdateStatusMapper.cs` | Snapshot → `UpdateStatusDto` (inkl. installierter Version via `IInstalledVersionProvider`, RID via `IAutoUpdatePlatformResolver`, `AutoUpdateState` → `UpdateStatusKind`) |
| `UpdateErrorMessageMapper.cs` | Erkennt GitHub-Rate-Limit („403" + „rate limit") und mappt auf deutschen Hinweistext |
| `InstalledReleaseMetadataProvider.cs` | Dünner Mapper `IInstalledVersionProvider` → eigenes DTO (schirmt UI von Library-Typen ab) |
| `UpdateContracts.cs` | `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `IUpdateServiceCatalog`, `IUpdateOrchestrator` (GetStatus/GetSettings/SaveSettings/Schedule/Check/StartInstall/ResetLock) |
| `UpdateServiceCatalog.cs` | Kandidaten-Liste für Dienstnamen-Autocomplete (Service-Probe-Umfeld) |
| `UpdateLockResetException.cs` | Typisierter Lock-Reset-Fehler (`UpdateLockResetFailureKind`, `…FailureSource`) |

## Konfiguration (`FinanceManager.Web/appsettings.json` „Updates")

```json
"Updates": {
  "Enabled": false, "HostedServicesEnabled": true,
  "SourceCheckStartTime": "20:00:00", "SourceCheckEndTime": "06:00:00",
  "RepositoryOwner": "martin-stromberg", "RepositoryName": "FinanceManager",
  "ManifestAssetName": "update.json", "WorkingDirectory": "updates",
  "ServiceName": null, "HealthTimeoutSeconds": 120, "MaxAssetBytes": 536870912,
  "SourceType": "Github", "LocalFolderPath": null,
  "EnableAutomaticDownload": true, "EnableAutomaticInstallation": false,
  "StopHostAfterScriptStart": false
}
```

Kanalwahl = `IncludePrereleases` in den persistierten Admin-Settings → `AllowPrereleaseUpdates` (einfacher bool; kein eigener Enum). Persistenz liegt **nicht** in der DB, sondern als `settings.json` im Update-Workspace (durch Atomic-Write abgesichert).

## Pipeline-Referenz

- `.github/actions/build-and-package/action.yml`: published `FinanceManager.Web` für `win-x64` + `linux-x64` self-contained und schreibt **je RID** `release-metadata.json` (`version`, `publishedAt`, `commitSha`, `repository`, `runtimeIdentifier`) ins Publish-Root (Z. 58–65); ZIPs heißen `FinanceManager-v{version}-{rid}.zip`; `update.json` wird per Node-Skript erzeugt.
- `scripts/generate-update-manifest.mjs`: Rezepte-Format-Manifest (`version`, `releaseNotes`, `publishedAt`, `assets[]` mit `platform`/`runtimeIdentifier`/`assetName`/`assetUrl`/`sha256`/`sizeBytes`), SHA-256 + Größe real gehascht, Download-URL `https://github.com/{repo}/releases/download/{tag}/{asset}`.

## Reale Deployments (Rezepte/VideoPlayer-Backups, nur Dateien)

- `release-metadata.json` im App-Root: z. B. `{"version":"1.21.2-rc.4","publishedAt":"…","commitSha":"…","repository":"martin-stromberg/Rezepte","runtimeIdentifier":"linux-x64"}`.
- `updates/status.json`: persistierter `AutoUpdateStatusSnapshot` (State numerisch, installedVersion, availableVersion, lastCheckResult mit Package-Descriptor `fileName`/`uri`/`sha256`/`sizeBytes`, lastDownloadResult mit `localPath`/`checksumValid`, lastInstallResult mit `scriptPath`/`updateLogPath`, `isLocked`/`lockCreatedAt`).
- `updates/update.log` (Linux-Pfad): `systemd-run`-getriebenes `update.sh` — `systemctl stop <app>.service` → Staging-Clean → `unzip` → `release-metadata.json`-Prüfung → (VideoPlayer zusätzlich: „Backing up protected files…" / „Applying protected file preservation/merge…") → `cp` ins App-Verzeichnis → `systemctl start` → Erfolgsprüfung (Service aktiv) → Workspace-Cleanup.
- `update.failed`-Marker-Mechanik aus README deckt sich mit dem Status-Modell (`LastErrorCode`/`Failed`).

## Tests (Referenzkonventionen)

`FinanceManager.Tests/Updates/`: `UpdateOrchestratorAdapterTestFactory` baut `AutoUpdateStatusService` auf `Mock<IAutoUpdateStateStore>` + `Mock<IInstalledVersionProvider>` und den Adapter auf Mocks von `IAutoUpdateOrchestrator`, `IAutoUpdatePackageStore` u. a.; daneben `AutoUpdateOptionsMapperTests`, `UpdateSettingsStoreTests`, `UpdateStatusMapperTests`, `UpdateOrchestratorAdapter(LockAndSchedule)Tests`, `InstalledReleaseMetadataProviderTests`, `TestWebHostEnvironment`, `UpdateStatusTestData`. Tests der Bibliotheks-DTOs laufen also komplett ohne Netz/Filesystem über die öffentlichen Interfaces (Moq).
