# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Der Plan deckt die Anforderung in Umsetzung, Reihenfolge, Scope-Klärung, Scope-Ausschlüssen
(alle mit Begründung und Status `bestätigt`), Akzeptanzszenarien (inkl. vollständigem
Happy Path vom externen Auslöser `GET /` bis zur sichtbaren Wirkung) und Risiken nahezu
vollständig ab. Einzige festgestellte Lücke: Für den explizit geforderten CSRF-Schutz
aller POST-Formulare fehlt ein Negativ-Testnachweis (Ablehnung ohne/ungültigem
Antiforgery-Token); der Plan belegt den Schutz nur implizit über den positiven Pfad.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Erst-Registrierung erzeugt ersten Admin; danach dauerhaft geschlossen (GET+POST-Guard) | Programmablauf „Erst-Registrierung", `OnRedirectToLogin` setup-aware, `Users.Any()`-Guard in GET und POST | `Register_WithoutUsers_CreatesFirstAdmin_AndSignsIn`, `Register_WithExistingUsers_IsClosed`, E2E `HappyPath_FullSetupToRelayFlow` + `RegistrationClosed_AndUnauthenticatedRedirect` | Abgedeckt |
| Cookie-Anmeldung; nur angemeldete Benutzer lesen `/` und `/status.json`; Rollen `Administration`/`Ueberwacher` | Policies `PageAccess`/`AdminOnly`, `ConfigureApplicationCookie`, `RequireAuthorization` auf `/`/`/status.json` | `Login_ValidCredentials_GrantsAccess`/`Login_WrongPassword_Denied`, E2E `RegistrationClosed_AndUnauthenticatedRedirect`, `WatcherRole_AdminAccessDenied` | Abgedeckt |
| Lockout nach 3 Fehlversuchen, 1 h; Admin-Freischaltung; automatische Freischaltung nach 1 h | `IdentityOptions.Lockout` (3/1 h), `ResetLockoutAsync`, Unlock-Endpunkt + Button-Zustand | `Login_ThreeFailures_LocksUser_AdminUnlockRestores`, E2E `Lockout_AndAdminUnlock`; Zeitablauf bewusst nicht vorgespult — im Risiko-Abschnitt begründet | Abgedeckt |
| Deaktivierter Benutzer: Anmeldung verweigert + bestehende Session verliert Zugriff; Aktivierung nur durch Admin | `IsDeactivated`-Flag, Login-Nachprüfung + `OnValidatePrincipal`/`RejectPrincipal` je Request, `activate`-Endpunkt | `Login_DeactivatedUser_IsRejected`, `DeactivatedUser_LosesExistingSession`, E2E `DeactivatedUser_SessionRejected`, `AdminUsers_Create_Deactivate_Delete_Unlock` | Abgedeckt |
| Admin-UI: Benutzer anlegen (inkl. Stufe), Stufenänderung nachträglich, deaktivieren/aktivieren, löschen, Sperrung zurücksetzen | `UserAdminService` + `/admin/users*` inkl. `…/{id}/role`, `AdminPagesComposer.RenderUserList` mit Stufen-Control | `AdminUsers_Create_Deactivate_Delete_Unlock`, `AdminUsers_SetRole_ChangesAccess`, `AdminUsers_LastActiveAdmin_IsProtected`, E2E `RoleChange_TakesEffectOnExistingSession` | Abgedeckt |
| Letzter-Admin-Schutz (eigenes und fremdes Konto, inkl. Herabstufung) | `UserAdminService`-Schutzregel + deaktivierte Buttons mit Hinweistitel | `AdminUsers_LastActiveAdmin_IsProtected`, `AdminPagesComposerTests` (deaktivierter Herabstufungs-Button), E2E-Szenario 9 | Abgedeckt |
| Relay-Tokens: DB-gestützt, SHA-256-gehasht, einmalige Klartext-Anzeige, widerrufbar | `RelayToken`/`RelayTokenService`, Unique-Index `TokenHash`, `RevokedUtc`, Einmal-Block in `RenderTokenList` | `AdminTokens_CreateShowsOnce_RevokeBlocks`, `RelayTokenServiceTests`, `RelayTokenValidatorTests` (DB), E2E `TokenLifecycle_OneTimeDisplayAndRevocation` | Abgedeckt |
| `/relay`-Transport unverändert (`access_token`-Query/Bearer), Prüfung gegen DB | `RelayTokenValidator.IsValidAsync` (Scoped, DB-Lookup), `StatusRelayHub.OnConnectedAsync` async | Umgebaute `StatusRelayRoundtripTests` (DB-Token-Seeding), Happy-Path-E2E mit echter `HubConnection` | Abgedeckt |
| Sitzungsdauer admin-konfigurierbar (global, persistent, Sliding) | `ServerSetting` + `ServerSettingsService` (Default 480, Bereich 5–43200), `OnSigningIn`/`OnValidatePrincipal` | `AdminSettings_SessionDuration_IsApplied`, `ServerSettingsServiceTests`, E2E `SessionDuration_ChangeAppliedToCookie` | Abgedeckt |
| Passwortänderung im eigenen Profil | `/profile` + `POST /profile/password`, `ChangePasswordAsync` + `RefreshSignInAsync` | `Profile_ChangePassword_Works_AndOldPasswordFails` (HTTP-Flow) | Abgedeckt |
| Persistenz über Neustarts in `data/*.db`; `data`-Verzeichnis + Migrationen beim Start; Rollen-Seeding | `Data:DatabasePath`-Default, `Directory.CreateDirectory`, `StatusServerDatabaseInitializer` (`MigrateAsync` + idempotentes Rollen-Seeding), Migration `InitialCreate` eingecheckt | `Startup_SecondFactoryOnSameDb_DataPersists`, E2E `Restart_SameDatabase_AllStateSurvives` | Abgedeckt |
| Entfernung `Page:Username`/`Page:Password`, `Relay:Tokens`/`Relay:DevToken` ohne Migration/Fallback | Options-Bereinigung, `appsettings*`-Bereinigung, `PageAccessMiddleware.cs` löschen, Factory-UseSettings entfernt | Alle neuen/umgebauten Tests laufen ohne die Config-Keys | Abgedeckt |
| CSRF-Schutz aller POST-Formulare | `IAntiforgery` + Endpoint-Filter auf allen POST-Routen, Hidden-Field in allen Formularen | Nur indirekt: `AuthTestClient` extrahiert/sendet Token in positiven Flows; **kein Test prüft die Ablehnung ohne/ungültigem Token (400)** | **Lücke** |
| Reverse-Proxy-/LAN-Betrieb: ForwardedHeaders, Cookie `Secure=SameAsRequest` | `Configure<ForwardedHeadersOptions>` + Cookie-Config, README-Hinweis `X-Forwarded-Proto` | Konfigurationsverhalten; kein automatisierter Test vorgesehen (nicht sinnvoll via `WebApplicationFactory` prüfbar) — manuelle Abnahme | Abgedeckt |
| Kein In-App-Recovery; Recovery = `data` löschen (Doku) | Ausschluss 2 (bestätigt), Akzeptanzszenario 10 | README-Abschnitt (bewusst kein automatisierter Test) | Abgedeckt |
| Betriebsdoku/Endpunktdoku anpassen (`server/README.md`, Root-`README.md`, `RELEASE_NOTES.md`, `docs/help/…`) | Schritt 6 der Reihenfolge mit konkreter Abschnittsliste | Doku-Aufgaben 34–37 | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] Negativtest CSRF/Antiforgery: mindestens ein HTTP-Flow-Test, der einen POST auf eine Formularroute **ohne** bzw. mit **ungültigem** Antiforgery-Token sendet und `400` erwartet (Bezug: Anforderung „CSRF-Schutz der POST-Formulare" sowie Validierungsregel „Alle POST-Routen → 400 Bad Request"). Aktuell belegen alle POST-Flow-Tests nur, dass der Token-Mechanismus mitgesendet wird — eine fehlende/falsch verdrahtete `ValidateRequestAsync`-Prüfung im Endpoint-Filter würde kein Test aufdecken. Ein sinnvoller Platz ist `Auth/SetupAndAuthTests` (z. B. `Post_WithoutAntiforgery_ReturnsBadRequest`) oder die E2E-Klasse.

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
| Profil-Passwortänderung | `Auth/ProfileTests.Profile_ChangePassword_Works_AndOldPasswordFails` — faktisch ein `WebApplicationFactory`-HTTP-Flow (Cookie, Antiforgery, Re-Login), entspricht der plan-eigenen E2E-Definition; nur nicht im E2E-Verzeichnis geführt | Abgedeckt (Einordnung siehe Hinweise) |
| Admin-Benutzer-CRUD (Anlegen inkl. Stufe, Deaktivieren/Aktivieren, Löschen, Unlock) | `Auth/AdminUserTests` — HTTP-Flows über Admin-Endpunkte mit Formular-Posts | Abgedeckt |
| Logout | `Auth/SetupAndAuthTests.Logout_ClearsSession` | Abgedeckt |
| CSRF-Blockade (POST ohne Token muss fehlschlagen) | — nicht geplant | **Lücke** |

## Fehlende oder unvollständige Planbestandteile

Keine weiteren festgestellt. Scope-Klärung ist vollständig gefüllt (inkl. Verhalten bei
fehlender `ServerSetting`-Zeile → Default 480), alle 7 Scope-Ausschlüsse tragen Begründung
und Status `bestätigt`, die Akzeptanzszenarien benennen Ausgangszustände (u. a. „kein
`data`-Verzeichnis, keine Benutzer"), die Umsetzungsreihenfolge enthält Voraussetzungen
je Schritt (u. a. `dotnet-ef`-Werkzeug, Temp-DB-Isolation vor Testumbau), und betroffene
Bestandstests sind vollständig benannt (`PageAccessTests`, `RelayTokenValidatorTests`,
`StatusRelayRoundtripTests`, `StatusPageComposerTests`, `StatusWebApplicationFactory`;
`InstanceSnapshotCacheTests`/`GitHubStatusServiceTests` korrekt als nicht betroffen
markiert). Die oben genannte fehlende Testanforderung (CSRF-Negativtest) ist die einzige
Lücke; sie betrifft den Tests-Abschnitt, nicht die Umsetzungsschritte.

## Hinweise

- **Lockout-Zeitablauf:** Der Plan begründet im Risiko-Abschnitt, dass die 1-h-Sperre
  nicht per `TestTimeProvider` vorspulbar sei. ASP.NET Core Identity löst `TimeProvider`
  seit .NET 8 aus DI auf — bei Registrierung des `TestTimeProvider` in der Factory wäre
  der Auto-Unlock-Ablauf ggf. doch testbar; alternativ zumindest `LockoutEnd ≈ now + 1 h`
  in `Login_ThreeFailures_LocksUser_AdminUnlockRestores` asserten, um die konfigurierte
  Dauer zu belegen.
- **E2E-Einordnung:** `ProfileTests`, `AdminUserTests`, `AdminTokenTests`,
  `AdminSettingsTests` und `SetupAndAuthTests` sind nach Plan-Definition
  (`WebApplicationFactory`-HTTP-Flows = E2E-Nachweis) faktisch E2E-Tests, liegen aber
  außerhalb von `E2E/`. Funktional unproblematisch; für die Nachverfolgbarkeit der
  AC↔E2E-Zuordnung ggf. in der Tests-Tabelle als „HTTP-Flow (E2E-Nachweis)" kennzeichnen.
- **`data/`-Verzeichnis vs. `.gitignore`:** `*.db`/`*.db-shm`/`*.db-wal` sind laut
  Bestandsaufnahme bereits repo-weit ignoriert — keine Plan-Ergänzung nötig.
- **Bearer-Header-Fallback am Hub:** bleibt wie bisher ohne expliziten Test (unveränderter
  Bestand; `AccessTokenProvider` sendet per Query). Optional als kleine Ergänzung in den
  umgebauten `StatusRelayRoundtripTests` möglich.
- **ForwardedHeaders:** Die `X-Forwarded-Proto`-Konfiguration ist via
  `WebApplicationFactory` nicht sinnvoll abzunehmen; die geplante manuelle
  Browser-Abnahme sollte einen Hinweis auf den Proxy-Fall enthalten (README-Punkt ist
  bereits geplant).
- **Duplikat-Benutzername:** Negativfall (zweiter Benutzer mit gleichem Namen) ist nur
  implizit über das Fehlermeldungs-Pattern abgedeckt; optional explizit in
  `AdminUserTests` ergänzen — keine Pflichtlücke.
