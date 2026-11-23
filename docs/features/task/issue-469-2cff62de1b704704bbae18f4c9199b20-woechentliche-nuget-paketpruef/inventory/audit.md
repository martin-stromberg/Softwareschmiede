# Audit-Ergebnis: Updates, Schwachstellen, Veraltung

Erhebungszeitpunkt: 2026-11-23 (UTC+1), Commit `cef41f6e619e94f5de7dffb9c70a68b2d3c146fe`,
.NET SDK 11.0.100. Quellen: `https://api.nuget.org/v3/index.json` +
`C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\`.

Alle drei Audit-Befehle wurden gegen `Softwareschmiede.slnx` ausgeführt und erfolgreich
beendet (Exit-Code 0). Rohausgaben liegen unter
[inventory/test-results/](test-results/):

- [dotnet-list-outdated.log](test-results/dotnet-list-outdated.log)
- [dotnet-list-vulnerable.log](test-results/dotnet-list-vulnerable.log)
- [dotnet-list-deprecated.log](test-results/dotnet-list-deprecated.log)
- [dotnet-list-all-transitive.log](test-results/dotnet-list-all-transitive.log)

## Verfügbare Updates — direkte Pakete

`dotnet list Softwareschmiede.slnx package --outdated --include-transitive` meldet nur
**zwei** veraltete Top-Level-Referenzen:

| Paket | Projekt | Installiert | Neueste (stabil) | Sprung |
|-------|---------|-------------|------------------|--------|
| `Serilog.Extensions.Hosting` | `Softwareschmiede.App` | 10.0.0 | 11.0.0 | Major |
| `Microsoft.Extensions.TimeProvider.Testing` | `Softwareschmiede.Tests` | 11.0.0 | 11.0.1 | Patch |

Alle übrigen 28 direkten `PackageReference`s sind auf dem neuesten stabilen Stand
(u. a. `FlaUI.*` 5.0.0, `Serilog.Sinks.File` 7.0.0, `Serilog.Sinks.Console` 6.1.1,
`SQLitePCLRaw.lib.e_sqlite3` 3.53.3, komplette `Microsoft.Extensions.*`- und
EF-Core-Familie 11.0.0, Test-Stack).

### Abhängigkeiten der Update-Kandidaten (nuget.org-Metadaten)

- `Serilog.Extensions.Hosting` 11.0.0 deklariert: `Serilog` ≥ 4.4.0,
  `Serilog.Extensions.Logging` ≥ 11.0.0, `Microsoft.Extensions.Hosting.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`
  je ≥ 11.0.0 — deckt sich mit den im Outdated-Scan gemeldeten Transitiva
  (`Serilog` 4.3.0→4.4.0, `Serilog.Extensions.Logging` 10.0.0→11.0.0) und mit dem
  bereits vorhandenen `Microsoft.Extensions.*`-11.0.0-Niveau.
- `Microsoft.Extensions.TimeProvider.Testing` 11.0.1 deklariert nur
  `Microsoft.Bcl.TimeProvider` ≥ 8.0.1 (reine Patch-Hebung).

### NuGet.org-Versionsstände (stabil vs. Prerelease)

| Paket | Installiert | Neueste stabil | Neueste Prerelease | Befund |
|-------|-------------|----------------|--------------------|--------|
| `Serilog.Extensions.Hosting` | 10.0.0 | 11.0.0 | — | stabiler Major-Update |
| `Microsoft.Extensions.TimeProvider.Testing` | 11.0.0 | 11.0.1 | — | stabiler Patch-Update |
| `SQLitePCLRaw.lib.e_sqlite3` | 3.53.3 | 3.53.3 | 3.53.4.1-pre1 | kein stabiler Update; Prerelease vorhanden |
| `Serilog.Sinks.File` | 7.0.0 | 7.0.0 | 8.0.0-dev-* (mehrere) | kein stabiler Update |
| `Serilog.Sinks.Console` | 6.1.1 | 6.1.1 | — | aktuell |

## Verfügbare Updates — transitive Pakete

Transitiv veraltete Pakete (folgen automatisch den Eltern, keine direkte Referenz):

| Transitivum | Aufgelöst | Neueste | Eingezogen über | Projekte |
|-------------|-----------|---------|-----------------|----------|
| `Serilog` | 4.3.0 | 4.4.0 | `Serilog.Extensions.Hosting` | App (+ Tests via `ProjectReference`) |
| `Serilog.Extensions.Logging` | 10.0.0 | 11.0.0 | `Serilog.Extensions.Hosting` | App (+ Tests) |
| `SQLite` (nativ) | 3.53.4 | 3.54.1 | `SQLitePCLRaw.lib.e_sqlite3` | App, Softwareschmiede, Tests, IntegrationTests |
| `Microsoft.Testing.Platform`, `Microsoft.Testing.Platform.MSBuild`, `Microsoft.Testing.Extensions.Telemetry`, `Microsoft.Testing.Extensions.TrxReport.Abstractions` | 2.5.2 | 2.5.3 | `Microsoft.NET.Test.Sdk` / `xunit.v3` | Tests, IntegrationTests |
| `Microsoft.ApplicationInsights` | 2.23.0 | 3.2.0 | `Microsoft.Testing.Extensions.Telemetry` | Tests, IntegrationTests |
| `Microsoft.Bcl.AsyncInterfaces` | 6.0.0 | 11.0.0 | (Test-Transitiva) | Tests, IntegrationTests |
| `System.Management` | 8.0.0 | 11.0.0 | (Test-Transitiva) | Tests |
| `System.Diagnostics.EventLog` | 6.0.0 | 11.0.0 | (Test-Transitiva) | IntegrationTests |
| `Microsoft.Build.Framework`, `Microsoft.NET.StringTools` | 18.9.6 | 18.12.2 | `Microsoft.EntityFrameworkCore.Design` | Softwareschmiede |
| `Microsoft.CodeAnalysis.Analyzers` | 5.9.0-1.26328.17 | 5.12.0 | `Microsoft.EntityFrameworkCore.Design` | Softwareschmiede |
| `Microsoft.CodeAnalysis.Common`/`.CSharp`/`.CSharp.Workspaces`/`.Workspaces.Common`/`.Workspaces.MSBuild` | 5.9.0 | 5.12.0 | `Microsoft.EntityFrameworkCore.Design` | Softwareschmiede |
| `System.CodeDom` | 6.0.0 | 11.0.0 | `Microsoft.EntityFrameworkCore.Design` | Softwareschmiede |
| `System.Composition` + `.AttributedModel`/`.Convention`/`.Hosting`/`.Runtime`/`.TypedParts` | 10.0.1 | 11.0.0 | `Microsoft.EntityFrameworkCore.Design` | Softwareschmiede |

Hinweis: `SQLite` 3.54.1 lässt sich nur über ein `SQLitePCLRaw.lib.e_sqlite3`-Update
heben — aktuell existiert kein stabiler Update (nur Prerelease 3.53.4.1-pre1).
Die `Microsoft.CodeAnalysis.*`/`System.Composition.*`/`Microsoft.Build.*`-Transitiva sind
reine Dev-Time-Abhängigkeiten von `Microsoft.EntityFrameworkCore.Design`
(`PrivateAssets=all`) und werden nur über ein EF-Core-Design-Update angehoben.

## Schwachstellen

`dotnet list Softwareschmiede.slnx package --vulnerable --include-transitive`:
**keine verwundbaren Pakete** in allen 13 Projekten (identischer Befehl wie das
CI-Gate in `.github/actions/security-scan/action.yml`, dort würde „has the following
vulnerable packages"/„Severity" zum Fehlschlag führen — beides nicht im Output).

## Veraltung / Abkündigung

`dotnet list Softwareschmiede.slnx package --deprecated`: **keine als veraltet
markierten Pakete** in allen 13 Projekten. Auf nuget.org deklarierte
Nachfolgepaket-Hinweise: keine für die verwendeten Pakete gefunden.

## Pflegestatus-Auffälligkeiten (Meta, ohne sofortigen Handlungsbedarf)

- `Microsoft.ApplicationInsights` 2.23.0 (nur transitiv, Test-Telemetrie) → Neueste 3.2.0,
  ein Major-Sprung; Anhebung nur über das Elternpaket möglich.
- `SQLitePCLRaw.lib.e_sqlite3`: Native SQLite 3.54.1 existiert, das Wrapper-Paket hat
  nur die Prerelease 3.53.4.1-pre1 — Paketpflege hinkt leicht hinterher.
- `Xunit.SkippableFact` wird in `THIRD_PARTY_LICENSES.md` noch geführt, ist aber in
  keiner `*.csproj` mehr referenziert (siehe
  [infrastruktur.md](infrastruktur.md#third_party_licensesmd)).
