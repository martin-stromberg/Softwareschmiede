# Plan-Check – Schritt 3

**Status:** Plan vollständig

- Alle v2-API-Berührungspunkte inventarisiert und je genau einer Änderung
  zugeordnet (Paket, OutputType, Trait-Attribute, SkippableFact-Ersatz).
- v3-API-Fakten sind gegen die realen Paketinhalte 4.0.3 verifiziert
  (nicht aus Doku angenommen): `Assert.Skip*`, `ITraitAttribute.GetTraits`,
  `OutputType=Exe`-Erzwingung, MTP-Modus mit vstest-Brücke.
- Kein Verlust der Testabdeckung: Attribut-Umbenennung ist verhaltensneutral,
  `Category`-Filter bleibt trait-basiert identisch.
- Testbedarf: reine Migration → Bestandstests + Filter-Verifikation reichen.
