# Inventory: Workflow-Vertraege

## Gemeinsame technische Konfiguration in `pr.yml`

Beide aktuellen Jobs verwenden:

- `runs-on: windows-latest`.
- `timeout-minutes: 20`.
- `actions/checkout@v4`.
- `actions/setup-dotnet@v4` mit .NET `10.0.x`.
- Restore von `Softwareschmiede.slnx` mit PowerShell.

Beim Zusammenlegen ist darauf zu achten, dass diese gemeinsamen Schritte nur einmal benoetigt werden und danach alle Validierungs- und Testschritte im selben Job laufen.

## Reihenfolge-Vertrag

Die fachlich vorgegebenen Gates sind:

1. Lint-/Format-Check.
2. Security-Dependency-Check.
3. Statische Codeanalyse.
4. Tests.

Ein gemeinsamer Job stellt die Reihenfolge innerhalb der GitHub-Actions-Schritte automatisch sicher, weil ein spaeterer Schritt erst nach erfolgreichem Abschluss des vorherigen Schritts startet. Die beiden OS-Interface-Testschritte bleiben bewusst fehlertolerant; die Regeltests und die vorgeschalteten Qualitaetspruefungen bleiben blockierend.

## Erhaltenswerte Ergebnisse

- `test-results-pr` fuer TRX-Dateien aus beiden Testprojekten.
- `vulnerable-packages-pr` fuer `vulnerable-packages-scan.log`.
- PR-Trigger und `github.head_ref != 'main'`.
- `back-merge-skip` fuer den Sonderfall `main` nach `staging`.
