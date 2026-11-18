# Umsetzungsplan – Schritt 3: xunit.v3-Migration

## Änderungen

1. csproj (beide Testprojekte): `xunit` → `xunit.v3` 4.0.3,
   `<OutputType>Exe</OutputType>` ergänzen; in Tests `Xunit.SkippableFact`
   entfernen.
2. OsInterface-Infrastruktur neu schreiben:
   - `OsInterfaceAttribute`: `ITraitAttribute.GetTraits()` → `Category=OsInterface`
   - `OsInterfaceFactAttribute`/`OsInterfaceTheoryAttribute`: gleiche Implementierung
   - `OsInterfaceTraitDiscoverer.cs` löschen
3. Mechanische Ersetzungen:
   - `[SkippableFact]` → `[Fact]` (26×)
   - `Skip.If(` → `Assert.SkipWhen(`, `Skip.IfNot(` → `Assert.SkipUnless(`
   - `WpfTestBase`-Kommentare an v3-API anpassen
4. Build; v3-Analyzer-/API-Differenzen minimal beheben (Ziel 0 Warnungen).
5. Verifikation: reguläre Lanes beider Projekte (`Category!=OsInterface`),
   Trait-Selektion `Category=OsInterface` auf kleine Teilmenge, Format-Check.

## Offene Punkte

Keine.
