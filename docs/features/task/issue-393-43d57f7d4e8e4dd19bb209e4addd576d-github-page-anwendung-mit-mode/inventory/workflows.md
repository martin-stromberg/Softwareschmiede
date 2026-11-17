# Bestandsaufnahme: CI/CD-Workflows und Automatisierung

Analysiert wurden die bestehenden GitHub-Actions-Workflows und Composite Actions unter `.github/`, bezogen auf die Anforderung, einen neuen GitHub-Pages-Deploy-Workflow zu ergänzen, der zum bestehenden Staging-Workflow passt. Es existiert **kein** Workflow für GitHub Pages; `gh api repos/martin-stromberg/Softwareschmiede/pages` liefert HTTP 404 (Pages nicht aktiviert, Stand 2026-11-17).

## Branch- und Auslieferungsmodell

Der etablierte Weg für Änderungen ist: Feature-Branch → PR nach `staging` → Push auf `staging` (Pre-Release-Validierung) → automatischer Promotion-PR `staging` → `main` → manueller Merge. Auslieferung geschieht ausschließlich vom `main`-Stand.

## Workflows

### `release.yml` — „Release"
Datei: `.github/workflows/release.yml`

| Aspekt | Ist-Stand |
|--------|-----------|
| Trigger | `push` auf `branches: [main]` und `tags: ['v*.*.*']` (Zeilen 3–6) |
| Permissions | `contents: write` (Zeilen 8–9) |
| Concurrency | `group: release`, `cancel-in-progress: false` (Zeilen 15–17) |
| Runner | `windows-latest`, `timeout-minutes: 30` (Zeilen 23–24) |
| Zentrale Schritte | `resolve-release-version.mjs` entscheidet über `create`/`upload-existing`/kein Release; Release-Gates `dotnet test ... --filter "Category!=OsInterface"` (blockierend) und `Category=OsInterface` (`continue-on-error: true`); Composite Action `build-and-package`; Release-Erstellung via `npm run release` (semantic-release) bzw. `gh release create`/`gh release upload` (Zeilen 95–181) |

Referenz-Muster für den geplanten Pages-Workflow: Deployment-Serialisierung per `concurrency`, Versions-/Guard-Logik, Test-Gates vor Veröffentlichung.

### `staging-ci.yml` — „Pre-Release"
Datei: `.github/workflows/staging-ci.yml`

| Aspekt | Ist-Stand |
|--------|-----------|
| Trigger | `push` auf `staging` (Zeilen 10–13) |
| Concurrency | `group: staging-ci`, `cancel-in-progress: true` (Zeilen 15–17) |
| Permissions | `contents: write`, `checks: write` |
| Besonderheit | Job `detect-backmerge` überspringt CI bei reinen Back-Merges von `main` (Zeilen 24–59); der Anzeigename „Pre-Release" ist vertraglich — `staging-to-main-promotion.yml` referenziert ihn im `workflow_run`-Trigger |

### `pr-staging-ci.yml` — „PR CI for Staging"
Datei: `.github/workflows/pr-staging-ci.yml`

| Aspekt | Ist-Stand |
|--------|-----------|
| Trigger | `pull_request` auf `staging` (`opened`/`synchronize`/`reopened`) |
| Concurrency | `group: pr-staging-${{ github.event.pull_request.number }}`, `cancel-in-progress: true` |
| Jobs | `detect-backmerge` (Back-Merge-Erkennung), `static checks` (`dotnet format --verify-no-changes`, Security-Scan, `TreatWarningsAsErrors`-Build), danach blockierende reguläre Tests |

### `staging-to-main-promotion.yml` — „Promote staging to main"
Datei: `.github/workflows/staging-to-main-promotion.yml`

| Aspekt | Ist-Stand |
|--------|-----------|
| Trigger | `workflow_run` für Workflow „Pre-Release" (`types: [completed]`, `branches: [staging]`) |
| Bedingung | Läuft nur bei `conclusion == 'success'`; erzeugt Draft-PR `staging` → `main`; Merge ist manuell |
| Kommentar im Code | Der Trigger referenziert den **Anzeigenamen** („Pre-Release"), nicht den Dateinamen — Namensänderungen müssen in demselben Commit nachgezogen werden |

### `sync-staging-with-main.yml` — „Backmerge Main to Staging"
Datei: `.github/workflows/sync-staging-with-main.yml`

| Aspekt | Ist-Stand |
|--------|-----------|
| Trigger | `push` auf `main` |
| Zweck | Automatischer Back-Merge-PR `main` → `staging` (hält `staging` synchron) |

### `verify-pr-source.yml` — „Verify PR Source"
Datei: `.github/workflows/verify-pr-source.yml`

| Aspekt | Ist-Stand |
|--------|-----------|
| Trigger | `pull_request` auf `main` |
| Zweck | Bricht PRs ab, deren Quelle nicht `staging` ist (`github.head_ref != "staging"` → `exit 1`); erzwingt den Staging-Workflow |

### `create-review-issue.yml` — „Create Review Issue (reusable)"
Datei: `.github/workflows/create-review-issue.yml`

| Aspekt | Ist-Stand |
|--------|-----------|
| Trigger | `workflow_call` (wiederverwendbarer Workflow) |
| Zweck | Gemeinsame Logik für die drei Review-Workflows: Secret-Guard (`REVIEW_ISSUE_TOKEN`), Label-Erzeugung, Duplikatprüfung, Issue-Erzeugung mit `@softwarewerkstatt`-Mention |

### Zeitgesteuerte Review-/Scan-Workflows
Dateien: `.github/workflows/review-architecture-security.yml`, `review-nuget-packages.yml`, `review-compliance.yml`, `security-scan.yml`

| Workflow | Trigger | Zweck |
|----------|---------|-------|
| „Monthly Architecture and Security Review" | `cron: '0 6 1 * *'` + `workflow_dispatch` | Ruft `create-review-issue.yml` auf |
| „Weekly NuGet Package Review" | `cron: '0 6 * * 1'` + `workflow_dispatch` | Ruft `create-review-issue.yml` auf |
| „Yearly Compliance and Copyright Review" | `cron: '0 6 1 1 *'` + `workflow_dispatch` | Ruft `create-review-issue.yml` auf |
| „Security Scan" | `cron: '0 4 * * 1'` + `workflow_dispatch` | Wöchentlicher Dependency-Security-Scan |

Alle drei Review-Workflows besitzen je eine eigene `concurrency`-Gruppe mit `cancel-in-progress: false` — Vorbild für die geforderte `concurrency: group: "pages"`.

## Composite Actions

### `build-and-package`
Datei: `.github/actions/build-and-package/action.yml`

| Schritt | Zweck |
|---------|-------|
| `Setup .NET 10` (`actions/setup-dotnet@v6`) | SDK-Bereitstellung |
| `Restore dependencies` | `dotnet restore` |
| `Publish (win-x64, self-contained)` | `dotnet publish` von `Softwareschmiede.App.csproj` nach `publish/` |
| `Rename executable for release` | `Softwareschmiede.App.exe` → `Softwareschmiede.exe` (Self-Hosting-Risiko, siehe `CLAUDE.md`) |
| `Write version manifest` | `publish/version.json` |
| `Verify publish output` | **Publish-Guard:** bricht ab, wenn `publish/Softwareschmiede.exe`, `publish/version.json` oder Plugin-DLLs fehlen — analoger Guard-Gedanke wie der in der Anforderung erwähnte Artefakt-Check für die Page |
| `Create Release ZIP` / `Create update manifest` | `release.zip` + `update.json` (SHA-256-Manifest für den In-App-Updater) |

### `security-scan`
Datei: `.github/actions/security-scan/` — Composite Action für den verwundbare-Pakete-Scan (von `security-scan.yml` und `pr-staging-ci.yml` genutzt).

## Lokale Git-Hooks

- `.githooks/pre-push`: spiegelt `dotnet format Softwareschmiede.slnx --verify-no-changes --no-restore` aus `pr-staging-ci.yml`; Aktivierung pro Klon via `git config core.hooksPath .githooks` (Install-Skripte `install-hooks.cmd`/`install-hooks.sh` liegen bei).
- `.githooks/pre-commit` und `.githooks/translation-check.py` vorhanden.

## Lücken / Befunde für die Anforderung

- Kein Workflow deployt statische Inhalte; `actions/configure-pages`, `actions/upload-pages-artifact`, `actions/deploy-pages` werden nirgends verwendet.
- Kein `site/`- oder vergleichbarer Seitenquell-Ordner; keine `_config.yml`/`.nojekyll`; keine HTML-Dateien im Repository.
- GitHub Pages ist im Remote-Repository **nicht aktiviert** (`GET /repos/martin-stromberg/Softwareschmiede/pages` → 404, verifiziert 2026-11-17 via `gh api`).
- `docs/CI_CD.md` beschreibt Workflows `.github/workflows/test.yml` (Zeile 18) und `.github/workflows/pr.yml` (Zeile 28), die im Repository nicht existieren — gemeint sind `staging-ci.yml`/`pr-staging-ci.yml`; zudem nennt Zeile 54 `softprops/action-gh-release`, während `release.yml` inzwischen `gh release create` nutzt. **Dokumentationsdrift**, relevant, weil `CI_CD.md` für die Anforderung erweitert werden soll.
