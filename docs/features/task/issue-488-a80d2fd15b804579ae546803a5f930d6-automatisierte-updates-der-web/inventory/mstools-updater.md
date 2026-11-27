# msTools.Updater — Paket und API-Oberfläche

Quellen der Dokumentation:
- NuGet-Cache: `C:\Users\Martin\.nuget\packages\mstools.updater\0.11.0-rc.8\` (`lib/net10.0/msTools.Updater.xml`, `README.md`, `mstools.updater.nuspec`)
- Paket-Repository (laut nuspec): `https://github.com/martin-stromberg/msTools.Updater` (privat, hier nicht eingesehen)
- Referenz-Konsument: `D:\Repositories\FinanceManager\Current\FinanceManager.Web` (Version 0.11.0-rc.8)

## Paket-Metadaten

| Eigenschaft | Wert |
|-------------|------|
| Id | `msTools.Updater` |
| Höchste verfügbare Version im lokalen NuGet-Cache | `0.11.0-rc.8` (ältere: 0.5.1 … 0.11.0-rc.6) |
| TargetFramework | `net10.0` (einzige lib-Gruppe) |
| Beschreibung (nuspec) | „Hosting-independent, DI-compatible self-update library for .NET applications: configurable update sources, background checking and scheduled installation, thread-safe status tracking and cancellable lifecycle events." |

Paketabhängigkeiten (net10.0-Gruppe): `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` (je 10.0.7), `System.Management` 10.0.11. Keine ASP.NET-Core-Abhängigkeit — läuft in Web-, Worker- und Console-Hosts. Die net10.0-Bibliothek ist vom net11.0-StatusServer referenzierbar (Versionsaufwertung der Extensions-Pakete auf 11.x).

## Lokale NuGet-Einbindung (Ist-Zustand)

- Im Softwareschmiede-Repo existiert **kein** `NuGet.config`/`nuget.config`, **kein** `external/`-Verzeichnis und **keine** msTools-Referenz in csproj/slnx (verifiziert per Suche).
- FinanceManager-Muster (`D:\Repositories\FinanceManager\Current`):
  - `NuGet.config` mit `<add key="local-msTools.Updater" value="external/msTools.Updater" />` (plus `local-msTools` für `msTools.Web.Blazor`).
  - Eingechecktes Paket: `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg`.
  - `<PackageReference Include="msTools.Updater" Version="0.11.0-rc.8" />` in `FinanceManager.Web`, `FinanceManager.Tests`, `FinanceManager.Tests.Integration`.

## Einstiegspunkt und Registrierung

`AutoUpdateHostBuilderExtensions.UseAutoUpdate(IHostApplicationBuilder, Action<AutoUpdateBuilder>?)` — der einzige unterstützte Einstieg; `WebApplicationBuilder` implementiert `IHostApplicationBuilder`. Registriert u. a. `AutoUpdateOptions` als **runtime-mutablem Singleton**, `AutoUpdateStatusService`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`, `IAutoUpdateStatusProvider`, `IAutoUpdateEventAggregator`, `IAutoUpdatePackageStore`, `IAutoUpdatePlatformResolver`, `IInstalledVersionProvider` und (sofern aktiviert) die beiden Hosted Services sowie einen Workspace-Initialisierungsdienst. Bindung erfolgt standardmäßig aus der Konfigurationssektion `AutoUpdate` (`DefaultConfigurationSectionName`); explizit gesetzte Fluent-Werte werden nach der Bindung erneut angewendet (gewinnen also gegen `appsettings.json`). Bei ungültigen Optionen wirft die Startvalidierung `OptionsValidationException`.

### `AutoUpdateBuilder` — Fluent-API

| Methode | Parameter | Zweck |
|---------|-----------|-------|
| `BindConfiguration(sectionName)` | `string` | Bindet `AutoUpdateOptions` aus einer anderen Sektion als `AutoUpdate` |
| `EnableAutomaticDownload(downloadPath)` | `string?` | Automatischer Download gefundener Updates; setzt optional `DownloadPath` |
| `EnableAutomaticInstallation()` | – | Automatische Installation zum `ScheduledInstallTime` |
| `EnablePrereleaseUpdates()` | – | `AllowPrereleaseUpdates = true` (Kanal-Stellgröße) |
| `UseGithubSource(owner, name, manifestAssetName)` | `string, string, string?` | GitHub-Releases-Quelle; `manifestAssetName = null` → `DefaultManifestAssetName` (`update.json`) |
| `UseLocalFolderSource(dir, manifestFileName)` | `string, string?` | Lokale Quelle (gleiches Manifestschema; Offline/Tests; Default-Fallback ohne konfigurierte Quelle: `<DownloadPath>/source`) |
| `UseSource(IAutoUpdateSource)` | Instanz | Eigene Quelle (muss stateless + thread-safe sein, Singleton in den Options) |
| `WithSourceCheck(interval, timeRanges)` | `int` (Minuten!), `IEnumerable<SourceCheckTimeRange>?` | Periodische Hintergrundprüfung; `null`-Ranges = jederzeit erlaubt |
| `WithDownloadPath(path)` | `string` | Workspace-Wurzel (Pakete, Status, Locks) ohne Download zu aktivieren |
| `WithUpdateUnitName(unitName)` | `string` | systemd-Transient-Unit-Name für den Skriptstart (muss pro Anwendung eindeutig sein) |
| `WithIisApplicationPool(appPoolName, siteName)` | `string, string?` | Privilegierter IIS-Pfad (WebAdministration) |
| `DisableHostedServices()` | – | Keine Registrierung von Checker-/Scheduler-Service (Tests) |
| `PreserveFile(path)` / `MergeFile(path, cfg)` / `ProtectFile(AutoUpdateProtectedFile)` | Pfad relativ zum App-Verzeichnis, Wildcards `*`/`?` | Geschützte Dateien (s. u.) |

### `AutoUpdateOptions` (Singleton, zur Laufzeit änderbar)

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `Enabled` | `bool` | Master-Schalter des Subsystems (nur per Konfiguration/Property, kein Fluent-Setter) |
| `EnableAutomaticDownload` | `bool` | Auto-Download gefundener Updates |
| `DownloadPath` | `string` | Workspace-Wurzel (Default `DefaultDownloadPath` = „updates") |
| `EnableAutomaticInstallation` | `bool` | Auto-Installation — nur wirksam mit gesetztem `ScheduledInstallTime` |
| `ScheduledInstallTime` | `TimeOnly?` | Tägliche Installationsuhrzeit; `null` → keine Auto-Installation |
| `AllowPrereleaseUpdates` | `bool` | Pre-Releases anbieten/herunterladen/installieren (Default `false`) — **der Kanal-Stellgröße der Anforderung entsprechend** |
| `Source` | `IAutoUpdateSource?` | Konfigurierte Quelle (laufzeitersetzbar — FinanceManager ersetzt `AutoUpdateGithubSource` zur Laufzeit) |
| `SourceCheck` | `SourceCheckOptions` | `Interval` (Minuten) + `TimeRanges` (`SourceCheckTimeRange`: `DayOfWeek`, `StartTime`, `EndTime` — Fenster dürfen nicht über Mitternacht spannen, werden vom `AutoUpdateOptionsValidator` verworfen; Workaround: zwei Ranges) |
| `MaxAssetBytes` | `long` | Max. Paketgröße |
| `HostedServicesEnabled` | `bool` | Registrierung der beiden Hosted Services (Default `true`) |
| `ServiceName` / `ExecutablePath` / `AppPoolName` / `SiteName` | `string?` | Restart-Ziel: Dienst / Exe / IIS-AppPool |
| `WorkspaceUser` / `WorkspaceGroup` | `string?` | Linux-Eigentümer des Updater-Workspace |
| `StopHostAfterScriptStart` | `bool` | Host nach Skriptstart selbst beenden (`DefaultAutoUpdateHostTerminator` via `IHostApplicationLifetime.StopApplication`) |
| `HealthTimeoutSeconds` | `int` | Altersschwelle, ab der ein Installations-Lock als stale gilt (Min/Max-Konstanten, Wert wird geclampet statt rejected) |
| `UpdateUnitName` | `string` | systemd-Unit-Name (s. o.) |
| `ProtectedFiles` | Liste `AutoUpdateProtectedFile` | Geschützte Dateien: `Path` (relativ, Wildcards), `Strategy` (`Preserve` = Backup + Restore, `Merge` = `XmlElements`-XPath, `XmlAttributes` (`{xpath}@{attr}`), `JsonKeys` (`:`-Pfade)); bindbar aus `AutoUpdate:ProtectedFiles` |

### Zentrale Interfaces (Auswahl)

| Interface | Wesentliche Member | Zweck |
|-----------|--------------------|-------|
| `IAutoUpdateOrchestrator` | `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync(confirmDowntime, force, ct)`, `GetStatusAsync`, `RunUpdateAsync` | Vollworkflow, serialisiert per Semaphore; `GetStatusAsync` rekonziliert Lock/`Installing`/`update.failed`-Marker nach Neustart |
| `IAutoUpdateCommandHandler` (`AutoUpdateCommandService`) | `CheckAsync`, `DownloadAsync`, `InstallAsync(confirmDowntime, force, ct)` | Schmale Fassade für manuelle/UI-Auslösung |
| `IAutoUpdateStatusProvider` | `GetSnapshot()` | Liefert `AutoUpdateStatusSnapshot` |
| `IAutoUpdateEventAggregator` | Events `BeforeCheckSource`, `BeforeDownload`, `BeforeInstall`, `BeforeStartUpdateScript` (cancellable via `args.Cancel`), `AfterStartUpdateScript`, `ErrorOccurred` (informativ) | Lifecycle-Hooks/Vetoes; fehlschlagende Subscriber destabilisieren nichts |
| `IAutoUpdateSource` | `CheckAsync`, `DownloadAsync(descriptor, path, maxBytes, ct)` | Update-Quelle; Implementierungen: `AutoUpdateGithubSource`, `AutoUpdateLocalFolderSource` |
| `IAutoUpdatePlatformResolver` | `CurrentPlatform` („windows"/„linux"), `CurrentRuntimeIdentifier`, `SelectPackage(AutoUpdateReleaseInfo)` | Plattformwahl des Pakets — **Selektion nur über Plattform + RID** |
| `IAutoUpdatePackageStore` (`FileSystemAutoUpdatePackageStore`) | `RootDirectory`, `LockPath`, `TryCreateLockAsync`, `DeleteLockAsync`, `GetLockCreatedAtAsync`, `IsLockStale`, `PendingAssetPath`, `ScriptPath`, `PersistDescriptorAsync`, `EnsureAsync`, `RecoverWorkspaceAsync` | Workspace-Layout und Installations-Lock (`update.lock`) |
| `IInstalledVersionProvider` (`ReleaseMetadataInstalledVersionProvider`) | `GetAsync` → `InstalledReleaseInfo(Version, PublishedAt, CommitSha, Repository, RuntimeIdentifier)` | Ermittelt installierte Version aus **`release-metadata.json` im Anwendungsstamm** |
| `IAutoUpdateServiceResolver` (`AutoUpdateServiceResolver`) | `Resolve()` → `AutoUpdateInstallationTarget(Platform, ServiceName, ExecutablePath, AppPoolName, SiteName, IisHostingModel, IisPhysicalPath)` | Restart-Ziel-Auflösung: AppPool → IIS-Erkennung → konfigurierter Service/Exe → Auto-Detect (`IAutoUpdateServiceProbe`) |
| `IAutoUpdateHostTerminator` | `StopApplication()` | Host-Beenden nach Skriptstart |

Hilfstypen: `JsonFileStore` (`ReadAsync`/`WriteAtomicAsync`, `JsonOptions` — wird von FinanceManager für `settings.json` wiederverwendet), `AutoUpdateSourceDownloadHelper`, `AutoUpdateError.FromException`.

### Ergebnis-/Statusmodell

- `AutoUpdateResult(Outcome, State, Code, Message, Error)` — `AutoUpdateError(Code, Message, Exception, ProcessOutput)`; die Bibliothek wirft **keine** Exceptions für Update-Fehlschläge.
- `AutoUpdateStatusSnapshot(State, InstalledVersion, AvailableVersion, LastCheckedAt, LastCheckResult, LastDownloadResult, LastInstallResult, LastError, LastErrorCode, IsLocked, LockCreatedAt, UpdateLogPath, ReadyToInstallAt)` — immutable, persistiert als `status.json` im Workspace (Felder siehe reale Datei im Rezepte-Backup).
- `AutoUpdatePackageDescriptor(Version, Platform, RuntimeIdentifier, FileName, Uri, Sha256, SizeBytes)`.
- `AutoUpdateReleaseInfo(Version, ReleaseNotes, PublishedAt, Packages, IsPrerelease)`.

### Enums

| Enum | Werte |
|------|-------|
| `AutoUpdateState` | `Idle`, `Checking`, `UpdateAvailable`, `Downloading`, `ReadyToInstall`, `Installing`, `Success`, `Failed`, `Disabled` |
| `AutoUpdateOutcome` | `Success`, `NoUpdate`, `Skipped`, `Canceled`, `Failed` |
| `AutoUpdateResultCode` | `Success`, `NoNewerUpdateAvailable`, `NoUpdateSourceConfigured`, `AutoUpdateDisabled`, `UpdateAvailable`, `DownloadCompleted`, `ReadyToInstall`, `InstallationStarted`, `Skipped`, `Canceled`, `Failed`, `Unknown` |
| `AutoUpdateErrorCode` | `None`, `SourceUnavailable`, `ManifestInvalid`, `ManifestDownloadFailed`, `AssetNotFound`, `AssetSizeMismatch`, `DownloadFailed`, `HashMismatch`, `InstallationFailed`, `SourceNotConfigured`, `ConfigurationInvalid`, `UnsupportedPlatform`, `LockActive`, `NoPackageAvailable`, `PackagePersistenceFailed`, `PackageValidationFailed`, `WorkspaceRecoveryFailed`, `Canceled`, `Unknown`, `InstallationVerificationInconclusive` |
| `AutoUpdateIisHostingModel` | `OutOfProcess`, `InProcess` |
| `AutoUpdateProtectedFileStrategy` | `Preserve`, `Merge` |

## Update-Quellen und Manifest

- **`AutoUpdateGithubSource`**: liest das Release-Manifest (`update.json` Default-Assetname) aus dem jeweils neuesten passenden GitHub-Release (`/releases`-API); Pre-Releases nur mit `AllowPrereleaseUpdates`. Konstruktor: `(HttpClient, repositoryOwner, repositoryName, IAutoUpdatePlatformResolver, manifestAssetName, includePrereleases)`; Factory `Create(owner, name, manifestAssetName, includePrereleases)` mit internem HttpClient. **Kein Token-Parameter** — ein GitHub-Token ließe sich nur über `UseSource(new AutoUpdateGithubSource(httpClientMitAuthHeader, …))` einbringen.
- **`AutoUpdateLocalFolderSource`**: gleiches Manifestschema aus lokalem Verzeichnis; Default-Fallback (`<DownloadPath>/source`), Testweg laut README.
- **Manifestschema (Rezepte-Format)**, erzeugt z. B. von FinanceManagers `scripts/generate-update-manifest.mjs`:
  ```json
  {
    "version": "1.2.3",
    "releaseNotes": "…",
    "publishedAt": "<ISO-8601>",
    "assets": [
      { "platform": "windows|linux", "runtimeIdentifier": "win-x64|linux-x64",
        "assetName": "<zip-Name>", "assetUrl": "<Download-URL>",
        "sha256": "<lower-hex-64>", "sizeBytes": 0 }
    ]
  }
  ```
  Lokale/Custom-Manifeste können `isPrerelease: true` tragen; der Orchestrierer blockt solche ohne `AllowPrereleaseUpdates`.
- **Selektionsregel:** `SelectPackage` wählt das Paket per `platform` + `runtimeIdentifier` — zwei Assets mit identischem Paar sind für einen Konsumenten nicht eindeutig selektierbar (wichtig für die Manifest-Design-Frage, siehe `release-pipeline.md`).

## Automatisierung (Hosted Services)

- `AutoUpdateCheckerService`: periodisch `CheckForUpdateAsync` unter Beachtung von `SourceCheck.Interval`/`TimeRanges`; bei neuerem Update und `EnableAutomaticDownload` → `DownloadAsync`. **Installiert nie.**
- `AutoUpdateSchedulerService`: einmal täglich zu `ScheduledInstallTime` Installation, wenn Paket `ReadyToInstall` und `EnableAutomaticInstallation`. Verpasste Fenster werden nur am selben Tag nachgeholt, wenn das Paket zur geplanten Zeit bereits bereitstand (`ReadyToInstallAt` in `status.json`); später bereit gewordene Pakete warten auf den nächsten Termin.
- Es gibt **keine** „Prüfung beim Anwendungsstart" als eigenen Modus — die Automation ist periodisch + Zeitfenster + tägliche Installationszeit.

## Installation, Neustart, Plattformen

- Generiertes Installationsskript (`.ps1` Windows / `.sh` Linux) läuft **außerhalb** des Host-Prozesses: Windows detached via WMI `Win32_Process.Create` (überlebt Host-Terminierung/Pool-Recycle; bei verweigertem WMI-Zugriff → `InstallationFailed`, kein stiller Fallback), Linux via `systemd-run`-Transient-Unit (`UpdateUnitName`).
- Windows-Ziele: IIS **Out-of-Process** (ANCM-Env-Erkennung, `app_offline.htm`, Warten auf Backend-Exit — keine Adminrechte nötig), IIS **In-Process** (beendet eigenes `w3wp`, dedizierter AppPool erforderlich), Windows-Dienst (`ServiceName`), Executable (`ExecutablePath`). Konfigurierter `ServiceName`/`ExecutablePath` wird unter IIS von der Erkennung mit Warnung überschrieben.
- Linux-Ziel: systemd-Unit — Skript stoppt Service, staged/entpackt, kopiert, `systemctl start` (belegt durch Rezepte-/VideoPlayer-`update.log`).
- Gesperrte Dateien unter Windows: Retry beim Kopieren; jeder Schritt geht nach `{DownloadPath}/update.log`; bei Abbruch `update.failed`-Marker neben `update.log` (Orchestrierer wertet ihn bei Rekonziliation aus → `Failed`/`InstallationFailed`). Linux `Merge` benötigt `python3` (sonst Skip mit Warnung).
- `StopHostAfterScriptStart`: Host beendet sich selbst nach Skriptstart (Default `false` — der Neustart obliegt sonst dem Skript/Dienst-Manager).
- Reihenfolge im Skript: **Backup → Paketkopie → Merge/Restore → Neustart**; Backup-/Merge-Fehler brechen die Installation nicht ab (Warnung), Installationsfehler nach Backup restoren die geschützten Dateien (Windows).
- macOS wird nicht unterstützt (`InvalidOperationException` im `AutoUpdateScriptGenerator`).
- `EnsureUpdateUnitAvailable` (`IAutoUpdateProcessRunner`) resettet ggf. eine zuvor fehlgeschlagene Unit und wirft, wenn bereits ein Update läuft.

## Workspace-Layout (reale Deployment-Dateien, Rezepte/VideoPlayer-Backups)

Unter `DownloadPath` (z. B. `/opt/webapps/<app>/updates`): `pending/` (ZIP + Skript `update.sh`), `staging/` (Entpackziel), `backup/` (geschützte Dateien), `status.json`, `update.log`, `update.lock`, `update.failed` (bei Fehlschlag), optional `settings.json` (FinanceManager-eigen), `source/` (Default-LocalFolder-Quelle).

## Testhaken der Bibliothek

`UseLocalFolderSource` + `DisableHostedServices()` laut README für Tests; `AutoUpdateOptions` ist ein regulärer Singleton und kann in `WebApplicationFactory.ConfigureWebHost`/`ConfigureTestServices` ersetzt oder vorkonfiguriert werden; diverse Konstruktoren nehmen injizierbare Plattform-/Zeit-Abstraktionen (`TimeProvider`, `IAutoUpdatePlatformResolver`-Testkonstruktor).
