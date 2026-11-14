# Tasks: PowerShell-Auflösung im UpdateScriptService auf feste Installationspfade beschränken

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `GetDefaultPowerShellCandidates` in `UpdateScriptService` auf zwei feste Pfade reduzieren (WinPS via `SpecialFolder.System`, PS7 via `SpecialFolder.ProgramFiles`) und auf `internal static` öffnen | Offen | — |
| 2 | Logik | `FindPowerShellExecutable` in `UpdateScriptService` auf `Path.IsPathFullyQualified` + `File.Exists` umstellen und `"powershell.exe"`-Fallback durch `InvalidOperationException` (Meldung mit geprüften Pfaden) ersetzen | Offen | — |
| 3 | Logik | `IsExecutableAvailable` in `UpdateScriptService` entfernen (PATH-/PATHEXT-Suchpfad entfällt) | Offen | — |
| 4 | Dokumentation | XML-Doc-Kommentar in `PathExecutableResolver` korrigieren (`UpdateScriptService`-Verweise entfernen) | Offen | — |
| 5 | Tests | `GetDefaultPowerShellCandidates_ShouldReturnOnlyFixedFullyQualifiedPaths` in `UpdateScriptServiceTests` schreiben | Offen | — |
| 6 | Tests | `StartScriptAsync_ShouldThrow_WhenNoFixedPowerShellCandidateExists` in `UpdateScriptServiceTests` schreiben | Offen | — |
| 7 | Tests | `StartScriptAsync_ShouldIgnoreUnqualifiedCandidates` in `UpdateScriptServiceTests` schreiben | Offen | — |
| 8 | Tests | `StartScriptAsync_ShouldPassArgumentsToLauncher` um `Path.IsPathFullyQualified(launcher.FileName)`-Assertion erweitern | Offen | — |
| 9 | E2E-Tests | `Fixture_UsesIsolatedRealUpdatePipeline` in `E2E_UpdateFixture.cs`: Assertion auf vollqualifiziertes/festes `fileName` im `UpdateProcessStartRecorded`-Ereignis ergänzen | Offen | — |
| 10 | Verifikation | `dotnet format Softwareschmiede.slnx --verify-no-changes`, `dotnet build Softwareschmiede.slnx`, Regulär-Lane `dotnet test ... --filter "Category!=OsInterface"` und Update-E2E-Subset `--filter "FullyQualifiedName~End2EndTest.General_Update"` (Sandbox: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, synchron) | Offen | — |
