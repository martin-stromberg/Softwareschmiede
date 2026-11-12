# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Workflow-Artefakte (`.github/workflows/`)

- [x] `review-architecture-security.yml` (GitHub-Actions-Workflow) — angelegt; Cron `0 6 1 * *` + `workflow_dispatch`, `permissions: issues: write`, `concurrency`-Gruppe `review-architecture-security` (`cancel-in-progress: false`), `env: REVIEW_LABEL: automated-review-architecture`, Job `ubuntu-latest`/`timeout-minutes: 10`, Kopf-Kommentar mit Zweck, `REVIEW_ISSUE_TOKEN`-Pflicht und `docs/CI_CD.md`-Verweis — alle per `yaml.safe_load` verifiziert
- [x] `review-nuget-packages.yml` (GitHub-Actions-Workflow) — angelegt; Cron `0 6 * * 1` + `workflow_dispatch`, Gruppe `review-nuget-packages`, Label `automated-review-nuget`, gleicher Aufbau
- [x] `review-compliance.yml` (GitHub-Actions-Workflow) — angelegt; Cron `0 6 1 1 *` + `workflow_dispatch`, Gruppe `review-compliance`, Label `automated-review-compliance`, gleicher Aufbau

### Programmablauf (identisch in allen drei Workflows)

- [x] Guard-Schritt `Check REVIEW_ISSUE_TOKEN secret` — Schritt-`env` bindet `${{ secrets.REVIEW_ISSUE_TOKEN }}`, `pwsh` prüft `[string]::IsNullOrWhiteSpace`, bei leer `Write-Error` mit Anleitung (Secret-Name, PAT-Anforderung, `docs/CI_CD.md`-Verweis) + `exit 1`; kein Fallback auf `github.token`
- [x] Label-Schritt `Ensure review label exists` — `gh label create $env:REVIEW_LABEL --color "5319E7" --description "<Typ-Beschreibung>" --force` unter `GH_TOKEN: ${{ github.token }}`
- [x] Duplikatprüfung — `gh issue list --state open --label $env:REVIEW_LABEL --json number` unter `github.token`, Ergebnis via `ConvertFrom-Json` in Variable, Fehlerpropagierung via `$LASTEXITCODE`
- [x] Bedingte Issue-Erzeugung — nur bei `$existing.Count -eq 0`; `GH_TOKEN` wird vor `gh issue create` auf `REVIEW_ISSUE_TOKEN` umgeschaltet (Token-Nutzung je Operation wie geplant: Liste unter `GITHUB_TOKEN`, Erzeugung unter Owner-PAT); `Write-Host "Created review issue: $issueUrl"` bzw. `Write-Host "Skipped: an open review issue already exists (#<Nr>)."` — beide Ausgänge protokolliert
- [x] Issue-Body als single-quoted pwsh-Here-String (`@'…'@`) inline im `run`-Block — erste Zeile frei stehendes Literal `@softwarewerkstatt` (Spalte 0 nach YAML-De-Indentierung), keine Interpolation
- [x] Titel-Schemata — `Monatliches Architektur- und Sicherheitsreview – YYYY-MM` (UTC `yyyy-MM`), `Wöchentliche NuGet-Paketprüfung – YYYY-KWnn` (ISO-8601 via `[System.Globalization.ISOWeek]::GetYear`/`GetWeekOfYear`), `Jährliches Compliance- und Copyright-Review – YYYY`

### Issue-Bodies (zeilenexakt gegen Issue #352 geprüft)

- [x] Architektur-/Sicherheitsreview-Body — zeilenexakt identisch mit Issue #352 (9 Prüfpunkte + „Erstelle eine priorisierte Liste konkreter Handlungsempfehlungen.")
- [x] NuGet-Body — zeilenexakt identisch mit Issue #352 (6 Prüfpunkte + „Erstelle konkrete Update-Empfehlungen inklusive Risikobewertung.")
- [x] Compliance-Body — zeilenexakt identisch mit Issue #352 (9 Prüfpunkte + „Erstelle konkrete Änderungsvorschläge und liste erkannte Risiken oder Handlungsbedarfe auf.")

### Dokumentation

- [x] `docs/CI_CD.md` — neuer Abschnitt „Geplante Review-Workflows": Tabelle (Datei, Cron/Intervall, Titel-Schema, Typlabel), Guard-/Label-/Duplikat-/Erzeugungs-Sequenz, `concurrency`-Begründung, Aktivierungshinweis (`schedule`/`workflow_dispatch` erst nach Promotion `staging` → `main`), Plattformverhalten (Cron-Verzögerung, 60-Tage-Deaktivierung), 1.-Januar-Kollision
- [x] `docs/CI_CD.md` — Abschnitt „Einmalige Betriebshandlungen (Rollout)": PAT-Anlage (feingranuliert, Owner `martin-stromberg`, nur dieses Repo, Issues: Read and write, max. 1 Jahr), Secret `REVIEW_ISSUE_TOKEN`, Collaborator-Prüfung `@softwarewerkstatt`, `workflow_dispatch`-Probelauf inkl. Autor-/Label-/Body- und „Skipped"-Prüfung
- [x] `docs/CI_CD.md` — Secrets-Tabelle um `REVIEW_ISSUE_TOKEN` erweitert; Satz „keine weiteren Secrets" mit `REVIEW_ISSUE_TOKEN`-Ausnahme angepasst
- [x] `docs/CI_CD.md` — Troubleshooting-Tabelle: neue Zeile „Review-Workflow schlägt im Guard- oder Erzeugungsschritt fehl" (Secret fehlt / PAT abgelaufen → Secret erneuern + `workflow_dispatch`)
- [x] `docs/help/plugins/github-ereignisse/troubleshooting.md` — neuer Eintrag „Ein vom Workflow erzeugtes Review-Issue löst kein Ereignis aus" mit den vier Ursachen (Autor `github-actions[bot]`, Nicht-Owner-Bearbeitung, fehlende Collaborator-Einladung, nicht erfasstes/aktiviertes Repository) inkl. Lösung und `docs/CI_CD.md`-Verweis

### Scope-Ausschlüsse (eingehalten)

- [x] Keine C#-Codeänderungen, keine UI, keine Teständerungen — `git status` zeigt ausschließlich die drei Workflow-Dateien und die zwei Doku-Dateien
- [x] Keine Änderung an `security-scan.yml` oder anderen bestehenden Workflows
- [x] Keine Eskalations-/Erinnerungslogik, kein Issue-Lebenszyklus-Management in den Workflows

## Hinweise

- **Strukturelle Abweichung ohne fachliche Lücke:** Der Plan beschreibt Duplikatprüfung und bedingte Erzeugung als getrennte Ablaufpunkte; implementiert sind sie in einem gemeinsamen Schritt `Create review issue if none is open`. Die geplante Token-Zuordnung je Operation ist gewahrt (`gh issue list` unter `GH_TOKEN: ${{ github.token }}`, Umschaltung auf `REVIEW_ISSUE_TOKEN` unmittelbar vor `gh issue create`) — funktional identisch.
- **NuGet-Titel:** Zusätzlich zu `ISOWeek::GetWeekOfYear` wird `ISOWeek::GetYear` verwendet — korrekte ISO-Jahreszahl an Jahresgrenzen, über den Plan hinaus, ohne Abweichung vom Titel-Schema.
- **Rollout-Betriebshandlungen liegen außerhalb des Commits** (Plan-Schritt 6): PAT anlegen, `REVIEW_ISSUE_TOKEN` hinterlegen, Collaborator-Status prüfen, `workflow_dispatch`-Probeläufe nach Promotion nach `main`. Kein Code-Rest.
- Prüfmethode: `yaml.safe_load` über alle drei Workflows (Trigger, Permissions, Concurrency, Env, Job, Schritte), Here-String-Extraktion und `difflib`-Vergleich gegen `gh issue view 352` — drei mal „LINE-EXACT MATCH".
