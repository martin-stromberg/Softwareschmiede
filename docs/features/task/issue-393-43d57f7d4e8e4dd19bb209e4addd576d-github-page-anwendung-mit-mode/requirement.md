# Übersetzte Anforderung: GitHub Page „Softwareschmiede" (Issue #393)

**Quelle:** `issue.md` (Aufgabe `43d57f7d-4e8e-4dd1-9bb2-09e4addd576d`), GitHub Issue #393
**Branch:** `task/issue-393-43d57f7d4e8e4dd19bb209e4addd576d-github-page-anwendung-mit-mode`

## Fachliche Zusammenfassung

Für das Repository `martin-stromberg/Softwareschmiede` wird eine öffentliche, statische Projektseite über GitHub Pages bereitgestellt, die die Anwendung mit modernem, responsivem Design (hell/dunkel, an das WPF-Erscheinungsbild angelehnt) präsentiert. Die Seite führt Zweck, zentrale Features, Screenshots sowie Download-/Release- und Dokumentationsverweise. Seiteninhalte und -assets werden im Repository gepflegt und über einen zu den bestehenden CI/CD-Workflows passenden Mechanismus (GitHub-Actions-Deployment oder `docs/`-Ordner) ausgeliefert, sodass die Seite mit der Anwendung mitwachsen kann. Die Pflege und der Update-Workflow sind im Repository dokumentiert.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die statische Projektseite (GitHub-Pages-Site) als versioniertes Artefakt-Bündel — bestehend aus HTML/CSS/JS-Quellen und Assets (Screenshots, Icons), die im Repository liegen und als Ganzes unter der Project-Pages-URL `https://martin-stromberg.github.io/Softwareschmiede/` ausgeliefert werden.
- **Auslösende Ereignisse:**
  - Einmalig: Aktivierung von GitHub Pages im Repository (derzeit **nicht aktiviert** — `GET /repos/martin-stromberg/Softwareschmiede/pages` liefert 404) sowie Auswahl der Quelle (Actions-Deployment vs. Branch/`docs/`).
  - Wiederkehrend: Merge/Push von Seitenänderungen auf `main` (im Staging-Workflow wird die Seite wie jede andere Änderung über Feature-Branch → `staging` → Promotion-PR → `main` ausgeliefert).
  - Optional: manuelles Auslösen des Deploy-Workflows (`workflow_dispatch`) für Aktualisierungen ohne Code-Push.
- **Erwartete Wirkung:**
  - Die URL `https://martin-stromberg.github.io/Softwareschmiede/` liefert öffentlich eine moderne, responsive Seite mit klarer Typografie und konsistentem Stil.
  - Die Seite unterstützt helle und dunkle Darstellung (z. B. via `prefers-color-scheme`), angelehnt an die App-Themes (`DarkTheme.xaml`/`LightTheme.xaml`: dunkle Flächen um `#1C1C1C`/`#252525`, helle Flächen `#FAFAFA`/`#F0F0F0`, Akzentblau `#0078D4`/`#4CC2FF`/`#60CDFF`).
  - Inhalte auf der Seite: prägnante Vorstellung der Anwendung (KI-gestützter Softwareentwicklungs-Workflow als lokale Windows-WPF-Desktopanwendung), zentrale Features (Plugin-basierte SCM-Quellen GitHub/BitBucket/Local Directory, KI-Agenten Claude CLI/GitHub Copilot/Codex/Devin, eingebettetes Terminal, PR-Workflow, GitHub-Ereignisüberwachung, Programmupdate), Screenshots, Download-/Release-Verweis (GitHub Releases, `releases/latest`), Verweise auf Repository und Dokumentation (`README.md`, `docs/help/`).
  - Änderungen an Seiteninhalten/Screenshots im Repository führen über den etablierten Branch-Workflow zu einer aktualisierten Auslieferung.
- **Umgang mit fehlenden Objekten:**
  - GitHub-Pages-Aktivierung: existiert nicht → **einmalig aktivieren** (Repo-Settings → Pages; bei Actions-Deployment `build_type: workflow`, alternativ per `gh api repos/martin-stromberg/Softwareschmiede/pages -X POST -f build_type=workflow` — erfordert Admin-/PAT-Rechte, daher als dokumentierte Betriebshandlung).
  - Seitenquellen-Ordner: existiert nicht → **erzeugen** (z. B. `site/` oder dedizierter Unterordner unter `docs/`; `docs/` ist bereits durch Projektdokumentation `CI_CD.md`, `help/`, `features/` belegt — bei Wahl des `docs/`-Deploys ist zu verhindern, dass interne Dokumente ungewollt mitveröffentlicht werden).
  - Screenshots: existieren derzeit **keine** im Repository → **erzeugen** und als Assets einchecken (manuell aus der laufenden Anwendung; idealerweise helle und dunkle Variante).
  - Deploy-Workflow: existiert nicht → **erzeugen** als neue Datei unter `.github/workflows/` (kein bestehender Workflow wird für Pages wiederverwendet).
  - Favicons/App-Bildmaterial: teils vorhanden (`src/Softwareschmiede.App/images/favicon-hammer-pick.svg`, `Softwareschmiede.ico`) → **wiederverwenden** bzw. in die Assets der Seite kopieren.
- **Scope-Ausschlüsse:**
  - Keine serverseitige/dynamische Funktionalität — rein statische Auslieferung (kein Backend, keine Datenbank, keine Formular-Verarbeitung).
  - Keine automatische Screenshot-Erzeugung aus FlaUI-E2E-Tests — Screenshots werden manuell gepflegt und committed.
  - Keine mehrsprachige Ausgabe — die Seite erscheint in einer Sprache (Annahme: Deutsch, konsistent zu `README.md`/`docs/`; bei Bedarf zu klären).
  - Kein Blog, keine News-Sektion, keine eigene Release-Notes-Darstellung — Verlinkung auf GitHub Releases genügt.
  - Keine Änderung am Anwendungscode (`src/`, `plugins/`) — ausschließlich Repository-Infrastruktur (Seitenquellen, Workflow, Dokumentation).
  - Keine eigene Domain, kein Tracking/Analytics.
  - Kein Verzicht auf den bestehenden Staging-Workflow — die Seite wird **nicht** direkt von Feature-Branches oder `staging` ausgeliefert (Annahme: öffentlicher Auftritt spiegelt ausschließlich den `main`-Stand).

## Betroffene Klassen und Komponenten

Keine Datenmodell-, Logikklassen, Interfaces oder Enums der Anwendung betroffen. Betroffene bzw. neu zu erstellende Repository-Artefakte:

- **Seitenquellen (neu):** statische Dateien — `index.html`, Stylesheet(s), ggf. minimales JS für Theme-Umschaltung — in einem dedizierten Quellordner (Vorschlag: `site/`; Alternative `docs/page/` oder `docs/` als Ganzes, siehe Offene Fragen).
- **Assets (neu/teils wiederverwendet):** Screenshots (hell/dunkel) unter einem Asset-Verzeichnis der Seite; Logo/Icon aus `src/Softwareschmiede.App/images/favicon-hammer-pick.svg` bzw. `Softwareschmiede.ico`.
- **Workflow (neu):** `.github/workflows/pages.yml` (Name frei) — Trigger `push` auf `main` mit `paths`-Filter auf den Seitenordner plus `workflow_dispatch`; Jobs `configure-pages` → `upload-pages-artifact` → `deploy-pages` mit `permissions: pages: write`, `id-token: write` und `concurrency: group: pages`. Bei einem `docs/`-Ordner-Deploy entfällt der Workflow; dann ist die Quelle stattdessen in den Repo-Settings zu hinterlegen.
- **Dokumentation (Erweiterung):** Abschnitt in `docs/CI_CD.md` (neuer Workflow, Trigger, einmalige Pages-Aktivierung als Betriebshandlung analog zum `REVIEW_ISSUE_TOKEN`-Rollout) und/oder eigene Datei (z. B. `docs/github-page.md`) mit Pflegeanleitung (wo liegen Inhalte/Screenshots, wie wird deployed); Verweis/Badge in `README.md` auf die Page.
- **Konfiguration im Repository:** bei Branch-Deploy aus `docs/` ggf. `_config.yml`/`.nojekyll`; bei Actions-Deploy nicht nötig.
- **Tests:** keine automatisierten Tests für die Seite vorgesehen (keine Testinfrastruktur für statisches HTML im Projekt; Prüfung erfolgt über Review und manuelle Sichtprüfung der deployed Page). Als minimaler technischer Guard denkbar — nicht gefordert — ein Workflow-Schritt, der das Vorhandensein der Artefakte prüft (analog zum Publish-Guard in `build-and-package`).

## Implementierungsansatz

- **Empfohlene Variante (Annahme):** GitHub-Actions-Deployment. Begründung: bestehende Workflows (`release.yml`, `staging-ci.yml`) deployen bereits automatisiert nach Merge auf `main`; ein `pages.yml`-Workflow mit `paths`-Filter deployt die Seite genau dann, wenn sich Seiteninhalte ändern, ohne dass jeder `main`-Push ein Pages-Deployment auslöst. Der bisherige `docs/`-Ordner bleibt reine Projektdokumentation und wird nicht öffentlich publiziert.
- **Alternative:** `docs/` (oder ein dedizierter Unterordner) als Pages-Quelle aus `main` ohne eigenen Workflow — einfacher, veröffentlicht aber ggf. interne Markdown-Strukturen über Jekyll-Processing bzw. erfordert `include`/`exclude`-Steuerung via `_config.yml`.
- **Base-Path:** Project Pages laufen unter `/Softwareschmiede/` — alle Asset- und interne Verweise relativ zur Seitenwurzel bzw. mit Base-Path angeben, damit die Seite nicht still unter einer User-Pages-Root-URL gebaut wird.
- **Design:** Anlehnung an die App-Themes — Farbwerte aus `src/Softwareschmiede.App/Themes/DarkTheme.xaml`/`LightTheme.xaml` übernehmen (Akzentblau-Familie, neutrale Flächen), CSS-Custom-Properties mit `prefers-color-scheme: dark` für automatische Hell/Dunkel-Darstellung, optional manueller Umschalter.
- **Inhalte:** Aus `README.md` (Kernfunktionen-Liste, Überblick) und `docs/help/` ableitbar; Release-Verweis auf `https://github.com/martin-stromberg/Softwareschmiede/releases/latest` (passt zum in der Anwendung vorhandenen Update-Mechanismus gegen GitHub-Releases).
- **Aktivierung:** Einmalige Betriebshandlung — Pages-Quelle im Repository auf „GitHub Actions" stellen (oder via `gh api` mit passendem Token). Erst danach kann `deploy-pages` erfolgreich laufen; der Workflow scheitert sonst mit „Pages site failed"/„Not Found".

## Konfiguration

- Keine Anwendungskonfiguration betroffen.
- Repositorium-Ebene (einmalig, Betriebshandlung): Pages-Source = `GitHub Actions` (empfohlen) oder `Deploy from a branch: main /docs`.
- Workflow-Ebene: `permissions: pages: write`, `id-token: write`; `concurrency: group: "pages"`, `cancel-in-progress: false` (Deployments serialisieren, analog `release`-Guard).
- Seiten-Theme: Hell/Dunkel folgt standardmäßig der Systemeinstellung (`prefers-color-scheme`); manueller Umschalter optional (nicht gefordert).

## Offene Fragen

1. **Deployment-Variante:** GitHub-Actions-Deployment aus dediziertem Ordner (Vorschlag `site/`) oder Pages-Quelle „`main` / `docs`"? Bei Letzterem würden die vorhandenen Projektdokumente (`docs/CI_CD.md`, `docs/features/**`, `docs/help/**`) unter der Pages-URL mitveröffentlicht — ist das akzeptabel oder muss ein Unterordner/`_config.yml`-Excludes das verhindern?
2. **Pages-Aktivierung:** Wer führt die einmalige Aktivierung durch (Repo-Settings) bzw. darf das automatisiert per `gh api` mit vorhandenem Token geschehen? Ohne diesen Schritt ist das Akzeptanzkriterium „öffentlich erreichbar" nicht erfüllbar.
3. **Screenshots:** Welche Ansichten sollen gezeigt werden (Vorschlag: Dashboard/Aufgabenliste, Aufgabendetail mit eingebettetem Terminal, Einstellungen)? Werden helle und dunkle Varianten gewünscht? Erstellung erfolgt manuell aus einer laufenden App-Instanz — wer stellt die Screenshots bereit bzw. auf welcher Installationsumgebung?
4. **Sprache der Seite:** Deutsch (konsistent zur README) oder Englisch (größere Reichweite)?
5. **Zeitpunkt der Sichtbarkeit:** Da Feature-Arbeit über `staging` → `main` läuft, wird die Seite erst nach der Promotion auf `main` (+ Pages-Aktivierung) öffentlich — ist ein Zwischen-Deploy (z. B. Preview aus dem Feature-Branch) gewünscht oder reicht der `main`-Stand?
6. **Inhaltsumfang:** Reicht eine einzelne Landing-Page (`index.html` mit Anker-Abschnitten Features/Screenshots/Download/Doku) oder sind mehrere Seiten gewünscht (z. B. eigene Feature-/Dokumentationsseiten)?
