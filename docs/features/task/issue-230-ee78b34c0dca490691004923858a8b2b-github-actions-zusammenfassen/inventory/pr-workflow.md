# Inventory: Pull Request Workflow

Datei: `.github/workflows/pr.yml`

## Trigger und Ausfuehrungsrahmen

- Workflow-Name: `Pull Request CI for staging`.
- Trigger: `pull_request` auf `staging` bei `opened`, `synchronize` und `reopened`.
- Concurrency: pro Pull Request (`pr-staging-${{ github.event.pull_request.number }}`), laufende Ausfuehrungen werden abgebrochen.
- Berechtigungen: `contents: read` und `checks: write`.
- Fuer einen normalen PR gilt `github.head_ref != 'main'`.
- Ein PR von `main` nach `staging` verwendet stattdessen `back-merge-skip` und ueberspringt die beiden Pruefjobs.

## Aktuelle Jobs

### `test`

Der Job laeuft auf `windows-latest` und hat ein Timeout von 20 Minuten. Er checkt den Code aus, richtet .NET 10 ein, restauriert `Softwareschmiede.slnx` und baut im Debug-Modus. Danach laufen vier Testschritte:

1. Regeltests aus `Softwareschmiede.Tests` ohne `OsInterface`.
2. Regel-Integrationstests aus `Softwareschmiede.IntegrationTests` ohne `OsInterface`.
3. OS-Interface-Tests aus `Softwareschmiede.Tests` mit `continue-on-error: true`.
4. OS-Interface-Integrationstests aus `Softwareschmiede.IntegrationTests` mit `continue-on-error: true`.

Die TRX-Ergebnisse werden immer als `test-results-pr` hochgeladen.

### `validate`

Der Job verwendet dieselbe Runner-, Timeout-, Checkout-, .NET- und Restore-Konfiguration wie `test`. Seine Schritte sind bereits in der fachlich geforderten Reihenfolge angeordnet:

1. `dotnet format ... --verify-no-changes --no-restore`.
2. Vulnerability-Scan der direkten und transitiven NuGet-Pakete; bei erkannter Schwachstelle wird der Job beendet.
3. Debug-Build mit `TreatWarningsAsErrors=true` als statische Codeanalyse.

Das Scan-Log wird immer als `vulnerable-packages-pr` hochgeladen.

## Relevanz fuer die Anforderung

Die Jobs `test` und `validate` haben derzeit keine `needs`-Abhaengigkeit und starten somit parallel. Die Tests koennen vor Abschluss von Formatcheck, Security-Scan und statischer Analyse beginnen. Die Zusammenfassung muss deshalb mindestens die beiden Jobs in einen gemeinsamen Job mit dem Anzeige-/Jobnamen `validate & test` ueberfuehren und die bestehende Schrittfolge vor den Testschritten erhalten.

Die gemeinsame Ausfuehrung sollte die vorhandenen Filter, `continue-on-error`-Regeln, Artefaktnamen, Trigger, Bedingungen, Runner und Berechtigungen unveraendert bewahren, sofern die Anforderung nichts anderes vorgibt.
