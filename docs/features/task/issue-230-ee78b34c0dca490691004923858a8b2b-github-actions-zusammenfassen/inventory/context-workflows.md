# Inventory: Kontext-Workflows

## `.github/workflows/test.yml`

- Workflow-Name: `Tests`.
- Trigger: Pushes nach `main` und Pull Requests gegen `main`.
- Zweck: stabiler Testlauf fuer Main-Merge-Nachkontrollen und Main-PRs.
- Laeuft ebenfalls auf `windows-latest` mit .NET 10, Build und denselben beiden Testprojekten.
- Teilt Regeltests und OS-Interface-Tests in getrennte Schritte; OS-Interface-Tests sind Best-Effort.
- Verwendet eigene Concurrency-Gruppe und eigene Artefakte (`test-results-regular`, `test-results-os-interface`).

Dieser Workflow ist nicht der PR-Workflow gegen `staging` und wird durch die Anforderung nicht ersetzt oder umbenannt. Die dort vorhandene Testlogik dient nur als Vergleich fuer Testfilter und Artefaktbehandlung.

## `.github/workflows/staging-ci.yml`

- Workflow-Name: `Staging Branch CI`.
- Trigger: Pushes nach `staging`.
- Zweck: Build, Tests und anschliessende Staging-/Release-Candidate-Automatisierung.
- Enthaelt zusaetzlich Node.js-Setup, `npm ci`, Versionsbestimmung, RC-Tag-Berechnung, Packaging und GitHub-Pre-Release-Erstellung.
- Verwendet einen eigenen Concurrency-Schluessel `staging-ci`, `contents: write` und eine Laufzeit von bis zu 30 Minuten.

Dieser Workflow ist kein Pull-Request-Gate und bleibt ausserhalb des Aenderungsumfangs. Insbesondere darf die Zusammenfassung in `pr.yml` keine Release-Schritte aus `staging-ci.yml` uebernehmen.

## Weitere Abgrenzung

`.github/workflows/security-scan.yml` scannt NuGet-Abhaengigkeiten fuer PRs und Pushes gegen `main` sowie woechentlich. Der Security-Dependency-Check fuer den PR gegen `staging` ist dagegen bereits als Schritt in `pr.yml` enthalten. Die Anforderung verlangt keine Zusammenlegung mit dem separaten `security-scan`-Workflow.
