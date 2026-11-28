# Endpunkte und Interaktionsmodell

Analysierte Dateien: `server/Softwareschmiede.StatusServer/Program.cs`,
`Endpoints/AuthEndpoints.cs`, `Endpoints/AdminEndpoints.cs`,
`Endpoints/EndpointSecurityExtensions.cs`, `Endpoints/StatusMessages.cs`,
`Security/StatusServerCookieEvents.cs`, `Security/StatusRoles.cs`.

Alle Links/Formular-Actions werden als literale, root-absolute Pfade gebaut und mit
`PageShell.BasePath` präfixiert (konfigurierbarer `PathBase` für
Reverse-Proxy-Unterpfad, `Program.cs:133-138`). Mockup-Pfade müssen daher auf
`{BasePath}/…` mappbar bleiben.

## Routenübersicht

### Auth (`AuthEndpoints.MapAuthEndpoints`, `AuthEndpoints.cs:18-39`)

| Methode+Route | Auth | Antiforgery | Handler / Ergebnis |
|---------------|------|-------------|--------------------|
| `GET /register` | `AllowAnonymous` | — | Redirect `/` falls Benutzer existieren; sonst `RenderRegister` |
| `POST /register` | `AllowAnonymous` | ✓ | Erfolg: SignIn + Redirect `/`; Fehler: `RenderRegister` mit Fehler |
| `GET /login` | `AllowAnonymous` | — | Redirect `/` falls angemeldet; sonst `RenderLogin` |
| `POST /login` | `AllowAnonymous` | ✓ | Erfolg: Redirect `/`; Fehler: `RenderLogin` mit Fehler |
| `POST /logout` | PageAccess | ✓ | SignOut + Redirect `/login` |
| `GET /profile` | PageAccess | — | `RenderProfile` (`?msg=` aufgelöst) |
| `POST /profile/password` | PageAccess | ✓ | Erfolg: Redirect `/profile?msg=password-changed`; Fehler: `RenderProfile` mit Fehler |
| `POST /profile/theme` | PageAccess | ✓ | Erfolg: Redirect `/profile?msg=theme-saved`; Fehler: `RenderProfile` mit Fehler |
| `GET /access-denied` | `AllowAnonymous` | — | `RenderAccessDenied` (Nav nur wenn angemeldet) |

### Admin (`AdminEndpoints.MapAdminEndpoints`, `AdminEndpoints.cs:32-58`) — alle `AdminOnlyPolicy`

| Methode+Route | Antiforgery | Ergebnis |
|---------------|-------------|----------|
| `GET /admin/users` | — | `RenderUserList` (`?msg=`/`?err=`) |
| `POST /admin/users` | ✓ | Benutzer anlegen → Redirect `?msg=user-created` / Seite mit Fehler |
| `POST /admin/users/{id}/role` | ✓ | Stufenwechsel → `?msg=role-changed` / `?err=user-not-found` / Fehlerseite |
| `POST /admin/users/{id}/deactivate` | ✓ | → `?msg=user-deactivated` |
| `POST /admin/users/{id}/activate` | ✓ | → `?msg=user-activated` |
| `GET /admin/users/{id}/delete` | — | `RenderUserDeleteConfirm` (Ziel nicht gefunden → Liste mit Fehler) |
| `POST /admin/users/{id}/delete` | ✓ | → `?msg=user-deleted` / `?err=user-not-found` |
| `POST /admin/users/{id}/unlock` | ✓ | → `?msg=lockout-reset` |
| `GET /admin/tokens` | — | `RenderTokenList` (`?msg=`/`?err=`/`?created={guid}`) |
| `POST /admin/tokens` | ✓ | Token anlegen → Redirect `?created={store-key}` |
| `GET /admin/tokens/{id:long}/revoke` | — | `RenderTokenRevokeConfirm` (nicht gefunden/bereits widerrufen → Liste mit Fehler) |
| `POST /admin/tokens/{id:long}/revoke` | ✓ | → `?msg=token-revoked` / `?err=token-not-found` |
| `GET /admin/settings` | — | `RenderSettings` (`?msg=`/`?err=`) |
| `POST /admin/settings/session-duration` | ✓ | → `?msg=settings-saved` / Fehlerseite |
| `POST /admin/settings/update` | ✓ | → `?msg=update-settings-saved` / Fehlerseite |
| `POST /admin/settings/update/check` | ✓ | → `?msg=update-check-done` / `?err=update-check-failed` |
| `POST /admin/settings/update/install` | ✓ | InstallationStarted → **direkte Antwort** `RenderUpdateInstallStarted` (kein Redirect); sonst `?msg=update-install-not-started` / `?err=update-install-failed` / Fehlerseite (fehlende Downtime-Checkbox) |
| `POST /admin/settings/update/lock-reset` | ✓ | → `?msg=update-lock-reset` / `?err=update-lock-reset-failed` |

### Kern (`Program.cs:149-184`)

| Route | Auth | Zweck |
|-------|------|-------|
| `MapHub<StatusRelayHub>("/relay")` | Token-basiert (Relay-Token, kein Cookie) | SignalR-Hub der App-Instanzen (`RegisterInstance`, `PushSnapshot`) |
| `GET /` | PageAccess | `StatusPageComposer.Render` |
| `GET /status.json` | PageAccess | JSON-Variante (GeneratedUtc, Instances, GitHub) |

## Interaktionsmodell (JS-frei)

- **POST-only für Mutationen**: Jede zustandsändernde Aktion ist ein
  `<form method="post">` mit Hidden-Antiforgery-Feld (`__RequestVerificationToken`).
  Inline-Aktionen nutzen `form.inline`; destruktive Aktionen (`delete`, `revoke`)
  sind GET-Links (`a.action-link danger`) auf eigene Bestätigungsseiten, deren POST
  auf denselben Pfad geht (`class="danger"`-Button „Endgültig …" + Abbrechen-Link).
- **Post/Redirect/Get**: Erfolgreiche POSTs und „nicht gefunden"-Fehlschläge
  redirecten mit festen `?msg=`/`?err=`-Schlüsseln, die `StatusMessages` auf
  deutsche Texte mappt (Tabelle `StatusMessages.s_texts`, `StatusMessages.cs:70-92`
  — 19 Schlüssel). Andere fachliche Fehler rendern die Seite direkt mit `.msg err`.
- **Antiforgery**: `RequireAntiforgery()` ist ein Endpoint-Filter
  (`EndpointSecurityExtensions.cs:41`), der `IAntiforgery.ValidateRequestAsync`
  aufruft und bei `AntiforgeryValidationException` HTTP 400 +
  `AuthPagesComposer.RenderFormExpired()` liefert. `UseAntiforgery()` läuft in der
  Pipeline (`Program.cs:147`). Tokens werden je GET via
  `antiforgery.GetAndStoreTokens` (Cookie + Request-Token) erzeugt.
- **Einmal-Klartext**: `RelayTokenPlaintextStore` (Singleton) liefert den
  Token-Klartext genau einmal per `Take(key)` auf `GET /admin/tokens?created={key}`;
  Refresh zeigt Hinweismeldung statt Klartext.
- **Rollen/Policies** (`StatusRoles`): `PageAccessPolicy = "PageAccess"`
  (Rollen `Administration` + `Ueberwacher`), `AdminOnlyPolicy = "AdminOnly"`.
  `DisplayName`: `Ueberwacher` → „Überwacher".
- **Auth-Routing**: Cookie `LoginPath = "/login"`, `AccessDeniedPath =
  "/access-denied"` (`Program.cs:95-96`). `StatusServerCookieEvents.RedirectToLogin`
  (Zeilen 113–122) leitet unauthentifizierte Aufrufe **geschützter** Seiten auf
  `/register` um, solange noch kein Benutzerkonto existiert — **nicht** aber ein
  direktes `GET /login` (bekannte Lücke, siehe requirement.md „Offene Fragen").
- **Formularfelder-Whitelist** (aus den Composern): `username`, `password`,
  `confirmPassword`, `currentPassword`, `newPassword`, `theme`, `role`, `label`,
  `minutes`, `channel`, `mode`, `installTime`, `confirmDowntime` —
  plus `__RequestVerificationToken` in jedem Formular.
