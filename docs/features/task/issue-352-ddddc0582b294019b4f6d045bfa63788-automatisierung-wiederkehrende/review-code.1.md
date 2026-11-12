# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### `.github/workflows/review-architecture-security.yml`, `review-nuget-packages.yml`, `review-compliance.yml` (Job `create-review-issue`)

- **Doppelter Code** — Die drei Workflow-Dateien sind zu ca. 80 % identisch: Der Guard-Schritt (`Check REVIEW_ISSUE_TOKEN secret`), der Label-Schritt (`Ensure review label exists`) und die komplette PowerShell-Logik im Erzeugungsschritt (Duplikatprüfung via `gh issue list`, `$env:GH_TOKEN`-Umschaltung auf `REVIEW_ISSUE_TOKEN`, `gh issue create`, `$LASTEXITCODE`-Behandlung) unterscheiden sich nur in Labelname, Label-Beschreibung, Issue-Titel, Issue-Body und Cron-Ausdruck. Ein Fehler in der gemeinsamen Logik (z. B. in der `ConvertFrom-Json`-Auswertung oder Token-Umschaltung) müsste an drei Stellen konsistent behoben werden.

  Empfehlung: Die gemeinsame Logik in einen wiederverwendbaren Workflow (`on: workflow_call` mit `inputs` für Label, Label-Beschreibung, Titel-Ausdruck und Body) oder in eine Composite Action unter `.github/actions/` auslagern; die drei zeitgesteuerten Workflows rufen sie dann nur noch mit ihren Parametern auf. Alternativ bewusst dokumentieren, dass die Duplikation zugunsten der Einfachheit/Lesbarkeit der Einzel-Workflows in Kauf genommen wird.

## Geprüfte Dateien

- `.github/workflows/review-architecture-security.yml`
- `.github/workflows/review-nuget-packages.yml`
- `.github/workflows/review-compliance.yml`
- `docs/CI_CD.md` (nur Diff-Abschnitt „Geplante Review-Workflows" sowie Secrets-/Troubleshooting-Ergänzungen)
- `docs/help/plugins/github-ereignisse/troubleshooting.md` (nur Diff-Abschnitt „Ein vom Workflow erzeugtes Review-Issue löst kein Ereignis aus")

## Geprüfte Schwerpunkte (ohne Befund)

- **pwsh-Korrektheit:** Here-String `@' … '@` wird durch YAML-Dedent korrekt an Spalte 0 geschlossen; `ConvertFrom-Json`-Auswertung ist für 0 Treffer (`$null`, `.Count` = 0), Einzeltreffer (`PSCustomObject`, `.Count` = 1, `$existing[0].number` funktioniert über Skalar-Indizierung) und Mehrfachtreffer (`Object[]`) korrekt; `$env:GH_TOKEN`-Umschaltung vor `gh issue create` und `$LASTEXITCODE`-Prüfung nach jedem `gh`-Aufruf sind korrekt; `[System.Globalization.ISOWeek]` ist auf pwsh 7.x verfügbar.
- **Secret-Handling:** `REVIEW_ISSUE_TOKEN` wird nur per `env` injiziert und nie in Ausgaben/Fehlermeldungen verwendet; GitHub maskiert Secret-Werte in Logs; `gh` gibt kein Token aus.
- **Injection:** Keine Nutzereingaben — Titel/Body sind statisch bzw. datumabgeleitet, Parameterübergabe an `gh` erfolgt als separate Argumente.
- **YAML:** `"on":` korrekt gequotet, Cron-Ausdrücke gequotet und syntaktisch gültig (`0 6 1 * *`, `0 6 * * 1`, `0 6 1 1 *`), `permissions: issues: write` ist minimal und ausreichend (deckt Label-Create, Issue-List und Issue-Create ab; `contents: read` wird ohne Checkout nicht benötigt), `concurrency` mit `cancel-in-progress: false` serialisiert Duplikatprüfung/Erzeugung korrekt.
- **Konsistenz Docs ↔ Workflows:** Cron-Tabelle, Label-Namen, Titel-Formate und der Hinweis „2 h versetzt zum `security-scan.yml`" (dort `0 4 * * 1`) stimmen mit den Workflow-Dateien überein.
