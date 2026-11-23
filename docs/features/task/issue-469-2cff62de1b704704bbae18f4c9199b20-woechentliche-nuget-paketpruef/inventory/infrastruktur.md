# Mitbetroffene Infrastruktur- und Dokumentationsartefakte

## CI/Workflow-Artefakte

### `.github/workflows/review-nuget-packages.yml`

Wöchentlicher Auslöser dieses Issues: `cron: '0 6 * * 1'` (montags 06:00 UTC) plus
`workflow_dispatch`. Delegiert an den wiederverwendbaren Workflow
`.github/workflows/create-review-issue.yml` mit Label `automated-review-nuget`, Titel
„Wöchentliche NuGet-Paketprüfung", `@softwarewerkstatt`-Mention. Laut Anforderung nicht
Teil des Änderungsscopes.

### `.github/actions/security-scan/action.yml`

Composite Action, die `dotnet list <solution> package --vulnerable --include-transitive`
ausführt, in `vulnerable-packages-scan.log` schreibt und mit Exit 1 fehlschlägt, wenn der
Output „has the following vulnerable packages" oder „Severity" enthält. Wird genutzt als
Gate 2 der `static-checks`-Jobs in `pr-staging-ci.yml` (Zeile 90) und `staging-ci.yml`
(Zeile 83) sowie vom wöchentlichen `security-scan.yml` (Zeile 42). → Ein Versions-PR darf
keine verwundbaren Pakete einführen; der lokale Scan
([Nachweis](test-results/dotnet-list-vulnerable.log)) ist aktuell sauber.

### Weitere relevante CI-Schritte (beide `pr-staging-ci.yml` und `staging-ci.yml`)

- `dotnet format Softwareschmiede.slnx --verify-no-changes --no-restore` (Format-Gate,
  gespiegelt durch den lokalen `.githooks/pre-push`-Hook)
- `dotnet build Softwareschmiede.slnx -c Debug --no-restore -p:TreatWarningsAsErrors=true`
  — neue Obsolet-/Compiler-Warnungen nach Paketupdates würden den Build brechen
- Testläufe getrennt nach `Category!=OsInterface` (stabil) und `Category=OsInterface`
  (E2E/OS-abhängig), jeweils für `Softwareschmiede.Tests` und
  `Softwareschmiede.IntegrationTests`; identische Aufteilung in `release.yml`

## `THIRD_PARTY_LICENSES.md`

Datei: `THIRD_PARTY_LICENSES.md` (Stand laut Kopfzeile: 2026-07-16). Listet ausgelieferte
NuGet-Abhängigkeiten (direkt + transitiv) mit Versionsnummer und SPDX-Kennung sowie eine
Test-/Build-Sektion. **Die Datei ist nachweislich veraltet** — Abgleich der dokumentierten
Versionen mit dem aktuell aufgelösten Paketgraph
([Nachweis](test-results/dotnet-list-all-transitive.log)):

| Eintrag in THIRD_PARTY_LICENSES.md | Dokumentierte Version | Tatsächlich aufgelöst |
|------------------------------------|-----------------------|------------------------|
| `Microsoft.EntityFrameworkCore.Sqlite` + EF-Transitiva | 10.0.9 | 11.0.0 |
| `Microsoft.Extensions.*` (alle ~25 Zeilen) | 10.0.9 | 11.0.0 |
| `SQLitePCLRaw.lib.e_sqlite3` | 2.1.12 | 3.53.3 |
| `SQLitePCLRaw.core`/`.bundle_e_sqlite3`/`.provider.e_sqlite3` | 2.1.11 | 3.0.5 |
| `Serilog` (transitiv) | 4.3.0 | 4.3.0 ✓ |
| `Serilog.Extensions.Hosting`/`.Logging` | 10.0.0 | 10.0.0 ✓ |
| `Serilog.Sinks.Console`/`.File` | 6.1.1 / 7.0.0 | 6.1.1 / 7.0.0 ✓ |
| `xunit` | 2.9.3 | Paket ersetzt durch `xunit.v3` 4.0.3 (plus `xunit.v3.*`-Transitiva) |
| `xunit.runner.visualstudio` | 3.1.5 | 4.0.1 |
| `Xunit.SkippableFact` | 1.5.23 | **nicht mehr referenziert** (kein Eintrag in einer `*.csproj`, nicht im Paketgraph) |
| `Microsoft.NET.Test.Sdk` | 18.7.0 | 18.12.0 |
| `coverlet.collector` | 10.0.1 | 10.1.1 |
| `FluentAssertions` | 8.10.0 | 8.11.0 |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.9 | 11.0.0 |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.7.0 | 11.0.0 |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.9 | 11.0.0 |
| `Moq` | 4.* (Platzhalter) | 4.21.0 |

Nicht erfasste, aktuell aufgelöste Transitiva (Auswahl): `Interop.UIAutomationClient`
10.19041.0 (FlaUI), `Castle.Core` 5.2.1 (Moq), `xunit.analyzers` 2.3.0, `SQLite` 3.53.4,
`SQLitePCLRaw.config.e_sqlite3` 3.0.5, `Microsoft.Data.Sqlite.Core` 11.0.0 (in der Datei
bereits aufgeführt, aber ebenfalls mit 10.0.9), diverse `Microsoft.Testing.*`-Pakete 2.5.2.

## Quellcode-Berührpunkte bei Paketupdates

Keine Fachklassen im Scope — Quellcode wäre nur betroffen, falls ein Update Breaking
Changes auslöst. Relevante Nutzungspunkte der updatebaren Pakete:

- `Serilog.Extensions.Hosting` / `Serilog.Sinks.*`: Logging-Pipeline wird in
  `src/Softwareschmiede.App/App.xaml.cs` aufgebaut (`UseSerilog` am Generic Host).
- `Microsoft.Extensions.TimeProvider.Testing`: genutzt in
  `src/Softwareschmiede.Tests/Helpers/TestTimeProvider.cs` (gelinkt auch in
  `Softwareschmiede.IntegrationTests`).
- `Microsoft.EntityFrameworkCore.*`: `SoftwareschmiededDbContext` +
  `db.Database.MigrateAsync()` beim App-Start (EF-Core-Migrationen unter
  `src/Softwareschmiede/Migrations/`).
- `FlaUI.*`: ausschließlich E2E-Infrastruktur unter `src/Softwareschmiede.Tests/E2E/`
  (`WpfTestBase` u. a., Kategorie `OsInterface`).
