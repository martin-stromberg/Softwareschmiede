# Bestandsaufnahme: PowerShell-Auflösung im UpdateScriptService auf feste Installationspfade beschränken

Analysiert wurde die Interpreter-Auflösung für den Start von `updates/update.ps1` in `UpdateScriptService` (Issue #381, PATH-Hijacking-Befund aus dem Sicherheitsreview 2026-11) sowie deren Aufrufer, Contracts und Testabdeckung.

## Zusammenfassung

- `UpdateScriptService.GetDefaultPowerShellCandidates` (UpdateScriptService.cs:92–101) liefert heute vier Kandidaten: einen vollqualifizierten Windows-PowerShell-Pfad (`SpecialFolder.System` + `WindowsPowerShell\v1.0\powershell.exe`) sowie die PATH-aufgelösten Namen `"powershell.exe"`, `"pwsh.exe"`, `"pwsh"` — die Zielmenge der Anforderung.
- `IsExecutableAvailable` (Zeilen 114–136) enthält den PATH-/PATHEXT-Suchpfad über `PathExecutableResolver.EnumeratePathDirectories`; vollqualifizierte Kandidaten werden bereits nur per `File.Exists` geprüft.
- `FindPowerShellExecutable` (Zeilen 103–112) fällt ohne Treffer auf den unqualifizierten Basisnamen `"powershell.exe"` zurück — dieser geht unverändert als `ProcessStartInfo.FileName` an `UpdateProcessLauncher.Start`, bei `RequiresElevation` mit `Verb = "runas"` (Elevation aus `UpdatePackageService.CanWriteToBaseDirectory`).
- Die Fehlerkette für einen kontrollierten Abbruch existiert bereits: `InvalidOperationException` aus `StartScriptAsync` → `UpdateService.StartPreparedUpdateAsync` (kein `Shutdown()`) → `MainWindowUpdateFlow` → `progressViewModel.SetError`. Ein Unit-Test (`StartPreparedUpdateAsync_ShouldNotShutdown_WhenScriptStartFails`) sichert das Shutdown-Verhalten bereits ab.
- `PathExecutableResolver` bleibt für `RepositoryScriptExecutor` (bash.exe) und `VisualStudioCodeLocator` bestehen; nur sein XML-Doc nennt `UpdateScriptService` noch (fälschlich) als Nutzer.
- Der Testkonstruktor mit injizierbarer `powerShellCandidates`-Liste existiert bereits und wird im Update-E2E-Modus (App.xaml.cs:418–422) mit echten Standardkandidaten und `testwurzel`-Basis verwendet; `RecordingUpdateProcessLauncher` macht den aufgelösten `fileName` im E2E-Protokoll (`UpdateProcessStartRecorded`) sichtbar.
- Kein Test prüft heute den Inhalt der Standard-Kandidatenliste, das PATH-/PATHEXT-Verhalten oder den `"powershell.exe"`-Letztfallback; auch `FileName`-Vollqualifizierung wird nicht assertiert.

Test-Ausgangszustand: Regulär-Lane `Softwareschmiede.Tests` 2352 erfolgreich / 0 fehlgeschlagen / 3 plattformbedingt übersprungen; Regulär-Lane `Softwareschmiede.IntegrationTests` 280/0/0; OsInterface-Update-Subset (2 konsolidierte E2E-Fakten) 2/0/0. Übrige OsInterface-Lanes nicht ausgeführt (unbekannter Zustand) — Nachweis und Details in [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `UpdatePreparationResult`, `UpdateOptions`
- [Logik](inventory/logic.md) — `UpdateScriptService`, `UpdateProcessLauncher`, `PathExecutableResolver`, `UpdateService`, `UpdatePackageService`, `MainWindowUpdateFlow`, `RecordingUpdateProcessLauncher`, DI-Registrierung
- [Interfaces](inventory/interfaces.md) — `IUpdateScriptService`, `IUpdateProcessLauncher`, `IApplicationShutdownService`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Nachweise, bestehende Testklassen und Hilfsmethoden
