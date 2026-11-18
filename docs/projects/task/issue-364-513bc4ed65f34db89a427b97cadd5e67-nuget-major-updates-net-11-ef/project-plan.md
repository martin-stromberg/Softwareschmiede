# Projektplan – NuGet-Major-Updates (.NET 11 / EF Core 11, xunit.v3, SQLitePCLRaw 3.x)

## Übersicht

**Projekttitel:** NuGet-Major-Updates: .NET 11 / EF Core 11, xunit.v3, SQLitePCLRaw 3.x (Issue #364)

**Gesamtziel:** Die gesamte Lösung läuft auf .NET 11 mit EF Core 11 / Microsoft.Extensions 11,
die Testsuite auf xunit.v3 und SQLitePCLRaw.lib.e_sqlite3 auf 3.x. Build, Tests und CI-Toolchain
sind grün; PR gegen `staging` schließt #364.

**Betroffene Bereiche:** alle 13 csproj (TFM/Versionen), App.csproj Plugin-Copy-Pfade,
5 CI-Dateien (`dotnet-version`), Testinfrastruktur (xunit-v2-Erweiterungen, SkippableFact),
EF-Migrationsbestand.

## Grobe Vorgehensentscheidungen

1. **Runtime-Wechsel zuerst, Paket-Upgrade danach.** Die TFM-Umstellung auf `net11.0` ist mit
   den bestehenden 10.0.15-Paketen baubar und testbar (net10-Assets laufen auf net11). Erst
   danach wird der Microsoft/EFCore-Block auf 11.0.x gehoben — so ist jeder Schritt einzeln
   verifizierbar und Fehler sind dem richtigen Schritt zuordenbar.
2. **SQLitePCLRaw 3.x wird mit EF Core 11 gebündelt** (Issue-Empfehlung: gleiche Provider-API,
   EF.Sqlite zieht transitiv SQLitePCLRaw — der Sprung gehört zum EF-/Runtime-Upgrade).
3. **xunit.v3 als eigener Schritt zuletzt**, weil es die größte API-Oberfläche hat
   (Metapaket, EXE-Ausgabe, `Xunit.Sdk`-Portierung, SkippableFact-Ersatz) und fachlich
   unabhängig vom EF-Block ist.
4. **CPM (`Directory.Packages.props`) ist explizites Nicht-Ziel** (Issue: „Optionaler
   Folgeschritt") — wird hier nicht eingeführt.
5. **CI-Toolchain gehört zu Schritt 1** (`dotnet-version: '11.0.x'` in allen 9 Vorkommen),
   damit der erste Schritt-PR bereits CI-grün bauen kann.
6. Lokale Verifikation mit benutzerinstalliertem .NET-11-SDK (`C:\Users\Martin\.dotnet11`),
   Aufruf per explizitem Pfad/`DOTNET_ROOT`; Sandbox-Regeln (kein `dotnet test` im
   Hintergrund, `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`, voller Build vor Tests,
   kein Beenden der laufenden App.exe) gelten in allen Schritten.

## Entwicklungsschritte

### Schritt 1: TFM-Umstellung aller Projekte auf .NET 11 inkl. CI-Toolchain

Alle `*.csproj` von `net10.0` auf `net11.0` bzw. `net10.0-windows10.0.17763.0` auf
`net11.0-windows10.0.17763.0` umstellen; die 8 hartcodierten
`bin\$(Configuration)\net10.0`-Plugin-Pfade in `Softwareschmiede.App.csproj` auf `net11.0`;
alle 9 `dotnet-version: '10.0.x'`-Vorkommen in `.github/workflows/` und
`.github/actions/build-and-package/action.yml` auf `'11.0.x'`; `net10.0-windows`-Kommentare
in `release.yml` aktualisieren. Paketversionen bleiben unverändert (10.0.15 läuft auf net11).

**Akzeptanzkriterien:**

- `dotnet build Softwareschmiede.slnx` mit .NET-11-SDK fehlerfrei; kein `net10` mehr in
  csproj/CI-Dateien (Ausnahme: `TargetPlatformMinVersion`-Windows-Version, fremde Artefakte).
- `dotnet test` reguläre Lane beider Testprojekte grün (mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`).
- `dotnet format Softwareschmiede.slnx --verify-no-changes` sauber.

**Abhängigkeiten:** Keine.
**Betroffene Bereiche:** alle csproj, App.csproj-Targets, CI/Workflows, Build-Umgebung.

### Schritt 2: Microsoft.*/EFCore.* 11.0.x + SQLitePCLRaw.lib.e_sqlite3 3.x

Alle im Bestand genannten `Microsoft.EntityFrameworkCore.*`- und
`Microsoft.Extensions.*`-Referenzen von 10.0.15 auf 11.0.x (jeweils neueste verfügbare
11.0.x-Version) heben — inkl. `EntityFrameworkCore.Design`, `.InMemory` (Tests) und
`Microsoft.Extensions.Hosting*` in App; `SQLitePCLRaw.lib.e_sqlite3` von 2.1.14 auf die
aktuelle 3.x; `Microsoft.Extensions.TimeProvider.Testing` auf die .NET-11-Linie, sofern
vorhanden (sonst begründet belassen). EF-Migrations-Kompatibilität für Bestandsinstallationen
nachweisen (87 Dateien / ~43 Migrationen; Aufsteig von aktuellem Stand muss funktionieren).

**Akzeptanzkriterien:**

- Keine `10.0.x`-Versionsangabe mehr bei Microsoft/EFCore-Paketen; `SQLitePCLRaw.lib.e_sqlite3` ≥ 3.x.
- Build ohne neue Fehler/Warnungen; NuGet-Auflösung ohne Downgrade-/Konfliktwarnungen.
- Reguläre Testlane grün; EF-bezogene Tests (InMemory + Sqlite-Integrationstests) grün.
- Nachweis: `dotnet ef migrations list`/Äquivalent bzw. DB-Erzeugung+Migrate gegen eine
  zuvor mit EF 10 erzeugte Datenbank läuft ohne Fehler (Upgrade-Sicherheit).
- `dotnet format --verify-no-changes` sauber.

**Abhängigkeiten:** Schritt 1 (net11.0-TFM ist Voraussetzung für EF Core 11).
**Betroffene Bereiche:** Paketversionen aller Projekte, EF-Core-/Persistenzschicht,
Testprojekte (InMemory/TimeProvider), ggf. Code-Anpassungen für EF11-API-Änderungen.

### Schritt 3: Migration der Testprojekte auf xunit.v3

`Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` von `xunit` 2.9.3 auf
`xunit.v3` (Metapaket, aktuelles 4.x/1.x-Release) umstellen: PackageReference-Tausch,
EXE-Output-Anforderungen, Portierung der v2-Erweiterungspunkte unter
`Infrastructure/Testing/` (`OsInterface*Attribute`, `OsInterfaceTraitDiscoverer` —
`Xunit.Sdk`/`Xunit.Abstractions`/`ITraitDiscoverer` → v3-Modell), `using Xunit.Abstractions`
in `E2E_ScmEvents.cs`, `CollectionBehavior`-Assembly-Attribut, Kompatibilitätsprüfung von
`Xunit.SkippableFact` 1.5.85 mit v3 — bei Inkompatibilität Ersatz durch v3-natives
dynamisches Überspringen (`Assert.Skip`/`SkipException`-Mechanik), sodass die ~30
Skippable-Stellen und die `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS`-Mechanik weiter funktionieren
(übersprungene Tests melden weiterhin „Skipped", nicht „Failed"/„Passed").
`xunit.runner.visualstudio` 4.x + `Microsoft.NET.Test.Sdk` bleiben als `dotnet test`-Pfad;
`Category`-Trait-Filterung (`OsInterface`, `E2E`) muss in beiden CI-Lanes weiter greifen.

**Akzeptanzkriterien:**

- Kein `xunit`-Legacy-Paket mehr referenziert; beide Testprojekte auf `xunit.v3`.
- `dotnet test` reguläre Lane (`Category!=OsInterface`) beider Projekte grün;
  OS-Interface-Lane ausführbar (Sandbox: ConPTY-Skip greift, Tests melden „Skipped").
- Trait-/Category-Filterung und Test-Discovery unverändert (Testanzahl-Niveau bleibt
  erhalten, keine still verschwundenen Tests — Discovery-Count dokumentieren).
- `dotnet format --verify-no-changes` sauber.

**Abhängigkeiten:** Schritt 2 (gleiche Testprojekt-csproj werden dort bereits angefasst;
sequentielle Umsetzung vermeidet Merge-Dissonanz).
**Betroffene Bereiche:** beide Testprojekte (csproj + Testinfrastruktur-Code), ggf. CI-Filter.

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | TFM-Umstellung auf .NET 11 inkl. CI-Toolchain | Keine | csproj/TFM, App-Targets, Workflows |
| 2 | Microsoft.*/EFCore.* 11.0.x + SQLitePCLRaw 3.x | 1 | Paketversionen, EF/Persistenz, Migrations |
| 3 | xunit.v3-Migration der Testprojekte | 2 | Testprojekte, Testinfra, CI-Filter |

## Offene Punkte

1. **`Microsoft.Extensions.TimeProvider.Testing`:** Gehört es zum Microsoft-Block?
   → Vorschlag (übernommen): auf die .NET-11-Linie heben, falls eine 11.x-Version existiert;
   sonst bei 10.11.0 belassen (kein Shared-Framework-Bezug, unproblematisch). — behandelt
2. **SkippableFact-Ersatzstrategie:** → Vorschlag (übernommen): Erst Kompatibilität mit
   v3 prüfen; bei Inkompatibilität v3-native Skip-Mechanik als minimal-invasiver Ersatz
   (gleiche Attribut-Signatur wo möglich, um die ~30 Aufrufstellen unverändert zu lassen). — behandelt
3. **CPM-Einführung:** → Entschieden: Nicht-Ziel, da Issue es als „Optionalen Folgeschritt"
   führt. — behandelt
