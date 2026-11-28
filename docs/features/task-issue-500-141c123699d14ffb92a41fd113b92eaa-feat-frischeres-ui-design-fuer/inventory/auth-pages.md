# Ansichten 2, 3, 4, 8, 9: `AuthPagesComposer`

Analysierte Datei: `server/Softwareschmiede.StatusServer/Pages/AuthPagesComposer.cs`
(Render-Methoden statisch, alle mit `PageShell.BeginPage`/`AppendHeader`/`EndPage`,
`data-theme` immer `auto` für anonyme Seiten, `user.Theme` für angemeldete).

Gemeinsames Layout: `header.top` (Titel „Softwareschmiede-Status" bzw. „Profil"),
ggf. `nav.topnav`, eine `<section>` als Inhaltskarte, Footer via `EndPage`.
Fehlermeldungen via `AppendMessage` (`.msg`/`.msg err`) oberhalb des Formulars.

## Ansicht 2: Login `/login` — `RenderLogin(antiforgeryFieldName, antiforgeryToken, error?)`

Zeilen 46–61. Seitentitel „Anmeldung – Softwareschmiede-Status".

- `<section><h1>Anmeldung</h1>`
- Fehlerbox (`isError: true`), mögliche Texte aus `AuthEndpoints.PostLogin`
  (`Endpoints/AuthEndpoints.cs:119-145`):
  - „Benutzername oder Passwort ist falsch."
  - „Das Konto ist gesperrt. Die Sperrung endet automatisch nach {Dauer} oder kann
    durch die Administration aufgehoben werden." (`FormatLockoutDuration`: „einer
    Stunde"/„n Stunden"/„einer Minute"/„n Minuten"; Default-Lockout 1 h,
    `Program.cs:80`)
  - „Dieses Konto ist deaktiviert."
- `<form method="post" action="{BasePath}/login">` + Hidden-Antiforgery:
  - `Benutzername` — `input type="text" name="username" required autocomplete="username"`
  - `Passwort` — `input type="password" name="password" required autocomplete="current-password"`
  - `<button type="submit">Anmelden</button>`
- **Kein Registrierungs-Link** (Anwender-Entscheidung).
- **Keine Nav** (anonyme Seite).
- Randfall (siehe `requirement.md`, „Offene Fragen"): `GET /login` rendert auch ohne
  vorhandenes Benutzerkonto das Formular — Redirect auf `/register` gibt es nur für
  geschützte Seiten via `StatusServerCookieEvents.RedirectToLogin`
  (`Security/StatusServerCookieEvents.cs:113-122`).

## Ansicht 3: Registrierung `/register` — `RenderRegister(antiforgeryFieldName, antiforgeryToken, error?)`

Zeilen 16–39. Seitentitel „Ersteinrichtung – Softwareschmiede-Status". Nur nutzbar,
solange kein Benutzer existiert (`GetRegister`/`PostRegister` leiten sonst auf `/` um,
`Endpoints/AuthEndpoints.cs:46-63`).

- `<section><h1>Ersteinrichtung</h1>`
- `<p class="muted">` Erklärtext: „Es existiert noch kein Benutzerkonto. Lege jetzt
  das Administratorkonto an — die Registrierung schließt sich danach dauerhaft."
- Fehlerbox (mögliche Texte `AuthEndpoints.cs:71-89` + `UserAdminService`/
  `DescribeErrors`: leerer Benutzername, Passwort < 8 Zeichen, Passwörter ungleich,
  „Der Benutzername ist bereits vergeben.", „Der Benutzername enthält unzulässige
  Zeichen …", „Es existiert bereits ein Benutzerkonto.")
- `<form method="post" action="{BasePath}/register">` + Hidden-Antiforgery:
  - `Benutzername` — `input type="text" name="username" required autocomplete="username"
    pattern="[a-zA-Z0-9._@+-]+" title="{FieldHint}"`
  - `<p class="muted">` Hinweistext `UserNamePolicy.FieldHint`: „Erlaubte Zeichen:
    a–z, A–Z, 0–9 sowie -._@+ (keine Umlaute, keine Leerzeichen)."
  - `Passwort (mindestens 8 Zeichen)` — `input type="password" name="password"
    required minlength="8" autocomplete="new-password"` (`PasswordPolicy.MinLength = 8`)
  - `Passwort wiederholen` — `input type="password" name="confirmPassword" required
    autocomplete="new-password"`
  - `<button type="submit">Administratorkonto anlegen</button>`
- Keine Nav.

## Ansicht 4: Profil `/profile` — `RenderProfile(user, message?, isError)`

Zeilen 69–103. Seitentitel „Profil – Softwareschmiede-Status", `data-theme` = `user.Theme`,
Header „Profil", **mit Nav**.

Eine `<section>` mit drei `h2`-Blöcken:

1. **Konto** — `<p class="muted">Angemeldet als <strong>{UserName}</strong> · Stufe
   {DisplayName}</p>` (Administration/Überwacher; Ü HTML-encodiert `&#220;`).
   Danach `AppendMessage` (Erfolg/Fehler, z. B. `?msg=password-changed` /
   `?msg=theme-saved` via `StatusMessages`).
2. **Anzeige** — `<form method="post" action="{BasePath}/profile/theme">` +
   Hidden-Antiforgery:
   - `Farbschema` — `<select name="theme">` mit `<option value="auto">Automatisch
     (System)</option>`, `light` → „Light", `dark` → „Dark"; aktuelle Einstellung `selected`
   - `<button>Anzeige speichern</button>`
3. **Passwort ändern** — `<form method="post" action="{BasePath}/profile/password">` +
   Hidden-Antiforgery:
   - `Aktuelles Passwort` — `name="currentPassword" required autocomplete="current-password"`
   - `Neues Passwort (mindestens 8 Zeichen)` — `name="newPassword" required minlength="8"
     autocomplete="new-password"`
   - `Neues Passwort wiederholen` — `name="confirmPassword" required autocomplete="new-password"`
   - `<button>Passwort ändern</button>`

Fehlerfälle (direkt gerendert, `isError`): zu kurzes neues Passwort, ungleiche
Wiederholung, `DescribeErrors`-Texte (z. B. „Das aktuelle Passwort ist falsch."),
„Unbekannte Anzeigeeinstellung." (ungültiger Theme-Wert).

## Ansicht 8: Zugriff verweigert `/access-denied` — `RenderAccessDenied(user?)`

Zeilen 127–141. `data-theme` = `user?.Theme ?? auto`; Nav nur wenn angemeldet.

- `<section><h1>Zugriff verweigert</h1>`
- `<p class="muted">Deine Berechtigungsstufe reicht für diesen Bereich nicht aus.
  Die Status-Übersicht bleibt dir weiterhin zugänglich.</p>`
- `<p><a href="{BasePath}/">Zur Status-Übersicht</a></p>`

Erreicht wird die Seite via `options.AccessDeniedPath = "/access-denied"`
(`Program.cs:96`) — Cookie-Auth-Redirect bei Rollen-Fehlschlag (Überwacher ruft
`/admin/*` auf → `RequireAuthorization(AdminOnlyPolicy)` schlägt fehl).

## Ansicht 9: Formular abgelaufen — `RenderFormExpired()`

Zeilen 108–122. `data-theme="auto"`, keine Nav (kann anonym auftreten).

- `<section><h1>Formular abgelaufen</h1>`
- `<p class="muted">` Erklärung: Formular abgelaufen/ungültig — lange geöffnete
  Seite oder veralteter Browser-Tab; Eingaben nicht übernommen.
- `<p class="muted">Lade die Seite neu und sende das Formular erneut ab.</p>`
- `<p><a href="{BasePath}/">Zur Status-Übersicht</a></p>`

Ausgeliefert wird sie mit HTTP 400 aus dem `RequireAntiforgery`-Endpoint-Filter
(`Endpoints/EndpointSecurityExtensions.cs:41-58`) bei `AntiforgeryValidationException`
— d. h. sie ersetzt den nackten 400-Fehler **aller** POST-Endpunkte.
