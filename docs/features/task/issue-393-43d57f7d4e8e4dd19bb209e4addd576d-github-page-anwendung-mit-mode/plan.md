# Umsetzungsplan: GitHub Page „Softwareschmiede" (Issue #393)

## Übersicht

Im Repository wird eine öffentliche, statische Projektseite für GitHub Pages aufgebaut: ein neuer Quellordner `site/` mit `index.html`, Stylesheet und Assets (Icons, Screenshots), ein neuer Deploy-Workflow `.github/workflows/pages.yml` (Actions-Deployment, Trigger `push` auf `main` mit `paths`-Filter sowie `workflow_dispatch`) und ergänzende Dokumentation (`docs/CI_CD.md`, `README.md`). Anwendungscode (`src/`, `plugins/`) ist nicht betroffen; die einmalige Aktivierung von GitHub Pages im Repository bleibt eine dokumentierte Betriebshandlung.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Das statische Seiten-Artefakt-Bündel `site/` (HTML/CSS/Assets inkl. Screenshots und Icons), versioniert im Repository und ausgeliefert unter `https://martin-stromberg.github.io/Softwareschmiede/` |
| Auslösende Ereignisse | Einmalig: manuelle Pages-Aktivierung in den Repo-Settings (Source = „GitHub Actions"). Wiederkehrend: `push` auf `main` mit Änderungen unter `site/**` bzw. am Workflow (ausgeliefert über Feature-Branch → `staging` → Promotion-PR → `main`). Optional: `workflow_dispatch`. |
| Erwartete Wirkung | Die Pages-URL liefert eine moderne, responsive, deutschsprachige Landing-Page mit Hell/Dunkel-Darstellung (`prefers-color-scheme`, Farbwerte aus `DarkTheme.xaml`/`LightTheme.xaml`), Inhalten zu Zweck, Features, Screenshots, Download (`releases/latest`), Repository und Dokumentation. |
| Umgang mit fehlenden Objekten | Pages-Aktivierung: **einmalig aktivieren** (Betriebshandlung, Admin-Rechte). `site/`-Quellordner: **erzeugen**. Screenshots: **erzeugen** (manuell aus laufender App, committen). Deploy-Workflow `pages.yml`: **erzeugen** (kein bestehender Workflow wird wiederverwendet). Icons (`favicon-hammer-pick.svg`, `Softwareschmiede.ico`): **wiederverwenden** (in `site/assets/` kopieren). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine serverseitige/dynamische Funktionalität (kein Backend, keine Formulare, kein Tracking/Analytics) | GitHub Pages liefert rein statisch aus; Anforderung verlangt ausdrücklich nur statische Inhalte | bestätigt (2026-11-17, Anwender) |
| 2 | Keine automatische Screenshot-Erzeugung aus FlaUI-E2E-Tests | Expliziter Ausschluss in der Anforderung; Screenshots werden manuell gepflegt und committed | bestätigt (2026-11-17, Anwender) |
| 3 | Keine Mehrsprachigkeit — die Seite erscheint nur auf Deutsch | Konsistent zur durchgängig deutschsprachigen Repository-Dokumentation (`README.md`, `docs/**`); vom Anwender bestätigt | bestätigt (2026-11-17, Anwender) |
| 4 | Kein Blog, keine News-Sektion, keine eigene Release-Notes-Darstellung | Verlinkung auf GitHub Releases genügt laut Anforderung | bestätigt (2026-11-17, Anwender) |
| 5 | Keine Änderung am Anwendungscode (`src/`, `plugins/`) | Anforderung betrifft ausschließlich Repository-Infrastruktur | bestätigt (2026-11-17, Anwender) |
| 6 | Keine eigene Domain (`CNAME`), kein manueller Hell/Dunkel-Umschalter | Ausschluss in der Anforderung; Umschaltung erfolgt automatisch über `prefers-color-scheme` | bestätigt (2026-11-17, Anwender) |
| 7 | Kein Deploy von Feature-Branches oder `staging`, keine Branch-Preview | Öffentlicher Auftritt spiegelt ausschließlich den `main`-Stand; passt zum verbindlichen Staging-Modell | bestätigt (2026-11-17, Anwender) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Deployment-Variante | GitHub-Actions-Deployment aus dediziertem Quellordner `site/` (Artifact-Upload + `deploy-pages`) | Veröffentlicht `docs/**` nicht mit (interne Projektdokumentation bleibt privat), `paths`-Filter deployt nur bei Seitenänderungen, passt zum bestehenden Automatisierungsgrad (`release.yml`, Review-Workflows). Alternative „Deploy from a branch: `main`/`docs`" verworfen, da sie interne Markdown-Strukturen über Jekyll veröffentlichen bzw. `_config.yml`-Excludes erfordern würde. |
| Quellordner | `site/` auf Repository-Wurzelebene | Klare Trennung von `docs/` (Projektdokumentation); einfacher `paths`-Filter `site/**`; kein Jekyll nötig (Artifact-Deploy umgeht Jekyll — daher weder `_config.yml` noch `.nojekyll` erforderlich). |
| Seitentechnologie | Reines HTML + CSS, kein JavaScript, kein Static-Site-Generator | Anforderung verlangt statische Seite ohne dynamische Funktionalität; `package.json` enthält nur semantic-release-Abhängigkeiten, kein Web-Build-Tooling — ein Generator wäre eine unverhältnismäßige neue Infrastruktur. |
| Hell/Dunkel-Darstellung | CSS Custom Properties, zwei Farbblöcke mit `prefers-color-scheme: light/dark`, Farbwerte 1:1 aus `DarkTheme.xaml`/`LightTheme.xaml` | Erfüllt die geforderte Anlehnung an die App-Themes ohne JavaScript; manueller Umschalter ist laut Anforderung nicht gefordert. |
| Basis-Pfad / Verlinkung | Alle internen Verweise relativ (`assets/…`, `./`) bzw. mit Base-Path `/Softwareschmiede/` | Project Pages laufen unter `/Softwareschmiede/`, nicht unter der User-Pages-Root — absolute Root-Pfade (`/…`) würden Assets still ins Leere verlinken; relative Pfade funktionieren zudem bei lokaler Vorschau via `file://`. |
| Dokumentationsort | Erweiterung von `docs/CI_CD.md` (neuer Abschnitt „GitHub Pages") + Verweis in `README.md` | `CI_CD.md` enthält bereits das Muster „Einmalige Betriebshandlungen (Rollout)" (`REVIEW_ISSUE_TOKEN`), an das sich die Pages-Aktivierung nahtlos anlehnt; keine eigene neue Datei nötig. |
| Seitenumfang | Einzelne Landing-Page `index.html` mit Anker-Abschnitten (Features / Screenshots / Download / Dokumentation) | Deckt alle geforderten Inhalte ab; Mehrseitigkeit ist nicht gefordert (offener Punkt 6, Empfehlung: einzelne Seite). |

## Programmabläufe

### Einmalige Aktivierung von GitHub Pages (Betriebshandlung)

1. Repository-Administrator öffnet `Settings` → `Pages` im Repository `martin-stromberg/Softwareschmiede`.
2. Source wird auf „GitHub Actions" gestellt (entspricht `build_type: workflow`; alternativ `gh api repos/martin-stromberg/Softwareschmiede/pages -X POST -f build_type=workflow` mit Admin-Token).
3. Verifikation: `gh api repos/martin-stromberg/Softwareschmiede/pages` liefert danach HTTP 200 mit `build_type: workflow`.
4. Erst danach kann `actions/deploy-pages` erfolgreich laufen — vorher schlägt der Deploy-Job mit „Pages site failed"/„Not Found" fehl.

Beteiligte Komponenten: GitHub-Repo-Settings, `gh api`, dokumentiert in `docs/CI_CD.md`.

### Deployment der Seite (wiederkehrend)

1. Änderungen unter `site/**` oder an `.github/workflows/pages.yml` gelangen über den Staging-Workflow (Feature-Branch → PR nach `staging` → „Pre-Release" → Promotion-PR → manueller Merge) auf `main`.
2. Der `push` auf `main` löst `pages.yml` aus (mit `paths`-Filter); `workflow_dispatch` erlaubt manuelles Auslösen ohne Code-Push.
3. Job `build` (Runner `ubuntu-latest`): `actions/checkout` → Guard-Schritt prüft Vorhandensein von `site/index.html` und `site/assets/favicon-hammer-pick.svg` (Abbruch mit klarer Meldung bei fehlenden Artefakten, analog „Verify publish output" in `build-and-package`) → `actions/configure-pages` → `actions/upload-pages-artifact` mit `path: site`.
4. Job `deploy` (abhängig von `build`, Umgebung `github-pages` mit `url`-Output): `actions/deploy-pages` stellt das Artifact unter `https://martin-stromberg.github.io/Softwareschmiede/` bereit und gibt die Page-URL aus.
5. `concurrency: group: "pages"`, `cancel-in-progress: false` serialisiert Deployments (Muster aus `release.yml`).

Beteiligte Komponenten: `.github/workflows/pages.yml`, `actions/configure-pages`, `actions/upload-pages-artifact`, `actions/deploy-pages`, `sync-staging-with-main.yml` (hält `staging` synchron, unverändert).

### Pflege der Seiteninhalte

1. Autor editiert Inhalte in `site/index.html`/`site/assets/css/site.css` bzw. tauscht Screenshots unter `site/assets/screenshots/` aus.
2. Lokale Vorschau durch direktes Öffnen von `site/index.html` im Browser (relative Pfade machen dies ohne Webserver möglich).
3. Übergabe an den regulären Branch-Workflow wie jede andere Änderung; Deployment erfolgt automatisch nach Erreichen von `main` (siehe vorheriger Ablauf).

Beteiligte Komponenten: `site/`, Staging-Workflow, `pages.yml`.

### Fehlerfall: Deploy ohne aktivierte Pages-Quelle

1. `pages.yml` läuft, Pages ist aber noch nicht aktiviert (Ist-Zustand: `GET …/pages` → 404).
2. Der `deploy`-Job schlägt fehl („Not Found"/„Pages site failed"); die `build`-Phase inkl. Artifact-Upload ist bereits erfolgreich.
3. Lösung: einmalige Aktivierung (siehe oben), danach den Lauf per `Re-run failed jobs` oder `workflow_dispatch` wiederholen — dokumentiert im Troubleshooting-Abschnitt von `docs/CI_CD.md`.

## Neue Klassen

Es werden keine Code-Klassen angelegt (keine Anwendungsänderung). Neu zu erstellende Repository-Artefakte:

| Artefakt | Typ | Zweck |
|----------|-----|-------|
| `site/index.html` | HTML-Datei | Landing-Page mit Anker-Abschnitten: Hero/Tagline, Features, Screenshots, Download (`releases/latest`), Dokumentation/Repository-Verweise, Footer (MIT-Lizenz, `SECURITY.md`, `THIRD_PARTY_LICENSES.md`) |
| `site/assets/css/site.css` | Stylesheet | Responsives Layout, klare Typografie, CSS Custom Properties mit den Farbwerten aus `DarkTheme.xaml`/`LightTheme.xaml`, Umschaltung via `prefers-color-scheme` |
| `site/assets/favicon-hammer-pick.svg` | Asset (kopiert) | Favicon/Logo — Kopie aus `src/Softwareschmiede.App/images/favicon-hammer-pick.svg` (laut `<desc>` ausdrücklich für die Web-Anwendung gedacht) |
| `site/assets/Softwareschmiede.ico` | Asset (kopiert) | Fallback-Icon aus `src/Softwareschmiede.App/images/Softwareschmiede.ico` |
| `site/assets/screenshots/*.png` | Assets (manuell erzeugt) | Screenshots der Anwendung, idealerweise hell und dunkel (Vorschlag: Dashboard/Aufgabenliste, Aufgabendetail mit eingebettetem Terminal, Einstellungen) — Bereitstellung durch den Anwender, siehe Offene Punkte |
| `.github/workflows/pages.yml` | GitHub-Actions-Workflow | Deploy-Workflow: Trigger `push` auf `main` mit `paths: [site/**, .github/workflows/pages.yml]` plus `workflow_dispatch`; Jobs `build`/`deploy`; `permissions: pages: write`, `id-token: write`; `concurrency: group: "pages"`, `cancel-in-progress: false`; Umgebung `github-pages` mit Page-URL |

## Änderungen an bestehenden Klassen

Es werden keine Code-Klassen geändert. Änderungen an bestehenden Repository-Artefakten:

### `README.md` (Dokumentation)

- **Neuer Inhalt:** Verweis auf die Projektseite (`https://martin-stromberg.github.io/Softwareschmiede/`) im Kopfbereich — als Badge (Muster der vorhandenen Workflow-Badges, Zeilen 4–7) oder als Link in der Tagline-Zeile.

### `docs/CI_CD.md` (Dokumentation)

- **Neuer Abschnitt „GitHub Pages":** Beschreibung von `pages.yml` (Trigger, `paths`-Filter, Jobs, Permissions, Concurrency, Umgebung `github-pages`), Deployment-Auslöser im Staging-Modell und Verweis auf den Quellordner `site/`.
- **Erweiterung „Einmalige Betriebshandlungen (Rollout)":** Neuer nummerierter Schritt für die Pages-Aktivierung (Settings → Pages → „GitHub Actions", `gh api`-Alternative, Verifikation, Probelauf per `workflow_dispatch` nach dem ersten `main`-Stand mit dem Workflow).
- **Erweiterung „Troubleshooting-Guide":** Zeile für das Fehlerbild „`deploy-pages` schlägt mit Not Found fehl" → Ursache: Pages nicht aktiviert → Lösung: Betriebshandlung + Re-run.
- **Beiläufige Drift-Korrektur (beim Bearbeiten derselben Datei):** Verweise auf die nicht existierenden Workflows `test.yml`/`pr.yml` (Zeilen 18/28) auf `staging-ci.yml`/`pr-staging-ci.yml` sowie `softprops/action-gh-release` (Zeile 54) auf `gh release create` korrigieren — dokumentierter Befund aus der Bestandsaufnahme.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `site/index.html` | Muss im Artifact-Pfad `site/` vorhanden sein (Guard-Schritt vor `upload-pages-artifact`) | Workflow bricht mit verständlicher Fehlermeldung ab, bevor ein leeres Deployment entsteht |
| `site/assets/favicon-hammer-pick.svg` | Muss im Artifact-Pfad vorhanden sein (gleicher Guard-Schritt) | Ebenso kontrollierter Abbruch |
| Interne Verweise in `index.html` | Relativ zur Seitenwurzel bzw. mit Base-Path `/Softwareschmiede/` — keine absoluten Root-Pfade (`/…`) | Verletzung fällt im Review bzw. bei der manuellen Sichtprüfung der deployten Seite auf (kein automatisierter Check gefordert) |

## Konfigurationsänderungen

Keine Anwendungskonfiguration betroffen. Auf Repositorium-/Workflow-Ebene:

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| Pages-Source im Repository | Repo-Setting (Betriebshandlung) | `GitHub Actions` (`build_type: workflow`) | Voraussetzung für `deploy-pages` |
| `permissions: pages: write`, `id-token: write` | Workflow-Ebene in `pages.yml` | — | Deploy-Berechtigung für `deploy-pages` |
| `concurrency: group: "pages"`, `cancel-in-progress: false` | Workflow-Ebene in `pages.yml` | — | Serialisiert Deployments (Muster aus `release.yml`) |
| `environment: github-pages` | Job-Ebene (`deploy`) | — | Standard-Pages-Umgebung; liefert `url`-Output und Deployment-Historie |

## Seiteneffekte und Risiken

- **GitHub-Umgebung `github-pages`:** Der erste Deploy-Lauf legt automatisch die Umgebung `github-pages` im Repository an (Standardverhalten von `deploy-pages`) — erwünscht, kein Risiko.
- **Fehlende Screenshots zum Merge-Zeitpunkt:** `index.html` referenziert Bilddateien, die erst manuell erzeugt werden müssen. Ohne sie zeigt die deployte Seite fehlende Bilder — die Bereitstellung ist daher als Blocker für den Merge einzuplanen (siehe Offene Punkte), nicht als Nacharbeit.
- **Self-Hosting-Risiko bei Screenshot-Erzeugung:** Screenshots erfordern eine laufende `Softwareschmiede.App.exe` — darf **nicht** durch den Agenten gestartet/beendet werden (Gefahr, die hostende Instanz zu treffen). Erzeugung ausschließlich durch den Anwender.
- **`docs/CI_CD.md`-Drift:** Die zu erweiternde Datei nennt Workflows/Actions, die nicht (mehr) existieren — wird im selben Schritt korrigiert, damit die neue Pages-Dokumentation nicht auf veralteten Angaben aufbaut.
- **Keine Code-Berührung:** Die regulären Test-Lanes und CI-Checks sind fachlich unbetroffen; `pr-staging-ci.yml` prüft `dotnet format` und baut die Solution — ein reiner `site/`+Workflow-PR darf daran nicht scheitern (kein Code-Eingriff = kein neues Risiko, aber die üblichen PR-Gates laufen trotzdem).

## Umsetzungsreihenfolge

1. **`site/`-Struktur und Seiteninhalte anlegen**
   - Voraussetzungen: Keine (kein Build-Tooling, kein NuGet, kein Generator nötig).
   - Beschreibung: `site/index.html`, `site/assets/css/site.css` erstellen; `favicon-hammer-pick.svg` und `Softwareschmiede.ico` aus `src/Softwareschmiede.App/images/` nach `site/assets/` kopieren; Inhalte aus `README.md` (Tagline, Kernfunktionen) und `docs/help/index.md` (Linkziele) ableiten; Farbpalette aus den Theme-Dateien übernehmen; alle internen Verweise relativ.

2. **Screenshots bereitstellen und einbinden**
   - Voraussetzungen: `site/`-Struktur aus Schritt 1; Screenshots vom Anwender (manuell aus laufender App — nicht durch den Agenten).
   - Beschreibung: PNG-Dateien unter `site/assets/screenshots/` ablegen und in `index.html` referenzieren (idealerweise hell + dunkel; Vorschlag: Dashboard, Aufgabendetail mit Terminal, Einstellungen). Externer Blocker, falls noch nicht geliefert — siehe Offene Punkte.

3. **Deploy-Workflow `.github/workflows/pages.yml` erstellen**
   - Voraussetzungen: `site/` aus Schritt 1 vorhanden (sonst schlägt der Guard-Schritt beim ersten Lauf zu Recht fehl); Referenz-Muster aus `release.yml` (Concurrency, Guard).
   - Beschreibung: Trigger (`push` `main` + `paths`-Filter auf `site/**` und die Workflow-Datei, `workflow_dispatch`), Job `build` (Checkout → Artefakt-Guard → `configure-pages` → `upload-pages-artifact` mit `path: site`), Job `deploy` (`needs: build`, `environment: github-pages`, `deploy-pages`), Permissions und Concurrency wie geplant.

4. **Dokumentation ergänzen**
   - Voraussetzungen: Schritte 1–3 (Dokumentation beschreibt den tatsächlichen Stand).
   - Beschreibung: `docs/CI_CD.md` um Abschnitt „GitHub Pages" + Rollout-Schritt + Troubleshooting-Zeile erweitern, beiläufige Drift-Korrektur (`test.yml`/`pr.yml`/`softprops`); `README.md` um Seiten-Verweis im Kopfbereich ergänzen.

5. **Übergabe an Staging-Workflow und Betriebshandlung (außerhalb des Codes)**
   - Voraussetzungen: Schritte 1–4 gemerged bis `main`.
   - Beschreibung: Anwender aktiviert Pages-Source „GitHub Actions" in den Repo-Settings, danach `workflow_dispatch`-Probelauf von `pages.yml` und manuelle Sichtprüfung der URL (hell/dunkel, Responsivität, Links, Screenshots).

## Tests

### Neue Tests

Keine. Es existiert keine Testinfrastruktur für statisches HTML im Projekt, und die Anforderung sieht ausdrücklich keine automatisierten Tests für die Seite vor. Technischer Ersatz-Guard: der Artefakt-Prüfschritt in `pages.yml` (siehe Validierungsregeln). Die fachliche Prüfung erfolgt über Review und manuelle Sichtprüfung der deployten Page.

### Betroffene bestehende Tests

Keine — es gibt keine Anwendungscode-Änderung; sämtliche Testprojekte (`Softwareschmiede.Tests`, `Softwareschmiede.IntegrationTests`) bleiben unberührt.

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich — Begründung: Die Anforderung berührt keinen Benutzerfluss innerhalb der WPF-Anwendung. Die neue „Benutzerinteraktion" ist das Aufrufen einer externen Website im Browser, nicht eine über die getestete UI erreichbare Aktion; FlaUI/E2E-Infrastruktur kann eine GitHub-Pages-Auslieferung nicht sinnvoll abdecken. Der Nachweis des Funktionierens erfolgt über den Workflow-Lauf (`deploy-pages` mit Page-URL-Output) und die manuelle Sichtprüfung des Akzeptanzszenarios 1 durch den Anwender.

Bestehende E2E-Tests müssen nicht angepasst werden: Keine.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Seite öffentlich erreichbar | Merge des Feature-Stands auf `main` (Promotion-PR) | Pages in Repo-Settings aktiviert („GitHub Actions"); `site/` + `pages.yml` auf `main` | `push` auf `main` → `pages.yml` läuft (Build mit Guard → Upload → Deploy) → URL abrufen | `https://martin-stromberg.github.io/Softwareschmiede/` zeigt die Landing-Page mit allen Abschnitten, Icons und Screenshots | Grüner `pages.yml`-Lauf + manuelle Sichtprüfung der URL durch den Anwender |
| 2 | Pages noch nicht aktiviert | Merge auf `main` vor der Betriebshandlung | `GET …/pages` → 404 (heutiger Ist-Zustand) | `build`-Job erfolgreich, `deploy`-Job schlägt mit „Not Found" fehl | Fehlschlag im Actions-Log ist eindeutig; `docs/CI_CD.md` benennt Ursache und Lösung (Aktivierung + Re-run) | Troubleshooting-Eintrag greift; nach Aktivierung + `Re-run failed jobs` läuft Deploy grün |
| 3 | Inhaltsaktualisierung | Änderung an `site/**` erreicht `main` (oder `workflow_dispatch`) | Seite bereits deployed und Pages aktiviert | Push triggert `pages.yml` erneut; Deploy serialisiert über `concurrency`-Gruppe | Aktualisierte Inhalte erscheinen unter derselben URL | `workflow_dispatch`-Probelauf + Sichtprüfung |
| 4 | Hell/Dunkel-Darstellung | Besuch der Seite mit unterschiedlicher System-Einstellung | Seite deployed | Browser mit `prefers-color-scheme: dark` bzw. `light` öffnen | Dark-Variante zeigt Flächen `#1C1C1C`/`#252525` mit Akzent `#4CC2FF`; Light-Variante `#FAFAFA`/`#F0F0F0` mit `#0078D4` | Manuelle Sichtprüfung (DevTools-Emulation `prefers-color-scheme`) |
| 5 | Fehlendes Pflicht-Artefakt | `push` auf `main`, aber `site/index.html` fehlt/verschoben | `pages.yml` vorhanden | `build`-Job erreicht den Guard-Schritt | Kontrollierter Abbruch mit klarer Meldung, bevor ein leeres/defektes Deployment ausgeliefert wird | Workflow-Log zeigt Guard-Fehlermeldung |

## Entscheidungen der Anwender-Abstimmung (2026-11-17)

Alle offenen Punkte wurden dem Anwender vorgelegt und entschieden; alle Scope-Ausschlüsse wurden bestätigt:

| # | Ehemals offener Punkt | Entscheidung |
|---|------------------------|--------------|
| 1 | Deployment-Variante | **Actions-Deployment aus `site/`** — `docs/` bleibt interne Projektdoku |
| 2 | Pages-Aktivierung | **Anwender** führt sie als Repo-Admin durch (Betriebshandlung, dokumentiert in `docs/CI_CD.md`) |
| 3 | Screenshots | **Anwender liefert PNGs** (Vorschlag: Aufgabenliste dunkel, Aufgabendetail mit Terminal dunkel, Einstellungen hell) unter `site/assets/screenshots/`; bleibt Merge-Blocker bis vorhanden |
| 4 | Sprache | **Deutsch** |
| 5 | Sichtbarkeitszeitpunkt | **Nur `main`-Stand**, keine Branch-Preview; lokale Vorschau per `file://` |
| 6 | Inhaltsumfang | **Einzelne Landing-Page** `index.html` mit Anker-Abschnitten |
