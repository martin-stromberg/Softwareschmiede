# Logik

## `UpdateScriptService`
Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/UpdateScriptService.cs`

Zentrale Klasse der Anforderung. Erzeugt `updates/update.ps1` unterhalb des Basisverzeichnisses und startet es über `IUpdateProcessLauncher`. Implementiert `IUpdateScriptService`.

Felder:

| Feld | Typ | Zweck |
|------|-----|-------|
| `_processLauncher` | `IUpdateProcessLauncher` | Testbar gekapselter Prozessstart. |
| `_options` | `UpdateOptions` | Liefert `UpdateDirectoryName` und `ExecutableName`. |
| `_logger` | `ILogger<UpdateScriptService>` | Logging (wird aktuell nirgends im Methodenrumpf genutzt). |
| `_baseDirectory` | `string` | Programm-/Testwurzel (`Path.GetFullPath` normalisiert). |
| `_powerShellCandidates` | `IReadOnlyList<string>` | Kandidatenliste für die Interpreter-Auflösung; injizierbar über den Testkonstruktor, sonst aus `GetDefaultPowerShellCandidates()`. |

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `UpdateScriptService(IUpdateProcessLauncher, IOptions<UpdateOptions>, ILogger<UpdateScriptService>)` | public (Zeilen 21–27) | Produktionskonstruktor; delegiert mit `AppContext.BaseDirectory`. |
| `UpdateScriptService(IUpdateProcessLauncher, IOptions<UpdateOptions>, ILogger<UpdateScriptService>, string baseDirectory, IReadOnlyList<string>? powerShellCandidates = null)` | public (Zeilen 30–42) | Testkonstruktor mit explizitem Basispfad und optional injizierbarer Kandidatenliste. |
| `CreateScriptAsync(string targetDirectory, string extractedDirectory, string executableName, string logPath, CancellationToken)` | public (Zeilen 45–58) | Legt `_baseDirectory/updates/` an und schreibt `update.ps1` (UTF-8 ohne BOM) aus `BuildScript()`; liefert den Skriptpfad. Die übergebenen Parameter fließen nicht in den Skriptinhalt, sondern später in die Startargumente. |
| `StartScriptAsync(UpdatePreparationResult preparation, CancellationToken)` | public (Zeilen 61–90) | Ruft `FindPowerShellExecutable(_powerShellCandidates)` auf, baut die Argumentliste (`-NoProfile -ExecutionPolicy Bypass -File <script> -AppPid <pid> -TargetDirectory <base> -ExtractedDirectory <dir> -ExecutablePath <exe> -LogPath <log>`) und startet über `_processLauncher.Start(executable, arguments, _baseDirectory, preparation.RequiresElevation)`. Bei Rückgabe `false`: `InvalidOperationException("Update-Skript konnte nicht gestartet werden.")`. |
| `GetDefaultPowerShellCandidates()` | private static (Zeilen 92–101) | Liefert `[<vollqualifizierter Pfad aus Environment.GetFolderPath(SpecialFolder.System) + "WindowsPowerShell\v1.0\powershell.exe">, "powershell.exe", "pwsh.exe", "pwsh"]` — drei der vier Einträge sind PATH-aufgelöste Namen. |
| `FindPowerShellExecutable(IReadOnlyList<string> candidates)` | private static (Zeilen 103–112) | Erster Kandidat mit `IsExecutableAvailable(candidate) == true` gewinnt; ohne Treffer Letztfallback `return "powershell.exe"` (unqualifizierter Basisname an `ProcessStartInfo.FileName`). |
| `IsExecutableAvailable(string executable)` | private static (Zeilen 114–136) | Vollqualifizierte Pfade: nur `File.Exists`. Unqualifizierte Namen: PATH-Suche via `PathExecutableResolver.EnumeratePathDirectories(Environment.GetEnvironmentVariable)` kombiniert mit PATHEXT-Erweiterungen (unter Windows `PATHEXT` oder `.EXE;.CMD;.BAT`, sonst keine Erweiterung). |
| `BuildScript()` | private static (Zeilen 138–183) | PowerShell-Template: wartet max. 15 s auf App-Exit (dann `Stop-Process -Force`), kopiert `ExtractedDirectory` → `TargetDirectory`, startet `ExecutablePath` neu, schreibt Update-Log. Wird von der Anforderung nicht geändert. |

Abonnierte Events: keine. Publizierte Events: keine.

Verwendet: `IUpdateProcessLauncher`, `UpdateOptions`, `UpdatePreparationResult`, `PathExecutableResolver`.
Aufgerufen von: `UpdatePackageService.PreparePackageAsync` (`CreateScriptAsync`), `UpdateService.StartPreparedUpdateAsync` (`StartScriptAsync`).

## `UpdateProcessLauncher`
Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/UpdateScriptService.cs` (Zeilen 186–215, dieselbe Datei)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Start(string fileName, IEnumerable<string> arguments, string workingDirectory, bool runElevated)` | public | Setzt `ProcessStartInfo` mit `FileName = fileName` (unverändert übernommen — bei relativem Namen PATH-Auflösung durch das OS), `UseShellExecute = runElevated`, `Verb = "runas"` bei `runElevated`, Argumente über `ArgumentList`. Fängt `Win32Exception`, `InvalidOperationException`, `FileNotFoundException` und wirft `InvalidOperationException("Update-Prozess konnte nicht gestartet werden: {fileName}")`. |

## `PathExecutableResolver`
Datei: `src/Softwareschmiede/Infrastructure/Services/PathExecutableResolver.cs`

`internal static`-Hilfsklasse für PATH-basierte Executable-Auflösung.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnumeratePathDirectories(Func<string, string?> getEnvironmentVariable)` | public (Zeilen 19–23) | Zerlegt `PATH` in getrimmte, nicht-leere Verzeichniseinträge. |
| `TryResolveByFileName(string fileName, Func<string, string?>, Func<string, bool>)` | public (Zeilen 33–48) | Sucht exakten Dateinamen in allen PATH-Verzeichnissen, liefert ersten Treffer oder `null`. |

Bestehende Nutzer (bleiben laut Anforderung unverändert):
- `UpdateScriptService.IsExecutableAvailable` — nutzt nur `EnumeratePathDirectories` (UpdateScriptService.cs:119).
- `RepositoryScriptExecutor.ResolveBashExecutable` — `TryResolveByFileName("bash.exe", ...)` (RepositoryScriptExecutor.cs:158–166).
- `VisualStudioCodeLocator.EnumeratePathCandidates` — `EnumeratePathDirectories` für `Code.exe`/`code.cmd`/`code` (VisualStudioCodeLocator.cs:45–56); zusätzlich eigene Installationspfad-Kandidaten via `LOCALAPPDATA`/`ProgramFiles`/`ProgramFiles(x86)`-Umgebungsvariablen (Zeilen 58–79).

Hinweis: Der XML-Doc-Kommentar (Zeilen 3–13) nennt `Updates.UpdateScriptService` aktuell als Nutzer der PATH-Zerlegung.

## `UpdateService`
Datei: `src/Softwareschmiede/Application/Services/Updates/UpdateService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartPreparedUpdateAsync(UpdatePreparationResult, CancellationToken)` | public (Zeilen 99–103) | `await _scriptService.StartScriptAsync(preparation, ct);` danach `_shutdownService.Shutdown();` — der Shutdown erfolgt ausschließlich nach erfolgreichem Skriptstart; eine Exception aus `StartScriptAsync` (z. B. `InvalidOperationException`) verhindert ihn. |

## `UpdatePackageService`
Datei: `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs`

Relevant für die Elevation-Entscheidung, selbst nicht änderungsbedürftig:

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PreparePackageAsync(...)` | public (Zeilen 44–90) | Download → Entpacken → `ValidateExtractedPackage` → `_scriptService.CreateScriptAsync` → `requiresElevation = !CanWriteToBaseDirectory()` (Zeile 79) → `UpdatePreparationResult`. |
| `CanWriteToBaseDirectory()` | private (Zeilen 159–173) | Schreibprobe im Programmverzeichnis; `false` bei `IOException`/`UnauthorizedAccessException` → `RequiresElevation = true` → später `Verb = "runas"`. |

## `MainWindowUpdateFlow`
Datei: `src/Softwareschmiede.App/ViewModels/MainWindowUpdateFlow.cs`

Fehlerpropagations-Ziel: `StartPreparedUpdateAsync` wird in Zeile 327 aufgerufen; `catch (Exception)` (Zeilen 334–339) loggt und ruft `progressViewModel.SetError("Update konnte nicht vorbereitet werden. Details wurden protokolliert.")` sowie `UpdateHinweis = "Update konnte nicht vorbereitet werden."`. Eine `InvalidOperationException` aus der Interpreter-Auflösung würde über diese Kette in den Fortschrittsdialog laufen.

## `RecordingUpdateProcessLauncher`
Datei: `src/Softwareschmiede.App/Services/Testing/RecordingUpdateProcessLauncher.cs`

E2E-Adapter (`Update-E2E-Testmodus`): protokolliert `fileName`, `arguments`, `workingDirectory`, `runElevated` als `UpdateE2EEreignisse.UpdateProcessStartRecorded` ins JSONL-Protokoll und liefert das szenariogesteuerte Ergebnis (`"Erfolg"`/`"Fehler"`/`"Ausnahme"`), ohne einen echten Prozess zu starten. Der protokollierte `fileName` ist der von `UpdateScriptService` aufgelöste Interpreter-Pfad.

## DI-Registrierung
Datei: `src/Softwareschmiede.App/App.xaml.cs`

- Produktion (Zeilen 339–340): `IUpdateScriptService → UpdateScriptService`, `IUpdateProcessLauncher → UpdateProcessLauncher` (Singletons).
- Update-E2E-Testmodus (Zeilen 418–429): `IUpdateScriptService` wird als `UpdateScriptService` mit `testwurzel` als `baseDirectory` registriert (ohne `powerShellCandidates` → echte `GetDefaultPowerShellCandidates()`); `IUpdateProcessLauncher → RecordingUpdateProcessLauncher`.
