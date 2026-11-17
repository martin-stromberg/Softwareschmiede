# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Öffentliche statische Seite unter `https://martin-stromberg.github.io/Softwareschmiede/` | Neuer Quellordner `site/` (`index.html`, `site.css`, Assets) + neuer Workflow `pages.yml` (Actions-Deployment, `configure-pages` → `upload-pages-artifact` → `deploy-pages`); einmalige Pages-Aktivierung als dokumentierte Betriebshandlung (Umsetzungsreihenfolge 1, 3, 5) | Akzeptanzszenario 1: grüner `pages.yml`-Lauf + manuelle Sichtprüfung der URL (Anforderung sieht ausdrücklich keine automatisierten Tests vor) | Abgedeckt |
| Modernes, responsives Design mit klarer Typografie, angelehnt ans WPF-Erscheinungsbild | `site/assets/css/site.css`: responsives Layout, CSS Custom Properties, Farbwerte 1:1 aus `DarkTheme.xaml`/`LightTheme.xaml` (Designentscheidungen, Neue Klassen) | Szenarien 1 und 4: manuelle Sichtprüfung inkl. Responsivität (Schritt 5 der Umsetzungsreihenfolge) | Abgedeckt |
| Helle und dunkle Darstellung via `prefers-color-scheme` | Zwei Farbblöcke mit `prefers-color-scheme: light/dark`, Akzente `#4CC2FF`/`#60CDFF` (dark) bzw. `#0078D4`/`#005A9E` (light); kein JS, kein manueller Umschalter (bestätigter Ausschluss 6) | Szenario 4: Sichtprüfung per DevTools-Emulation `prefers-color-scheme` | Abgedeckt |
| Inhalte: Zweck, zentrale Features, Screenshots, Download-/Release-Verweis, Repo-/Doku-Verweise | `index.html` mit Anker-Abschnitten (Hero/Tagline, Features, Screenshots, Download `releases/latest`, Dokumentation/Repository, Footer mit MIT/`SECURITY.md`/`THIRD_PARTY_LICENSES.md`); Inhalte aus `README.md`/`docs/help/index.md` abgeleitet | Szenario 1 (Sichtprüfung aller Abschnitte); Screenshots als Merge-Blocker eingeplant (Entscheidung 3, Seiteneffekte) | Abgedeckt |
| Änderungen an Seiteninhalten führen über den Staging-Workflow zur aktualisierten Auslieferung | Trigger `push` auf `main` mit `paths`-Filter (`site/**`, Workflow-Datei) + `workflow_dispatch`; Auslieferung nur vom `main`-Stand (Feature-Branch → `staging` → Promotion-PR → `main`, Ausschluss 7) | Szenario 3: Inhaltsaktualisierung inkl. `concurrency`-Serialisierung | Abgedeckt |
| Deploy-Mechanismus passend zu bestehenden CI/CD-Workflows | Actions-Deployment aus `site/` (Anwender-Entscheidung 1); `concurrency: group: "pages"`, `cancel-in-progress: false` und Guard-Schritt nach Muster `release.yml`/`build-and-package` | Szenarien 1–3; Workflow-Lauf selbst als Nachweis | Abgedeckt |
| Pflege- und Update-Workflow im Repository dokumentiert | Neuer Abschnitt „GitHub Pages" in `docs/CI_CD.md` (Trigger, Jobs, Permissions, Concurrency, `site/`-Verweis) + Rollout-Schritt + Troubleshooting-Zeile + Drift-Korrektur; Seiten-Verweis in `README.md`; Pflegeablauf in „Programmabläufe → Pflege der Seiteninhalte" beschrieben | Dokumentationsänderungen sind Schritt 4 der Umsetzungsreihenfolge; Nachweis per Review | Abgedeckt |
| Einmalige Pages-Aktivierung (Ist: 404) als Betriebshandlung | Eigener Programmablauf „Einmalige Aktivierung" inkl. `gh api`-Alternative und Verifikation (HTTP 200, `build_type: workflow`); Anwender führt sie durch (Entscheidung 2) | Szenario 2: Fehlerfall ohne Aktivierung, Troubleshooting-Eintrag, Wiederaufnahme per `Re-run failed jobs`/`workflow_dispatch` | Abgedeckt |
| Umgang mit fehlenden Objekten (`site/`, Screenshots, Workflow, Icons) | Scope-Klärung-Tabelle: alles „erzeugen" bzw. Icons „wiederverwenden" (Kopie aus `src/Softwareschmiede.App/images/`); konkrete Dateien in „Neue Klassen" gelistet | Guard-Schritt in `pages.yml` (prüft `index.html` + Favicon) + Szenario 5 (kontrollierter Abbruch bei fehlendem Pflicht-Artefakt) | Abgedeckt |
| Scope-Ausschlüsse (statisch, keine Auto-Screenshots, nur Deutsch, kein Blog, kein Anwendungscode, kein CNAME/Tracking, kein Deploy von Feature-Branches/`staging`) | Alle 7 Ausschlüsse als Tabelle mit Begründung und Status `bestätigt (2026-11-17, Anwender)` übernommen | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Aufruf der Pages-URL durch einen Besucher (externe Website, kein WPF-UI-Fluss) | Kein E2E-Test geplant | Nicht erforderlich mit Begründung: Die Anforderung berührt keinen Benutzerfluss innerhalb der WPF-Anwendung; FlaUI/E2E-Infrastruktur kann eine GitHub-Pages-Auslieferung nicht sinnvoll abdecken. Die Anforderung selbst sieht ausdrücklich keine automatisierten Tests für die Seite vor (Prüfung über Review + manuelle Sichtprüfung). Begründung steht in `plan.md` Abschnitt „E2E-Tests"; Nachweis erfolgt über den `deploy-pages`-Lauf mit Page-URL-Output und Akzeptanzszenario 1. |
| Hell/Dunkel-Umschaltung auf der Website | Kein E2E-Test geplant | Nicht erforderlich mit Begründung: ebenfalls externe Website; Nachweis über Szenario 4 (DevTools-Emulation) als manuelle Sichtprüfung. |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- Der neue Abschnitt in `docs/CI_CD.md` sollte neben der Workflow-Beschreibung auch die im Plan unter „Pflege der Seiteninhalte" beschriebenen Schritte enthalten (Inhalte in `site/` editieren, Screenshots unter `site/assets/screenshots/` austauschen, lokale Vorschau per `file://`), damit die geforderte dokumentierte Pflegeanleitung lückenlos nachweisbar ist.
- Der Guard-Schritt prüft `site/index.html` und das Favicon, nicht jedoch die in `index.html` referenzierten Screenshot-Dateien — fehlende PNGs würden eine Seite mit defekten Bildern deployen. Das Risiko ist über den dokumentierten Merge-Blocker (Anwender liefert Screenshots, Entscheidung 3) abgedeckt; optional könnte der Guard auf die referenzierten Assets erweitert werden.
- Self-Hosting-Regel ist im Plan korrekt verankert: Screenshots werden ausschließlich durch den Anwender aus einer laufenden App-Instanz erzeugt, nicht durch den Agenten (Seiteneffekte/Risiken).
- Betroffene bestehende Tests: keine — korrekt, da keine Anwendungscode-Änderung erfolgt. Die in der Bestandsaufnahme dokumentierten Testbasis-Befunde (1 Clipboard-Fehlschlag in der Sandbox, nicht ausgeführte `OsInterface`-Lanes) sind von der Änderung unberührt.
