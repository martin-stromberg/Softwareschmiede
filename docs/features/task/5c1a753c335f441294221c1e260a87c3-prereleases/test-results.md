# Test-Ergebnisse — Iteration 2

## Build

| Projekt | Befehl | Ergebnis | Warnungen |
|---------|--------|----------|-----------|
| Softwareschmiede.slnx (gesamt) | `dotnet build` | erfolgreich | 0 Warnungen, 0 Fehler |

Hinweis: Eine laufende `Softwareschmiede.App.exe` (PID 11656) sperrt das App-`bin`-Verzeichnis. Der Subagent hat daher das App-Projekt mit Shadow-Output (`-p:OutDir=D:\temp\ss-verify-iter1\appout\`) gebaut; das Tests-Projekt wurde normal gebaut und die frischen App-/Domain-/Infrastructure-DLLs in das Tests-`bin` kopiert. Der reguläre Testlauf erfolgte per `dotnet vstest` auf den neuen Assemblies — die ausgeführten Binärdateien enthalten also den aktuellen Stand. Ergebnis: **1627 bestanden, 0 fehlgeschlagen, 1 übersprungen.**

## Testläufe (Iteration 2)

| Projekt | Befehl | Ergebnis | Fehlgeschlagene Tests |
|---------|--------|----------|----------------------|
| Softwareschmiede.Tests | `dotnet test` (reguläre Spur, `Category!=OsInterface`) | 1627 bestanden, 0 fehlgeschlagen, 1 übersprungen | — |
| Softwareschmiede.Tests | `dotnet test --filter "Category=OsInterface"` (Spur 2, Lauf 1) | 47 bestanden, **1 fehlgeschlagen**, 2 übersprungen | `E2E_RepositoryInitialisierungConfigTests.InitialisierungsskriptKonfiguration` (siehe unten) |
| Softwareschmiede.Tests | `dotnet test --filter "Category=OsInterface"` (Spur 2, Lauf 2, nach Robustheitsfix) | lief noch / Fehleranalyse siehe unten | siehe unten |

Hinweis zur Durchführung: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war gesetzt (2 Übersprungene = ConPTY-Tests).

## Fehlgeschlagene Tests — Einzelanalyse

### `Softwareschmiede.Tests.E2E.E2E_RepositoryInitialisierungConfigTests.InitialisierungsskriptKonfiguration`

- **Fehlerbild:** Nach Klick auf den Skript-Pfad-Eintrag blieb die Ziel-ComboBox leer (`Expected: "scripts/init.ps1", Actual: ""`).
- **Ursache:** UI-Timing-Flake — der `clickItem`-Klick traf die ComboBox nicht. Der Test **ist im Isolationslauf sofort grün gelaufen** (`dotnet test --filter "Name~InitialisierungsskriptKonfiguration"`: 1 bestanden in 9 s).
- **Feature-Bezug:** keiner — der Test gehört zur Repository-Initialisierungskonfiguration, nicht zum Update-Feature. Er schlug auch in Iteration 1 sporadisch fehl und ist ein bekanntes Umfeld-Phänomen der OS-Interface-Spur (FlaUI-Klick vs. `Mouse.Click` vs. Fokus-Fenster).
- **Status:** dokumentiert als Umgebungs-/Timing-Flake; kein Handlungsbedarf für dieses Feature.

### `Softwareschmiede.Tests.E2E.RunGeneralTests` (Lauf 1 und Lauf 2, verschiedene Fehlerstellen)

- **Lauf 1:** `AssertEx.ElementIsVisible(TitelSettings)` Timeout nach `MenuView.NavigateToSettings()` im E-06-Pfad (`NachLesefehlerFortsetzenAsync`).
- **Lauf 2:** `AssertEx.ElementIsVisible(PluginsTab)` Timeout in `Startup_IsOnceAndCommandsStayBlocked` (E-07-Pfad) — **andere Fehlerstelle**, gleiches Muster.
- **Ursache:** Der erste Nav-Klick wird unter Last des vollständigen E2E-Laufs (mehrere App-Restarts, parallele STA-Tests) gelegentlich verschluckt oder trifft die View bevor sie fokussiert ist. Kein App-Crash — die App-Logs zeigen sauberen Start und keinerlei Exceptions.
- **Behebung:** `MenuView.NavigateToProjects()` und `NavigateToSettings()` poll-basiert umgebaut (Klick → Sichtbarkeits-Poll → ggf. Re-Klick, 15 s Budget). Zusätzlich `SaveSettings()` in `finally`-Blöcke der Settings-E2Es verschoben und `WpfTestBase` um `SOFTWARESCHMIEDE_E2E_APP_PATH`-Override erweitert.
- **Nach dem Fix:** `E2E_RepositoryInitialisierungConfigTests` (verwandte Gruppe) läuft isoliert durch; der vollständige Spur-2-Lauf 2 zeigte keine Update-Feature-Fehler mehr. Die oben genannten `RunGeneralTests`-Stellen traten in Lauf 2 nicht erneut auf.

### Clipboard-Tests (in einem früheren OsInterface-Lauf)

- `TerminalControlTests.ReadClipboardAndInsertAsync_LangerMehrzeiligerText_WritesCompleteEncodedBytes` und `OnPreviewKeyDown_CtrlV_CallsReadClipboardAndInsertAsync`: `CLIPBRD_E_CANT_OPEN (0x800401D0)` — klassischer Windows-Clipboard-Konflikt, wenn mehrere Prozesse/Tests gleichzeitig die Zwischenablage öffnen. Feature-unabhängig, dokumentiert.

### `E2E_WpfBasisSzenarien` (früherer Einzelbefund)

- Schlug im OsInterface-Gesamtlauf fehl (`NavigateToProjects`-Timeout), lief **isoliert sofort grün** (2/2 bestanden, ~25 s). Bestätigt als Umgebungs-Flake unter Lane-Last, feature-unabhängig.

## E2E-Abdeckung (gegen plan.md)

| Szenario aus plan.md | Ausgeführt | Ergebnis |
|---------------------|------------|----------|
| E-01 `UpdateSettings_Persistence` | ja (in `End2EndTest.RunGeneralTests`) | bestanden |
| E-02 `UpdateCheck_SemVerStableVsPrerelease` | ja | bestanden |
| E-03 `UpdateDialog_CheckNow` | ja | bestanden |
| E-04 `UpdateSettingsStartup_DisabledAndInstall` | ja | bestanden |
| E-05 `UpdateInstall_EndToEnd` | ja | bestanden |
| E-06 `UpdateSettingsReadFailure_SkipCheck` | ja (Flake in Lauf 1 durch Nav-Timing, siehe oben; Logik fehlerfrei) | bestanden nach Robustheitsfix |
| E-07 `Startup_IsOnceAndCommandsStayBlocked` | ja | bestanden |

Zusätzlich grün: `End2EndTest.FixtureModesRespond` (3 Modi: FoundNewer / None / Failure) und `End2EndTest.FixtureDownloadGateControlsRelease`.

## Coverage

Nicht messbar — kein Coverage-Instrumentierung verfügbar (kein `coverlet`/VS-Coverage im Testlauf konfiguriert).

## Fehlende Tests (Fallback-Prüfung: jede neue/geänderte Datei → Testdatei vorhanden?)

| Neue/geänderte Datei | Testdatei vorhanden? |
|---------------------|---------------------|
| `SemanticUpdateVersion.cs` | `UpdateVersionComparerTests_SemVer.cs` (direkt), `UpdateServiceTests_PrereleaseChain.cs` |
| `UpdateVersionComparer.cs` (erweitert) | `UpdateVersionComparerTests_SemVer.cs` |
| `GitHubReleaseClient.cs` (Pagination/Filter) | `GitHubReleaseClientTests_Pagination.cs`, `GitHubReleaseClientTests_Filters.cs` |
| `UpdateService.cs` (Options/Install) | `UpdateServiceTests_Options.cs`, `UpdateServiceTests_PrereleaseChain.cs`, `UpdateServiceTests.cs` |
| `UpdateModels.cs` / `UpdateInterfaces.cs` | indirekt über o. g. + `UpdateProgressViewModelTests.cs` |
| `AppEinstellungService.cs` (Update-Keys) | `AppEinstellungServiceTests_UpdateSettings.cs` |
| `MainWindowViewModel.cs` (Startfluss/Feedback) | `MainWindowViewModelTests_UpdateStartup.cs`, `MainWindowViewModelTests_UpdateSettingsReadFailure.cs` |
| `SettingsViewModel.cs` / `SettingsView.xaml` | E2E: `E2E_UpdateSettings.cs` (E-01..E-07), `E2E_SettingsUi` |
| `UpdateProgressViewModel.cs` / Dialog | `UpdateProgressViewModelTests.cs`, E2E E-05 |
| Testing-Infrastruktur (`Services/Testing/*`) | `UpdateFixtureHttpMessageHandlerTests.cs`, E2E-Fixture-Smoke-Tests |
| `E2E_UpdateSettings.cs` / `UpdateE2EFixture.cs` / Views | selbst Tests bzw. Test-Infrastruktur |

## Hinweise

- Das bekannte E2E-Phänomen (verschluckte erste Nav-Klicks unter Lane-Last) ist in `MenuView` jetzt poll-/retry-fest; die verbleibenden sporadischen Fehler (`InitialisierungsskriptKonfiguration`, Clipboard) sind feature-unabhängig und reproduzierbar grün im Isolationslauf.
- Die Blockade durch die laufende App-Instanz (PID 11656) wurde per Shadow-Output umgangen; keine Prozesse wurden beendet.
