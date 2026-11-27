# Bestandsaufnahme: EF-Core-Vorbild im Hauptprojekt

Der StatusServer hat heute **keine** EF-Core-Nutzung. Das Vorbild für die
SQLite-Persistenz liegt im Hauptprojekt `src/Softwareschmiede` — die Anforderung
verweist ausdrücklich darauf, dessen Konventionen zu übernehmen.

## `SoftwareschmiedeDbContext`
Datei: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiedeDbContext.cs` (795 Zeilen, `sealed`, `: DbContext` — **nicht** Identity-basiert)

| Element | Beschreibung |
|---------|--------------|
| `UnixMillisConverter` / `NullableUnixMillisConverter` (private static, Z. 13–19) | `ValueConverter<DateTimeOffset,long>` — **Kern-Konvention**: `DateTimeOffset` wird als Unix-ms (`long`) persistiert, damit SQLite `ORDER BY` korrekt sortiert |
| Konstruktor (Z. 22) | `SoftwareschmiedeDbContext(DbContextOptions<SoftwareschmiedeDbContext>)` |
| `DbSet<…>`-Properties (Z. 24–102) | ~28 Entities als `Set<T>()`-Properties mit XML-Doc |
| `OnModelCreating` (Z. 105–560) | Fluent-API-Konfiguration je Entity in eigenen Blöcken: Keys, `HasMaxLength`, `HasConversion<string>()` für Enums, `HasConversion(UnixMillisConverter)` für Zeitstempel, Indizes (`HasIndex`, `IsUnique`, `HasFilter`), Beziehungen mit `OnDelete(Cascade/SetNull)` |
| `PinLegacySchemaNames` (Z. 588–789) | Fixiert deutsche Tabellen-/Spaltennamen (`ToTable`, `HasColumnName`) und persistierte Enum-Strings, damit CLR-Umbenennungen kein Schema-Change erzeugen — im StatusServer-Neubau **nicht** relevant (kein Alt-Bestand) |
| `EnumLegacyConverter<TEnum>` (Z. 566–582) | Generischer Enum→String-Konverter mit Legacy-Mapping |

## `SoftwareschmiedeDbContextDesignTimeFactory`
Datei: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiedeDbContextDesignTimeFactory.cs`

- `internal sealed`, implementiert `IDesignTimeDbContextFactory<SoftwareschmiedeDbContext>`
- `CreateDbContext` baut `DbContextOptionsBuilder` mit `UseSqlite("Data Source=softwareschmiede-design.db")` — reine Design-Time-Connection für `dotnet ef migrations add`

## Migrationen
Verzeichnis: `src/Softwareschmiede/Migrations/` — **91 Dateien** (45 Migrationen + Designer + ModelSnapshot)

- Konvention: `yyyyMMddHHmmss_Name.cs` + `.Designer.cs` + `SoftwareschmiedeDbContextModelSnapshot.cs`
- Migrationen sind **eingecheckt**; es existiert eine Migration, die gezielt `DateTimeOffset`→Unix-ms korrigiert (`…_Fix_DateTimeOffset_SQLiteOrdering`) — Beleg, dass der Konverter-Pfad gelebt wird
- Migrations-Tests existieren: `src/Softwareschmiede.IntegrationTests/Infrastructure/WorkdirMigrationTests.cs`, `RemoveAutonomWorkItemModelsMigrationTests.cs` (Up/Down-Rollback-Tests via `MigrateAsync("<MigrationName>")`)

## Runtime-Anbindung (Muster für `Program.cs`)
Datei: `src/Softwareschmiede.App/App.xaml.cs`

- Z. 192: `Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!)` — **Verzeichnis wird vor DbContext-Registrierung angelegt** (genau das Muster, das die Anforderung für `data/` fordert)
- Z. 194–199: `services.AddDbContext<SoftwareschmiedeDbContext>(o => o.UseSqlite($"Data Source={dbPath}"))`
- Z. 94–101: Start-Migration im Scope — `db.Database.MigrateAsync()` direkt beim App-Start, bevor weitere Services laufen

## Paketstände (Repo-weit gepinnte Versionen, kein Central Package Management)

Es existiert **kein** `Directory.Packages.props` — Versionen stehen direkt in den csproj-Dateien.

| Paket | Version | Referenz |
|-------|---------|----------|
| `Microsoft.EntityFrameworkCore.Sqlite` | `11.0.0` | `src/Softwareschmiede/Softwareschmiede.csproj:19`, `src/Softwareschmiede.App/Softwareschmiede.App.csproj:60` |
| `Microsoft.EntityFrameworkCore.Design` | `11.0.0` (`PrivateAssets=all`) | `src/Softwareschmiede/Softwareschmiede.csproj:21–24` |
| `SQLitePCLRaw.lib.e_sqlite3` | `3.53.3` | `src/Softwareschmiede/Softwareschmiede.csproj:20`, `src/Softwareschmiede.App/Softwareschmiede.App.csproj:61` |
| `Microsoft.AspNetCore.SignalR.Client` | `11.0.0` | `src/Softwareschmiede/Softwareschmiede.csproj:18`, Test-csproj |
| `Microsoft.AspNetCore.Mvc.Testing` | `11.0.0` | `server/Softwareschmiede.StatusServer.Tests/…csproj:20` |
| `Microsoft.AspNetCore.SignalR.Client` | `11.0.0` | `server/Softwareschmiede.StatusServer.Tests/…csproj:21` |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | — | **nicht vorhanden** — Neuzugang laut Anforderung (Stand 11.0.0) |

Der StatusServer nutzt aktuell nur das Framework `Microsoft.NET.Sdk.Web` (net11.0) —
`Microsoft.AspNetCore.App`-Framework-Referenz liefert ASP.NET Core; EF Core/Identity
müssen als Pakete ergänzt werden (Identity EFCore enthält `Microsoft.EntityFrameworkCore`-Basis,
Sqlite + Design müssen separat).

## Test-Persistenz-Muster im Hauptprojekt (Vorbild für `StatusWebApplicationFactory`-Umbau)

- `src/Softwareschmiede.Tests/Helpers/TestDbContextFactory.cs` — `UseSqlite("Data Source=…;Pooling=False")` + `EnsureCreated`
- `src/Softwareschmiede.Tests/Helpers/WorkItemsListsModeServiceTestFactory.cs` — `AddDbContext` mit Datei-DB + `EnsureCreated` im Scope
- `src/Softwareschmiede.IntegrationTests/Infrastructure/DatabaseFixture.cs` — Datei-DB + `MigrateAsync`
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventPersistenceTests.cs` — auch `UseSqlite("Data Source=:memory:")` + `EnsureCreatedAsync` im Einsatz
- `Softwareschmiede.Tests` nutzt zusätzlich `UseInMemoryDatabase` (Paket `Microsoft.EntityFrameworkCore.InMemory`) an einzelnen Stellen
