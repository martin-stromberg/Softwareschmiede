# Datenmodelle

## `UpdatePreparationResult`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeilen 145–156)

`sealed record` — Ergebnis der Update-Vorbereitung; Eingabe von `IUpdateScriptService.StartScriptAsync` und `IUpdateService.StartPreparedUpdateAsync`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ZipPath` | `string` | Pfad zur heruntergeladenen ZIP-Datei. |
| `ExtractedDirectory` | `string` | Pfad zum entpackten Release-Verzeichnis; wird als `-ExtractedDirectory`-Skriptargument weitergereicht. |
| `ScriptPath` | `string` | Pfad zum erzeugten `update.ps1`; wird als `-File`-Argument weitergereicht. |
| `LogPath` | `string` | Pfad zur Update-Logdatei; wird als `-LogPath`-Argument weitergereicht. |
| `RequiresElevation` | `bool` | Steuert `runElevated` am `IUpdateProcessLauncher` (`Verb = "runas"`); gesetzt aus `!UpdatePackageService.CanWriteToBaseDirectory()`. |

## `UpdateOptions`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateOptions.cs`

Optionsklasse (`init`-Only-Properties) für Repository, Asset und lokale Arbeitsverzeichnisse. Kein Eintrag für Interpreter-Pfade vorhanden (laut Anforderung auch nicht vorgesehen).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `RepositoryOwner` | `string` | GitHub-Repository-Owner (Default `"martin-stromberg"`). |
| `RepositoryName` | `string` | GitHub-Repository-Name (Default `"Softwareschmiede"`). |
| `AssetName` | `string` | Erwartetes Release-Asset (Default `"release.zip"`). |
| `UpdateDirectoryName` | `string` | Update-Arbeitsverzeichnis relativ zum Programmverzeichnis (Default `"updates"`); `update.ps1` wird dort abgelegt. |
| `CheckTimeout` | `TimeSpan` | Timeout für HTTP-Prüfungen/Downloads (Default 15 s). |
| `ExecutableName` | `string` | Name der ausführbaren Datei im Release-Paket (Default `"Softwareschmiede.exe"`); wird zu `-ExecutablePath` aufgelöst. |
