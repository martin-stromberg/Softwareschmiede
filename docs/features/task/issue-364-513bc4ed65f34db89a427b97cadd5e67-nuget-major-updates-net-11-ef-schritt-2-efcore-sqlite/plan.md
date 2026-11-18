# Umsetzungsplan – Schritt 2: EF Core 11 + SQLitePCLRaw 3.x

## Änderungen

1. In den vier csproj mit Microsoft/EFCore-Paketen alle `Version="10.0.15"` → `11.0.0`
   und `Microsoft.Extensions.TimeProvider.Testing` `10.11.0` → `11.0.0`.
2. `SQLitePCLRaw.lib.e_sqlite3` `2.1.14` → `3.53.3` (Softwareschmiede + App).
3. Restore + Build; auftretende neue Warnungen/Obsoletes einzeln bewerten und im
   minimalen Umfang beheben (Ziel: 0 Warnungen).
4. Verifikation: reguläre Testlane beider Projekte; Migrations-Integrationstests
   explizit (`RemoveAutonomAufgabeModels*`, `Workdir*`, DatabaseFixture-Pfad);
   Format-Check.

## Akzeptanzszenarien

- A: Build/Restore ohne Fehler, ohne Konflikt-/Downgrade-Warnungen.
- B: Beide Testprojekte reguläre Lane grün; `EnsureCreated`- und `MigrateAsync`-
  Testpfade funktionieren unter EF 11 identisch.
- C: Migrations-Ketten-Tests (anwenden + zurückrollen realer SQLite-Migrationen)
  grün → Upgrade-Sicherheit Bestands-DBs nachgewiesen.
- D: Format-Check sauber.

## Offene Punkte

Keine.
