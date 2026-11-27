# Desktop-Update-Stack (`src/Softwareschmiede`)

Der Desktop-Stack implementiert den Self-Update-Workflow der WPF-Anwendung komplett eigenständig (vor msTools.Updater) und lebt in der einen Domänen-Assembly `Softwareschmiede` (net11.0). Für Issue #488 ist er fachlich die Blaupause (Kanal, Manifest-Verifikation, externer Updater-Prozess), technisch aber Windows/WPF-geprägt.

## Dateien

| Datei | Inhalt |
|-------|--------|
| `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` | `IApplicationVersionProvider`, `IUpdateReleaseClient`, `IUpdateService` (CheckForUpdate/PrepareUpdate/StartPreparedUpdate), `ICliUpdateSafetyService`, `IUpdatePackageService`, `IUpdateScriptService`, `IUpdateProcessLauncher`, `IApplicationShutdownService` |
| `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` | `InstalledVersionInfo` (liest `version.json`: `version`, `tagName`, `commit`, `createdAtUtc` — **anderes Schema als msTools' `release-metadata.json`**), `UpdateDownloadRoute` (Primary + optionaler API-Fallback), `UpdateInfo`, `UpdateSettings`, `UpdateCheckOptions` (leitet `Channel` aus `UpdatePath` ab), `UpdateReleaseLookupResult` (`Failed`/`NoMatch`/`Found`), `UpdateCheckResult`, `UpdatePreparationProgress`, `UpdatePreparationResult`, `CliUpdateSafetyResult` |
| `src/Softwareschmiede/Application/Services/Updates/UpdateOptions.cs` | `RepositoryOwner` (`martin-stromberg`), `RepositoryName` (`Softwareschmiede`), **`AssetName = "release.zip"`**, **`ManifestAssetName = "update.json"`**, `UpdateDirectoryName` (`updates`), `CheckTimeout` (15 s), `ExecutableName` (`Softwareschmiede.exe`) |
| `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs` | Orchestriert Check → Prepare → Start (Desktop-Entsprechung von `IAutoUpdateOrchestrator`) |
| `src/Softwareschmiede/Application/Services/Updates/SemanticUpdateVersion.cs` / `UpdateVersionComparer.cs` | Eigener SemVer-Parser/-Vergleicher inkl. Prerelease-Suffix |
| `src/Softwareschmiede/Application/Services/Updates/UpdatePathChannelResolver.cs` | `StableVersionsPath = "Produktivversionen"`, `PrereleaseVersionsPath = "Vorabversionen"`; `ResolveChannel`/`NormalizePath`/`IsValidPath`/`ResolveChannelForTagName` („-rc"-Infix → RC; leere Tags konservativ RC). Kanalwahl ist **pfadindirekt** (kein eigener Optionswert); wird auch von `DatabasePathResolver` wiederverwendet |
| `src/Softwareschmiede/Application/Services/Updates/CliUpdateSafetyService.cs` | Sicherheitscheck auf aktive CLI-Aufgaben vor Update (Desktop-spezifisch) |
| `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs` | `IUpdateReleaseClient`: `/repos/{owner}/{name}/releases?per_page=100` mit Link-Header-Pagination (Host/Pfad auf `api.github.com` gepinnt), Draft-Filter, Kanal-Filter (`isPrerelease == (Channel == ReleaseCandidate)` — **Kanäle sind exklusiv**), Asset-Suche per `options.AssetName` („release.zip"), optionales Manifest-Asset für SHA-256-Verifikation, API-Asset-URL als Download-Fallback. UserAgent `Softwareschmiede/1.0`; **kein Token** |
| `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs` | Download → SHA-256-Verifikation gegen `update.json`-Manifest (`Verify`-Phase) → Entpacken → Artefakte unter `<appdir>/updates` |
| `src/Softwareschmiede/Infrastructure/Services/Updates/UpdateScriptService.cs` | Schreibt `updates/update.ps1`, startet `powershell.exe`/WindowsPowerShell (`-NoProfile -ExecutionPolicy Bypass`) mit `-AppPid -TargetDirectory -ExtractedDirectory -ExecutablePath -LogPath` via `IUpdateProcessLauncher` (optional elevated). **Windows-only** |

## Enums (in `UpdateModels.cs`)

| Enum | Werte | Bemerkung |
|------|-------|-----------|
| `UpdateMode` | `Off`, `CheckOnly`, `CheckAndRunAtStartup` | persistiert als `updates.mode` |
| `UpdateChannel` | `Stable`, `ReleaseCandidate` | exklusiv; abgeleitet aus `updates.pfad` |
| `UpdateCheckStatus` | `NoUpdate`, `UpdateAvailable`, `NotCheckable` | |
| `UpdatePreparationPhase` | `Download`, `Verify`, `Extract`, `PrepareUpdate` | Fortschrittsdialog |

## Einstellungen und Persistenz

- Desktop-Update-Einstellungen liegen in `AppSettingService`-Schlüsseln `updates.mode` / `updates.pfad` (Domänen-DB), nicht in appsettings.
- `ApplicationVersionProvider` (Implementierung von `IApplicationVersionProvider`) liest `version.json` aus dem Programmverzeichnis — die Release-Pipeline schreibt diese Datei nur in das Desktop-Publish-Verzeichnis.

## WPF-Konsumenten (nur Kontext)

`src/Softwareschmiede.App`: `MainWindowUpdateFlow`/`MainWindowUpdateServices` (Startup-Check, Modusauswahl, Kanal über `UpdatePathOptions`/`UpdateModeOptions`), `UpdateProgressDialog(.xaml)` + `UpdateProgressViewModel`, `WpfUpdateProgressDialogService`, `LoggingUpdateService`, `RecordingUpdateProcessLauncher`, E2E-Hooks (`UpdateE2E*`).

## Relevanz für den StatusServer

- **Nicht wiederverwendbar als Ganzes:** PowerShell-Skriptmechanik, Elevated-Start, WPF-Fortschrittsdialog und die pfadbasierte Kanalwahl sind Desktop-spezifisch; msTools.Updater liefert mit `IAutoUpdateOrchestrator`, systemd-/Windows-Dienst-Skripten und `AllowPrereleaseUpdates` das Äquivalent serverseitig.
- **Fachlich wiederverwendbar:** `SemanticUpdateVersion`/`UpdateVersionComparer` (SemVer inkl. `-rc`-Vergleich) und die Kanal-Nomenklatur Stable ↔ ReleaseCandidate. `UpdatePathChannelResolver` selbst ist wegen der Pfad-Indirektion nicht direkt übertragbar; der boolesche `AllowPrereleaseUpdates` von msTools.Updater bildet denselben exklusiven Kanalwechsel ab (in der Bibliothek als „Prereleases zulässig" statt „nur Prereleases" — bei msTools.Updater betrachtet die Source ohne `includePrereleases` nur stabile Releases, mit dem Flag auch Pre-Releases).
- **`ProjectReference` technisch möglich, aber schwer:** `Softwareschmiede.csproj` (net11.0) würde `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.AspNetCore.SignalR.Client`, `Softwareschmiede.Plugin.Contracts` u. a. transitiv in den Server ziehen.
- **Namenskonflikte beachten:** `UpdateOptions` existiert sowohl als `Softwareschmiede.Application.Services.Updates.UpdateOptions` (Desktop) als auch FinanceManager-seitig als `FinanceManager.Web.Services.Updates.UpdateOptions`; msTools.Updater bringt `AutoUpdateOptions`. Für die geforderte `StatusServerUpdateOptions` ist ein eigener, eindeutiger Name ratsam.
