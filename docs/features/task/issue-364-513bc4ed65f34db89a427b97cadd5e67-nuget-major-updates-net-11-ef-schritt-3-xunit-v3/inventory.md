# Bestandsaufnahme – Schritt 3: xunit.v3

## Paketreferenzen

- `Softwareschmiede.Tests`: `xunit` 2.9.3, `xunit.runner.visualstudio` 4.0.1,
  `Xunit.SkippableFact` 1.5.85 (v2-only → ersetzen), TFM net11.0-windows10.0.17763.0
- `Softwareschmiede.IntegrationTests`: `xunit` 2.9.3, `xunit.runner.visualstudio`
  4.0.1, TFM net11.0

## Custom-Extensibility (v2-APIs)

- `Infrastructure/Testing/OsInterfaceAttribute.cs` — `Xunit.Sdk.TraitDiscoverer`,
  `ITraitAttribute` (Marker)
- `OsInterfaceFactAttribute.cs` / `OsInterfaceTheoryAttribute.cs` — erben
  `FactAttribute`/`TheoryAttribute` + `ITraitAttribute`
- `OsInterfaceTraitDiscoverer.cs` — `ITraitDiscoverer.GetTraits(IAttributeInfo)`,
  `Xunit.Abstractions` → in v3 entbehrlich (ITraitAttribute.GetTraits direkt)

## SkippableFact-Verbrauch

- 26× `[SkippableFact]` in 12 Dateien (v. a. E2E + 2 Service-Testklassen)
- `Skip.If`/`Skip.IfNot`: `ArbeitsverzeichnisOeffnenServiceTests` (2),
  `SystemShutdownServiceTests` (4), `E2E_SessionLimitPause` (1),
  `E2E_ZeitgesteuerterPrompt` (1), `WpfTestBase.SkipWenn*` (2)
- `[SkippableTheory]` nur in Kommentar erwähnt — kein realer Einsatz

## Verhalten, das erhalten bleiben muss

- `Category!=OsInterface`-Filter der regulären Lane (OsInterface-Trait
  via `Category=OsInterface`)
- `SkipWennConPtyNichtVerfuegbar`/`SkipWennWpfE2ENichtVerfuegbar` erzeugen
  echtes "Skipped" (SkipException-Mechanismus in v3 identisch wirksam)
- `CollectionBehavior(DisableTestParallelization = true)`, `[Collection("E2E")]`,
  `[Trait("Category","E2E")]` — v3-kompatible Attribute, unverändert
- Keine catch-Blöcke im Skip-Pfad (`MitFrischerAppAsync`: nur `finally`)

## Verifizierte v3-Fakten (Paketprüfung 4.0.3)

- `Assert.Skip`/`SkipWhen`/`SkipUnless` in `xunit.v3.assert` vorhanden
- `ITraitAttribute` in `Xunit.v3` mit parameterlosem `GetTraits()`
- `OutputType=Exe` per MSBuild-Targets erzwungen (`_XunitValidateBuild`)
- `xunit.v3` 4.0.3 → `xunit.v3.mtp-v2` → `xunit.v3.core.mtp-v2` (MTP-Modus;
  vstest-Brücke via `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk`)
