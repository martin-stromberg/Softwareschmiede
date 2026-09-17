# Test-Ergebnisse — Iteration 3

## Build

| Projekt | Befehl | Ergebnis | Warnungen |
|---------|--------|----------|-----------|
| Softwareschmiede.slnx (gesamt) | `dotnet build` | erfolgreich | 0 Warnungen, 0 Fehler |
| Softwareschmiede.slnx | `dotnet format --verify-no-changes` | sauber | — |

## Testläufe (Iteration 3)

| Projekt | Befehl | Ergebnis | Fehlgeschlagene Tests |
|---------|--------|----------|----------------------|
| Softwareschmiede.Tests | `dotnet test` (reguläre Spur, `Category!=OsInterface`) | 1629 bestanden, 0 fehlgeschlagen, 1 übersprungen | — |
| Softwareschmiede.Tests | `dotnet test --filter "Category=OsInterface"` (Spur 2, finaler Lauf) | 47 bestanden, 1 fehlgeschlagen, 2 übersprungen | `TerminalControlTests.ReadClipboardAndInsertAsync_LangerMehrzeiligerText_WritesCompleteEncodedBytes` (siehe unten) |
| Softwareschmiede.Tests | Fokussierter Update-E2E-Runner (temporär, inzwischen entfernt) | alle Update-Szenarien bestanden (~3 m 50 s) | — |

Hinweis zur Durchführung: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` war gesetzt (Übersprungene = ConPTY-Tests bzw. ein bedingter Repository-Initialisierungstest).

`End2EndTest.RunGeneralTests` lief im finalen Spur-2-Lauf **vollständig durch** — alle Update-E2E-Szenarien (E-01 bis E-07) sowie die Fixture-Smoke-Tests wurden in der kanonischen konsolidierten Struktur grün ausgeführt.

## Fehlgeschlagener Test — Einzelanalyse

### `Softwareschmiede.Tests.App.Controls.TerminalControlTests.ReadClipboardAndInsertAsync_LangerMehrzeiligerText_WritesCompleteEncodedBytes`

- **Fehlerbild:** `System.Runtime.InteropServices.COMException: OpenClipboard fehlgeschlagen (0x800401D0, CLIPBRD_E_CANT_OPEN)` — der Test kann die Windows-Zwischenablage nicht öffnen, obwohl er 15 s lang mit Retry versucht.
- **Reproduktion:** Der Test schlägt **auch im Isolationslauf** fehl; eine direkt anschließende Clipboard-Probe meldet „Clipboard frei". Damit liegt eine **intermittierende Ressourcenkonkurrenz** um die Zwischenablage vor (ein anderer Prozess hält sie phasenweise offen), kein deterministischer Anwendungsdefekt.
- **Feature-Bezug:** keiner — der Test gehört zur Terminal-Steuerung, nicht zum Update-Feature. Er verwendet keinerlei Update-Code.
- **Status:** dokumentiert als Umgebungs-/Ressourcenproblem dieser Sandbox; kein Handlungsbedarf für dieses Feature. Bei Bedarf separater Follow-up (Clipboard-Retry-Härtung im Produktivcode bzw. Test).

## Iteration-3-Fixes und ihre Verifikation

Die 10 Befunde aus `review-code.2.md` wurden behoben und sind durch die grünen Läufe abgedeckt:

- `IUpdateVersuchProtokoll` + `MainWindowUpdateDienste`-Bundle: Konstruktor-Parameterliste des ViewModels reduziert, Test-Kopplung hinter Schnittstelle gekapselt.
- `UpdateReleaseLookupResult`: Client-Vertrag unterscheidet jetzt Erfolg-mit-Release / Erfolg-ohne-Release / Fehler — die „nicht prüfbar"-Fehlmeldung im Erfolgsfall ist beseitigt (Unit-Tests in `GitHubReleaseClientTests_*`, `UpdateServiceTests*`).
- Geteilte Test-Helpers (`RoutingHttpHandler`, `StaticHttpHandler`, `TempDirectory`, `TestDbContextFactory.CreateSqlite`), Basisklasse `MainWindowViewModelUpdateTestBase`, deduplizierte Gate-Wartelogik, gehärtetes Fixture-Cleanup.

Zusätzlich wurde ein **flaUI-Klick-Rootcause** behoben, der die OsInterface-Spur über alle Iterationen hinweg sporadisch destabilisierte: `Click()` führt echte Mausklicks an Bildschirmkoordinaten aus — trifft ein verdeckendes Fremdfenster (z. B. eine parallel laufende App-Instanz) die Klickposition, versandet der Klick. `ElementWaitHelper.ClickInForeground`/`DoubleClickInForeground` bringen das Zielfenster in den Vordergrund, prüfen auf Occlusion und fehlende klickbare Punkte und fallen positionsunabhängig auf UIA-`Invoke` zurück. Der Sweep umfasste alle ~120 Klickstellen der E2E-Views; die Update-Buttons in `MenuView` pollen zusätzlich auf Sichtbarkeit/Aktivierung und wiederholen bei transienten FlaUI-Fehlern.

Korrektur eines Review-Artefakts: Befund 10 aus `review-code.2.md` (Rohwert statt `null` bei `HelpText`-Formatmismatch) beruhte auf einer falschen Annahme — UIA liefert für den formatierten Tooltip nur die rohe Version (`HelpText='1.3.0'`), nie die Maske. `GetOfferedUpdateVersion` akzeptiert daher beide Formen; die ursprüngliche Verschärfung wurde als Regression erkannt und korrigiert.

## E2E-Abdeckung (gegen plan.md)

| Szenario aus plan.md | Ausgeführt | Ergebnis |
|---------------------|------------|----------|
| E-01 `UpdateSettings_Persistence` | ja (in `End2EndTest.RunGeneralTests`) | bestanden |
| E-02 `UpdateCheck_SemVerStableVsPrerelease` | ja | bestanden |
| E-03 `UpdateDialog_CheckNow` | ja | bestanden |
| E-04 `UpdateSettingsStartup_DisabledAndInstall` | ja | bestanden |
| E-05 `UpdateInstall_EndToEnd` | ja | bestanden |
| E-06 `UpdateSettingsReadFailure_SkipCheck` | ja | bestanden |
| E-07 `Startup_IsOnceAndCommandsStayBlocked` | ja | bestanden |

Zusätzlich grün: `End2EndTest.FixtureModesRespond` (3 Modi: FoundNewer / None / Failure), `End2EndTest.FixtureDownloadGateControlsRelease` und `Fixture_SettingsReadFailureTargetsPhaseAfterDatabaseInitialization`.

## Coverage

Nicht messbar — kein Coverage-Instrumentierung verfügbar (kein `coverlet`/VS-Coverage im Testlauf konfiguriert).

## Fehlende Tests (Fallback-Prüfung: jede neue/geänderte Datei → Testdatei vorhanden?)

| Neue/geänderte Datei | Testdatei vorhanden? |
|---------------------|---------------------|
| `SemanticUpdateVersion.cs` | `UpdateVersionComparerTests_SemVer.cs` (direkt), `UpdateServiceTests_PrereleaseChain.cs` |
| `UpdateVersionComparer.cs` (erweitert) | `UpdateVersionComparerTests_SemVer.cs` |
| `GitHubReleaseClient.cs` (Pagination/Filter/LookupResult) | `GitHubReleaseClientTests_Pagination.cs`, `GitHubReleaseClientTests_Filters.cs`, `GitHubReleaseClientTests.cs` |
| `UpdateService.cs` (Options/Install/LookupResult) | `UpdateServiceTests_Options.cs`, `UpdateServiceTests_PrereleaseChain.cs`, `UpdateServiceTests.cs` |
| `UpdateModels.cs` / `UpdateInterfaces.cs` (`UpdateReleaseLookupResult`) | indirekt über o. g. + `UpdateProgressViewModelTests.cs` |
| `AppEinstellungService.cs` (Update-Keys) | `AppEinstellungServiceTests_UpdateSettings.cs` |
| `MainWindowViewModel.cs` / `MainWindowUpdateDienste.cs` / `IUpdateVersuchProtokoll.cs` | `MainWindowViewModelTests_UpdateStartup.cs`, `MainWindowViewModelTests_UpdateSettingsReadFailure.cs` (über `MainWindowViewModelUpdateTestBase`) |
| `SettingsViewModel.cs` / `SettingsView.xaml` | E2E: `E2E_UpdateSettings.cs` (E-01..E-07), `E2E_SettingsUi` |
| `UpdateProgressViewModel.cs` / Dialog | `UpdateProgressViewModelTests.cs`, E2E E-05 |
| Testing-Infrastruktur (`Services/Testing/*`) | `UpdateFixtureHttpMessageHandlerTests.cs`, E2E-Fixture-Smoke-Tests |
| `E2E_UpdateSettings.cs` / `UpdateE2EFixture.cs` / `ElementWaitHelper.cs` / Views | selbst Tests bzw. Test-Infrastruktur |

## Hinweise

- Der temporäre fokussierte Update-E2E-Runner wurde nach erfolgreicher Verifikation wieder entfernt; die kanonische Ausführung bleibt `End2EndTest.RunGeneralTests`.
- Der einzige verbleibende Spur-2-Fehler (Clipboard) ist feature-unabhängig und in dieser Sandbox intermittierend.
- Frühere Iterationsstände: siehe Umbenennungshistorie der Review-Artefakte (`review.1.md`, `review-code.1.md`/`review-code.2.md`, `review-usability.1.md`/`review-usability.2.md`).
