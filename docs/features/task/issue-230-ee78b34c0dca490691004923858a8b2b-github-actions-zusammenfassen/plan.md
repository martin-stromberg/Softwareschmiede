# Umsetzungsplan: GitHub Actions zusammenfassen

## Ziel

`.github/workflows/pr.yml` soll die bisherigen Jobs `test` und `validate` fuer Pull Requests gegen `staging` in einem Job zusammenfassen. Der Job wird in der GitHub-Actions-Oberflaeche als `validate & test` angezeigt.

## Aenderungsumfang

- Nur `.github/workflows/pr.yml` fachlich aendern.
- Die bestehende Job-ID `test` durch eine gueltige, referenzierbare gemeinsame ID ersetzen, zum Beispiel `validate-and-test`, und den sichtbaren Jobnamen mit `name: validate & test` setzen.
- Die gemeinsame `if`-Bedingung `github.head_ref != 'main'`, `windows-latest`, das 20-Minuten-Timeout sowie Checkout, .NET-10-Setup und Restore beibehalten; gemeinsame Setup-Schritte nur einmal ausfuehren.
- Den Job `back-merge-skip` fuer `main` nach `staging` unveraendert beibehalten.

## Geplante Schrittfolge

Die Schritte werden in einem einzigen Job in dieser Reihenfolge angeordnet:

1. Checkout.
2. Setup von .NET `10.0.x`.
3. Restore von `Softwareschmiede.slnx`.
4. Lint- und Formatpruefung mit `dotnet format ... --verify-no-changes --no-restore`.
5. Security-Dependency-Scan mit dem bestehenden Vulnerability-Aufruf, der bestehenden Erkennung und dem bestehenden Fehlerverhalten.
6. Upload des Scan-Logs als `vulnerable-packages-pr` mit `if: always()` und 14 Tagen Aufbewahrung.
7. Statische Codeanalyse durch den bestehenden Debug-Build mit `TreatWarningsAsErrors=true`.
8. Den bisherigen Debug-Build ohne Restore beibehalten, damit der explizite Build-Schritt des bisherigen `test`-Jobs und seine Ausgabe-Erwartung erhalten bleiben.
9. Die beiden regulaeren `dotnet test`-Schritte mit den bestehenden Projekten, Filtern, Loggern und `--no-build`.
10. Die beiden OsInterface-`dotnet test`-Schritte mit den bestehenden Filtern und jeweils `continue-on-error: true`.
11. Upload der Testresultate als `test-results-pr` mit `if: always()` und 14 Tagen Aufbewahrung.

Damit starten alle `dotnet test`-Schritte erst nach Lint/Format, Security-Scan und statischer Codeanalyse. Die Reihenfolge innerhalb des Jobs liefert die dafuer notwendige implizite Schritt-Abhaengigkeit.

## Erhaltensregeln

- Pull-Request-Trigger auf `staging` sowie die Ereignisse `opened`, `synchronize` und `reopened` nicht aendern.
- Concurrency-Gruppe, Berechtigungen und `cancel-in-progress` unveraendert lassen.
- Testprojekte, `Category!=OsInterface`- und `Category=OsInterface`-Filter, TRX-Logger und Artefaktpfade unveraendert lassen.
- Best-effort-Verhalten der OsInterface-Tests durch `continue-on-error: true` erhalten.
- Beide Artefakte auch bei Fehlern ueber `if: always()` bereitstellen.
- Keine Aenderungen an `test.yml`, `staging-ci.yml` oder `security-scan.yml` vornehmen.

## Verifikation

- YAML-Syntax und Workflow-Struktur von `.github/workflows/pr.yml` pruefen.
- Sicherstellen, dass nur ein Job den sichtbaren Namen `validate & test` traegt und keine separaten `test`-/`validate`-Jobs verbleiben.
- Die Reihenfolge aller `dotnet test`-Schritte gegen die drei vorgeschalteten Qualitaetsschritte pruefen.
- Bedingungen und Sonderfall `back-merge-skip` mit dem bisherigen Workflow vergleichen.
- Artefaktnamen, `if: always()`, Testfilter und `continue-on-error` anhand des Workflows pruefen.

## Offene Punkte

Keine.
