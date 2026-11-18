# Abnahmeprüfung – Entwicklungsschritt 1

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Prüfumfang

- `git diff {basisbranch}...HEAD`: 26 Dateien (13 csproj, 5 CI-Dateien, 6 Doku-Dateien,
  2 Testinfrastruktur-Dateien).
- Quelldateien: alle csproj + App.csproj-Targets + Workflow-YAML im Wortlaut geprüft.
- Testnachweise: Build-/Test-/Format-Protokolle (s. Hinweise).

## Abgleich Akzeptanzkriterien (project-plan.md, Schritt 1)

| Kriterium | Nachweis | Status |
|-----------|----------|--------|
| `dotnet build Softwareschmiede.slnx` mit .NET-11-SDK fehlerfrei | SDK 11.0.100: 0 Fehler, 0 Warnungen | ✓ |
| Kein `net10` in csproj/CI (Ausnahmen dokumentiert) | grep: 0 Treffer in src/plugins/.github (nur `.github/upgrades/` Altbestand anderer Projekte) | ✓ |
| Reguläre Testlane grün | Tests 2679+3skip (2 Umgebungsflakes, Re-Run grün), IntegrationTests 348/348 | ✓ |
| `dotnet format --verify-no-changes` sauber | Exit 0 (SDK 11) | ✓ |
| Plugin-Copy aus net11.0-Pfaden | 8/8 Plugin-DLLs im App-Output unter `net11.0-windows…` | ✓ |
| CI `dotnet-version '11.0.x'` | 9/9 Vorkommen | ✓ |

## Abweichungen

- [ ] Keine — alle Kriterien erfüllt. Zusatzarbeiten innerhalb des Schritts dokumentiert:
  NU1510-Referenzbereinigung (13×, framework-bereitgestellte Pakete), WFO2000-
  Factory-Methoden-Fix (2×) — beides zwingende Folgen des SDK-Wechsels, keine
  Scope-Erweiterung.

## Hinweise

- 2 Test-Flakes (Zwischenablage/ViewModel-Timing) ohne Bezug zur Änderung; isoliert grün.
- OS-Interface-Lane nicht Teil der regulären Verifikation (Sandbox); CI-Runner deckt sie ab.
- `Microsoft.Extensions.TimeProvider.Testing` bleibt in Schritt 2 (Paketblock), korrekt.
