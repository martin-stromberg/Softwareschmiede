# Interfaces

Datei aller Update-Verträge: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs`

## `IUpdatePackageService` (Zeilen 44–51)

Implementiert von `UpdatePackageService`. Vertrag bleibt laut Anforderung unverändert.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `PreparePackageAsync` | `UpdateInfo update`, `IProgress<UpdatePreparationProgress>? progress`, `CancellationToken ct` | `Task<UpdatePreparationResult>` | Lädt ein Update herunter, entpackt es und erzeugt die nötigen Update-Artefakte |

## `IUpdateReleaseClient` (Zeilen 11–18)

Implementiert von `GitHubReleaseClient`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetLatestReleaseAsync` | `UpdateCheckOptions options`, `CancellationToken ct` | `Task<UpdateReleaseLookupResult>` | Ruft die neueste zulässige Release-Information ab |

## `IUpdateService` (Zeilen 21–34)

Implementiert von `UpdateService` (Produktion) und `ProtokollierenderUpdateService` (E2E-Dekorator).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckForUpdateAsync` | `UpdateCheckOptions options`, `CancellationToken ct` | `Task<UpdateCheckResult>` | Prüft, ob ein neueres Update verfügbar ist |
| `PrepareUpdateAsync` | `UpdateInfo update`, `IProgress<UpdatePreparationProgress>? progress`, `CancellationToken ct` | `Task<UpdatePreparationResult>` | Bereitet Download, Entpacken und Skript vor |
| `StartPreparedUpdateAsync` | `UpdatePreparationResult preparation`, `CancellationToken ct` | `Task` | Startet das vorbereitete externe Update |

## `IUpdateScriptService` (Zeilen 54–66)

Implementiert von `UpdateScriptService` (nicht betroffen).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateScriptAsync` | `string targetDirectory`, `string extractedDirectory`, `string executableName`, `string logPath`, `CancellationToken ct` | `Task<string>` | Schreibt das Update-Skript für die angegebenen Pfade |
| `StartScriptAsync` | `UpdatePreparationResult preparation`, `CancellationToken ct` | `Task` | Startet ein vorbereitetes Update-Skript |

## `ICliUpdateSafetyService` (Zeilen 37–41)

Implementiert von `CliUpdateSafetyService` bzw. `FixtureCliUpdateSafetyService` (E2E).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckAsync` | `CancellationToken ct` | `Task<CliUpdateSafetyResult>` | Liefert riskante aktive CLI-Aufgaben vor dem Update |

## `IApplicationVersionProvider` (Zeilen 4–8)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetInstalledVersionAsync` | `CancellationToken ct` | `Task<InstalledVersionInfo?>` | Liest die installierte Version bzw. `null` |

## `IUpdateProcessLauncher` (Zeilen 69–73)

Implementiert von `UpdateProcessLauncher` bzw. `RecordingUpdateProcessLauncher` (E2E).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Start` | `string fileName`, `IEnumerable<string> arguments`, `string workingDirectory`, `bool runElevated` | `bool` | Startet den externen Prozess |

## `IApplicationShutdownService` (Zeilen 76–80)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Shutdown` | — | `void` | Beendet die laufende Anwendung geordnet |

## App-seitige Verträge

| Interface | Datei | Relevanz |
|-----------|-------|----------|
| `IUpdateProgressDialogService` | `src/Softwareschmiede.App/Services/IUpdateProgressDialogService.cs` | `Show(UpdateProgressViewModel)` — öffnet den Fortschrittsdialog (`WpfUpdateProgressDialogService`) |
| `IUpdateVersuchProtokoll` | `src/Softwareschmiede.App/Services/IUpdateVersuchProtokoll.cs` | Versuchs-Beginn/Ende-Protokoll der E2E-Umgebung (`UpdateE2ETestKontext`) |
