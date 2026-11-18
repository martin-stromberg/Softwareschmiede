# Anforderung – Schritt 2: Microsoft.*/EFCore.* 11.0.x + SQLitePCLRaw.lib.e_sqlite3 3.x

## Ziel

Alle `Microsoft.*`-/`EntityFrameworkCore.*`-Pakete auf die .NET-11-Linie (11.0.0) und
`SQLitePCLRaw.lib.e_sqlite3` von 2.1.14 auf die aktuelle stabile 3.x (3.53.3) heben.
EF-Migrations-Upgrade-Sicherheit für Bestandsinstallationen nachweisen.

## Konkrete Versionen (NuGet-Stand 2026-11-18, stable)

- `Microsoft.EntityFrameworkCore.Sqlite|.Design|.InMemory` → `11.0.0`
- `Microsoft.Extensions.Caching.Memory|DependencyInjection|Hosting|Hosting.Abstractions|
  Logging|Logging.Console|Options` → `11.0.0` (die in Schritt 1 entfernten
  framework-bereitgestellten Referenzen — Logging.Abstractions/Options/Hosting.Abstractions —
  bleiben entfernt; verbliebene echte Pakete werden gehoben)
- `Microsoft.Extensions.TimeProvider.Testing` → `11.0.0` (11.x-Linie vorhanden)
- `SQLitePCLRaw.lib.e_sqlite3` → `3.53.3`

## Scope-Klärung

- Scope-Ausschlüsse: keine. Keine Modell-/Schema-Änderung → **keine neue Migration**
  (Begründung: reine Paket-Linie; EF-Migrations-Historie bleibt unverändert gültig —
  entityframework-database-Regel 6 erlaubt explizite Begründung statt Migration).

## Akzeptanzszenarien

- A (Happy Path): `dotnet build` 0 Fehler/0 Warnungen; NuGet-Auflösung ohne
  Downgrade-/Konfliktwarnungen (NU1603/NU1605 etc.).
- B: `dotnet test` reguläre Lane beider Projekte grün (inkl. EF-InMemory-Tests und
  SQLite-Integrationstests; ConPTY-Skip-Env gesetzt).
- C (Upgrade-Sicherheit): Die Integrations-Migrationstests
  (`RemoveAutonomAufgabeModelsMigrationTests`, `WorkdirMigrationTests`,
  `DatabaseFixture.MigrateAsync`-Pfad) laufen unter EF 11 fehlerfrei — sie wenden
  reale Migrationen an und rollen zurück; zusätzlich `dotnet ef migrations list` /
  DB-Neuanlage+Migrate als Toolchain-Nachweis.
- D: `dotnet format --verify-no-changes` sauber.
