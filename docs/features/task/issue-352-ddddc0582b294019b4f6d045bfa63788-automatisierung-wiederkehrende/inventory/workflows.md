# GitHub-Workflows, Composite Actions und Label-Konventionen

Bestand unter `.github/` (7 Workflows, 2 Composite Actions, 3 Issue-Templates). Kein Workflow erzeugt heute Issues (`gh issue` kommt nirgends vor). Alle Angaben aus dem Arbeitsstand `2378cdfeea9717fb8cba28deb50386102c6fcba8`.

## `.github/workflows/sync-staging-with-main.yml` — direkte Vorlage für `gh`-CLI-Muster

Name: `Backmerge Main to Staging` (71 Zeilen).

| Aspekt | Stand | Zeile |
|--------|-------|-------|
| Trigger | `push` auf `main` | 12–14 |
| Permissions | `contents: read`, `pull-requests: write`, `issues: write` | 16–19 |
| Concurrency | `group: sync-staging-with-main`, `cancel-in-progress: false` | 21–23 |
| Runner | `ubuntu-latest`, `timeout-minutes: 10` | 27–28 |
| Shell | `pwsh` in allen `run`-Schritten | 39, 46, 53, 71 |
| Auth | `env: GH_TOKEN: ${{ github.token }}` je `gh`-Schritt | 51, 58 |
| Label idempotent anlegen | `gh label create automated-backmerge --color "1D76DB" --description "..." --force` | 48–52 |
| Duplikatprüfung | `$existing = gh pr list --base staging --head main --state open --json number \| ConvertFrom-Json`; bei `Count -eq 0` → `gh pr create`, sonst `Write-Host`-Hinweis im Log | 55–70 |

Checkout mit `actions/checkout@v7` + `fetch-depth: 0` (wird hier für `git rev-list` gebraucht — für einen reinen Issue-Workflow nicht nötig).

## `.github/workflows/staging-to-main-promotion.yml` — zweites Exemplar desselben Musters

Name: `Promote staging to main`.

| Aspekt | Stand | Zeile |
|--------|-------|-------|
| Trigger | `workflow_run` auf `workflows: ["Pre-Release"]`, `types: [completed]`, `branches: [staging]`; Kommentar warnt, dass der Display-Name (nicht der Dateiname) referenziert wird | 12–19 |
| Permissions | `contents: read`, `pull-requests: write`, `issues: write` | 21–24 |
| Concurrency | `group: staging-to-main-promotion`, `cancel-in-progress: false` | 26–28 |
| Job-Guard | `if: github.event.workflow_run.conclusion == 'success'` | 32 |
| Label idempotent anlegen | `gh label create automated-promotion --color "0E8A16" --description "..." --force` unter `GH_TOKEN: ${{ github.token }}` | 57–61 |
| Duplikatprüfung + Erstellung | `gh pr list --base main --head staging --state open --json number` → bedingte `gh pr create --draft --label automated-promotion`; Log-Hinweis bei vorhandenem PR | 64–82 |

## `.github/workflows/security-scan.yml` — Vorlage für Cron + `workflow_dispatch`

Name: `Security Scan` (45 Zeilen).

| Aspekt | Stand | Zeile |
|--------|-------|-------|
| Trigger | `schedule: - cron: '0 4 * * 1'` (wöchentlich Montag 04:00 UTC) **plus** `workflow_dispatch:` | 9–14 |
| Concurrency | `group: security-scan-${{ github.workflow }}-${{ github.ref }}`, `cancel-in-progress: true` | 16–18 |
| Permissions | `contents: read` | 20–21 |
| Runner | `windows-latest`, `timeout-minutes: 20` (Scan braucht .NET/Windows; für Issue-Erzeugung unnötig) | 25–26 |
| Composite Action | `uses: ./.github/actions/security-scan` mit Inputs `solution-path`, `artifact-name` | 41–45 |

Kopf-Kommentar (Zeilen 3–8) dokumentiert die Herkunft der Scan-Logik (ausgelagert in Composite Action, genutzt von `pr-staging-ci.yml`, `staging-ci.yml` und diesem Workflow). Verweist auf `ci-target-schema.md` Abschnitt 4.9 — **diese Datei existiert im Repo nicht** (wahrscheinlich früheres Feature-Verzeichnis-Dokument).

## Übrige Workflows

| Datei | Name | Trigger | Relevanz für diese Anforderung |
|-------|------|---------|-------------------------------|
| `pr-staging-ci.yml` | `PR CI for Staging` | `pull_request` → `staging` | Test-Lanes (`Category!=OsInterface` blockierend, `OsInterface` best-effort); keine Issue-/Label-Nutzung |
| `staging-ci.yml` | `Pre-Release` | `push` → `staging` | Enthält einzige `secrets.*`-Referenz des Repos: `GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}` (Zeile 267, Semantic-Release-Dry-Run) |
| `release.yml` | `Release` | `push` → `main`, `tags: v*.*.*` | `concurrency: { group: release, cancel-in-progress: false }`; `gh release create` unter `GITHUB_TOKEN`; Action-Pinning-Konvention (`softprops/action-gh-release` auf Commit-SHA) |
| `verify-pr-source.yml` | `Verify PR Source` | `pull_request` → `main` | `permissions: {}` — Beleg für die Konvention, Berechtigungen explizit minimal zu setzen |

## Composite Actions `.github/actions/`

| Pfad | Zweck |
|------|-------|
| `build-and-package/action.yml` | `dotnet publish` für die WPF-App inkl. Publish-Guard und ZIP-Erstellung (in `release.yml` genutzt) |
| `security-scan/action.yml` | `dotnet list package --vulnerable --include-transitive` + Artefakt-Upload (`vulnerable-packages-scan`-Log); `shell: pwsh`, bricht mit `exit 1` bei Befunden |

## Label-Konventionen

Remote im Repository `martin-stromberg/Softwareschmiede` (Stand 2026-11-12, `gh label list`):

| Label | Farbe | Beschreibung | Erzeugt von |
|-------|-------|--------------|-------------|
| `automated-backmerge` | `#1D76DB` | Automated back-merge PR from main to staging | `sync-staging-with-main.yml` (per `--force` idempotent) |
| `automated-promotion` | `#0E8A16` | Automated PR from staging to main | `staging-to-main-promotion.yml` |
| `automated-sync` | `#0E8A16` | Automated sync PR from main to staging | **kein aktueller Workflow** — Relikt bzw. Vorgängername von `automated-backmerge` |
| Standard-Labels | — | `bug`, `documentation`, `duplicate`, `enhancement`, `good first issue`, `help wanted`, `invalid`, `question`, `wontfix`, `dependencies`, `javascript` | GitHub-Default / Renovate |

Kein review-/prüfungsbezogenes Label existiert. Die Repo-Konvention für automationserzeugte Objekte ist das Präfix `automated-*` + idempotentes `gh label create ... --force` vor Verwendung.

## Issue-Templates `.github/ISSUE_TEMPLATE/`

`bug_report.md`, `feature_request.md`, `custom.md` — jeweils mit leeren `labels:`/`assignees:`/`title:`-Frontmatter-Feldern; für die automatische Erzeugung irrelevant (Workflows nutzen `gh issue create` ohne Template).

## Secrets- und Token-Verwendung

- Einzige `secrets.*`-Referenz: `${{ secrets.GITHUB_TOKEN }}` in `staging-ci.yml:267` — das automatisch bereitgestellte Token, **kein** manuell hinterlegtes Secret. `docs/CI_CD.md` Abschnitt „Secrets und Umgebungsvariablen" bestätigt: „Es sind keine weiteren, manuell zu hinterlegenden Secrets erforderlich."
- `gh`-Aufrufe laufen über `env: GH_TOKEN: ${{ github.token }}` pro Schritt (pwsh). Autor von `gh`-Erzeugnissen unter `GITHUB_TOKEN` ist `github-actions[bot]` — siehe [logic.md](logic.md), Owner-Prüfung.
- `gh` ist auf GitHub-hosted Runnern (`ubuntu-latest`) vorinstalliert; `sync-staging-with-main.yml` nutzt ihn ohne Setup-Schritt.

## Plattform-Eigenschaften (verifizierte Dokumentationsfakten)

- `schedule`-Cron läuft auf **UTC** und feuert nur vom **Default-Branch** (`main`, via `gh repo view` verifiziert). Neue Cron-Workflows werden erst nach der Promotion `staging` → `main` aktiv.
- GitHub kann geplante Läufe unter Last verzögern und deaktiviert Schedules nach 60 Tagen Repository-Inaktivität (Plattformverhalten).
- Lokale gh-Umgebung (Faktum): `softwarewerkstatt` aktiv via `GH_TOKEN` (Scopes u. a. `notifications`, `repo`, `workflow`; `read:org` fehlt laut `gh auth status`), `martin-stromberg` im Keyring (inaktiv). `gh version 2.92.0`.
