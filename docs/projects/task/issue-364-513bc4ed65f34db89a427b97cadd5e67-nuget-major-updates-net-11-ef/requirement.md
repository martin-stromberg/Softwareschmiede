# Anforderung – NuGet-Major-Updates: .NET 11 / EF Core 11, xunit.v3, SQLitePCLRaw 3.x

Quelle: Issue #364 (aufgeschobene Prio-3-Updates aus der wöchentlichen NuGet-Prüfung #361),
Auftragskommentar: „Warte bitte zunächst das Release ab, schließe dann den Backmerge-PR mit
einem Admin-Merge ab (Benutzerkontextwechsel) und erledige dann dieses Upgrade."

## Ziel

Die gesamte Lösung (App, Domain/Library, Plugin-Contracts, 8 Plugins, 2 Testprojekte) wird auf
.NET 11 / EF Core 11 sowie xunit.v3 und SQLitePCLRaw.lib.e_sqlite3 3.x gehoben. Build und
Tests bleiben grün; CI-Workflows und Release-Pipeline laufen mit der .NET-11-Toolchain.

## Fachliche Anforderungen (belegt aus Issue #364)

1. **`Microsoft.*` / `EFCore.*` → 11.0.x**
   - Voraussetzung: TFM-Umstellung aller Projekte auf `net11.0`
     (inkl. `net11.0-windows10.0.17763.0` für App und Tests). EF Core 11 setzt .NET 11 voraus;
     `Microsoft.Extensions.* 11.0.0` liefert für net10.0 nur netstandard-Fallbacks →
     Mischstand vermeiden.
   - Issue-Empfehlung (GA Nov 2026 / ggf. erstes Patch-Release) ist erfüllt: .NET 11 SDK
     11.0.100 ist verfügbar und wird verwendet.

2. **`xunit` 2.9.3 → `xunit.v3`**
   - NuGet markiert `xunit` als Legacy; offizieller Nachfolger `xunit.v3` (Metapaket,
     bündelt Assert + Core + Runner; Testprojekt wird zur ausführbaren EXE).
   - Betroffene Projekte: `Softwareschmiede.Tests`, `Softwareschmiede.IntegrationTests`.
   - Kompatibilität von `Xunit.SkippableFact` mit v3 vorher prüfen (Issue-Vorgabe).
   - `xunit.runner.visualstudio` 4.x ist bereits v3-kompatibel (Issue-Feststellung).

3. **`SQLitePCLRaw.lib.e_sqlite3` → 3.x**
   - Neues Versionsschema an SQLite-Engine-Version gekoppelt (2.1.14 → 3.x).
   - Gleiche Provider-API; gebündelt mit dem EF-/Runtime-Sprung umsetzen (Issue-Empfehlung).

## SCM-Vorsequenz (belegt aus Auftragskommentar)

- Release-Lauf zur Staging→main-Promotion abwarten.
- Backmerge-PR #419 (main → staging) per Admin-Merge im Benutzerkontext
  `martin-stromberg` schließen.
- Anschließend Task-Branch auf aktualisiertes `staging` bringen und Upgrade umsetzen;
  Ergebnis als PR gegen `staging` mit Closing-Tag `Closes #364`.

## Betroffene Bereiche

- Alle 13 `*.csproj` (TFM + PackageReference-Versionen)
- `Softwareschmiede.App.csproj`: `CopyPluginsToOutput`-Pfade enthalten `net10.0` hart codiert
- `.github/workflows/*.yml` + `.github/actions/build-and-package/action.yml`:
  `actions/setup-dotnet` mit `dotnet-version: '10.0.x'` → `11.0.x`
- Testinfrastruktur mit xunit-v2-Erweiterungen (`Xunit.Sdk`, `Xunit.Abstractions`,
  `TraitDiscoverer`, Custom `FactAttribute`-Ableitungen) unter
  `src/Softwareschmiede.Tests/Infrastructure/Testing/`
- `Xunit.SkippableFact`-Verwendungen (~30 Stellen, überwiegend E2E)
- EF-Core-Migrations (`src/Softwareschmiede/Migrations/`) — Upgrade-Sicherheit für
  bestehende Datenbanken prüfen

## Akzeptanzkriterien

- `dotnet build Softwareschmiede.slnx` mit .NET-11-SDK ohne Fehler und ohne neue Warnungen.
- `dotnet test` (reguläre Lane `Category!=OsInterface`) für beide Testprojekte erfolgreich;
  OS-Interface-Lane separat ausführbar (Sandbox-Einschränkungen gemäß CLAUDE.md beachten:
  `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`).
- `dotnet format Softwareschmiede.slnx --verify-no-changes` sauber (spiegelt CI-Format-Check).
- CI-Workflows referenzieren `dotnet-version: '11.0.x'`.
- Keine PackageReference mehr auf `xunit` (Legacy), `SQLitePCLRaw.lib.e_sqlite3` 2.x oder
  `Microsoft.*`/`EFCore.*` 10.0.x.
- Bestehende EF-Core-Migrations bleiben für Bestandsinstallationen anwendbar
  (keine Breaking Changes am Migration-Code ohne Migration-Neuerstellung).

## Explizite Nicht-Ziele

- `Directory.Packages.props` / Central Package Management — im Issue ausdrücklich als
  „Optionaler Folgeschritt" geführt, nicht Teil dieses Upgrades.
- Updates sonstiger Pakete (Serilog, FlaUI, Moq, FluentAssertions, coverlet, …) — nur die im
  Issue genannten drei Blöcke plus direkt erzwungene Abhängigkeiten.
- Funktionale Änderungen an der Anwendung.

## Annahmen (nicht belegt, zu prüfen)

- `Microsoft.Extensions.TimeProvider.Testing` (10.11.0) gehört zum `Microsoft.*`-Block und
  wird auf die für .NET 11 passende Version gehoben, sofern verfügbar.
- `xunit.v3`-Migration kann Ersatz für `Xunit.SkippableFact` erfordern (v3 unterstützt
  dynamisches Überspringen nativ); Entscheidung im zugehörigen Schritt.
- PR-Zielbranch ist `staging` (gemäß Projekt-Konvention/promptSuffix).

## Offene Fragen

- Keine im Sinne der Anforderung. Technische Klärungen (SkippableFact-Ersatz,
  TraitDiscoverer-Portierung, EF-Migrations-Verhalten) werden in der Bestandsaufnahme
  bzw. im jeweiligen Entwicklungsschritt behandelt.
