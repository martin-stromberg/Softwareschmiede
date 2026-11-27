# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweite Prüfrunde. Die in Runde 1 (`plan-check.1.md`) festgestellte einzige Lücke —
fehlender CSRF-Negativtest — ist geschlossen: `Post_WithoutAntiforgery_IsRejected`
prüft POST ohne Token → 400, mit ungültigem/gefälschtem Token → 400 und enthält eine
Gegenprobe mit gültigem Token (kein 400), die belegt, dass der Endpoint-Filter
tatsächlich validiert. Der Test ist in der Tests-Tabelle, in der E2E-Tabelle
(„HTTP-Flow, E2E-Nachweis") und in der Aufgabenliste (Task 63) konsistent verankert.
Ebenfalls nachgezogen: die `LockoutEnd ≈ now + 1 h`-Assertion als Nachweis der
konfigurierten Sperrdauer und die explizite Einordnung der `Auth/*`-HTTP-Flow-Tests als
faktische E2E-Nachweise. Die vollständige erneute Prüfung aller Anforderungspunkte
ergab keine weiteren Lücken.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Erst-Registrierung erzeugt ersten Admin; danach dauerhaft geschlossen (GET+POST-Guard) | Programmablauf „Erst-Registrierung", `OnRedirectToLogin` setup-aware, `Users.Any()`-Guard in GET und POST | `Register_WithoutUsers_CreatesFirstAdmin_AndSignsIn`, `Register_WithExistingUsers_IsClosed`, E2E `HappyPath_FullSetupToRelayFlow` + `RegistrationClosed_AndUnauthenticatedRedirect` | Abgedeckt |
| Cookie-Anmeldung; nur angemeldete Benutzer lesen `/` und `/status.json`; Rollen `Administration`/`Ueberwacher` | Policies `PageAccess`/`AdminOnly`, `ConfigureApplicationCookie`, `RequireAuthorization` auf `/`/`/status.json` | `Login_ValidCredentials_GrantsAccess`/`Login_WrongPassword_Denied`, E2E `RegistrationClosed_AndUnauthenticatedRedirect`, `WatcherRole_AdminAccessDenied` | Abgedeckt |
| Lockout nach 3 Fehlversuchen, 1 h; Admin-Freischaltung; automatische Freischaltung nach 1 h | `IdentityOptions.Lockout` (3/1 h), `ResetLockoutAsync`, Unlock-Endpunkt + Button-Zustand | `Login_ThreeFailures_LocksUser_AdminUnlockRestores` mit `LockoutEnd ≈ now + 1 h`-Assertion (belegt konfigurierte Dauer; Vorspulen im Risiko-Abschnitt begründet nicht erforderlich), E2E `Lockout_AndAdminUnlock` | Abgedeckt |
| Deaktivierter Benutzer: Anmeldung verweigert + bestehende Session verliert Zugriff; Aktivierung nur durch Admin | `IsDeactivated`-Flag, Login-Nachprüfung + `OnValidatePrincipal`/`RejectPrincipal` je Request, `activate`-Endpunkt | `Login_DeactivatedUser_IsRejected`, `DeactivatedUser_LosesExistingSession`, E2E `DeactivatedUser_SessionRejected`, `AdminUsers_Create_Deactivate_Delete_Unlock` | Abgedeckt |
| Admin-UI: Benutzer anlegen (inkl. Stufe), Stufenänderung nachträglich, deaktivieren/aktivieren, löschen, Sperrung zurücksetzen | `UserAdminService` + `/admin/users*` inkl. `…/{id}/role`, `AdminPagesComposer.RenderUserList` mit Stufen-Control und Status-/Lockout-Anzeige | `AdminUsers_Create_Deactivate_Delete_Unlock`, `AdminUsers_SetRole_ChangesAccess`, `AdminUsers_LastActiveAdmin_IsProtected`, E2E `RoleChange_TakesEffectOnExistingSession` | Abgedeckt |
| Letzter-Admin-Schutz (eigenes und fremdes Konto, inkl. Herabstufung) | `UserAdminService`-Schutzregel + deaktivierte Buttons mit Hinweistitel | `AdminUsers_LastActiveAdmin_IsProtected`, `AdminPagesComposerTests` (deaktivierter Herabstufungs-Button), E2E-Szenario 9 | Abgedeckt |
| Relay-Tokens: DB-gestützt, SHA-256-gehasht, einmalige Klartext-Anzeige, widerrufbar | `RelayToken`/`RelayTokenService`, Unique-Index `TokenHash`, `RevokedUtc`, Einmal-Block in `RenderTokenList` | `AdminTokens_CreateShowsOnce_RevokeBlocks`, `RelayTokenServiceTests`, `RelayTokenValidatorTests` (DB), E2E `TokenLifecycle_OneTimeDisplayAndRevocation` | Abgedeckt |
| `/relay`-Transport unverändert (`access_token`-Query/Bearer), Prüfung gegen DB | `RelayTokenValidator.IsValidAsync` (Scoped, DB-Lookup), `StatusRelayHub.OnConnectedAsync` async | Umgebaute `StatusRelayRoundtripTests` (DB-Token-Seeding), Happy-Path-E2E mit echter `HubConnection` | Abgedeckt |
| Sitzungsdauer admin-konfigurierbar (global, persistent, Sliding; Bereich 5–43200, Default 480) | `ServerSetting` + `ServerSettingsService`, `OnSigningIn`/`OnValidatePrincipal` | `AdminSettings_SessionDuration_IsApplied`, `ServerSettingsServiceTests`, E2E `SessionDuration_ChangeAppliedToCookie` | Abgedeckt |
| Passwortänderung im eigenen Profil | `/profile` + `POST /profile/password`, `ChangePasswordAsync` + `RefreshSignInAsync` | `Profile_ChangePassword_Works_AndOldPasswordFails` (HTTP-Flow, E2E-Nachweis) | Abgedeckt |
| Persistenz über Neustarts in `data/*.db`; `data`-Verzeichnis + Migrationen + Rollen-Seeding beim Start | `Data:DatabasePath`-Default, `Directory.CreateDirectory`, `StatusServerDatabaseInitializer` (`MigrateAsync` + idempotentes Rollen-Seeding), Migration `InitialCreate` eingecheckt | `Startup_SecondFactoryOnSameDb_DataPersists`, E2E `Restart_SameDatabase_AllStateSurvives` | Abgedeckt |
| Entfernung `Page:Username`/`Page:Password`, `Relay:Tokens`/`Relay:DevToken` ohne Migration/Fallback | Options-Bereinigung, `appsettings*`-Bereinigung, `PageAccessMiddleware.cs` löschen, Factory-UseSettings entfernt | Alle neuen/umgebauten Tests laufen ohne die Config-Keys | Abgedeckt |
| CSRF-Schutz aller POST-Formulare | `IAntiforgery` + Endpoint-Filter auf allen POST-Routen, Hidden-Field in allen Formularen | `Post_WithoutAntiforgery_IsRejected` (ohne Token → 400, ungültiges Token → 400, Gegenprobe gültig → kein 400); positive Flows senden Token via `AuthTestClient` | Abgedeckt |
| Reverse-Proxy-/LAN-Betrieb: ForwardedHeaders, Cookie `Secure=SameAsRequest` | `Configure<ForwardedHeadersOptions>` + Cookie-Config, README-Hinweis `X-Forwarded-Proto` | Konfigurationsverhalten; nicht sinnvoll via `WebApplicationFactory` automatisierbar — manuelle Abnahme (im Plan begründet) | Abgedeckt |
| Kein In-App-Recovery; Recovery = `data` löschen (Doku) | Ausschluss 2 (bestätigt), Akzeptanzszenario 10 | README-Abschnitt (bewusst kein automatisierter Test) | Abgedeckt |
| Betriebsdoku/Endpunktdoku anpassen (`server/README.md`, Root-`README.md`, `RELEASE_NOTES.md`, `docs/help/…`) | Schritt 6 der Reihenfolge mit konkreter Abschnittsliste | Doku-Aufgaben 34–37 | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kompletter Happy Path (Erstaufruf → Registrierung → Admin → Überwacher → Token → `/relay`-Connect → Status sichtbar) | `E2E/StatusServerUserFlowTests.HappyPath_FullSetupToRelayFlow` | Abgedeckt |
| Registrierung geschlossen + unauthentifizierter Zugriff → Login-Umleitung | `…RegistrationClosed_AndUnauthenticatedRedirect` | Abgedeckt |
| Lockout nach 3 Fehlversuchen + Admin-Freischaltung | `…Lockout_AndAdminUnlock` | Abgedeckt |
| Deaktivierter Benutzer verliert laufende Session | `…DeactivatedUser_SessionRejected` | Abgedeckt |
| Token-Einmalanzeige + Widerruf blockiert `/relay` | `…TokenLifecycle_OneTimeDisplayAndRevocation` | Abgedeckt |
| Persistenz über Neustart (zweite Factory, gleiche DB) | `…Restart_SameDatabase_AllStateSurvives` | Abgedeckt |
| `Ueberwacher`-Restriktion (UI-Links unsichtbar + Endpunkte verweigern mit erklärter Blockade) | `…WatcherRole_AdminAccessDenied` | Abgedeckt |
| Stufenänderung wirkt auf bestehende Session ohne Re-Login (beide Richtungen) | `…RoleChange_TakesEffectOnExistingSession` | Abgedeckt |
| Sitzungsdauer-Änderung wirkt auf Cookie-Laufzeit | `…SessionDuration_ChangeAppliedToCookie` | Abgedeckt |
| CSRF-Blockade (POST ohne/ungültigem Token → 400, Gegenprobe gültig → kein 400) | `Auth/SetupAndAuthTests.Post_WithoutAntiforgery_IsRejected` (HTTP-Flow, E2E-Nachweis) | Abgedeckt |
| Profil-Passwortänderung | `Auth/ProfileTests.Profile_ChangePassword_Works_AndOldPasswordFails` (HTTP-Flow, E2E-Nachweis) | Abgedeckt |
| Admin-Benutzer-CRUD (Anlegen inkl. Stufe, Deaktivieren/Aktivieren, Löschen, Unlock) | `Auth/AdminUserTests` (HTTP-Flows über Admin-Endpunkte mit Formular-Posts) | Abgedeckt |
| Admin-Token- und Settings-Verwaltung | `Auth/AdminTokenTests`, `Auth/AdminSettingsTests` (HTTP-Flows) | Abgedeckt |
| Logout | `Auth/SetupAndAuthTests.Logout_ClearsSession` | Abgedeckt |

**Abnahmeform:** Der Plan definiert `WebApplicationFactory`-HTTP-Client-Flows mit
Cookie-Verwaltung, Antiforgery-Handling und echter `HubConnection` als E2E-Nachweis und
begründet dies nachvollziehbar (serverseitig gerendertes, JavaScript-freies HTML; kein
FlaUI-/Browser-Stack im Repo; Minimal-API ohne UI-Thread). Geometrische Assertions
entfallen begründet (keine WPF-Dialoge). Die `Auth/*`-Klassen außerhalb von `E2E/` sind
nun explizit als „HTTP-Flow, E2E-Nachweis" gekennzeichnet. Konsolidierung: Happy Path
bewusst als einzelner Test über eine laufende Instanz — entspricht der
Laufzeit-Konsolidierungsregel.

## Hinweise

- **Bearer-Header-Fallback am Hub:** bleibt wie bisher ohne expliziten Test
  (unveränderter Bestand; `AccessTokenProvider` sendet per Query). Optional als kleine
  Ergänzung in den umgebauten `StatusRelayRoundtripTests` möglich.
- **Duplikat-Benutzername:** Negativfall (zweiter Benutzer mit gleichem Namen) ist nur
  implizit über das Fehlermeldungs-Pattern abgedeckt; optional explizit in
  `AdminUserTests` ergänzen — keine Pflichtlücke.
- **ForwardedHeaders:** `X-Forwarded-Proto`-Verhalten ist via `WebApplicationFactory`
  nicht sinnvoll abzunehmen; die manuelle Browser-Abnahme sollte den Proxy-Fall
  einschließen (README-Punkt ist geplant).
- **Formatierungs-Kleinigkeit:** Die Tabellenzeile für
  `Post_WithoutAntiforgery_IsRejected` in der Tests-Tabelle (plan.md) beginnt mit einer
  leeren ersten Zelle — rein kosmetisch, inhaltlich korrekt.
- **`data/`-Verzeichnis vs. `.gitignore`:** `*.db`/`*.db-shm`/`*.db-wal` sind repo-weit
  bereits ignoriert — keine Plan-Ergänzung nötig.
- Die Scope-Klärung ist vollständig gefüllt (inkl. Verhalten bei fehlender
  `ServerSetting`-Zeile → Default 480), alle 7 Scope-Ausschlüsse tragen Begründung und
  Status `bestätigt`, die 11 Akzeptanzszenarien benennen Ausgangszustände, und die
  Umsetzungsreihenfolge enthält Voraussetzungen je Schritt. Betroffene Bestandstests
  sind vollständig benannt (`PageAccessTests`, `RelayTokenValidatorTests`,
  `StatusRelayRoundtripTests`, `StatusPageComposerTests`, `StatusWebApplicationFactory`;
  `InstanceSnapshotCacheTests`/`GitHubStatusServiceTests` korrekt als nicht betroffen
  markiert); Test-Fixtures (`AuthTestClient`, `StatusServerDbSeeder`) sind benannt.
