# Test Results: Re-Checks bei geschlossenem Issue (Schritt 2)

## Integrationstests (`Softwareschmiede.IntegrationTests`)

`dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --filter "FullyQualifiedName~ScmEventDispatchTests_IssuePreparation"`

Ergebnis: **15 bestanden, 0 fehlgeschlagen** — darunter vier neue Tests:

- `Dispatch_Aufbereitung_IssueVorZustellungGeschlossen_VerarbeitetOhneZustellung`
  — PASS: 1. Read offen, 2. Read geschlossen → `Verarbeitet`, CLI gestartet,
  aber kein Prompt, keine Remote-Aktion, Protokolleintrag „geschlossen".
- `Dispatch_Aufbereitung_IssueWährendAnalyseGeschlossen_KeinRemoteUpdate`
  — PASS: gelieferte issueBodyUpdate-/comment-Aktionen werden nach Schließung
  nicht angewendet (`fixture.Responses` leer), Ergebnisdatei mit
  `success=false` + „geschlossen"-Begründung, `Verarbeitet`, Protokolleintrag.
- `Dispatch_Aufbereitung_IssueVorZustellungUnlesbar_WartetDannZugestellt`
  — PASS: `ReadIssueAsync → null` → `Wartend` „nicht gelesen", kein Prompt;
  nach Rückkehr der Lesbarkeit regulär `Zugestellt`.
- `Dispatch_Aufbereitung_IssueImAbgleichUnlesbar_WartetDannVerarbeitet`
  — PASS: unlesbar → `InBearbeitung` bleibt, keine Remote-Aktion; nach
  Lesbarkeit vollständige Verarbeitung (`Verarbeitet`, 2 Remote-Aktionen).
- Bestehender `Dispatch_Aufbereitung_IssueGeschlossen_VerarbeitetOhneÄnderung`
  (Vor-Dispatch-Check) und alle übrigen Bestandstests — PASS.

## Format / Build

- `dotnet format Softwareschmiede.slnx --verify-no-changes` — sauber.
- `dotnet build` (`Softwareschmiede`, IntegrationTests) — 0 Warnungen, 0 Fehler.

## Zusammenfassung

Alle Akzeptanzszenarien belegt: finaler Pre-Zustell-Check, Abgleich-Check mit
Unterdrückung gelieferter Remote-Aktionen, konsistente „unlesbar = warten"-
Semantik an beiden Stellen, sauberer `Verarbeitet`-Abschluss mit Begründung;
TOCTOU-Restlücke dokumentiert.
