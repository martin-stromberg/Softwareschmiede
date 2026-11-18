# Abnahmeprüfung – Entwicklungsschritt 2

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Prüfumfang

- `git diff {projektbranch}...HEAD` des Schritt-2-Branches: 6 Dateien
  (4 csproj, 2 C#-Call-Sites) + Lifecycle-Artefakte.
- Quelldateien: alle csproj-Diffs sowie beide `ExecuteSqlAsync`-Umstellungen
  im Wortlaut geprüft.
- Testnachweise: Build-/Test-/Format-Protokolle unter .NET SDK 11.0.100.

## Abgleich Akzeptanzkriterien (requirement.md Schritt 2)

| Kriterium | Nachweis | Status |
|-----------|----------|--------|
| EFCore.* 10.0.15 → 11.0.0 | Sqlite/Design/InMemory in allen 4 csproj; repo-weiter grep ohne 10er-Restbestand (Ausnahmen: Serilog 10.0.0, coverlet 10.1.1 — außerhalb Scope) | ✓ |
| Microsoft.Extensions.* → 11.0.0 | Caching.Memory, DependencyInjection, Hosting, Logging, Logging.Console, TimeProvider.Testing | ✓ |
| SQLitePCLRaw.lib.e_sqlite3 → 3.x stable | 3.53.3 (NuGet-verifiziert) in Softwareschmiede + App | ✓ |
| Build 0 Fehler/0 Warnungen, keine NU1603/NU1605 | 0/0 unter SDK 11.0.100 | ✓ |
| Reguläre Testlane grün | Tests 2681+3skip/0fail; IntegrationTests 348/348 | ✓ |
| Upgrade-Sicherheit: Migrations-Ketten unter EF 11 | Integrations-Suite enthält Anwenden+Rollback realer SQLite-Migrationen — grün | ✓ |
| Kein EnsureCreated/EnsureDeleted in Produktion | grep: nur Test-Fixtures; App-Start weiterhin `MigrateAsync` | ✓ |
| Keine neue Migration nötig | begründet: reine Paket-Linie ohne Modell-/Schema-Änderung | ✓ |
| `dotnet format --verify-no-changes` | Exit 0 | ✓ |

## Abweichungen

- [ ] Keine — alle Kriterien erfüllt. Zusatzarbeiten: `ExecuteSqlInterpolatedAsync` →
  `ExecuteSqlAsync` (2 Call-Sites), zwingende Folge der EF-11-Obsolets (CS0618),
  identische Parameterbindung.

## Hinweise

- `ExecuteSqlAsync` übernimmt in EF 11 die interpolierte `FormattableString`-
  Semantik — SQL-Parameterbindung und Injection-Schutz unverändert.
- OS-Interface-Lane wird projektweit abschließend bzw. über die PR-CI
  (Self-Hosted-Runner) nachgewiesen.
