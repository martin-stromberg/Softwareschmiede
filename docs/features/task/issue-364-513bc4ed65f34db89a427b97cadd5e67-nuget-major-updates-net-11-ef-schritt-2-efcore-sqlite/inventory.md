# Bestandsaufnahme – Schritt 2 (EF Core 11 + SQLitePCLRaw 3.x)

## Zu hebende Referenzen (nach Schritt-1-Stand)

| Datei | Referenzen → 11.0.0 | Sonstiges |
|-------|--------------------|-----------|
| `src/Softwareschmiede/Softwareschmiede.csproj` | EFCore.Sqlite, EFCore.Design (PrivateAssets), Caching.Memory, DependencyInjection, Logging | SQLitePCLRaw 2.1.14 → 3.53.3 |
| `src/Softwareschmiede.App/Softwareschmiede.App.csproj` | EFCore.Sqlite, DependencyInjection, Hosting, Logging, Logging.Console | SQLitePCLRaw 2.1.14 → 3.53.3 |
| `src/Softwareschmiede.Tests/` | EFCore.InMemory, TimeProvider.Testing | – |
| `src/Softwareschmiede.IntegrationTests/` | EFCore.Sqlite | – |
| `plugins/*` (8) | – (Logging.Abstractions in Schritt 1 entfernt; keine Pakete mehr) | – |

## EF-Nutzung (Skill: entityframework-database)

- `SoftwareschmiededDbContext` + `IDesignTimeDbContextFactory` in
  `src/Softwareschmiede/Infrastructure/Data/`; `Migrations/`: 87 Dateien (~43 Migrationen
  + Snapshot). Produktiv-Pfad: `App.xaml.cs:96 await db.Database.MigrateAsync()` —
  kein `EnsureCreated` außerhalb von Testcode (Quality-Gate erfüllt).
- IntegrationTests enthalten echte Migrations-Ketten-Tests:
  `RemoveAutonomAufgabeModelsMigrationTests` (Migrate→Ziel, Rollback→Vorgänger),
  `WorkdirMigrationTests`, `DatabaseFixture.MigrateAsync`. Diese sind der
  EF-11-Kompatibilitätsnachweis für Bestands-DBs.
- `dotnet ef` Tooling: kein dotnet-tools-Manifest; Toolchain-Nachweis über lokale
  `dotnet ef`-Installation oder Migrations-Tests (letztere sind stärker).

## Bekannte EF-10→11-Beobachtungspunkte

- EF Core 11 bricht i. d. R. keine Public-APIs des verwendeten Stacks (Sqlite, InMemory,
  MigrateAsync); mögliche neue Warnungen/Obsoletes werden beim Build sichtbar.
- `Microsoft.Extensions.*` 11.0.0 liefert net11-Assets (kein netstandard-Fallback mehr —
  genau die vom Issue geforderte Vermeidung des Mischstands).
- SQLitePCLRaw 3.x: gleiche `SQLitePCL.Batteries_V2.Init()`/`raw.SetProvider`-API;
  EF.Sqlite 11 zieht transitiv `SQLitePCLRaw.bundle_e_sqlite3` 3.x — explizite
  lib-Referenz bleibt versioniert konsistent (Versionssprung an Engine gekoppelt).
