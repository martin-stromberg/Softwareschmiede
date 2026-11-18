# Abnahmeprüfung – Entwicklungsschritt 3

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Prüfumfang

- `git diff {projektbranch}...HEAD` des Schritt-3-Branches: 207 Dateien
  (~195 mechanische CancellationToken-Ergänzungen, Rest gezielte Migration).
- Quelldateien: csproj-Diffs, OsInterface-Attribute, AssemblyInfo,
  SkippableFact-Ersatzstellen und die 6 manuell korrigierten
  Fixer-Fehlstellen im Wortlaut geprüft.
- Testnachweise: Build-/Test-/Format-Protokolle unter .NET SDK 11.0.100.

## Abgleich Akzeptanzkriterien (requirement.md Schritt 3)

| Kriterium | Nachweis | Status |
|-----------|----------|--------|
| `xunit` → `xunit.v3` 4.0.3 (beide Testprojekte) | csproj-Diff | ✓ |
| `OutputType=Exe` (v3-Pflicht) | beide csproj; Build läuft | ✓ |
| `Xunit.SkippableFact` ersetzt | 26×[Fact], 12×Assert.SkipWhen/Unless; 3 echte SKIP-Ergebnisse im Lauf | ✓ |
| Trait-Infrastruktur v3-portiert | ITraitAttribute.GetTraits(); Discoverer gelöscht; `Category=OsInterface`-Selektion belegt (4 Treffer) | ✓ |
| `xunit.runner.visualstudio`+`Microsoft.NET.Test.Sdk` behalten | unverändert; `dotnet test` läuft VSTest-Pfad | ✓ |
| Build 0 Fehler/0 Warnungen | --no-incremental: 0/0 | ✓ |
| Reguläre Lane grün + Filter wirksam | Tests 2681+3skip/0fail; Integration 348/348; keine E2E-Starts im Lauf | ✓ |
| `dotnet format --verify-no-changes` | Exit 0 | ✓ |

## Abweichungen

- `IsTestingPlatformApplication=false` (documented override): hält den
  kompletten VSTest-Pfad inkl. coverlet/`--filter`/trx in CI — ein
  MTP-Umstieg hätte alle Workflow-Testaufrufe und das Coverage-Gate
  umgebaut (coverlet.collector ist VSTest-only). Verhalten verifiziert.
- `CollectionBehavior.DisableTestParallelization` →
  `ParallelizationAttribute(Mode=None)` — v3-API, Semantik identisch.
- xUnit1051-Bereinigung (~800 Stellen) ist eine vom Analyzer erzwungene
  Folgeanpassung, keine Scope-Erweiterung; 6 Fixer-Fehlstellen manuell
  gegen Signaturen korrigiert.

## Hinweise

- Dynamisches Skippen (ConPTY-/WPF-E2E-/OS-Guards) erzeugt in v3 echtes
  "Skipped" ohne Spezialattribut — Verhalten belegt.
- OS-Interface-Lane: Trait-Selektion positiv verifiziert; volle Lane wie
  gehabt über die PR-CI (Self-Hosted-Runner).
