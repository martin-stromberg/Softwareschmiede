# Tasks: GitHub Page „Softwareschmiede" (Issue #393)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Seiteninhalte | `site/index.html` anlegen: Hero/Tagline, Features, Screenshots, Download (`releases/latest`), Doku-/Repo-Verweise, Footer (MIT, `SECURITY.md`, `THIRD_PARTY_LICENSES.md`); alle internen Verweise relativ bzw. mit Base-Path `/Softwareschmiede/` | Offen | — |
| 2 | Seiteninhalte | `site/assets/css/site.css` anlegen: responsives Layout, CSS Custom Properties mit Farbwerten aus `DarkTheme.xaml`/`LightTheme.xaml`, Umschaltung via `prefers-color-scheme` | Offen | — |
| 3 | Assets | `favicon-hammer-pick.svg` aus `src/Softwareschmiede.App/images/` nach `site/assets/` kopieren und in `index.html` einbinden | Offen | — |
| 4 | Assets | `Softwareschmiede.ico` aus `src/Softwareschmiede.App/images/` nach `site/assets/` kopieren (Fallback-Icon) | Offen | — |
| 5 | Assets | Screenshots vom Anwender bereitstellen lassen (Vorschlag: Dashboard dunkel, Aufgabendetail mit Terminal dunkel, Einstellungen hell), unter `site/assets/screenshots/` ablegen und in `index.html` referenzieren | Offen | — |
| 6 | Workflow | `.github/workflows/pages.yml` anlegen: Trigger `push` auf `main` mit `paths`-Filter auf `site/**` und die Workflow-Datei sowie `workflow_dispatch`; `permissions: pages: write`, `id-token: write`; `concurrency: group: "pages"`, `cancel-in-progress: false` | Offen | — |
| 7 | Workflow | Job `build` in `pages.yml` implementieren: `actions/checkout` → Guard-Schritt (prüft `site/index.html` + `site/assets/favicon-hammer-pick.svg`, bricht sonst mit klarer Meldung ab) → `actions/configure-pages` → `actions/upload-pages-artifact` mit `path: site` | Offen | — |
| 8 | Workflow | Job `deploy` in `pages.yml` implementieren: `needs: build`, `environment: github-pages` mit Page-URL-Output, `actions/deploy-pages` | Offen | — |
| 9 | Dokumentation | `docs/CI_CD.md` um Abschnitt „GitHub Pages" erweitern (Trigger, `paths`-Filter, Jobs, Permissions, Concurrency, Umgebung `github-pages`, Quellordner `site/`) | Offen | — |
| 10 | Dokumentation | In `docs/CI_CD.md` unter „Einmalige Betriebshandlungen (Rollout)" die Pages-Aktivierung dokumentieren (Settings → Pages → „GitHub Actions", `gh api`-Alternative, Verifikation, `workflow_dispatch`-Probelauf) | Offen | — |
| 11 | Dokumentation | In `docs/CI_CD.md` Troubleshooting-Zeile ergänzen: `deploy-pages` schlägt mit „Not Found" fehl → Pages nicht aktiviert → Betriebshandlung + Re-run | Offen | — |
| 12 | Dokumentation | Beiläufige Drift-Korrektur in `docs/CI_CD.md`: Verweise auf `test.yml`/`pr.yml` (Zeilen 18/28) auf `staging-ci.yml`/`pr-staging-ci.yml` und `softprops/action-gh-release` (Zeile 54) auf `gh release create` korrigieren | Offen | — |
| 13 | Dokumentation | `README.md` um Verweis auf die Projektseite (`https://martin-stromberg.github.io/Softwareschmiede/`) im Kopfbereich ergänzen (Badge oder Link) | Offen | — |
| 14 | Betrieb | Nach Merge auf `main`: Pages-Source „GitHub Actions" in den Repo-Settings aktivieren, `workflow_dispatch`-Probelauf von `pages.yml` ausführen und URL manuell sichtprüfen (hell/dunkel, responsiv, Links, Screenshots) — durch den Anwender | Offen | — |
