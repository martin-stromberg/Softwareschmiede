# Anforderung – Schritt 3: xunit.v3-Migration der Testprojekte

## Ziel

`Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` von `xunit` 2.9.3
auf `xunit.v3` 4.0.3 (stable, NuGet-verifiziert) migrieren. `Xunit.SkippableFact`
1.5.85 ist v2-only (Abhängigkeit `xunit.extensibility.execution`) und wird durch
die v3-eingebaute dynamische Skip-API ersetzt.

## Konkrete Änderungen

- `xunit` 2.9.3 → `xunit.v3` 4.0.3 (beide Testprojekte)
- `<OutputType>Exe</OutputType>` ergänzen (v3-Targets erzwingen ausführbares
  Testprojekt; `xunit.v3.core.mtp-v2` generiert den Einstiegspunkt)
- `Xunit.SkippableFact` entfernen:
  - `[SkippableFact]` → `[Fact]` (26 Stellen, 12 Dateien)
  - `Skip.If(` → `Assert.SkipWhen(`, `Skip.IfNot(` → `Assert.SkipUnless(`
    (v3 fängt `SkipException` für jeden Test — kein Spezialattribut nötig)
  - Kommentare in `WpfTestBase.cs` (SkippableFact-Hinweise) aktualisieren
- Custom-Trait-Infrastruktur portieren (Discoverer-Muster existiert in v3 nicht):
  - `OsInterfaceAttribute`/`OsInterfaceFactAttribute`/`OsInterfaceTheoryAttribute`
    implementieren `Xunit.v3.ITraitAttribute.GetTraits()` (parameterlos, liefert
    `Category=OsInterface`)
  - `OsInterfaceTraitDiscoverer` entfällt; `[TraitDiscoverer]`-Verdrahtung entfällt
- `xunit.runner.visualstudio` 4.0.1 und `Microsoft.NET.Test.Sdk` 18.12.0 bleiben
  (vstest-Brücke für `dotnet test` inkl. `Category`-Filter)

## Akzeptanzszenarien

- A: `dotnet build` 0 Fehler/0 Warnungen (v3-Analyser-Befunde werden behoben).
- B: `dotnet test` reguläre Lane beider Projekte grün; `Category!=OsInterface`-
  Filter wirkt weiterhin (OsInterface-Tests werden nicht ausgeführt).
- C: `Category=OsInterface`-Selektion funktioniert weiterhin (Trait-Mapping).
- D: `dotnet format --verify-no-changes` sauber.
