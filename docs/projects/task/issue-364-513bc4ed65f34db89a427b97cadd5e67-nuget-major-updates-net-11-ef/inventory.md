# Bestandsaufnahme – NuGet-Major-Updates (#364)

Stand: Branch `task/issue-364-...-nuget-major-updates-net-11-ef`, Basis = `staging` @ d4e6d17d.

## Projektstruktur und TFMs

| Projekt | TFM | Rolle |
|---------|-----|-------|
| `src/Softwareschmiede/Softwareschmiede.csproj` | `net10.0` | Domain/Application/Library (317 .cs) |
| `src/Softwareschmiede.Plugin.Contracts/` | `net10.0` | Plugin-Verträge |
| `src/Softwareschmiede.App/` | `net10.0-windows10.0.17763.0` | WPF-App (`UseWPF`, `WinExe`) |
| `src/Softwareschmiede.Tests/` | `net10.0-windows10.0.17763.0` | Unit-/E2E-Tests (320 .cs, FlaUI/WPF) |
| `src/Softwareschmiede.IntegrationTests/` | `net10.0` | Integrationstests (42 .cs) |
| `plugins/*` (8 Projekte) | `net10.0` | Plugins: BitBucket, ClaudeCli, Codex, Devin, GitHub, GitHubCopilot, KiSimulator, LocalDirectory |

Kein `global.json`, kein `Directory.Build.props`, kein `NuGet.config`, kein
`Directory.Packages.props` — Versionen stehen direkt in den csproj.

## Aktuelle Paketversionen (alle relevanten PackageReferences)

**Microsoft/EFCore-Block (→ 11.0.x):**

- `Softwareschmiede`: `Microsoft.EntityFrameworkCore.Sqlite` 10.0.15,
  `Microsoft.EntityFrameworkCore.Design` 10.0.15 (PrivateAssets all),
  `Microsoft.Extensions.Caching.Memory|DependencyInjection|Logging|Options` 10.0.15,
  `Microsoft.Extensions.Hosting.Abstractions` 10.0.15,
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.14
- `Softwareschmiede.App`: `Microsoft.Extensions.DependencyInjection|Hosting|Logging|
  Logging.Abstractions|Logging.Console` 10.0.15, `Microsoft.EntityFrameworkCore.Sqlite`
  10.0.15, `SQLitePCLRaw.lib.e_sqlite3` 2.1.14
- `Softwareschmiede.Tests`: `Microsoft.EntityFrameworkCore.InMemory` 10.0.15,
  `Microsoft.Extensions.Logging.Abstractions` 10.0.15,
  `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 (abweichendes Schema — prüfen ob 11.x)
- `Softwareschmiede.IntegrationTests`: `Microsoft.EntityFrameworkCore.Sqlite` 10.0.15,
  `Microsoft.Extensions.Logging.Abstractions` 10.0.15
- Alle 8 Plugins: `Microsoft.Extensions.Logging.Abstractions` 10.0.15

**Test-Stack:**

- Beide Testprojekte: `xunit` 2.9.3, `xunit.runner.visualstudio` 4.0.1 (PrivateAssets),
  `Microsoft.NET.Test.Sdk` 18.12.0, `coverlet.collector` 10.1.1, `FluentAssertions` 8.11.0,
  `Moq` 4.21.0
- `Softwareschmiede.Tests` zusätzlich: `FlaUI.Core|UIA3` 5.0.0, `Xunit.SkippableFact` 1.5.85
- `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 nur in Tests

**Sonstige (nicht Teil des Auftrags):** Serilog.Extensions.Hosting 10.0.0,
Serilog.Sinks.File 7.0.0, Serilog.Sinks.Console 6.1.1

## Hartcodierte `net10.0`-Pfade

- `src/Softwareschmiede.App/Softwareschmiede.App.csproj` → `CopyPluginsToOutput`:
  8× `plugins\*\bin\$(Configuration)\net10.0\*.dll` (Zeilen ~127-173)
- `TargetPlatformMinVersion` 10.0.17763.0 bezieht sich auf Windows-Version, nicht .NET —
  bleibt.

## CI/CD-Stellen mit `dotnet-version: '10.0.x'` (9 Vorkommen, alle → 11.0.x)

- `.github/workflows/pr-staging-ci.yml` (3×: Zeilen 77, 113, 209)
- `.github/workflows/staging-ci.yml` (3×: Zeilen 74, 107, 200)
- `.github/workflows/release.yml` (1×: Zeile 41)
- `.github/workflows/security-scan.yml` (1×: Zeile 35)
- `.github/actions/build-and-package/action.yml` (1×: Zeile 26)
- `release.yml` kommentiert außerdem `net10.0-windows` in einem Kommentar (Zeilen 21, 33 ff.)
- `staging-ci.yml`/`release.yml` führen `dotnet test` mit `--filter "Category!=OsInterface"`
  bzw. `Category=OsInterface` aus — Category-Filter muss unter xunit.v3 weiterhin funktionieren
  (Trait `Category` bleibt Konvention).

## EF-Core-Nutzung

- `src/Softwareschmiede/Migrations/`: 87 Dateien (~43 Migrationen inkl. Designer + Snapshot),
  älteste `20260506195327_InitialCreate`. Bestandsinstallationen migrieren sich von dort —
  Upgrade-Sicherheit Pflicht (siehe Skill `entityframework-database`).
- `DbContext`/`UseSqlite`/`Migrate()` in `src/Softwareschmiede/**` — SQLite als Store.
- `Microsoft.EntityFrameworkCore.Design` ist für `dotnet ef`-Scaffolding referenziert.
- Tests nutzen `Microsoft.EntityFrameworkCore.InMemory`.

## xunit-v2-Erweiterungspunkte (Breaking-Change-Oberfläche für v3)

Nur 5 Dateien mit `Xunit.Sdk`/`Xunit.Abstractions` — kleine, abgegrenzte Oberfläche:

- `src/Softwareschmiede.Tests/Infrastructure/Testing/OsInterfaceAttribute.cs`
  (`Attribute + ITraitAttribute`, `TraitDiscoverer`-Verweis)
- `.../OsInterfaceFactAttribute.cs` (`FactAttribute + ITraitAttribute`)
- `.../OsInterfaceTheoryAttribute.cs` (`Theory`-Analogon, prüfen)
- `.../OsInterfaceTraitDiscoverer.cs` (`ITraitDiscoverer`, `IAttributeInfo` aus
  `Xunit.Abstractions`) — liefert `Category=OsInterface`-Trait für die CI-Filterung
- `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (`using Xunit.Abstractions` →
  `ITestOutputHelper`; in v3 im `Xunit`-Namespace)

Weitere v3-relevante Punkte:

- `src/Softwareschmiede.Tests/AssemblyInfo.cs`: `[assembly: CollectionBehavior(
  DisableTestParallelization = true)]` — in v3 weiterhin vorhanden, Namespace/Verhalten prüfen.
- `Xunit.SkippableFact`: 13 Dateien, ~30 Attribute (v. a. E2E, `WpfTestBase`,
  `SystemShutdownServiceTests`, `ArbeitsverzeichnisOeffnenServiceTests`). v3 hat natives
  dynamisches Überspringen (`Assert.Skip`/`SkipException`); Kompatibilität von
  `Xunit.SkippableFact` 1.5.85 mit v3 ist zu prüfen, sonst ersetzen.
- `[Trait("Category", "E2E")]` u. ä. massenhaft — bleibt kompatibel.
- v3: Testprojekt wird ausführbare EXE (`<OutputType>Exe</OutputType>` von Metapaket bzw.
  implizit); `dotnet test` über `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`
  bleibt der Ausführungspfad.
- `ConPtyEnvironmentProbe`/Skip-Mechanik: `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`-Auswertung
  liegt im Testcode (`SkippableFact`?), muss unter v3 weiter funktionieren.

## Testlandschaft / Konventionen (CLAUDE.md)

- Voller `dotnet build` vor Tests; reguläre Lane `--filter "Category!=OsInterface"`,
  OS-Interface-Lane separat. In dieser Sandbox `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
  setzen (14 ConPTY-E2E-Tests würden sonst timeouten).
- FlaUI-E2E-Tests sind auf wenige Methoden konsolidiert (Laufzeitkritisch).
- `dotnet format Softwareschmiede.slnx --verify-no-changes` spiegelt CI-Format-Check
  (pre-push-Hook `.githooks/pre-push`, nicht aktiv in diesem Klon).
- `.githooks/pre-push`, `scripts/Run-AllTestsIndividually.ps1` — kein `net10`-Bezug.

## Laufzeitumgebung dieser Ausführung

- Installiertes SDK: `10.0.403` (C:\Program Files\dotnet). .NET 11 SDK wird benutzerlokal
  nach `C:\Users\Martin\.dotnet11` installiert (Download läuft); Aufruf über expliziten
  Pfad bzw. `DOTNET_ROOT`+PATH je Kommando.
- `gh`-Auth: `softwarewerkstatt` via `GH_TOKEN` (Standard); `martin-stromberg` via Keyring
  (`env -u GH_TOKEN` → Owner-Kontext für den Admin-Merge von PR #419).
- Release-Lauf 44268624839 (Promotion #418) zum Auftragszeitpunkt `in_progress`;
  PR #419 `BLOCKED` (pending `release`-Check) → danach Admin-Merge (Merge-Commit, Konvention
  aus #415 u. a.).
- Selbst-Hosting-Risiko: laufende `Softwareschmiede.App.exe` darf nie beendet werden;
  bei Build-Locks (MSB3027) Teil-Build ohne App-Projekt oder Anwender informieren.

## Wiederverwendbare Bausteine

- `issue.md` im Repo-Root enthält ggf. die Aufgabenbeschreibung der Task-Umgebung.
- `.github/workflows/verify-pr-source.yml` prüft PR-Quellbranch-Konventionen —
  PR gegen `staging` einhalten.
- SCM-Antwortkanal: accepted.json bereits geschrieben; Ergebnis via `complete.json`
  (+ `pullRequestNumber`) melden.

## Einschränkungen / Risiken

- xunit.v3: größter Umstellungsblock (Metapaket, EXE-Ausgabe, Sdk-API-Umzug
  `Xunit.Sdk`→`Xunit.v3`-APIs, `ITraitDiscoverer`→v3-Modell, `ITestOutputHelper`-Namespace).
- EF Core 11: SQLite-Provider verhält sich i. d. R. kompatibel; neue obsolet gewordene APIs/
  Warnungen möglich; Migrationen bleiben kompatibel, aber `dotnet ef` für EF11 braucht
  passendes Tooling, falls neue Migrationen nötig wären (nicht geplant).
- `Microsoft.NET.Test.Sdk` 18.12.0 muss mit xunit.v3 4.x zusammenspielen (unterstützt v3
  seit 17.12/18.x — verifizieren).
- App-Build in dieser Sandbox kann auf DLL-Locks durch die laufende Host-Instanz treffen
  (MSB3027) → dann Teil-Build ohne App-Projekt und transparent dokumentieren.
