# Paketbestand: Direkte `PackageReference`s der Softwareschmiede.slnx

Erhebungszeitpunkt: 2026-11-23, Commit `cef41f6e619e94f5de7dffb9c70a68b2d3c146fe`.
Quelle: `*.csproj`-Dateien sowie `dotnet list Softwareschmiede.slnx package --include-transitive`
([Nachweis](test-results/dotnet-list-all-transitive.log)).

Kein Central Package Management (`Directory.Packages.props` nicht vorhanden), keine
`NuGet.config` im Repo — Quellen sind `https://api.nuget.org/v3/index.json` und der lokale
SDK-Fallback-Ordner `C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\`. Alle Versionen
stehen dezentral in den `*.csproj`-Dateien.

## Projekte mit direkten `PackageReference`s

### `Softwareschmiede` (Domain/Service-Bibliothek)

Datei: `src/Softwareschmiede/Softwareschmiede.csproj` (TFM `net11.0`, Zeilen 17–25)

| Paket | Version | Besonderheiten |
|-------|---------|----------------|
| `Microsoft.EntityFrameworkCore.Sqlite` | 11.0.0 | |
| `SQLitePCLRaw.lib.e_sqlite3` | 3.53.3 | |
| `Microsoft.EntityFrameworkCore.Design` | 11.0.0 | `PrivateAssets=all`, `IncludeAssets=runtime; build; native; contentfiles; analyzers` |
| `Microsoft.Extensions.Caching.Memory` | 11.0.0 | |
| `Microsoft.Extensions.DependencyInjection` | 11.0.0 | |
| `Microsoft.Extensions.Logging` | 11.0.0 | |

### `Softwareschmiede.App` (WPF-Anwendung)

Datei: `src/Softwareschmiede.App/Softwareschmiede.App.csproj`
(TFM `net11.0-windows10.0.17763.0`, Zeilen 53–61)

| Paket | Version | Besonderheiten |
|-------|---------|----------------|
| `Microsoft.Extensions.DependencyInjection` | 11.0.0 | |
| `Microsoft.Extensions.Hosting` | 11.0.0 | |
| `Microsoft.Extensions.Logging` | 11.0.0 | |
| `Microsoft.Extensions.Logging.Console` | 11.0.0 | |
| `Serilog.Extensions.Hosting` | 10.0.0 | zieht transitiv `Serilog` 4.3.0 + `Serilog.Extensions.Logging` 10.0.0 |
| `Serilog.Sinks.File` | 7.0.0 | |
| `Serilog.Sinks.Console` | 6.1.1 | |
| `Microsoft.EntityFrameworkCore.Sqlite` | 11.0.0 | |
| `SQLitePCLRaw.lib.e_sqlite3` | 3.53.3 | |

### `Softwareschmiede.Tests`

Datei: `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
(TFM `net11.0-windows10.0.17763.0`, Zeilen 19–40)

| Paket | Version | Besonderheiten |
|-------|---------|----------------|
| `FlaUI.Core` | 5.0.0 | `PrivateAssets=all`, eingeschränkte `IncludeAssets` (GPL-Lizenzhygiene) |
| `FlaUI.UIA3` | 5.0.0 | `PrivateAssets=all`, eingeschränkte `IncludeAssets` |
| `coverlet.collector` | 10.1.1 | `PrivateAssets=all`, eingeschränkte `IncludeAssets` |
| `FluentAssertions` | 8.11.0 | |
| `Microsoft.EntityFrameworkCore.InMemory` | 11.0.0 | |
| `Microsoft.Extensions.TimeProvider.Testing` | 11.0.0 | |
| `Microsoft.NET.Test.Sdk` | 18.12.0 | |
| `Moq` | 4.21.0 | |
| `xunit.v3` | 4.0.3 | |
| `xunit.runner.visualstudio` | 4.0.1 | `PrivateAssets=all`, eingeschränkte `IncludeAssets` |

### `Softwareschmiede.IntegrationTests`

Datei: `src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj`
(TFM `net11.0`, Zeilen 18–30)

| Paket | Version | Besonderheiten |
|-------|---------|----------------|
| `coverlet.collector` | 10.1.1 | `PrivateAssets=all`, eingeschränkte `IncludeAssets` |
| `FluentAssertions` | 8.11.0 | |
| `Microsoft.EntityFrameworkCore.Sqlite` | 11.0.0 | |
| `Microsoft.NET.Test.Sdk` | 18.12.0 | |
| `Moq` | 4.21.0 | |
| `xunit.v3` | 4.0.3 | |
| `xunit.runner.visualstudio` | 4.0.1 | `PrivateAssets=all`, eingeschränkte `IncludeAssets` |

## Projekte ohne direkte `PackageReference`s

- `src/Softwareschmiede.Plugin.Contracts/Softwareschmiede.Plugin.Contracts.csproj`
  (TFM `net11.0`) — keine `PackageReference`, keine `ProjectReference`.
- Alle 8 Plugin-Projekte unter `plugins/` (BitBucket, ClaudeCli, Codex, Devin, GitHub,
  GitHubCopilot, KiSimulator, LocalDirectory; je TFM `net11.0`) — jeweils nur eine
  `ProjectReference` auf `Softwareschmiede.Plugin.Contracts`.

**Korrektur zur Anforderung:** Die Plugin-Projekte ziehen — anders als in
`requirement.md` angenommen — **keine** transitiven NuGet-Pakete ein. `dotnet list
package --include-transitive` meldet für alle 9 Projekte „keine Pakete gefunden", und
`plugins/*/obj/project.assets.json` enthält ausschließlich den Projekteintrag
`Softwareschmiede.Plugin.Contracts/1.0.0`. Die von den Plugins genutzten Typen
(`Microsoft.Extensions.Logging.ILogger<T>` u. a.) kommen aus dem .NET-11-Shared-Framework,
nicht aus einem NuGet-Paket.

## Versionsgekoppelte Gruppen (müssen konsistent angehoben werden)

- **EF-Core-Familie (5 Vorkommen, alle 11.0.0):** `Microsoft.EntityFrameworkCore.Sqlite`
  (Softwareschmiede, App, IntegrationTests), `Microsoft.EntityFrameworkCore.Design`
  (Softwareschmiede), `Microsoft.EntityFrameworkCore.InMemory` (Tests).
- **`SQLitePCLRaw.lib.e_sqlite3` (2 Vorkommen, je 3.53.3):** Softwareschmiede + App.
- **`Microsoft.Extensions.*` (7 direkte Referenzen, alle 11.0.0):** `Caching.Memory`
  (Softwareschmiede), `DependencyInjection` (Softwareschmiede + App), `Hosting` (App),
  `Logging` (Softwareschmiede + App), `Logging.Console` (App); plus
  `Microsoft.Extensions.TimeProvider.Testing` 11.0.0 (Tests).
- **Test-Stack (je 2× in Tests + IntegrationTests identisch):** `coverlet.collector`
  10.1.1, `FluentAssertions` 8.11.0, `Microsoft.NET.Test.Sdk` 18.12.0, `Moq` 4.21.0,
  `xunit.v3` 4.0.3, `xunit.runner.visualstudio` 4.0.1.
- **FlaUI (2 Vorkommen, je 5.0.0):** `FlaUI.Core` + `FlaUI.UIA3` (nur Tests).
- **Serilog (3 direkte Referenzen in App):** `Serilog.Extensions.Hosting` 10.0.0,
  `Serilog.Sinks.File` 7.0.0, `Serilog.Sinks.Console` 6.1.1 — transitiv zieht
  `Serilog.Extensions.Hosting` die Pakete `Serilog` (4.3.0) und
  `Serilog.Extensions.Logging` (10.0.0); dieselben Transitiva erscheinen über die
  `ProjectReference` auf `Softwareschmiede.App` auch im aufgelösten Paketgraph von
  `Softwareschmiede.Tests`.

## Relevante Transitive (Stand aufgelöst)

Aus dem vollständigen Paketgraph ([Log](test-results/dotnet-list-all-transitive.log)) —
Auswahl der für Updates/Verifikation relevanten:

- `Serilog` 4.3.0, `Serilog.Extensions.Logging` 10.0.0 (via `Serilog.Extensions.Hosting`)
- `SQLite` 3.53.4 (native Bibliothek, via `SQLitePCLRaw.lib.e_sqlite3` 3.53.3);
  `SQLitePCLRaw.bundle_e_sqlite3`/`config`/`core`/`provider` je 3.0.5
- `Microsoft.Testing.Platform*`/`Microsoft.Testing.Extensions.*` je 2.5.2 (via
  `Microsoft.NET.Test.Sdk`/`xunit.v3`), `Microsoft.ApplicationInsights` 2.23.0
- `Microsoft.CodeAnalysis.*` 5.9.0, `Microsoft.Build.Framework`/`Microsoft.NET.StringTools`
  18.9.6, `System.Composition.*` 10.0.1, `System.CodeDom` 6.0.0, `Humanizer.Core` 3.0.10,
  `Mono.TextTemplating` 3.0.0, `Microsoft.VisualStudio.SolutionPersistence` 1.0.52 —
  ausschließlich Dev-Time-Transitiva von `Microsoft.EntityFrameworkCore.Design`
- `Castle.Core` 5.2.1 (via `Moq`), `Interop.UIAutomationClient` 10.19041.0 (via FlaUI),
  `xunit.analyzers` 2.3.0, `xunit.v3.*` 4.0.3
