# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Mockups aller neun Ansichten inkl. Zwischen-/Ergebnisseiten (12 Seiten: `/`, `/login`, `/register`, `/profile`, `/admin/users` + Löschen-Bestätigung, `/admin/tokens` + Widerrufen-Bestätigung, `/admin/settings` + Update-installiert-Seite, `/access-denied`, Formular-abgelaufen) | 14 kanonische HTML-Dateien gelistet (12 Seiten + 2 Leer-Zustände); Funktionsumfang je Ansicht über inventory-Detaildokumente referenziert (plan.md „Mockup-HTML je Ansicht erstellen") | Je Ansicht ein Composite-PNG + Pflicht-Sichtprüfung (Schritt 6, Akzeptanzszenario 1) | Abgedeckt |
| Jede Ansicht in Light- UND Dark-Variante | Generierte `*.dark.html` je kanonischer Datei (Tausch `data-theme`), Composite zeigt beide Schemata nebeneinander | Prüfkriterium „Light/Dark nebeneinander, kein Theme-Farbrest in der falschen Hälfte" | Abgedeckt |
| ≤16 Composite-Screenshots für den Issue-Kommentar | Exakt 16 PNGs: 14 Ansichten + 2 Mobil-Nachweise (`status`, `admin-benutzer`) | Dateiliste `screenshots/*.png` + Issue-Kommentar in Schritt 7 | Abgedeckt |
| JS-freies Interaktionsmodell: nur Links + POST-Formulare, Bestätigungen als Zwischenseiten, keine modalen Dialoge | Scope-Ausschluss 2 + Mockup-Regeln (Hidden-Antiforgery in jedem Formular, `action-link danger`, POST-Formulare); Bestätigungsseiten als eigene Dateien | Textuelle Prüfung: „kein `<script>`-Tag, jede Mutation als `form method=\"post\"` mit Antiforgery-Feld" | Abgedeckt |
| Design-Tokens kompatibel zu `PageShell.ThemeCss` (Light `:root`-Default, Dark `data-theme="dark"`); keine Inline-Farbwerte | `theme.css` übernimmt Token-Namen (`--bg`, `--fg`, `--surface`, `--accent*`, `--ok-*`, `--err-*`, `--warn-*`, `--badge-*`, `--src-*`, `--pill-*` u. a.); `prefers-color-scheme`-Media-Query als Umsetzungsreferenz dokumentiert | Prüfkriterium „keine Inline-Farbwerte / keine Literal-Hexfarben außerhalb `theme.css`" | Abgedeckt |
| Kein Registrierungs-Link auf der Login-Seite (Anwender-Entscheidung) | Scope-Ausschluss 3 (Status: bestätigt) + Scope-Klärung | Teil der Login-Mockup-Spezifikation | Abgedeckt |
| Voller Ist-Funktionsumfang je Ansicht, keine neuen Funktionen | Scope-Ausschluss 4; Beispieldaten decken alle Elemente der Detailinventur ab (Instanz-Tag, Quellen-Badges, Pills, Wartelisten, SCM-Events, PR-/Issue-Links, Einmal-Klartext, Status-/Update-Tabellen) | Sichtprüfung der befüllten Ansichten | Abgedeckt |
| Nebenzustände: Leer-Zustand, Fehlermeldungen, deaktivierte Aktionen (soweit für Abnahme relevant) | Leer-Zustände (`status-leer`, `admin-tokens-leer`), deaktivierte Aktionen + letzter-Admin-Pill (`admin-benutzer`), Einmal-Klartext (`admin-tokens`), verwaiste Sperre + Fehler-Pill + GitHub-Abruffehler (`admin-einstellungen`, `status`) — **aber: kein Mockup zeigt eine `.msg`-Erfolgs- oder `.msg.err`-Fehlermeldungsbox**, obwohl die Komponente in `site.css` gestaltet wird und die Anforderung „Fehlermeldungen" explizit nennt | Prüfkriterien-Tabelle prüft kein `.msg`/`.msg err` | Lücke |
| Kontrastreiche Badges/Pills/Links in beiden Paletten | Token-basierte Paletten, Composite-Vergleich erzwingt visuellen Abgleich | Sichtprüfung „Kontrastprobleme in einem der Schemata" | Abgedeckt |
| Mobiltauglichkeit (Tabellen-Umbruch auf schmalen Viewports) | 2 Mobil-PNGs mit ~450 px je Pane; `overflow-wrap`- und Media-Query-Anforderung in `site.css` | Prüfkriterium „Tabellenumbauche auf schmalem Viewport lesbar" | Abgedeckt |
| Verifikation Registrierungs-Flow (kein Benutzerkonto → `/register`) | Ist-Stand dokumentiert: `GET /login` rendert auch ohne Konto das Formular; Umleitung nur via `StatusServerCookieEvents` bei geschützten Seiten — als Hinweis für die Umsetzungsphase in README + Seiteneffekte festgehalten | README-Hinweis (kein sichtbarer Mockup-Effekt, korrekt begründet) | Abgedeckt |
| Meta-Refresh der Status-Übersicht ohne Lade-/Blink-Effekte | Als Meta-Tag/Design-Hinweis im Mockup abgebildet (Konfigurationsabschnitt) | Teil des `status.html`-Mockups | Abgedeckt |
| Ablage der Artefakte im Repo + Bereitstellung als Issue-Kommentar | `docs/design/status-webseite/` + `screenshots/` + README; Schritt 7 stellt PNGs im Issue vor | Akzeptanzszenario 1 (Happy Path bis zur sichtbaren Wirkung) | Abgedeckt |
| Kein Produktivcode-/Test-Diff in diesem Lauf | Scope-Ausschluss 1 (bestätigt); „Änderungen an bestehenden Klassen: keine" | Baseline-Verweis auf `inventory/tests.md` (165/165 grün) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] Kein Mockup-Screenshot zeigt eine `.msg`-Erfolgs- oder `.msg.err`-Fehlermeldungsbox (z. B. Login-Fehler „Benutzername oder Passwort ist falsch.", Sperr-Hinweis mit Restdauer, `?msg=`-Erfolgsmeldung im Profil oder in den Admin-Listen). Die Komponente wird in `site.css` gestaltet, ist aber in keiner der 14 Dateien als sichtbarer Zustand geplant und in der Prüfkriterien-Tabelle nicht als Sichtprüfung benannt — die Anforderung nennt „Fehlermeldungen" ausdrücklich als darzustellenden Nebenzustand. Abhilfe ohne Erhöhung des 16er-Budgets: Fehlerbox in ein bestehendes Mockup integrieren (z. B. `login.html` mit `.msg.err`, `profil.html` mit `.msg`-Erfolg) oder einen Dateieintrag entsprechend ergänzen.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Betrachtung und Abnahme des Entwurfs durch den Anwender | Keine — Begründung im Plan: Ergebnis sind statische Design-Artefakte außerhalb der Anwendung; Abnahme erfolgt im Issue-Kommentar, Nachweis durch einzelne Sichtprüfung aller 16 PNGs plus Korrekturschleife (Akzeptanzszenarien 1, 2, 4) | Nicht erforderlich mit Begründung |
| Umgestaltetes Rendering der StatusWebseite | Explizit in spätere Umsetzungsphase verschoben; die 35 betroffenen Markup-Contract-Tests sind in `inventory/tests.md` vollständig dokumentiert und als dann anzupassend benannt | Nicht erforderlich mit Begründung (kein Code-Diff in diesem Lauf) |

## Fehlende oder unvollständige Planbestandteile

- [ ] Sichtbarer Zustand „Meldungsbox" (`.msg` / `.msg err`) fehlt in der Dateiliste und in den Sichtprüfungskriterien — siehe Testlücke oben; die Scope-Ausschluss-Tabelle dokumentiert diese Einschränkung weder als ausdrücklichen Ausschluss mit Begründung noch ist sie integriert geplant.

## Hinweise

- **Überwacher-Nav-Variante (optional):** Die Nav unterscheidet sich sichtbar je Rolle (Überwacher ohne Admin-Links und ohne Rollenzusatz, Administration mit „ · Administration"). Alle Mockups zeigen vermutlich die Admin-Nav. Für die Abnahme könnte eine Überwacher-Variante hilfreich sein — als Hinweis, kein hartes Kriterium, da die Anforderung sie nicht als eigenen Nebenzustand nennt.
- **Ablageort:** `requirement.md` schlägt `docs/features/task-issue-500-…/design/` vor („z. B."), der Plan wählt `docs/design/status-webseite/` — dokumentierte, begründete Abweichung (langlebiger Referenzort), konsistent mit dem Issue („z. B. unter docs/…") und der Repo-Praxis (`e2e-artifacts` unter docs). Kein Mangel, aber bei der Abnahme erwähnenswert.
- **Screenshot-Pipeline plausibel:** `C:\Program Files\Google\Chrome\Application\chrome.exe` ist auf dem System vorhanden (verifiziert). Iframe-Composite-Ansatz ist JS-frei und konsistent; das Risiko abgeschnittener Inhalte (Viewport-Höhe, Iframe-Höhe) ist über die Pflicht-Sichtprüfung und Akzeptanzszenario 2 abgesichert; Szenario 3 definiert einen Halt-und-Klären-Weg bei fehlendem Chrome.
- **Scope-Ausschlüsse:** Alle acht Ausschlüsse tragen Begründung und Status (`offen`/`bestätigt`); die `offen`-Statius sind zulässig, sollten aber vor/durch die Issue-Abnahme bestätigt werden — insbesondere Nr. 5 (Nebenzustände nur integriert) und Nr. 6 (nur zwei Mobil-Nachweise), da sie die Bildmenge direkt begrenzen.
- **Akzeptanzszenarien:** Vollständiger Happy Path vom Auftrag bis zur sichtbaren Wirkung vorhanden (Szenario 1 inkl. Ausgangszustand „`docs/design/` existiert nicht"); Fehler-/Wiederaufnahmewege für die beiden realistischen Blocker (Chrome fehlt, Layout-Defekt, Nebenzustand nicht erkennbar) definiert.
