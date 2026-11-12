# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine. Der Befund aus Iteration 1 (Duplikation der drei Workflows) ist durch die Auslagerung in den wiederverwendbaren `workflow_call`-Workflow `.github/workflows/create-review-issue.yml` sauber behoben: Die drei Aufrufer enthalten nur noch Trigger, `concurrency`-Gruppe und einen `uses:`-Job mit Parametern; die verbleibende Wiederholung (`permissions`/`uses`/`with`/`secrets`-Gerüst) ist strukturell minimal und unvermeidbar.

## Geprüfte Dateien

- `.github/workflows/create-review-issue.yml`
- `.github/workflows/review-architecture-security.yml`
- `.github/workflows/review-nuget-packages.yml`
- `.github/workflows/review-compliance.yml`
- `docs/CI_CD.md` (Diff-Abschnitt „Geplante Review-Workflows", Secrets-Tabelle, Troubleshooting-Zeile)
- `docs/help/plugins/github-ereignisse/troubleshooting.md` (Diff-Abschnitt „Ein vom Workflow erzeugtes Review-Issue löst kein Ereignis aus")
- `docs/features/task/issue-352-ddddc0582b294019b4f6d045bfa63788-automatisierung-wiederkehrende/plan.md` (Diff)

## Geprüfte Schwerpunkte (ohne Befund)

- **`workflow_call`-Korrektheit:** Alle fünf Inputs (`label`, `label-description`, `title`, `period`, `body`) sind `type: string` + `required: true` und werden im Job-`env` konsistent gemappt; `secrets.REVIEW_ISSUE_TOKEN` ist deklariert (`required: true`) und in allen drei Aufrufern explizit gemappt. `uses: ./.github/workflows/create-review-issue.yml` ist die korrekte Pfadform für denselben Branch; `permissions: issues: write` ist ein erlaubtes Keyword am `uses:`-Job und wird an den aufgerufenen Workflow weitergegeben. Die Aufrufer-Jobs enthalten ausschließlich erlaubte Schlüssel (`permissions`, `uses`, `with`, `secrets`) — per YAML-Parse verifiziert; kein `runs-on`/`steps`/`env` im Aufrufer.
- **pwsh-Korrektheit:** `switch` über `$env:REVIEW_PERIOD` deckt `month`/`isoweek`/`year` ab, `default` bricht mit `Write-Error` + `exit 1` ab. `[System.Globalization.ISOWeek]::GetYear`/`GetWeekOfYear` liefert korrekte ISO-8601-Woche; Format `"$isoYear-KW$($isoWeek.ToString('00'))"` erzeugt `yyyy-KWnn` passend zur Doku. `$env:GH_TOKEN`-Umschaltung von `github.token` (Label/Duplikatprüfung) auf `REVIEW_ISSUE_TOKEN` (Issue-Erzeugung) ist korrekt platziert; `$LASTEXITCODE` wird nach jedem `gh`-Aufruf geprüft. `ConvertFrom-Json`-Verhalten für 0/1/n Treffer wurde auf pwsh 7 live verifiziert: `[]`/`$null` → `.Count` = 0 (Issue wird erzeugt), Einzeltreffer → `.Count` = 1 und `$existing[0].number` funktioniert über Skalar-Indizierung, Mehrfachtreffer → Array.
- **Secret-Handling:** `REVIEW_ISSUE_TOKEN` wird ausschließlich per `env` injiziert und nie in Ausgaben/Fehlermeldungen verwendet (GitHub maskiert Secret-Werte). Der Guard (`IsNullOrWhiteSpace` → `Write-Error` + `exit 1` mit Handlungsanleitung) fängt leeres/fehlendes Mapping sichtbar ab; bewusst fehlender Fallback auf `GITHUB_TOKEN` ist in Datei-Kopfkommentar und Doku konsistent begründet.
- **Issue-Body-Transport:** `body: |` (Literal-Block) → `with:`-Input → Job-`env` `REVIEW_BODY` → `gh issue create --body $env:REVIEW_BODY` als einzelnes Argument — Mehrzeiligkeit bleibt erhalten. Erste Zeile ist in allen drei Aufrufern die frei stehende Zeile `@softwarewerkstatt`. Die drei Bodies wurden gegen `gh issue view 352` verglichen und stimmen exakt überein (Architektur: 9 Prüfpunkte + „Erstelle eine priorisierte Liste konkreter Handlungsempfehlungen."; NuGet: 6 Prüfpunkte + „Erstelle konkrete Update-Empfehlungen inklusive Risikobewertung."; Compliance: 9 Prüfpunkte + „Erstelle konkrete Änderungsvorschläge und liste erkannte Risiken oder Handlungsbedarfe auf.").
- **YAML-Details:** `"on":` ist in allen vier Dateien korrekt gequotet; Cron-Ausdrücke gequotet und gültig (`0 6 1 * *`, `0 6 * * 1`, `0 6 1 1 *`); `concurrency`-Gruppen mit `cancel-in-progress: false` je Aufrufer serialisieren Duplikatprüfung → Erzeugung; `timeout-minutes: 10`, `ubuntu-latest` und `shell: pwsh` entsprechen den Repo-Konventionen (`security-scan.yml`, `sync-staging-with-main.yml` mit identischem `gh label create ... --force`/`GH_TOKEN: ${{ github.token }}`-Muster). En-Dash im Titel (`"$env:REVIEW_TITLE_PREFIX – $suffix"`) stimmt mit den dokumentierten Titel-Formaten überein.
- **Konsistenz Kommentare/Doku ↔ Implementierung:** Aufrufer-Kopfkommentare beschreiben die Auslagerung zutreffend; `docs/CI_CD.md` (Tabelle, Ablaufschritte, Secrets-Tabelle, Troubleshooting-Zeile, Rollout-Schritte) ist konsistent — u. a. „2 h versetzt zum `security-scan.yml`" verifiziert (`0 4 * * 1` vs. `0 6 * * 1`); `plan.md` listet `create-review-issue.yml` mit allen Inputs/Secrets korrekt; `troubleshooting.md`-Neuabschnitt beschreibt die tatsächlichen Fehlerbilder (Bot-Autor, Nicht-Owner-Bearbeitung, fehlende Collaborator-Rolle).
