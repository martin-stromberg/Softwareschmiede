# Interfaces

Datei aller folgenden Interfaces: `src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs`

## `IUpdateReleaseClient`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetLatestReleaseAsync` | `UpdateCheckOptions options`, `CancellationToken ct = default` | `Task<UpdateReleaseLookupResult>` | Ruft die neueste zulässige Release-Information ab; unterscheidet Fehlschlag von „kein Kandidat". Implementiert von `GitHubReleaseClient`. |

## `IUpdatePackageService`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `PreparePackageAsync` | `UpdateInfo update`, `IProgress<UpdatePreparationProgress>? progress`, `CancellationToken ct = default` | `Task<UpdatePreparationResult>` | Lädt das Update herunter, entpackt es und erzeugt die Update-Artefakte. Implementiert von `UpdatePackageService`. Die Signatur reicht die komplette `UpdateInfo` durch — eine neue Eigenschaft am Record ändert den Contract nicht. |

## `IUpdateService`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckForUpdateAsync` | `UpdateCheckOptions options`, `CancellationToken ct = default` | `Task<UpdateCheckResult>` | Prüft auf neueres Update |
| `PrepareUpdateAsync` | `UpdateInfo update`, `IProgress<UpdatePreparationProgress>? progress`, `CancellationToken ct = default` | `Task<UpdatePreparationResult>` | Bereitet Download, Entpacken und Skript vor (delegiert an `IUpdatePackageService`) |
| `StartPreparedUpdateAsync` | `UpdatePreparationResult preparation`, `CancellationToken ct = default` | `Task` | Startet das vorbereitete externe Update |

## `IApplicationVersionProvider`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetInstalledVersionAsync` | `CancellationToken ct = default` | `Task<InstalledVersionInfo?>` | Liest die installierte Version |

## `ICliUpdateSafetyService`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CheckAsync` | `CancellationToken ct = default` | `Task<CliUpdateSafetyResult>` | Bewertet aktive CLI-Aufgaben vor einem Update |

## `IUpdateScriptService`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateScriptAsync` | `string targetDirectory`, `string extractedDirectory`, `string executableName`, `string logPath`, `CancellationToken ct = default` | `Task<string>` | Schreibt das Update-Skript |
| `StartScriptAsync` | `UpdatePreparationResult preparation`, `CancellationToken ct = default` | `Task` | Startet ein vorbereitetes Update-Skript |

## `IUpdateProcessLauncher`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Start` | `string fileName`, `IEnumerable<string> arguments`, `string workingDirectory`, `bool runElevated` | `bool` | Startet den externen Updater-Prozess |

## `IApplicationShutdownService`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Shutdown` | — | `void` | Beendet die laufende Anwendung geordnet |

## `IUpdateVersuchProtokoll` (App-Projekt)
Datei: `src/Softwareschmiede.App/Services/IUpdateVersuchProtokoll.cs`

E2E-Instrumentierung der Updateversuche; implementiert von `UpdateE2ETestKontext` (`ProtokolliereWindowReady`, `BeginneVersuch`, `BeendeVersuch`). Für den Fallback nicht erweiterungsbedürftig.
