# Bestandsaufnahme: GitHub Page „Softwareschmiede" (Issue #393)

Analysiert wurde das Repository bezogen auf die Anforderung, eine öffentliche statische Projektseite über GitHub Pages bereitzustellen (Inhalte, Design-Anlehnung an die App-Themes, Deploy-Mechanismus passend zum Staging-Workflow, dokumentierte Pflege). Datenmodell-, Logikklassen, Interfaces und Enums der Anwendung sind **nicht** betroffen — die Bestandsaufnahme erfasst daher die relevanten Repository-Artefakte statt Codeklassen.

## Zusammenfassung

- **GitHub Pages ist nicht aktiviert:** `GET /repos/martin-stromberg/Softwareschmiede/pages` liefert HTTP 404 (verifiziert 2026-11-17 via `gh api`); die einmalige Aktivierung bleibt eine Betriebshandlung (Admin-/PAT-Rechte), wie in der Anforderung beschrieben.
- **Kein Seitenquell-Baum vorhanden:** weder `site/` noch ein `docs/`-basierter Pages-Bestand; keine `index.html`, kein `_config.yml`/`.nojekyll`, kein `CNAME`. `package.json` enthält nur semantic-release-DevDependencies — kein Static-Site-Generator.
- **Kein Pages-Deploy-Workflow:** unter `.github/workflows/` liegen 11 Workflows (Release, Staging-CI, PR-CI, Promotion, Back-Merge, PR-Quellen-Guard, 3 Review-Workflows + wiederverwendbarer `create-review-issue`, Security-Scan); keiner nutzt `configure-pages`/`upload-pages-artifact`/`deploy-pages`. Referenz-Muster für `concurrency`-Serialisierung und Guard-Schritte existieren (`release.yml`, Review-Workflows, Publish-Guard in `.github/actions/build-and-package/action.yml`).
- **Staging-Modell ist verbindlich:** `verify-pr-source.yml` lässt PRs nach `main` nur von `staging` zu; `staging-to-main-promotion.yml` erzeugt den Promotion-PR nach erfolgreichem „Pre-Release"-Lauf. Ein `push`-Trigger auf `main` ist der korrekte Deploy-Auslöser; `workflow_dispatch` ist etabliertes Muster.
- **Design-Referenzen vollständig vorhanden:** `DarkTheme.xaml`/`LightTheme.xaml` liefern die komplette Farbpalette (dunkle Flächen `#1C1C1C`/`#252525`/`#2D2D2D`, helle `#FAFAFA`/`#F0F0F0`/`#FFFFFF`, Akzente `#4CC2FF`/`#60CDFF` bzw. `#0078D4`/`#005A9E`) — deckt sich mit den Werten in der Anforderung.
- **Assets teils vorhanden:** `src/Softwareschmiede.App/images/favicon-hammer-pick.svg` (laut `<desc>` ausdrücklich für die Web-Anwendung gedacht), `Softwareschmiede.ico`, `Softwareschmiede-RC.ico`. **Keine Screenshots** im Repository — müssen manuell erzeugt werden.
- **Inhaltsquellen reichlich:** `README.md` (deutsch, Kernfunktionen-Liste, Terminalintegration, Programmupdate) und `docs/help/index.md` (Themenstruktur) als Textbasis; `releases/latest` als Download-Verweis passt zum In-App-Update-Mechanismus.
- **Dokumentationsziele vorhanden:** `docs/CI_CD.md` enthält bereits das Muster „Einmalige Betriebshandlungen (Rollout)" (`REVIEW_ISSUE_TOKEN`) für die Pages-Aktivierung. Achtung: `CI_CD.md` hat Drift — nennt `test.yml`/`pr.yml`/`softprops/action-gh-release`, die so nicht (mehr) existieren.
- **Sprache:** Repository-Dokumentation ist durchgehend Deutsch — konsistent zur Annahme „Seite auf Deutsch" in der Anforderung.

**Test-Ausgangszustand:** Reguläre Lane `Softwareschmiede.Tests`: 2479 Tests — 2475 bestanden, **1 nachgewiesener Fehlschlag** (`TerminalControlTests.Selection_NewOutputOutsideSelection_KeepsSelectionAndCopyText`, `COMException OpenClipboard CLIPBRD_E_CANT_OPEN` — Umgebungs-/Clipboard-Problem der Sandbox), 3 plattformbedingt übersprungen. Reguläre Lane `Softwareschmiede.IntegrationTests`: 285/285 bestanden. `Category=OsInterface`-Lanes nicht ausgeführt (Zustand unbekannt). Nachweis und Details in [Tests](inventory/tests.md).

## Details

- [CI/CD-Workflows und Automatisierung](inventory/workflows.md) — alle 11 Workflows, Composite Actions, Staging-Modell, Referenz-Muster für `pages.yml`
- [Inhaltsquellen, Design-Referenzen und Assets](inventory/content-assets.md) — `README.md`, `docs/help/`, Theme-Farbpaletten, Icons, fehlende Screenshots
- [Tests](inventory/tests.md) — Test-Ausgangszustand, nachgewiesener Clipboard-Fehlschlag, Übersprungenes, nicht ausgeführte Lanes
