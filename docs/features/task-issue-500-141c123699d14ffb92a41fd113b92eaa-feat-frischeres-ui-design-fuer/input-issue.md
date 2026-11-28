# feat: Frischeres UI-Design fuer die Status-Webseite (Entwurf via Google Stitch)

## Ziel

Die Status-Webseite (`server/Softwareschmiede.StatusServer`) soll ein frischeres
visuelles Design erhalten. Ein Entwurf dafür soll mit Hilfe von Google Stitch
generiert werden, indem der Funktionsumfang der einzelnen Ansichten als Eingabe
beschrieben wird (siehe unten).

## Abhängigkeit

**Issue #489** (Light-/Darkmode für die Status-Webseite) ist aktuell noch
offen. Der Design-Entwurf muss Light- und Dark-Mode bereits mitdenken (beide
Paletten bzw. austauschbare Design-Tokens), auch wenn #489 im Code noch nicht
umgesetzt ist — wird #489 bis zur Umsetzung dieses Issues abgeschlossen, ist
das Theming als bereits vorhandenes Verhalten zu berücksichtigen.

## Übergreifende Vorgaben für alle Ansichten

- **Server-seitig gerendert, ohne JavaScript:** kein JS-Framework und kein
  clientseitiger Zustand — Interaktionen laufen ausschließlich über
  HTML-Links und -Formulare (POST). Der Entwurf muss unter dieser
  Einschränkung funktionieren; „Bestätigungsdialoge" werden daher als eigene
  Zwischenseiten (GET → Bestätigungs-POST) umgesetzt, nicht als modale
  Dialoge.
- Schlichtes, klares Verwaltungs-/Dashboard-Design, mobiltauglich (Tabellen
  müssen auf schmalen Viewports lesbar umbrechen).
- Light- und Dark-Mode gemäß #489: automatisch via `prefers-color-scheme`
  (Fallback Light), Override im Profil (Automatisch/Light/Dark). Der Entwurf
  soll beide Schemata zeigen; Status-Badges, Pills und Links müssen in beiden
  Paletten ausreichend kontrastreich sein. Farben als Tokens/Klassen
  beschreiben — keine Inline-Farbwerte (diese werden in #489 ohnehin durch
  CSS-Variablen ersetzt).
- Navigationsleiste (angemeldet): Status-Übersicht, Profil, zusätzlich bei
  **Administration**: Benutzer, Tokens, Einstellungen; rechts Benutzername
  mit Rollenanzeige sowie Abmelden als POST-Formular-Button (JS-frei, kein
  GET-Link).
- Zwei Rollen: **Administration** (Vollzugriff inkl. Verwaltung) und
  **Überwacher** (lesender Zugriff — Status-Übersicht sowie das eigene
  Profil inkl. Passwortänderung sind für beide Rollen erreichbar).

## Funktionsumfang je Ansicht (Eingabe für den Stitch-Entwurf)

### 1. Status-Übersicht (`/`, Startseite nach Login)

- Kopfzeile mit Gesamtstatus-Badge (`online` / `offline` /
  `teilweise veraltet` / `keine Instanz`) und Namen der verbundenen
  Instanzen.
- Meta-Zeile: letzter Stand (UTC), Hinweis auf das
  Aktualisierungsintervall sowie ein kleiner, informativer Link auf die
  JSON-Variante `/status.json`.
- Leer-Zustand (noch keine Instanz registriert) mit kurzer Anleitung, wie
  die Remote-Übersicht in der App aktiviert wird.
- **Pro App-Instanz** eine Sektionsgruppe:
  - Warnhinweis, wenn die Instanz offline ist oder seit längerem keinen
    Snapshot geliefert hat; veraltete Sektionen werden abgedimmt markiert.
  - Sektion „Laufende Aufgaben": Tabelle mit Issue-Nummer/-Titel,
    Repository-Name, Status-Pill (`läuft`, `pausiert`, `Wartet auf Eingabe`
    u. a.), Detailspalte (Art, pausiert-bis, Heartbeat) sowie Links auf PRs
    und Issue.
  - Sektion „Wartet auf Anwender": wartende Work-Items und wartende
    SCM-Ereignisse im selben Tabellenformat.
  - Sektion „Letzte SCM-Ereignisse": Zeitpunkt, verlinkte Referenz,
    Status-Pill (`Verarbeitet`/`Aufgenommen`, `Abgelehnt`/`Fehlgeschlagen`,
    `Wartend`/`Verzögert`) und Grund.
  - Zähler erscheinen als `(n)` in den Sektionsüberschriften; Quellen-Badges
    (`App · <Zeit> UTC`) und Instanz-Tag je Sektion.
- **Pro konfiguriertem GitHub-Repository** eine Sektion: Repository-Name
  als Link, Quellen-Badge (`GitHub · <Zeit>`), Anzahl offener Issues,
  Liste offener Pull Requests (Nr. + Titel, verlinkt), Liste letzter
  Releases (Tag, Name, Datum, verlinkt). Bei Abruffehlern dezenter
  Fehlerhinweis oberhalb der Sektion statt Absturz.
- Die Seite aktualisiert sich per Meta-Refresh (Standard 15 s,
  konfigurierbar über `Page:RefreshSeconds`) — das Design sollte dafür
  keine aufdringlichen Lade-/Blink-Effekte brauchen.

### 2. Login (`/login`)

- Minimalistisches Formular: Benutzername, Passwort, Anmelden-Button.
- Fehlermeldungen oberhalb des Formulars (falsche Zugangsdaten; Konto
  gesperrt inkl. Restdauer; Konto deaktiviert).
- **Offen:** Soll ein Link zur Registrierung erscheinen, solange noch kein
  Benutzerkonto existiert (Erstregistrierung)? Im Ist-Stand gibt es keinen
  solchen Link; `/register` leitet nach erfolgter Ersteinrichtung ohnehin
  auf `/` um. Siehe Offene Punkte.

### 3. Registrierung / Ersteinrichtung (`/register`)

- Nur einmalig nutzbar: der erste Benutzer wird automatisch
  Administration, danach ist die Seite dauerhaft geschlossen (Redirect
  auf `/`).
- Erklärtext zur Ersteinrichtung (danach schließt sich die Registrierung).
- Formular: Benutzername (mit Zeichensatz-Hinweis: a–z, A–Z, 0–9 sowie
  -._@+, keine Umlaute/Leerzeichen), Passwort (min. 8 Zeichen),
  Passwort-Wiederholung, Button „Administratorkonto anlegen".

### 4. Profil (`/profile`)

- Kontoinformation: Benutzername und Berechtigungsstufe.
- Passwort ändern: aktuelles Passwort, neues Passwort (min. 8 Zeichen),
  Wiederholung; Erfolgs-/Fehlermeldungen oberhalb des Formulars.
- **Geplant (aus #489, im Entwurf einplanen):** Auswahl des Farbschemas —
  Radiobuttons/Segmented-Control „Automatisch (System)" / „Light" /
  „Dark".

### 5. Benutzerverwaltung (`/admin/users`, nur Administration)

- Tabelle aller Benutzer: Name (ggf. mit Pill „letzter Admin"), Stufe,
  Status (`aktiv` / `deaktiviert` / `gesperrt bis <Zeit> UTC`).
- Aktionen je Zeile (Inline-Buttons): Stufenwechsel („Zu Überwacher
  herabstufen" / „Zu Administration befördern"), Aktivieren/Deaktivieren,
  „Sperrung zurücksetzen" (nur aktiv, wenn gesperrt) sowie „Löschen" —
  letzteres führt auf eine eigene Bestätigungsseite (siehe 5a). Für den
  letzten aktiven Administrator sind schützende Aktionen deaktiviert
  (mit Tooltip-Begründung).
- Formular „Benutzer anlegen": Benutzername (Zeichensatz-Hinweis),
  Passwort (min. 8 Zeichen), Stufen-Auswahl (Überwacher/Administration).

#### 5a. Bestätigungsseite „Benutzer löschen" (`/admin/users/{id}/delete`)

- Eigene JS-freie Zwischenseite: Warnhinweis (unwiderruflich),
  Bestätigen-Button (danger), Abbrechen-Link.

### 6. Token-Verwaltung (`/admin/tokens`, nur Administration)

- Nach dem Anlegen prominent eingeblendeter Hinweisblock mit dem
  Klartext des neuen Tokens — einmalige Anzeige mit „jetzt kopieren"-
  Hinweis.
- Tabelle der Relay-Tokens (je eine App-Instanz): Bezeichnung,
  Erstellungsdatum (UTC), Status (`gültig` / `widerrufen <Datum> UTC`),
  Aktion „Widerrufen" (führt auf die Bestätigungsseite, siehe 6a; bei
  bereits widerrufenen Tokens deaktiviert).
- Leer-Zustand mit Hinweis, dass der `/relay`-Endpunkt ohne Tokens alle
  Verbindungen ablehnt.
- Formular „Token anlegen": Bezeichnung.

#### 6a. Bestätigungsseite „Token widerrufen" (`/admin/tokens/{id}/revoke`)

- Eigene JS-freie Zwischenseite: Warnhinweis (verbundene Instanzen
  verlieren den Zugang, für eine erneute Anbindung ist ein neues Token
  nötig), Bestätigen-Button (danger), Abbrechen-Link.

### 7. Einstellungen (`/admin/settings`, nur Administration)

- Sektion „Sitzungsdauer": Zahlenfeld in Minuten (5–43200) mit
  Hinweistext und Speichern-Button.
- Sektion „Updates":
  - Statustabelle: installierte Version, Zustand, verfügbare Version,
    letzte Prüfung, letzter Fehler (als Pill), Installationssperre
    (`aktiv` / `verwaist — Reset möglich` / `keine`), Pfad des
    Update-Protokolls.
  - Bei verwaister Sperre: Aktion „Update-Sperre zurücksetzen".
  - Formular „Kanal und Automatisierung": Release-Kanal (Stable /
    Pre-Release), Automatisierung (Aus / Nur prüfen / Prüfen und
    installieren), Installationsuhrzeit.
  - Aktionen: „Jetzt nach Update suchen" sowie „Update jetzt
    installieren" mit Downtime-Bestätigungs-Checkbox (danger).

#### 7a. Ergebnisseite „Update wird installiert"

- Statische Seite ohne Redirect: Hinweis auf den Neustart während der
  Installation, Verweis auf das Update-Protokoll.

### 8. Zugriff verweigert (`/access-denied`)

- Einfache Fehlerseite für die Überwacher-Rolle beim Aufruf einer
  Administration-Aktion; Link zurück zur Status-Übersicht.

### 9. Formular abgelaufen (Antiforgery-/CSRF-Fehlerseite)

- Einfache Fehlerseite mit Erklärung (abgelaufenes/veraltetes Formular),
  Hinweis auf Neuladen und erneutes Absenden, Link zurück zur
  Status-Übersicht.

## Betroffener Code (Referenz)

- `server/Softwareschmiede.StatusServer/Status/StatusPageComposer.cs`
- `server/Softwareschmiede.StatusServer/Pages/AdminPagesComposer.cs`
- `server/Softwareschmiede.StatusServer/Pages/AuthPagesComposer.cs`
- `server/Softwareschmiede.StatusServer/Pages/PageShell.cs`
- Endpunkt-Definitionen: `server/Softwareschmiede.StatusServer/Endpoints/AuthEndpoints.cs`,
  `server/Softwareschmiede.StatusServer/Endpoints/AdminEndpoints.cs`,
  `server/Softwareschmiede.StatusServer/Program.cs`

## Offene Punkte

1. **Ergebnisumfang:** Ist das Issue mit dem Stitch-Entwurf (Screens bzw.
   exportiertem HTML/CSS; Ablageort z. B. unter `docs/…`) abgeschlossen,
   oder umfasst es auch die Umsetzung im Code (Ablösung der eingebetteten
   CSS-Konstanten in den Composer-Klassen)?
2. **Design-Vorgaben:** Soll die heutige Farbwelt (Indigo-Primärfarbe,
   Emerald-/Amber-/Rot-Statusfarben) als Basis beibehalten oder neu
   gestaltet werden? Vorgaben zu Typografie, Iconografie, Logo?
3. **Reihenfolge zu #489:** Der Entwurf sollte direkt auf
   CSS-Custom-Properties/Design-Tokens zielen, damit die Dark-Mode-
   Umsetzung aus #489 nicht erneut angefasst werden muss.
4. **Abnahme:** Wer bewertet und gibt den Stitch-Entwurf frei (Screens
   aller neun Ansichten in Light und Dark)?
5. **Registrierungs-Link auf dem Login** (siehe Ansicht 2): gewünschtes
   neues Element oder entfällt der Punkt?

<!-- softwareschmiede:action:ae4860fb-980b-4abd-8a14-9158e5f27b19 -->

---

## Antworten des Anwenders auf die offenen Punkte (Issue-Kommentar)

1. Ergebnisumfang: Der Design-Vorschlag soll nur ein Zwischenschritt sein. Screenshots aller Ansichten sollen im Issue gezeigt werden. Nach einer Bestätigung soll dann die Umsetzung erfolgen.
2. Design-Vorgaben: Keine Orientierung am heutigen Layout. Volle Freiheit für neues Layout.
3. Die Dark-Mode-Implementierung (Issue #489, PR #508) ist inzwischen nach staging gemergt; diese Aufgabe beginnt nach deren Abschluss — Stand jetzt erfüllt.
4. Screenshots als Kommentar im Issue. Abnahme durch Kommentardiskussion.
5. Registrierungs-Link: Kein Registrierungs-Link. Registrierung ist nur möglich, wenn noch kein Benutzerkonto vorhanden ist. In dem Fall soll es stets eine Weiterleitung direkt zur Registrierung geben. Das sollte bereits der Fall sein — im Entwurf zu verifizieren.
