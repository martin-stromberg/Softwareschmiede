# Anforderung: Frischeres UI-Design für die Status-Webseite — Design-Entwurf als Mockups (Issue #500)

## Fachliche Zusammenfassung

Die Status-Webseite des ASP.NET-Core-StatusServers (`server/Softwareschmiede.StatusServer`) erhält einen kompletten visuellen Neu-Entwurf. Ergebnis dieses Laufs sind **HTML/CSS-Mockups samt Screenshots aller neun Ansichten in Light- und Dark-Mode**, die als Kommentar im Issue zur Abnahme gezeigt werden — angelehnt an die im Issue beschriebene Vorgehensweise über Google Stitch (Werkzeug frei, solange das Ergebnis gleichwertig ist). Der Entwurf hat volle Layout-Freiheit (keine Orientierung am heutigen Aussehen), muss aber die harten technischen Rahmenbedingungen abbilden: rein server-seitig gerendertes HTML **ohne JavaScript**, Interaktionen ausschließlich über Links und POST-Formulare, Bestätigungsdialoge als eigene Zwischenseiten, Farben als Design-Tokens auf Basis der mit Issue #489 bereits eingeführten CSS Custom Properties (`PageShell.ThemeCss`). Die eigentliche Code-Umsetzung in den Composer-Klassen erfolgt erst nach Anwender-Bestätigung des Entwurfs und ist nicht Teil dieses Laufs.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine server-seitig gerenderte Ansicht (HTML-Seite) des StatusServers als gestaltetes Mockup. Zum Scope gehören genau die neun im Issue benannten Ansichten — (1) Status-Übersicht `/`, (2) Login `/login`, (3) Registrierung/Ersteinrichtung `/register`, (4) Profil `/profile` inkl. Farbschema-Auswahl, (5) Benutzerverwaltung `/admin/users` inkl. Bestätigungsseite „Benutzer löschen" (5a), (6) Token-Verwaltung `/admin/tokens` inkl. Bestätigungsseite „Token widerrufen" (6a), (7) Einstellungen `/admin/settings` inkl. Ergebnisseite „Update wird installiert" (7a), (8) Zugriff verweigert `/access-denied`, (9) Formular-abgelaufen-Fehlerseite (Antiforgery/CSRF) — jeweils in der Light- und der Dark-Variante, also insgesamt 18 darzustellende Zustände (plus sinnvolle Nebenzustände wie Leer-Zustand, Fehlermeldungen, deaktivierte Aktionen, soweit sie für die Abnahme relevant sind).
- **Auslösende Ereignisse:** Der Entwurf wird einmalig in diesem Lauf erstellt; Auslöser für die nächste Stufe ist die Anwender-Bestätigung im Issue (Kommentardiskussion über die gezeigten Screenshots).
- **Erwartete Wirkung:** Ablage der Mockup-Artefakte (HTML/CSS je Ansicht) und der Screenshots (Light + Dark je Ansicht) im Repo unter `docs/features/task-issue-500-141c123699d14ffb92a41fd113b92eaa-feat-frischeres-ui-design-fuer/` (z. B. Unterordner `design/`); die Screenshots werden anschließend als Issue-Kommentar zur Abnahme bereitgestellt. Sichtbare Wirkung ist damit ein prüfbarer, vollständiger Design-Vorschlag — keine Änderung am laufenden Server.
- **Umgang mit fehlenden Objekten:**
  - **Beispiel-/Demodaten:** Die Mockups benötigen repräsentative Inhalte (mehrere App-Instanzen mit unterschiedlichen Status-Pills, Wartelisten, SCM-Ereignisse, GitHub-Repository-Sektionen, Benutzer-/Token-Tabellen, gesperrte/verwaiste Zustände). Diese sind als plausible Beispieldaten in den Mockups zu **erzeugen**, damit der Entwurf alle Elemente zeigt.
  - **Design-Tokens:** Die vorhandenen CSS Custom Properties aus `PageShell.ThemeCss` (Light-`:root`-Palette und `DarkThemeVars`, u. a. `--bg`, `--fg`, `--surface`, `--accent`, `--ok-*`, `--err-*`, `--warn-*`, `--badge-*`, `--pill-*`, `--src-*`) sind zu **wiederverwenden** bzw. das neue Token-Set ist so zu benennen, dass es später 1:1 auf diese Struktur mappbar ist — keine Inline-Farbwerte.
  - **Registrierungs-Flow:** Der Login enthält bewusst **keinen** Registrierungs-Link; der Entwurf soll den Flow „Ersteinrichtung ohne Benutzerkonto → automatische Weiterleitung nach `/register`" vorsehen (siehe Offene Fragen zum verifizierten Ist-Stand).
- **Scope-Ausschlüsse:**
  - Keine Code-Umsetzung: `PageShell`, `StatusPageComposer`, `AuthPagesComposer`, `AdminPagesComposer`, Endpunkte und Datenmodell bleiben in diesem Lauf unverändert.
  - Kein JavaScript und kein clientseitiger Zustand — weder im Mockup als funktionale Vorgabe noch als Designannahme; Interaktionen ausschließlich über HTML-Links und POST-Formulare; „Bestätigungsdialoge" werden als eigene Zwischenseiten (GET → Bestätigungs-POST) gezeigt, nicht als modale Dialoge.
  - Kein Registrierungs-Link auf der Login-Seite (Anwender-Entscheidung).
  - Keine Verpflichtung zur heutigen Farbwelt/Typografie — volle Layout-Freiheit; die Wiederverwendbarkeit der Token-Struktur ist jedoch zu wahren.
  - Keine neuen Funktionen: Der Funktionsumfang je Ansicht ist exakt der im Issue beschriebene Ist-Funktionsumfang; der Entwurf gestaltet nur um.

## Betroffene Klassen und Komponenten

In diesem Lauf werden **keine** Produktivcode-Artefakte geändert; betroffen sind die später umzusetzenden Render-Quellen als Referenz sowie neu anzulegende Design-Artefakte:

### Neu zu erstellende Design-Artefakte (Ergebnis dieses Laufs)

- `docs/features/task-issue-500-141c123699d14ffb92a41fd113b92eaa-feat-frischeres-ui-design-fuer/design/` (oder vergleichbarer Ablageort): statische HTML-Mockups je Ansicht mit eingebettetem oder geteiltem CSS auf Token-Basis, plus Screenshots jeder Ansicht in Light- und Dark-Variante zur Verwendung im Issue-Kommentar.

### Referenz-Artefakte (werden erst in der späteren Umsetzung angefasst)

- `server/Softwareschmiede.StatusServer/Pages/PageShell.cs` — gemeinsamer Seitenrahmen: `ThemeCss`/`DarkThemeVars` (Design-Tokens), `Css` (Basis-Styles), `NavCss` (Navigationsleiste), `BeginPage`/`AppendHeader`/`AppendNav`/`AppendMessage`/`EndPage`.
- `server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs` — `Render` und eigenes `Css` für die Status-Übersicht inkl. Meta-Refresh (`<meta http-equiv="refresh">`, `Page:RefreshSeconds`, Standard 15 s).
- `server/Softwareschmiede.StatusServer/Pages/AuthPagesComposer.cs` — `RenderLogin`, `RenderRegister`, `RenderProfile`, `RenderAccessDenied`, `RenderFormExpired`.
- `server/Softwareschmiede.StatusServer/Pages/AdminPagesComposer.cs` — `RenderUserList`, `RenderUserDeleteConfirm`, `RenderTokenList`, `RenderTokenRevokeConfirm`, `RenderSettings`, `RenderUpdateInstallStarted`.
- `server/Softwareschmiede.StatusServer/Data/ThemePreference.cs` (`Auto`/`Light`/`Dark`) und `PageUserContext` — liefern das wirksame `data-theme`-Attribut und den Navigationskontext (Benutzername, `IsAdmin` für Admin-Links).
- Endpunkt-Referenz: `Endpoints/AuthEndpoints.cs`, `Endpoints/AdminEndpoints.cs`, `Program.cs` — definieren Routes und damit die Link-/Formularziele, die der Entwurf abbilden muss.

### Tests

- Für den Design-Lauf keine. In der späteren Code-Umsetzung betroffen: `server/Softwareschmiede.StatusServer.Tests` (gerenderte-HTML-Aussagen, Endpunkt-Tests über `StatusWebApplicationFactory`).

## Implementierungsansatz

- **Eingabe je Ansicht:** Der im Issue beschriebene Funktionsumfang dient als Beschreibung für den Entwurf (Stitch-Prompt bzw. direkte Mockup-Erstellung); jede der neun Ansichten wird einzeln gestaltet, aber unter einem gemeinsamen Design-System (Layout, Typografie, Komponenten wie `section`, `table`, `.pill`, `.msg`, `header.top`, `nav.topnav`, Buttons inkl. `danger`-Variante, Leer-Zustände, `.token-once`-Hinweisblock).
- **JS-freie Interaktionsmuster beachten:** Inline-Aktions-Buttons als POST-Formulare, destruktive Aktionen über eigene GET-Zwischenseiten mit Bestätigen-Button (`danger`) und Abbrechen-Link, Abmelden als POST-Formular-Button in der Nav, Seitenaktualisierung per Meta-Refresh ohne Lade-/Blink-Effekte.
- **Beide Schemata von Anfang an:** Der Entwurf arbeitet mit Design-Tokens/Klassen und zeigt alle Ansichten in Light und Dark; Status-Badges, Pills und Links müssen in beiden Paletten ausreichend kontrastieren.
- **Mobiltauglichkeit:** Verwaltungs-/Dashboard-Charakter, Tabellen müssen auf schmalen Viewports lesbar umbrechen (heute u. a. via `overflow-wrap`, `.page`-Container, `viewport`-Meta).
- **Verifikation Registrierungs-Flow:** Der Entwurf berücksichtigt, dass es keinen Registrierungs-Link gibt; der Ist-Stand wurde geprüft (siehe Offene Fragen).

## Konfiguration

- Keine neue Konfiguration in diesem Lauf. Das Farbschema-Verhalten (`ThemePreference.Auto`/`Light`/`Dark`, automatisch via `prefers-color-scheme` mit Override im Profil über `POST /profile/theme`) ist bestehendes Verhalten aus #489 und wird als Design-Rahmen vorausgesetzt.
- Das Seiten-Aktualisierungsintervall bleibt `Page:RefreshSeconds` (Standard 15 s) — rein informativer Hinweis im Entwurf, keine Design-Abhängigkeit.

## Offene Fragen

- **Verifizierter Randfall „Weiterleitung zur Registrierung":** Die vom Anwender erwartete automatische Weiterleitung ist im Code nur teilweise vorhanden — `StatusServerCookieEvents.RedirectToLogin` (`server/Softwareschmiede.StatusServer/Security/StatusServerCookieEvents.cs:113-122`) leitet unauthentifizierte Aufrufe **geschützter** Seiten bei fehlendem Benutzerkonto auf `/register` um. Ein **direkter** `GET /login` rendert dagegen auch ohne vorhandenes Benutzerkonto das Login-Formular (`AuthEndpoints.GetLogin`, `AuthEndpoints.cs:97-106` prüft nur den bereits-angemeldeten Fall). Zu klären bzw. für die spätere Umsetzung zu vermerken, ob `/login` bei null Benutzern ebenfalls auf `/register` weiterleiten soll — für die Mockups ohne sichtbare Konsequenz.
- **Werkzeug/Ergebnisformat:** Das Issue nennt Google Stitch als Werkzeug; verbindlich ist das Ergebnis (Mockups + Screenshots aller Ansichten in beiden Schemata). Falls Stitch nicht verfügbar ist, werden gleichwertige HTML/CSS-Mockups direkt erstellt — Annahme, bei Abweichung mit dem Anwender abstimmen.
- **Umfang der darzustellenden Nebenzustände:** Neben den 18 Hauptbildern (9 Ansichten × 2 Schemata) ist zu entscheiden, welche Nebenzustände als eigene Screenshots nötig sind (Status-Übersicht: Leer-Zustand vs. befüllt; Benutzerliste: Pill „letzter Admin"/deaktivierte Schutz-Aktionen; Token-Seite: Einmal-Klartext-Hinweis vs. Leer-Zustand; Einstellungen: verwaiste Installationssperre). Vorschlag: in die Screenshots der jeweiligen Ansicht integrieren, nur bei deutlich abweichendem Layout separat zeigen.
