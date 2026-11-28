# Umsetzungsplan: Frischeres UI-Design für die Status-Webseite — Design-Entwurf als Mockups (Issue #500)

## Übersicht

Ergebnis dieses Laufs sind eigenständige, JS-freie HTML/CSS-Mockups aller zwölf
Seiten der Status-Webseite (neun Ansichten inkl. der drei Zwischen-/Ergebnisseiten
„Benutzer löschen", „Token widerrufen", „Update wird installiert") unter einem
gemeinsamen neuen Design-System auf Token-Basis — jeweils gerendert in Light- und
Dark-Variante. Pro Ansicht wird genau ein Screenshot erzeugt, der beide Schemata
nebeneinander zeigt (Composite, insgesamt ≤16 PNGs inkl. wichtiger Nebenzustände
und zwei Mobil-Nachweisen); die Screenshots werden als Kommentar im Issue zur
Abnahme vorgestellt. Produktivcode (`PageShell`, `StatusPageComposer`,
`AuthPagesComposer`, `AdminPagesComposer`, Endpunkte, Datenmodell) bleibt in
diesem Lauf unverändert — die Code-Umsetzung ist ein späterer, separater Schritt
nach Anwender-Bestätigung.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert
aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine server-seitig gerenderte Ansicht (HTML-Seite) des StatusServers als statisches Mockup — zwölf Seiten: Status-Übersicht `/`, Login `/login`, Registrierung `/register`, Profil `/profile`, Benutzerverwaltung `/admin/users`, Bestätigungsseite „Benutzer löschen", Token-Verwaltung `/admin/tokens`, Bestätigungsseite „Token widerrufen", Einstellungen `/admin/settings`, Ergebnisseite „Update wird installiert", Zugriff verweigert `/access-denied`, Formular-abgelaufen-Fehlerseite. |
| Auslösende Ereignisse | Einmalige Erstellung in diesem Lauf; Auslöser der nächsten Stufe (Code-Umsetzung) ist die Anwender-Bestätigung im Issue-Kommentar. |
| Erwartete Wirkung | Ablage der Mockup-Artefakte (HTML + geteiltes CSS) im langlebigen Repo-Ort `docs/design/status-webseite/`, dazu ≤16 Composite-Screenshots (Light + Dark nebeneinander) unter `docs/design/status-webseite/screenshots/`, bereitgestellt für den Issue-Kommentar zur Abnahme. Keine Änderung am laufenden Server und kein Produktivcode-Diff. |
| Umgang mit fehlenden Objekten | **Beispiel-/Demodaten:** in den Mockups plausibel zu **erzeugen** (mehrere App-Instanzen inkl. stale/offline, Wartelisten, SCM-Ereignisse, GitHub-Sektionen inkl. Abruffehler, Benutzer-/Token-Tabellen, letzter-Admin-Schutz, verwaiste Update-Sperre). **Design-Tokens:** bestehendes Token-System aus #489 (`PageShell.ThemeCss`, ~40 Custom Properties, `data-theme`-Mechanik) ist zu **wiederverwenden** — Mockup-CSS nutzt dieselben Token-Namen bzw. nur ergänzend benannte Tokens, die 1:1 mappbar sind; keine Inline-Farbwerte. **Registrierungs-Flow:** Login-Mockup zeigt bewusst keinen Registrierungs-Link (Anwender-Entscheidung); der Flow „kein Benutzerkonto → `/register`" ist als Verhalten bereits vorhanden bzw. als Hinweis für die Umsetzung dokumentiert. **Headless-Chrome:** als Screenshot-Werkzeug **wiederverwenden** (`C:\Program Files\Google\Chrome\Application\chrome.exe`, im System vorhanden — Verfügbarkeit in Schritt 1 zu testen). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Code-Umsetzung: `PageShell`, `StatusPageComposer`, `AuthPagesComposer`, `AdminPagesComposer`, Endpunkte und Datenmodell bleiben unverändert; keine Unit-/E2E-Tests in diesem Lauf. | Anwender-Entscheidung: Entwurf ist Zwischenschritt; Umsetzung erst nach Bestätigung der Screenshots. | bestätigt (Anwender-Antwort Issue-Kommentar, Ergebnisumfang) |
| 2 | Kein JavaScript und kein clientseitiger Zustand in den Mockups — Interaktionen ausschließlich über HTML-Links und POST-Formulare; Bestätigungen als eigene Zwischenseiten, keine modalen Dialoge. | Harte technische Rahmenbedingung des StatusServers (server-gerendert, JS-frei); Mockups müssen unter derselben Einschränkung gestaltet sein. | offen |
| 3 | Kein Registrierungs-Link auf der Login-Seite. | Anwender-Entscheidung (Punkt 5 der Issue-Antworten). | bestätigt (Anwender-Antwort Issue-Kommentar) |
| 4 | Keine neuen Funktionen: Funktionsumfang je Ansicht ist exakt der Ist-Funktionsumfang; der Entwurf gestaltet nur um. | Issue-Vorgabe: „frischeres UI-Design", kein Feature-Ausbau. | offen |
| 5 | Nebenzustände werden nicht als eigene Screenshots je Zustand gezeigt, sondern in die Screenshots der jeweiligen Ansicht integriert (letzter-Admin-Schutz, verwaiste Sperre, GitHub-Abruffehler in der befüllten Ansicht, `.msg err`-Fehlerbox im Login, `.msg`-Erfolgsbox im Profil; Leer-Zustände nur für Status-Übersicht und Token-Verwaltung separat). | Bild-Budget ≤16 Dateien; Nebenzustände sind layoutgleich und verlieren als Einzelbild keinen Erklärwert. | offen |
| 6 | Keine Darstellung weiterer Viewport-Größen außer Desktop (Composite-Breite) und zwei Mobil-Nachweisen (Status-Übersicht, Benutzerverwaltung). | Mobil-Nachweis der zwei tabellenlastigsten Ansichten genügt als Umbauchnachweis; übrige Ansichten folgen demselben Tabellen-/Formular-Muster. | offen |
| 7 | `GET /status.json` (maschinelle JSON-Variante) und der SignalR-Hub `/relay` werden nicht gestaltet. | Keine HTML-Ansichten; gehören nicht zur im Issue benannten Ansichtsliste. | offen |
| 8 | Kein Google Stitch als Werkzeug — die Mockups werden direkt als HTML/CSS erstellt. | Verbindlich ist das Ergebnis (Mockups + Screenshots); direkte HTML/CSS-Erstellung ist gleichwertig und exakter an das Token-System koppelbar. | offen |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ablageort der Mockups | `docs/design/status-webseite/` (HTML-Dateien + geteiltes `theme.css`/`site.css`), Screenshots in `docs/design/status-webseite/screenshots/` | Anwender-Vorgabe (langlebiger Referenzort für die spätere Umsetzung); passt zur Repo-Praxis (`docs/features/task/issue-488-…/e2e-artifacts/` als Präzedenzfall für HTML-Artefakte). |
| Theme-Varianten je Ansicht | Eine kanonische HTML-Datei je Seite/Zustand mit `data-theme="light"` am `<html>`-Tag; die Dark-Variante entsteht als generierte Kopie (`*.dark.html`, Attribut auf `data-theme="dark"` getauscht) über ein kleines Generator-Skript im Design-Verzeichnis. | Eine Quelldatei je Ansicht verhindert Drift zwischen Light- und Dark-Markup; die generierte Variante entspricht exakt dem Produktivmechanismus (`data-theme`-Attribut steuert die Custom Properties). |
| Composite-Screenshot (Light + Dark nebeneinander) | Generierte Wrapper-Datei `*.composite.html` mit zwei `<iframe>` nebeneinander (links `*.light`/kanonisch, rechts `*.dark`), je Ansicht ein Headless-Chrome-Screenshot der Wrapper-Datei. | JS-frei, kein Bildbearbeitungswerkzeug nötig; garantiert pixelgleichen Markup-Inhalt in beiden Hälften. |
| Token-System der Mockups | Geteiltes `theme.css`, das die Token-Namen aus `PageShell.ThemeCss` übernimmt (`--bg`, `--fg`, `--surface`, `--border*`, `--accent*`, `--danger*`, `--ok-*`, `--err-*`, `--warn-*`, `--pause-*`, `--badge-*`, `--src-*`, `--pill-*` u. a.) und neue Werte/Paletten in denselben Namen plus dokumentierte Ergänzungstokens definiert; Light als `:root`-Default, Dark unter `:root[data-theme="dark"]`. | 1:1-Mappbarkeit auf das mit #489 eingeführte Token-System ist Rahmenbedingung; die Umsetzungsphase kann `ThemeCss`/`DarkThemeVars` direkt aus dem Mockup-CSS ableiten. |
| Komponenten-CSS | Geteiltes `site.css` mit den neuen Komponenten (Layout, Nav, Karten, Tabellen mit Mobil-Umbruch, Pills/Badges, Formulare, `.msg`-Boxen, Danger-Buttons) — bewusst NICHT an den heutigen `PageShell.Css`-Klassennamen gebunden, aber strukturell vergleichbar. | Volle Layout-Freiheit (Anwender-Vorgabe), aber Komponenten bleiben den serverseitig renderbaren Mustern folgen (section/table/pill/form), damit die Composer-Umsetzung direkt möglich ist. |
| Beispieldaten | Plausible deutsche Beispielinhalte: zwei App-Instanzen („build-server-01" online, „laptop-martin" stale/offline), ein GitHub-Repo fehlerfrei + eins mit Abruffehler, drei Benutzer (letzter Admin geschützt, gesperrter, deaktivierter Überwacher), zwei Tokens (gültig + widerrufen) mit Einmal-Klartext-Block, Einstellungen mit verwaister Sperre + Fehler-Pill, Meldungsboxen (`.msg err` mit Sperr-Hinweis inkl. Restdauer im Login, `.msg`-Erfolg im Profil). | Ein einziges befülltes Szenario je Ansicht deckt die geforderten Nebenzustände ab, ohne die Dateianzahl zu sprengen. |

## Programmabläufe

Beschreibung der Arbeitsabläufe dieses Design-Laufs (kein Server-Laufzeitverhalten —
der Server bleibt unverändert).

### Design-System aufsetzen

1. Verzeichnis `docs/design/status-webseite/` anlegen.
2. `theme.css` schreiben: Light-Palette als `:root`-Default, Dark-Palette unter
   `:root[data-theme="dark"]` (Mockup nutzt direktes Attribut — die
   `prefers-color-scheme`-Media-Query aus `PageShell.ThemeCss` ist für die
   Umsetzung als Referenz zu dokumentieren, im Mockup steuert das `data-theme`-
   Attribut explizit); alle Farben ausschließlich als Custom Properties,
   Token-Namen kompatibel zu `PageShell.ThemeCss`.
3. `site.css` schreiben: neues Layout (Seitencontainer, Header, `nav`, Karten,
   Tabellen mit `overflow-wrap`-Umbruch und Media-Query für schmale Viewports,
   Pills/Badges, Formulare, `.msg`/`.msg.err`, `.token-once`-Hinweisblock,
   `.empty`-Leerzustand, `.stale-note`-Warnbox, Danger-Buttons/Links).

Beteiligte Artefakte: `docs/design/status-webseite/theme.css`,
`docs/design/status-webseite/site.css`

### Mockup-HTML je Ansicht erstellen

1. Pro Seite/Zustand eine kanonische HTML-Datei (vollständiges Dokument:
   `<!doctype html><html lang="de" data-theme="light">`, Viewport-Meta,
   `<link>` auf `theme.css` + `site.css`) mit dem jeweiligen Ist-Funktionsumfang
   gemäß inventory-Detaildokumenten — Links/Form-Actions mit denselben Routen wie
   produktiv (auf `{BasePath}/…` mappbar, im Mockup als relative Pfade),
   Hidden-Antiforgery-Feld in jedem Formular, POST-Formulare für Mutationen,
   `action-link danger`-Links zu Bestätigungsseiten, deaktivierte Schutz-Aktionen
   mit `title`-Tooltip.
2. Dateiliste (14 kanonische Dateien): `status.html` (befüllt: zwei Instanzen
   inkl. stale/offline + `dim`-Sektionen, GitHub-Repo + GitHub-Abruffehler-
   Hinweis), `status-leer.html`, `login.html` (inkl. `.msg err`-Fehlerbox
   „Das Konto ist gesperrt. Die Sperrung endet automatisch nach einer Stunde
   oder kann durch die Administration aufgehoben werden." — deckt zugleich den
   Nebenzustand „Konto gesperrt inkl. Restdauer" ab), `register.html`,
   `profil.html` (inkl. `.msg`-Erfolgsbox „Das Passwort wurde geändert." —
   das `?msg=`-Muster), `admin-benutzer.html` (inkl. letzter-Admin-Pill +
   deaktivierte Schutz-Aktionen, gesperrter + deaktivierter Benutzer),
   `admin-benutzer-loeschen.html`,
   `admin-tokens.html` (inkl. `.token-once`-Klartextblock + Tabelle mit gültigem
   und widerrufenem Token), `admin-tokens-leer.html`,
   `admin-token-widerrufen.html`, `admin-einstellungen.html` (inkl. verwaister
   Installationssperre mit Reset-Formular + Fehler-Pill),
   `admin-update-installation.html`, `zugriff-verweigert.html`,
   `formular-abgelaufen.html`.

Beteiligte Artefakte: `docs/design/status-webseite/*.html` (14 kanonische Dateien)

### Dark-Varianten und Composites erzeugen

1. Generator-Skript `docs/design/status-webseite/generate-previews.ps1`
   (PowerShell, keine neuen Abhängigkeiten): je kanonischer Datei
   `name.html` → `name.dark.html` (Tausch `data-theme="light"` → `"dark"`) und
   `name.composite.html` (Wrapper mit zwei nebeneinander liegenden `<iframe>`,
   links `name.html`, rechts `name.dark.html`, ohne Border, je 50 % Breite).
2. Generierte Dateien sind Build-Artefakte — kanonisch bleiben die Light-Dateien
   und die beiden CSS-Dateien.

Beteiligte Artefakte: `docs/design/status-webseite/generate-previews.ps1`,
generierte `*.dark.html` / `*.composite.html`

### Screenshots rendern und sichtprüfen

1. Verfügbarkeit des Headless-Chrome testen:
   `"C:\Program Files\Google\Chrome\Application\chrome.exe" --headless --version`.
2. Je Composite-Datei ein PNG erzeugen:
   `chrome.exe --headless --screenshot="<absoluter Pfad>\screenshots\<name>.png" --window-size=1920,<h> --hide-scrollbars "file:///<absoluter Pfad>/<name>.composite.html"` —
   Fensterhöhe so wählen, dass der Seiteninhalt vollständig sichtbar ist (Viewport-
   Screenshot, kein Full-Page-Flag; Höhe ggf. in 1–2 Iterationen anpassen).
3. Zwei Mobil-Nachweise: dieselben Composite-Dateien `status.composite.html` und
   `admin-benutzer.composite.html` mit schmalem Fenster (z. B. `--window-size=920,<h>`,
   je Pane ~450 px) rendern → `status-mobil.png`, `admin-benutzer-mobil.png`.
4. Ergebnis: 14 Ansichts-PNGs + 2 Mobil-PNGs = 16 Dateien unter
   `docs/design/status-webseite/screenshots/`.
5. **Jedes PNG einzeln sichtprüfen** (Bild öffnen/lesen): Layout-Defekte
   (abgeschnittene Inhalte, überlaufende Tabellen, Kontrastprobleme in einem der
   Schemata, fehlerhafte Iframe-Darstellung) → zurück zum CSS/HTML, erneut
   generieren und rendern.

Beteiligte Artefakte: `docs/design/status-webseite/screenshots/*.png` (16 Dateien)

### Bereitstellung und Issue-Kommentar

1. Kurzes `README.md` im Design-Verzeichnis: Struktur (kanonische Light-Dateien,
   generierte Dark-/Composite-Dateien), Regenerations- und Screenshot-Befehle,
   Hinweis auf Token-Mapping zu `PageShell.ThemeCss` sowie den bekannten
   Randfall `GET /login` (siehe Seiteneffekte/Hinweis für Umsetzungsphase).
2. PNGs aus `screenshots/` als Bildanhänge im Issue-Kommentar vorstellen (je
   Ansicht ein Bild, kurze Erläuterung der gezeigten Zustände) — Abnahme über
   Kommentardiskussion.

Beteiligte Artefakte: `docs/design/status-webseite/README.md`, Issue-Kommentar

## Neue Klassen

Keine — Design-Lauf ohne Produktivcode. Neu zu erstellende Artefakte stattdessen:

| Artefakt | Typ | Zweck |
|----------|-----|-------|
| `docs/design/status-webseite/theme.css` | CSS | Neue Light-/Dark-Paletten als Custom Properties auf den Token-Namen aus `PageShell.ThemeCss`. |
| `docs/design/status-webseite/site.css` | CSS | Komponenten-/Layout-Styles aller Mockups. |
| `docs/design/status-webseite/*.html` (14 kanonische) | HTML | Mockups der zwölf Seiten + 2 Leer-Zustände. |
| `docs/design/status-webseite/generate-previews.ps1` | Skript | Erzeugt `*.dark.html`- und `*.composite.html`-Dateien aus den kanonischen Mockups. |
| `docs/design/status-webseite/screenshots/*.png` (16) | Bild | Composite-Screenshots für den Issue-Kommentar (inkl. 2 Mobil-Nachweise). |
| `docs/design/status-webseite/README.md` | Doku | Struktur-, Regenerations- und Umsetzungshinweise. |

## Änderungen an bestehenden Klassen

Keine — `PageShell`, `StatusPageComposer`, `AuthPagesComposer`,
`AdminPagesComposer`, Endpunkte, Modelle und Tests bleiben in diesem Lauf
unverändert.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — die Mockups verwenden exakt die vorhandenen Formularfelder und
HTML-Validierungsattribute (`required`, `minlength="8"`,
`pattern="[a-zA-Z0-9._@+-]+"`, `min`/`max` der Sitzungsdauer, `maxlength="200"`
des Token-Labels), ohne neue Eingaben oder Regeln einzuführen.

## Konfigurationsänderungen

Keine — `Page:RefreshSeconds`, `ThemePreference`-Verhalten und alle
Server-Optionen bleiben unverändert (Meta-Refresh der Status-Übersicht wird im
Mockup nur als Meta-Tag/Design-Hinweis abgebildet).

## Seiteneffekte und Risiken

- **Produktivcode:** Keine bekannten Seiteneffekte — es wird kein Server-Code
  geändert; Build und Tests bleiben unberührt (Baseline: StatusServer-Tests
  165/165 grün, siehe `inventory/tests.md`).
- **Headless-Chrome-Rendering:** Viewport-Screenshots schneiden bei zu kleiner
  `--window-size`-Höhe Inhalte ab; Iframe-Höhen im Composite müssen zum Inhalt
  passen → Risiko wird durch die Pflicht-Sichtprüfung jedes PNGs abgefangen
  (Korrekturschleife statt stiller Abnahme).
- **Abweichung Mockup ↔ späterem Markup:** Die Mockups definieren Layout neu;
  35 bestehende StatusServer-Tests prüfen wörtliche Markup-Fragmente
  (`inventory/tests.md` → „Für das Redesign relevante Markup-Assertions") und
  werden in der Umsetzungsphase angepasst werden müssen — bewusst, kein Risiko
  dieses Laufs.
- **Hinweis für die Umsetzungsphase (Randfall, kein offener Punkt):** Direkter
  `GET /login` ohne Benutzerkonto rendert heute das Login-Formular; die
  Weiterleitung nach `/register` greift nur über
  `StatusServerCookieEvents.RedirectToLogin` bei geschützten Seiten
  (`Security/StatusServerCookieEvents.cs:113-122`, `AuthEndpoints.cs:97-106`).
  Für die Mockups ohne sichtbare Konsequenz, bei der Code-Umsetzung zu prüfen.

## Umsetzungsreihenfolge

1. **Headless-Chrome-Verfügbarkeit testen**
   - Voraussetzungen: Keine (Chrome unter
     `C:\Program Files\Google\Chrome\Application\chrome.exe` laut Rahmenbedingung
     vorhanden — trotzdem vorab mit `--version` verifizieren, da die gesamte
     Screenshot-Pipeline davon abhängt).
   - Beschreibung: `chrome.exe --headless --version` ausführen; bei Nichtverfügbarkeit
     vor Fortsetzung abstimmen.

2. **Design-System: `theme.css` + `site.css` anlegen**
   - Voraussetzungen: `docs/design/` anlegen; Token-Liste aus
     `inventory/pages-shell.md` (vorhanden).
   - Beschreibung: Neue Farbwelt als Custom Properties auf den bekannten
     Token-Namen (Light `:root`-Default, Dark `data-theme="dark"`), dazu alle
     Komponenten-Styles. Keine Inline-Farbwerte, keine Literal-Hexfarben außerhalb
     der Token-Blöcke.

3. **Kanonische Mockup-HTML-Dateien erstellen (14 Dateien)**
   - Voraussetzungen: Schritt 2 (CSS vorhanden); Funktionsumfang je Ansicht aus
     `inventory/status-page.md`, `inventory/auth-pages.md`,
     `inventory/admin-pages.md`, Routen aus `inventory/endpoints.md` (vorhanden).
   - Beschreibung: Pro Seite ein vollständiges, JS-freies HTML-Dokument mit
     Beispieldaten und den geplanten Nebenzuständen (Liste siehe Programmablauf
     „Mockup-HTML je Ansicht erstellen").

4. **Generator-Skript `generate-previews.ps1` + Dark-/Composite-Dateien erzeugen**
   - Voraussetzungen: Schritt 3 (kanonische Dateien vorhanden).
   - Beschreibung: Skript schreiben und ausführen; je Datei `*.dark.html`
     (Theme-Tausch) und `*.composite.html` (Iframe-Paar) erzeugen.

5. **Screenshots rendern (16 PNGs)**
   - Voraussetzungen: Schritte 1 + 4 (Chrome verfügbar, Composites erzeugt);
     Verzeichnis `screenshots/` anlegen.
   - Beschreibung: 14 Composite-Dateien in Desktop-Breite + 2 Mobil-Renderings
     (`status`, `admin-benutzer`) per Headless-Chrome als PNG ablegen.

6. **Sichtprüfung jedes PNGs + Korrekturschleife**
   - Voraussetzungen: Schritt 5.
   - Beschreibung: Jedes Bild einzeln öffnen und auf Layout-Defekte prüfen
     (vollständiger Inhalt, kein Überlauf, Kontrast in beiden Schemata, korrekte
     Light/Dark-Zuordnung); bei Defekten zurück zu Schritt 2/3, regenerieren,
     erneut rendern.

7. **`README.md` im Design-Verzeichnis + Issue-Kommentar bereitstellen**
   - Voraussetzungen: Schritt 6 (geprüfte PNGs vorhanden).
   - Beschreibung: Kurzdoku (Struktur, Regeneration, Token-Mapping, Randfall-
     Hinweis `GET /login`); PNGs als Kommentar im Issue vorstellen (Anhänge je
     Ansicht mit Kurzbeschreibung).

## Tests

### Neue Tests

Keine automatisierten Tests — der Lauf ändert keinen Produktivcode und liefert
statische Design-Artefakte. Der Funktionsnachweis ist die gerenderte und einzeln
sichtgeprüfte Screenshot-Menge (16 PNGs). Prüfkriterien je Screenshot:

| Prüfpunkt | Gilt für |
|-----------|----------|
| Light- und Dark-Variante nebeneinander, identischer Inhalt, kein Theme-Farbrest in der falschen Hälfte | alle 14 Ansichts-PNGs |
| Keine abgeschnittenen Inhalte (Fensterhöhe ausreichend), keine Iframe-Scrollbars-Artefakte | alle PNGs |
| Tabellenumbauche auf schmalem Viewport lesbar (`overflow-wrap`, keine horizontale Überlappung) | `status-mobil.png`, `admin-benutzer-mobil.png` |
| Nebenzustände sichtbar: Leer-Zustand + Anleitung (status-leer), `.token-once`-Klartextblock (admin-tokens), letzter-Admin-Pill + deaktivierte Buttons (admin-benutzer), verwaiste Sperre + Reset + Fehler-Pill (admin-einstellungen), GitHub-Abruffehler-Hinweis + stale/offline-Instanz (status) | jeweils benannte PNGs |
| Meldungsboxen sichtbar: `.msg err`-Fehlerbox mit Sperr-Hinweis inkl. Restdauer in `login.html`, `.msg`-Erfolgsbox „Das Passwort wurde geändert." in `profil.html` | `login.png`, `profil.png` |
| JS-Freiheit: kein `<script>`-Tag in kanonischen Dateien; jede Mutation als `form method="post"` mit Hidden-Antiforgery-Feld | alle kanonischen HTML-Dateien (textuelle Prüfung) |
| Keine Inline-Farbwerte (`style=` ohne Farben) bzw. keine Literal-Hexfarben außerhalb von `theme.css` | alle kanonischen HTML-Dateien + `site.css` |

### Betroffene bestehende Tests

Keine — in diesem Lauf wird kein Code geändert; die Baseline bleibt grün.

Referenz für die spätere Umsetzungsphase (kein Planungsgegenstand dieses Laufs,
in `inventory/tests.md` dokumentiert): 35 StatusServer-Tests prüfen wörtliche
Markup-Fragmente — u. a. `action=`/`href=`-Pfade, `class="msg"`/`"msg err"`,
`__RequestVerificationToken`-Feld pro Formular, `<button`-Verbot auf `/`,
`disabled`-Regex-Muster, `data-theme`-Attribut und Theme-CSS-Selektoren,
`NotContain("style=")`, `white-space: nowrap`-Einschränkung, Klassen-Strings
(`badge online|offline`, `inst on-dark`, `action-link danger`, `token-once`)
sowie wortgleiche deutsche UI-Texte. Diese Assertions bilden den Funktionsvertrag,
den das umgesetzte Redesign erhalten muss bzw. der dann anzupassen ist.

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich — Begründung: Der Lauf berührt keinen
ausführbaren Benutzerfluss der Anwendung; das Ergebnis sind statische,
nicht-lauffähige Design-Artefakte plus Screenshots. Der „Benutzerfluss" dieses
Laufs (Entwurf betrachten und abnehmen) findet außerhalb der Anwendung im
Issue-Kommentar statt und ist durch die Sichtprüfung nachgewiesen. E2E-Abdeckung
des umgestalteten Renderings ist Gegenstand der späteren Umsetzungsphase
(Anpassung der dokumentierten Markup-Contract-Tests in
`server/Softwareschmiede.StatusServer.Tests`).

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: vollständiger Design-Entwurf erstellt und zur Abnahme bereitgestellt | Auftrag dieses Laufs | `docs/design/` existiert nicht; kein Design-Artefakt vorhanden; Headless-Chrome auf dem System installiert | Design-System aufsetzen → 14 kanonische Mockups → Dark-/Composite-Generierung → 16 Screenshots → Sichtprüfung je PNG → README + Issue-Kommentar | `docs/design/status-webseite/` enthält HTML/CSS-Mockups aller zwölf Seiten; `screenshots/` enthält 16 geprüfte PNGs (je Ansicht Light + Dark nebeneinander + Nebenzustände + 2 Mobil-Nachweise); Issue-Kommentar zeigt die Bilder zur Diskussion | Dateiliste im Repo + einzeln geöffnete PNGs ohne Layout-Defekte + Issue-Kommentar |
| 2 | Layout-Defekt in einem Screenshot | Sichtprüfung (Schritt 6) | Gerenderte PNGs liegen vor; mind. ein PNG zeigt Defekt (Abschnitt, Überlauf, Kontrastproblem) | Defekte Datei identifizieren → CSS/HTML korrigieren → Generierung + Rendering wiederholen → erneute Prüfung | Korrigiertes PNG ohne den Defekt; andere PNGs unverändert gültig | Gegenprüfung des betroffenen PNGs |
| 3 | Headless-Chrome nicht verfügbar (fehlende Voraussetzung) | Verfügbarkeitstest Schritt 1 | Frischer Ausgangszustand; `chrome.exe` unter dem erwarteten Pfad fehlt | Pflichtenheft-Halt: kein stiller Fallback auf ungetestete Werkzeuge — dem Anwender melden und Alternativen klären (anderer Chrome-Pfad, Edge `--headless`, Firefox `-screenshot`) | Warten auf Klärung; Fortsetzung des Ablaufs ab Schritt 5 sobald ein getestetes Werkzeug benannt ist | Dokumentierte Abstimmung; Screenshot-Erzeugung mit benanntem Werkzeug |
| 4 | Nebenzustand im Screenshot nicht erkennbar | Sichtprüfung oder Anwender-Rückmeldung im Issue | PNG vorhanden, aber gefordertes Detail (z. B. letzter-Admin-Schutz, Einmal-Klartext) nicht sichtbar/verständlich | Betroffenes kanonisches Mockup um fehlenden Zustand ergänzen bzw. Beispieldaten anpassen → regenerieren → erneut rendern und prüfen | PNG zeigt den Nebenzustand eindeutig | Gegenprüfung + Issue-Kommentar-Update |

## Offene Punkte

Keine.
