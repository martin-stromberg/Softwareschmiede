# CI-/Skript-/Konventions-Infrastruktur

## CI-Workflow `.github/workflows/pr-staging-ci.yml`

Zwei getrennte Test-Jobs existieren bereits (Lane-Aufteilung eingeführt; identischer Aufbau in `staging-ci.yml`):

### Job `build & test` (Zeilen 100–178) — blockierend

| Schritt | Befehl | Bemerkung |
|---------|--------|-----------|
| `Build (Debug)` | `dotnet build` beider Test-csproj | vor den Testläufen, kein `--no-build`-Lauf ohne Build |
| `Test regular (Softwareschmiede.Tests)` | `dotnet test … --filter "Category!=OsInterface" --collect:"XPlat Code Coverage"` | Gate 4, blockierend, coverage-relevant |
| `Test regular (IntegrationTests)` | `dotnet test … --filter "Category!=OsInterface" --collect:"XPlat Code Coverage"` | blockierend |
| Coverage-Report + Threshold | `reportgenerator` + `COVERAGE_THRESHOLD: '70'` | Gate 5: ≥70 % Zeilenabdeckung, nur aus der regulären Lane |
| `Upload test results` | Artefakt `test-results-pr` | TRX aus beiden regulären Läufen |

`runs-on: windows-latest`, `timeout-minutes: 20`, `needs: detect-backmerge`.

### Job `os-interface tests` (Zeilen 180–237) — best-effort

- `continue-on-error: true`, `runs-on: windows-latest`, `timeout-minutes: 30`.
- Env (Zeilen 200–201): `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS: '1'`, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS: '1'` — GitHub-Runner ohne interaktive Desktop-Session; lokal bleiben beide ungesetzt.
- Schritte: Build beider Test-csproj, dann `dotnet test --filter "Category=OsInterface"` je Projekt mit TRX `test-results-os-interface-{tests,integration}.trx`, Artefakt-Upload `test-results-os-interface-pr` (Zeilen 229–237).
- **Befund:** Der Job führt heute im Integrationsprojekt nur `StartPs1IntegrationTests` (9 Tests) aus — die neu zu taggenden RestrictedProcess-Tests landen hier automatisch, sobald sie `Category=OsInterface` tragen. Kein Workflow-Umbau nötig für Variante „nur taggen".

## Lokale Lanes

| Befehl | Zweck |
|--------|-------|
| `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` | stabile Lane (Projektregel: immer mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` in dieser Sandbox; voller Build vorher) |
| `dotnet test … --filter "Category=OsInterface"` | OS-Lane pro Projekt |
| `./scripts/Run-AllTestsIndividually.ps1` | Einzeltest-Lauf aller regulären Tests (Default `-TestSet Regular`) mit Rebuild+Retry nur bei Ausführungsfehlern |
| `./scripts/Run-AllTestsIndividually.ps1 -TestSet OsInterface` | Einzeltest-Lauf der OS-Lane |
| `dotnet format Softwareschmiede.slnx --verify-no-changes` | Format-Gate (lokaler `.githooks/pre-push` spiegelt CI-Schritt „Format check") |

## Bestehende Konventions-Dokumentation

Datei `docs/help/stabilitaet/os-interface-tests.md` — hier wäre die neue Quarantäne-/Retry-Konvention zu dokumentieren:

- **Kategorie** (Z. 7–23): `[Trait("Category","OsInterface")]` generisch; `[OsInterface]`/`[OsInterfaceFact]`/`[OsInterfaceTheory]` in `Softwareschmiede.Tests`; Spezialkategorien `E2E`/`ConPTY` bleiben zusätzlich bestehen; Konvention gilt auch für `Softwareschmiede.IntegrationTests` (Z. 41).
- **CI-Verhalten** (Z. 43–53): beschreibt veraltet `test.yml` mit Schritten `Test regular tests`/`Test OS interfaces tests` — tatsächlich heißen die Jobs heute `build & test`/`os-interface tests` in `staging-ci.yml`/`pr-staging-ci.yml` (Doc-Drift, kein Funktionsunterschied).
- **Bekannte Fehlerbilder** (Z. 55–61): ConPTY-Verfügbarkeit, `CLIPBRD_E_CANT_OPEN`, Dateisystem-Cleanup-Locks, E2E-Build-Artefakt-Rennen.
- **Retry-Regel** (Z. 63–69): `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`/`…_WPF_E2E_TESTS` dokumentiert; „Retry ist nicht zur Stabilisierung regulaerer Tests vorgesehen. Das Einzeltest-Skript wiederholt nur erkannte Infrastruktur-/Ausfuehrungsfehler nach Rebuild-Recovery." OS-Schnittstellen-Fehler dürfen separat dokumentiert/bewertet werden.
- **Externe Runner** (Z. 71–79): Auswertungskonvention für `/run-tests` und `/lifecycle`.

## Env-Flags und Skip-Mechanik

| Flag | Auswertung | Wirkung |
|------|-----------|---------|
| `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` | `ConPtyEnvironmentProbe.IsAvailable` → `WpfTestBase.SkipWennConPtyNichtVerfuegbar` | ConPTY-E2E melden „Skipped" (xUnit `Assert.SkipWhen`) |
| `SOFTWARESCHMIEDE_SKIP_WPF_E2E_TESTS=1` | `WpfE2EEnvironmentProbe.IsAvailable` → zentral in `WpfTestBase.LaunchApp` | alle FlaUI-Fenster-E2E melden „Skipped" |
| `Category=OsInterface` / `!=OsInterface` | xUnit-Trait-Filter | Lane-Trennung in CI + Skript |

Kein `Flaky`-Trait, kein generischer Retry-Mechanismus, kein `gh run rerun`-Workflow-Hook vorhanden (Volltextsuche ohne Treffer).

## Weitere relevante Dateien

- `.githooks/pre-push` — spiegelt `dotnet format … --verify-no-changes`; `core.hooksPath` muss lokal aktiviert werden (darf nicht agentenseitig gesetzt werden).
- `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj` — Zeile 35: `<Compile Include="..\Softwareschmiede.Tests\Helpers\ScmDispatchFixture.cs" Link="Infrastructure\ScmDispatchFixture.cs" />` — bestehendes Link-Muster für geteilte Testdateien ohne Projektreferenz; keine Referenz auf `Softwareschmiede.Tests` (Zeilen 38–44 nur Plugin-/Hauptprojekt-Referenzen).
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj` — `UseWPF`, Referenz auf `Softwareschmiede.App.csproj` (E2E-Start), xUnit v3 im VSTest-Modus (`IsTestingPlatformApplication=false` in beiden Testprojekten).
