# Interfaces

## `IApplicationVersionProvider`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeilen 4–8)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetInstalledVersionAsync` | `CancellationToken ct = default` | `Task<InstalledVersionInfo?>` | Liest die installierte Version (`version.json`); `null` = nicht prüfbar |

Implementiert von `ApplicationVersionProvider`.

## `IUpdateReleaseClient`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeilen 11–18)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetLatestReleaseAsync` | `UpdateCheckOptions options, CancellationToken ct = default` | `Task<UpdateReleaseLookupResult>` | Ruft die neueste zulässige Release-Information ab; unterscheidet fehlgeschlagene Abfrage von „kein Kandidat" |

Implementiert von `GitHubReleaseClient`. Die Filtersteuerung erfolgt ausschließlich über `UpdateCheckOptions.IncludePrereleases` — kein Pfad-/Quellparameter vorhanden.

## `IUpdateService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeilen 21–34)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckForUpdateAsync` | `UpdateCheckOptions options, CancellationToken ct = default` | `Task<UpdateCheckResult>` | Prüft, ob ein neueres Update verfügbar ist |
| `PrepareUpdateAsync` | `UpdateInfo update, IProgress<UpdatePreparationProgress>? progress, CancellationToken ct = default` | `Task<UpdatePreparationResult>` | Bereitet Download, Entpacken und Skript vor |
| `StartPreparedUpdateAsync` | `UpdatePreparationResult preparation, CancellationToken ct = default` | `Task` | Startet das vorbereitete externe Update |

Implementiert von `UpdateService`; in der E2E-Umgebung dekoriert von `ProtokollierenderUpdateService`.

## `ICliUpdateSafetyService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeilen 37–41)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckAsync` | `CancellationToken ct = default` | `Task<CliUpdateSafetyResult>` | Bewertet riskante aktive CLI-Aufgaben vor einem Update |

## `IUpdatePackageService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeilen 44–51)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `PreparePackageAsync` | `UpdateInfo update, IProgress<UpdatePreparationProgress>? progress, CancellationToken ct = default` | `Task<UpdatePreparationResult>` | Download (`UpdateInfo.DownloadUrl`), Entpacken und Validierung des Update-Pakets |

Implementiert von `Infrastructure.Services.Updates.UpdatePackageService`.

## `IUpdateScriptService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeilen 54–66)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateScriptAsync` | `string targetDirectory, string extractedDirectory, string executableName, string logPath, CancellationToken ct = default` | `Task<string>` | Schreibt das Update-Skript |
| `StartScriptAsync` | `UpdatePreparationResult preparation, CancellationToken ct = default` | `Task` | Startet das vorbereitete Update-Skript |

Implementiert von `Infrastructure.Services.Updates.UpdateScriptService`.

## `IUpdateProcessLauncher`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeilen 69–73)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Start` | `string fileName, IEnumerable<string> arguments, string workingDirectory, bool runElevated` | `bool` | Startet den externen Updater-Prozess testbar gekapselt |

E2E-Implementierung: `RecordingUpdateProcessLauncher`.

## `IApplicationShutdownService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` (Zeilen 76–80)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Shutdown` | — | `void` | Beendet die Anwendung geordnet nach gestartetem Updater |

## `IUpdateVersuchProtokoll`
Datei: `src/Softwareschmiede.App/Services/IUpdateVersuchProtokoll.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ProtokolliereWindowReady` | — | `void` | Protokolliert Fensterbereitschaft vor dem ersten Updateversuch |
| `BeginneVersuch` | `string art` (`startup`/`pruefen`/`starten`) | `void` | Meldet Versuchsbeginn |
| `BeendeVersuch` | `string art, bool updateVerfuegbar, string? hinweis` | `void` | Meldet Versuchsende |

Nur E2E-Instrumentierung; produktiv `null` (in `MainWindowUpdateDienste` optional). Implementiert von `UpdateE2ETestKontext`.

## `IUpdateProgressDialogService`
Datei: `src/Softwareschmiede.App/Services/IUpdateProgressDialogService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Show` | `UpdateProgressViewModel` | `void` | Zeigt den Fortschrittsdialog während der Update-Vorbereitung |

Implementiert von `WpfUpdateProgressDialogService`.
