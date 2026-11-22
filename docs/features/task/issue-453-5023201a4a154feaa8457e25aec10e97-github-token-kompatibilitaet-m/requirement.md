# Übersetzte Anforderung: GitHub-Token-Kompatibilität mit neuem Installation-Token-Format (`ghs_`)

**Quelle:** Issue #453 / Aufgaben-ID `5023201a-4a15-4fea-a845-7e25aec10e97`
**Branch:** `task/issue-453-5023201a4a154feaa8457e25aec10e97-github-token-kompatibilitaet-m`

## Fachliche Zusammenfassung

GitHub stellt das Format der App-Installation-Tokens (Server-to-Server) auf stateless JWTs mit Präfix `ghs_` und ca. 520 Zeichen Länge um (inklusive `GITHUB_TOKEN` von GitHub Actions; nicht betroffen: GitHub Enterprise Server). Im GitHub-Plugin kann ein Anwender einen solchen Token in den beiden `PluginSettingFieldType.Secret`-Einstellfeldern `Token` und `AgentToken` (Credential-Keys `Softwareschmiede.GitHub.Token`/`Softwareschmiede.GitHub.AgentToken`, `GitHubPlugin.cs` Zeilen 22–23, 67–80) eintragen — die gesamte interne Token-Kette (Einstellungs-UI → `ICredentialStore`/`WindowsCredentialStore` → `GitHubHttpApiClient`-Bearer-Header → `GH_TOKEN`-Prozessumgebung → `GIT_CONFIG_*`-`http.extraheader`) ist auf Format- und Längenverträglichkeit zu prüfen. Ergänzend ist die funktionale Eignung zu bewerten: Installation-Tokens laufen nach ~1 Stunde ab, `GET /user` und `GET /user/repos` antworten mit 403, und der GraphQL-`viewer` ist eine Bot-Identität (`<app-slug>[bot]`), kein Benutzer. Ergebnis ist eine dokumentierte Aussage je Kettenglied plus eine Scope-Empfehlung; falls `ghs_` als Dauer-Credential ungeeignet eingestuft wird, ist das Verhalten beim Eintragen (akzeptieren/warnen/ablehnen) festzulegen und in der Hilfe zu dokumentieren.

## Fachlicher Scope

- **Verarbeitungseinheit:** Der benutzerseitig eingetragene GitHub-Authentifizierungs-Token als Credential, das durch alle internen Verbraucher-Schichten fließt — sowie als Nebenartefakt die dokumentierte Kompatibilitätsaussage je Kettenglied (verifiziert vs. Bruchstelle mit Reproduktion).
- **Auslösende Ereignisse:**
  - Nutzeraktion „Speichern" in den Einstellungen (`SettingsView` → `SettingsViewModel.SaveCommand` → `PluginSettingsService.SetValue` → `ICredentialStore.SetCredential` → `CredWriteW`).
  - Prozesslose API-Aufrufe des Plugins mit dem Token (`GitHubHttpApiClient.SendAsync`/`UploadAsync` — alle `IScmEventSource`-/`IScmEventResponder`-/`IScmNotificationSource`-Pfade sowie `CheckHealthAsync`, `GetAvailableRepositoriesAsync`).
  - `gh`-CLI-Mutationen (`gh issue create`, `gh pr create`, `gh pr merge`) und git-Operationen (Clone/Push/Pull/`ls-remote`, PR-Checkout) via `GetGhEnvironment`/`GetGitEnvironment`.
  - Start einer Agenten-CLI-Session (`AiExecutionService` → `IScmAgentAccess.GetAgentEnvironmentAsync`).
  - Hintergrundzyklen der Ereignisverarbeitung (`ScmEventMonitoringService`, `ScmBackmergeMonitoringService`, `ScmIssuePreparationMonitoringService`, `ScmTaskCompletionMonitoringService`, `ScmEventOrchestrator`, `ScmBackmergeService`) — alle über `GetEventRepositoryAsync` (`viewer`/`viewerPermission`) bzw. `CanWrite`.
  - Tokenablauf zur Laufzeit (401 → `ScmProviderFailureKind.Authentication` → `EvictResolutions` + Benutzerfehlermeldung `EventFailureMessage`).
- **Erwartete Wirkung:**
  - Pro Kettenglied eine dokumentierte Aussage „keine Längen-/Formatannahmen" oder eine gefundene Bruchstelle mit Reproduktion (Abnahmekriterium 1).
  - Eine dokumentierte Aussage zu den funktionalen Einschränkungen (Ablaufzeit, `/user`-403, Viewer-Bot-Identität) mit daraus folgender Scope-Empfehlung (Abnahmekriterium 2).
  - Gefundene Bruchstellen: direkt beheben oder als Folge-Issues ausgliedern. Falls `ghs_` als ungeeignet eingestuft wird: festgelegtes Verhalten beim Eintragen (akzeptieren/warnen/ablehnen) plus Dokumentation in der Hilfe (Abnahmekriterium 3).
- **Umgang mit fehlenden Objekten:**
  - *Testtoken im `ghs_`-JWT-Format:* liegt nicht vor und kann nur über eine installierte GitHub App erzeugt werden (`POST /app/installations/{id}/access_tokens` mit Header `X-GitHub-Stateless-S2S-Token: enabled`) — ist keine vorhandene App verfügbar, ist für die internen Schichten ein synthetisches ~520-stelliges Token (Zeichensatz `ghs_[A-Za-z0-9.\-_]{36,}`, zwei Punkte/JWT-Struktur) zu erzeugen; das API-Verhalten bleibt dann nur dokumentiert zu bewerten (**offene Frage**).
  - *Ergebnisdokumentation:* nicht vorhanden — ist zu erzeugen (Nachweis je Kettenglied in der Task-Dokumentation bzw. in `docs/help/plugins/github-ereignisse/`).
  - *Bruchstellen-Behandlung:* nicht vorhanden — je nach Befund direkt beheben oder Folge-Issues erzeugen (Entscheidung offen).
  - *UI-Verhalten bei `ghs_`-Eingabe:* nicht vorhanden — wäre neu zu erzeugen, sofern die Scope-Entscheidung eine Warnung/Ablehnung verlangt; bei stillem Akzeptieren entfällt es.
  - *Hilfe-Dokumentation:* vorhandene Seiten (`docs/help/plugins/github-ereignisse/installation.md`, `beschreibung.md`, `troubleshooting.md`) sind zu ergänzen (wiederverwenden).
  - *Legacy-`_netrc`-Einträge:* bestehende Bereinigung (`RemoveLegacyNetrcEntries`) bleibt bestehen — keine Wiedereinführung der netrc-Persistenz.
- **Scope-Ausschlüsse:**
  - Keine GitHub-App-Anbindung in der Anwendung: Ausstellung und Erneuerung von Installation-Tokens (`POST /app/installations/{id}/access_tokens`, JWT-Signierung mit App-private-key) ist nicht Teil der Anwendung — die App ist reiner Token-Verbraucher. Der GitHub-Vorabtest-Header `X-GitHub-Stateless-S2S-Token` wirkt ohnehin nur auf die Token-Ausstellung und ist für die Anwendung daher nicht nutzbar.
  - Keine Token-Erneuerungslogik: ein abgelaufenes `ghs_`-Token kann ohne App-Credentials nicht erneuert werden — wird nicht implementiert.
  - Keine Änderung am Credential-Modell: Speicherung bleibt `ICredentialStore`/`WindowsCredentialStore`; `PluginConfiguration` persistiert weiterhin nur `CredentialStoreKey` (kein Token in der DB).
  - Keine Wiedereinführung der `_netrc`-Persistenz.
  - Keine Token-Formatvalidierung anderer/bekannter Formate einführen; wird künftig eine `ghs_`-Erkennung eingeführt, ist die Changelog-Regex `ghs_[A-Za-z0-9.\-_]{36,}` zu verwenden — nicht enger (ausdrückliche Anforderung).
  - BitBucket-Plugin außerhalb des Scopes; GitHub Enterprise Server ist vom GitHub-Formatwechsel laut Changelog nicht betroffen (GHE-Hosts bleiben als Varianten der Kette jedoch im Nachweis relevant — Annahme).

## Betroffene Klassen und Komponenten

### Kettenglieder der Format-/Längenverträglichkeit

- `src/Softwareschmiede.App/Views/SettingsView.xaml` — `SecretTemplate` mit `PasswordBox` (Zeilen 17–25): kein `MaxLength`, keine Formatvalidierung; `PluginSettingEntryEditHelper.OnPasswordChanged` (`src/Softwareschmiede.App/Views/PluginSettingEntryEditHelper.cs` Zeilen 19–23) übernimmt `pb.Password` unverändert nach `PluginSettingEntry.Value`. → zu verifizieren: 520 Zeichen + JWT-Zeichensatz (`.`, `-`, `_`) passieren unverändert.
- `src/Softwareschmiede/Application/Services/PluginSettingsService.cs` — `SetValue`/`GetValue`/`DeleteValue` (Zeilen 33–57): reine Durchleitung an `ICredentialStore` mit Schlüssel `{PluginPrefix}.{FieldKey}` — keine Validierung.
- `src/Softwareschmiede/Infrastructure/Services/WindowsCredentialStore.cs` — `SetCredential` (Zeilen 60–83): UTF-16-Blob via `CredWriteW` (`CRED_TYPE_GENERIC`, `CredPersistLocalMachine`); die plattformseitige Grenze `CRED_MAX_CREDENTIAL_BLOB_SIZE` (2560 Byte ≈ 1280 UTF-16-Zeichen) wird im Code nicht geprüft — Fehlerbild bei Überschreitung ist `InvalidOperationException` (Zeilen 75–77). ~520 Zeichen ≈ 1040 Byte liegen rechnerisch unter der Grenze → zu verifizieren und zu dokumentieren.
- `src/Softwareschmiede/Domain/Entities/PluginConfiguration.cs` — persistiert nur `CredentialStoreKey` (Zeile 21); keine DB-Spaltenbegrenzung betroffen.
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubHttpApiClient.cs` — `SendAsync` (Zeilen 37–64) und `UploadAsync` (67–107): `AuthenticationHeaderValue("Bearer", token)` (Zeilen 47, 83), keine eigene Längen-/Formatvalidierung → zu verifizieren: JWT-Punkte/`-`/`_` sind für `AuthenticationHeaderValue` und den HTTP-Transport unbedenklich.
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` —
  - `GetGhEnvironment` (Zeilen 176–185): `GH_TOKEN`-Prozessumgebung für `gh`-Kindprozesse.
  - `GetGitEnvironment` (271–292): `GH_TOKEN` + hostgebundener `http.extraheader` für git-Aufrufe (`git clone`/`push`/`pull`/`ls-remote`, `CheckoutPullRequestSourceAsync`, `GetRemoteBranchesAsync`, `GetDefaultBranchAsync`).
  - `AddGitHubHttpAuthHeader` (223–235): `GIT_CONFIG_VALUE_0` = `"AUTHORIZATION: basic " + Base64("x-access-token:" + token)` — bei ~520 Zeichen Token ~720–740 Zeichen Wert → Umgebungs- und git-seitige Verträglichkeit zu verifizieren.
  - `GetAgentEnvironmentAsync` (198–213, `IScmAgentAccess`): gleiche Umgebung für Agenten-CLIs.
  - `RemoveLegacyNetrcEntries` (338–410): nur einmalige Bestandsbereinigung — netrc-Prüfpunkt entfällt weitgehend.
  - `SanitizeSensitiveOutput` (478–499): Literal-`Replace(token, "***")` über beide Credential-Keys — längenunabhängig, mit ~520-Zeichen-Token zu verifizieren (inkl. `EmbeddedTokenPattern` `oauth2:…@`, Zeile 26).
- `src/Softwareschmiede/Infrastructure/Services/CliRunner.cs` — `CreateProcess` (Zeilen 168–200): Token-Übergabe ausschließlich via `ProcessStartInfo.Environment` (Zeilen 193–197), nie als CLI-Argument.
- `src/Softwareschmiede/Application/Services/AiExecutionService.cs` — übernimmt `GetAgentEnvironmentAsync` in die `TerminalSessionStartSpec.EnvironmentVariables` der Agenten-CLI (Zeile ~465, Merge ~478).
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IScmAgentAccess.cs` — Vertragsseite der Agenten-Anbindung (Token gehört ausschließlich in die Prozessumgebung).

### Funktionale Berührungspunkte (Endpunkt-/Viewer-Einschränkungen)

- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` —
  - `CheckHealthAsync` (Zeilen 1701–1714): prüft `GET user` — für Installation-Tokens 403 (würde `false` liefern); **Befund:** nach aktuellem Codebestand kein produktiver Aufrufer in `src/` auffindbar (nur `IAiPlugin.CheckHealthAsync` wird gerufen — `ScmEventOrchestrator` Zeilen 257/724, `AiExecutionService` Zeile 356) → Tragweite zu verifizieren.
  - `GetAvailableRepositoriesAsync` (1751–1772): `GET user/repos?affiliation=owner` — für Installation-Tokens 403 → leere Liste + Log-Warnung. Verbraucher: `ProjectService.cs` Zeile 238, `ProjectListViewModel.cs` Zeile 232, `RepositoryAssignViewModel.cs` Zeile 277.
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs` —
  - `ResolveEventRepositoryGraphQlAsync` (88–107): `viewer{login id}` + `repository{… viewerPermission}` → befüllt `ScmEventRepository.ViewerId`/`ViewerLogin`/`CanWrite`; bei Installation-Token ist der Viewer eine Bot-Identität `<app-slug>[bot]`.
  - `ResolveEventRepositoryRestAsync` (112–127): REST-Fallback `GET user` + `GET repos/{owner}/{name}` — `GET user` scheitert mit 403.
  - `EvaluateResponse` (524–544): 403 → `ScmProviderFailureKind.Permission`, 401 → `Authentication` (`OnTransportFailure` → `EvictResolutions`, 504–513); Fehlertexte `EventFailureMessage` (546–551) weisen auf „Personal Access Token" hin.
  - `IsViewer` (613–615), `ValidateReference` (602–603: `ViewerId` Pflichtfeld).
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.EventResponses.cs` — `ExecuteResponseAsync` (43–89): `current.ViewerId != repository.ViewerId` → Permission-Fehler (Zeile 51), `!current.CanWrite` → Permission-Fehler (Zeile 53); `ReconcileResponseAsync`/`ReconcileReviewReactionAsync` gleichen eigene Aktionen über `IsViewer` ab (Zeilen 33, 128).
- `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs` — `GetRejection` (Zeilen 39–68): im Modus `ScmAccountMode.UserAccount` gilt „Angemeldeter Benutzer ist nicht Repository-Owner" (`SameIdentity(OwnerId/Owner ↔ ViewerId/ViewerLogin)`, Zeilen 55–56) — ein `<app-slug>[bot]`-Viewer ist nie der Owner → verworfene Ereignisse; `TrustedAutomationLogins` (Zeile 25: `github-actions[bot]`, `app/github-actions`) enthält kein generisches App-Bot-Login → Bot-verfasste Texte fallen unter „Textversion stammt nicht vom Repository-Owner" (62–64); im Modus `SeparateAccount` ist der Trigger die `@ViewerLogin`-Mention (Zeile 46) → mit Bot-Login funktional denkbar, aber nicht geprüft.
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` — `CanWrite`-Gates bei Zustellung/Dispatch (Zeilen 194–197, 662–665); `src/Softwareschmiede/Application/Services/ScmBackmergeService.cs` — `CanWrite`-Gate (Zeile 50).
- `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` — `ScmEventRepository`-Record (Zeilen 16–19: `ViewerId`, `ViewerLogin`, `CanWrite`, `Account`); `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmAccountMode.cs` — Kontomodi.
- Monitoring-Services (`ScmEventMonitoringService` Zeile 103, `ScmBackmergeMonitoringService` Zeile 77, `ScmIssuePreparationMonitoringService` Zeilen 84/202, `ScmTaskCompletionMonitoringService` Zeile 305) — alle über `GetEventRepositoryAsync` am selben Auflösungspfad betroffen.

### Tests und Dokumentation

- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` — vorhandene Seams für die Umgebungs-Assertions (`HasGitHubExtraHeader` → `GIT_CONFIG_KEY_0`/`GIT_CONFIG_VALUE_0`, Zeilen ~1457–1580, ~2947–2955), `CheckHealthAsync`-Tests (703–729), `SanitizeSensitiveOutput`-Tests (1274–1316, 1594ff.); `GitHubEventSourceTests.cs`; `src/Softwareschmiede.Tests/Application/Services/ScmEventValidationServiceTests.cs`; `PluginSettingsServiceIntegrationTests.cs`.
- `docs/help/plugins/github-ereignisse/installation.md` (Token-Einrichtung, Scopes), `beschreibung.md` (Kontomodi, Agenten-Anbindung), `troubleshooting.md` — Ort der geforderten Ergebnis-/Verhaltensdokumentation.

## Implementierungsansatz

1. **Kettenglied-weise Nachweisführung** mit einem synthetischen ~520-stelligen `ghs_`-Token (Präfix `ghs_`, zwei Punkte/JWT-Struktur, Zeichensatz `[A-Za-z0-9.\-_]` — bzw. mit echtem Token, falls beschaffbar): Eingabe in der `PasswordBox` → `PluginSettingsService.SetValue` → `WindowsCredentialStore.SetCredential` (Grenze `CRED_MAX_CREDENTIAL_BLOB_SIZE` 2560 Byte und Fehlerbild bei Überschreitung verifizieren) → `AuthenticationHeaderValue("Bearer", …)` (Header-Wert-Validierung) → `GH_TOKEN`-/`GIT_CONFIG_*`-Prozessumgebung (`CliRunner.CreateProcess`, Längen der Umgebungswerte inkl. Base64-aufgeblähtem `GIT_CONFIG_VALUE_0`) → `gh`-Mutationen und git-Operationen → `SanitizeSensitiveOutput`-Maskierung. Annahme: weitgehend als Unit-/Integrationstests auf den vorhandenen Seams (`ICliRunner`-Mock, `GitHubHttpApiClient`-Testhandler, `ScmE2ECredentialStore`) darstellbar; E2E-FlaUI-Nachweis der Settings-UI optional konsolidiert im bestehenden E2E-Rahmen.
2. **Funktionale Bewertung** anhand der dokumentierten Endpunkt-Einschränkungen: ~1h-Ablauf ohne Erneuerbarkeit; `GET /user`/`GET /user/repos` → 403 (betroffen: `CheckHealthAsync`, `GetAvailableRepositoriesAsync`, `ResolveEventRepositoryRestAsync`); GraphQL-`viewer`/`viewerPermission` mit Bot-Identität (`ViewerId` = Bot-`id`, `ViewerLogin` = `<app-slug>[bot]`, `CanWrite` aus `viewerPermission` bzw. `permissions.*`). Bewertung der daraus folgenden Regelverletzungen: Owner-Regel und `TrustedAutomationLogins` (`ScmEventValidationService`), `ViewerId`-/`CanWrite`-Gates (`ExecuteResponseAsync`, `ScmEventOrchestrator`, `ScmBackmergeService`), SeparatesKonto-Trigger `@ViewerLogin`.
3. **Scope-Entscheidung und Ergebnisdokumentation:** Aus den Befunden eine Empfehlung ableiten (erwartbar: „kein technischer Bruch, als dauerhaftes Credential ungeeignet") und je Kettenglied dokumentieren; Bruchstellen direkt beheben oder als Folge-Issues ausgliedern. Falls `ghs_` als ungeeignet eingestuft wird: Eintrags-Verhalten festlegen — bei Warnung/Ablehnung ist die Präfixerkennung über die Changelog-Regex `ghs_[A-Za-z0-9.\-_]{36,}` abzubilden (nicht enger); Dokumentation in `docs/help/plugins/github-ereignisse/installation.md`/`troubleshooting.md`.
4. **Regressionsschutz:** Verhalten bei abgelaufenem Token (401 → `Authentication` → `EvictResolutions` → Benutzerhinweis „GitHub-Anmeldung abgelaufen oder ungültig") und bei 403 (→ `Permission`-Meldung) auf Stimmigkeit prüfen — für `ghs_` tritt dieses Fehlerbild nach ~1 Stunde systematisch ein.

## Konfiguration

- Aus der Anforderung ist **kein** Konfigurationsbedarf ableitbar: Der Tokenpfad (Credential Store → Prozessumgebung/Bearer) ist unverändert; ein `ghs_`-Token wird — sofern nicht aktiv abgelehnt — über denselben Weg verarbeitet wie ein PAT.
- Optional denkbar (abhängig von der Scope-Entscheidung, nicht gefordert): eine Einstellung/Hinweis-Mechanik für das Warnen bei `ghs_`-Eingabe — wäre fachlich Teil der UI-Entscheidung, kein eigenständiger Konfigurationspunkt.

## Offene Fragen

1. **Scope-Entscheidung:** Nur die Format-/Längenverträglichkeit nachweisen, oder `ghs_` als unterstützten Tokentyp etablieren? Wegen Ablaufzeit und fehlendem User-Kontext ist der realistische Zielzustand vermutlich „kein technischer Bruch, aber als Dauer-Credential ungeeignet" — zu bestätigen.
2. **Testtoken-Beschaffung:** Ist eine installierte GitHub App mit Zugriff auf ein Test-Repository vorhanden, um ein echtes `ghs_`-JWT zu erzeugen (`POST /app/installations/{id}/access_tokens` mit `X-GitHub-Stateless-S2S-Token: enabled`)? Andernfalls: synthetisches ~520-stelliges Token für die internen Schichten + API-Verhalten nur anhand der dokumentierten Endpunkt-Einschränkungen bewerten.
3. **UI-Verhalten bei `ghs_`-Eingabe:** Soll die `PasswordBox`/Speicherlogik bei `ghs_`-Präfix warnen oder ablehnen (mit Begründung), statt den Token still zu akzeptieren — und gilt die Regel für beide Felder (`Token` und `AgentToken`) gleichermaßen?
4. **Fehlerbild bei Ablauf:** Nach ~1 Stunde schlägt jeder API-Aufruf mit 401 → `Authentication`-Meldung „GitHub-Anmeldung abgelaufen oder ungültig … Personal Access Token erneuern" (`EventFailureMessage`, `GitHubPlugin.Events.cs` Zeilen 546–551). Ist dieses generische Fehlerbild für `ghs_`-Tokens hinreichend, oder soll der Hinweistext tokenformat-sensibel sein?
5. **Tragweite von `CheckHealthAsync`:** `GitHubPlugin.CheckHealthAsync` (`GET /user`) hat nach aktuellem Codebestand keinen produktiven Aufrufer (nur `IAiPlugin.CheckHealthAsync` wird gerufen). Soll die 403-Bruchstelle trotzdem als potentielle dokumentiert werden (Interface-Pflicht `IGitPlugin.CheckHealthAsync`, `IGitPlugin.cs` Zeile 117)?
6. **Viewer-/Owner-Semantik:** Im Modus `UserAccount` verwirft `ScmEventValidationService.GetRejection` Ereignisse, weil der `<app-slug>[bot]`-Viewer nie der Repository-Owner ist; `TrustedAutomationLogins` kennt generische App-Bot-Logins nicht. Im Modus `SeparateAccount` wäre der Trigger eine `@<app-slug>[bot]`-Mention — soll das als legitimer (ggf. dokumentierter) Einsatzweg bewertet werden, oder ist die Bot-Identität insgesamt ein Ausschlusskriterium?
7. **`gh`-CLI/git-Verträglichkeit mit `ghs_`:** Akzeptieren die eingesetzten `gh`-CLI-Versionen `GH_TOKEN` mit `ghs_`-Präfix, und authentifiziert git den `http.extraheader`-Basic-Auth (`x-access-token:`) gegen Installation-Tokens — für GitHub Actions dokumentiert, für den Anwendungspfad zu verifizieren (echte CLI-Aufrufe, nicht nur Umgebungs-Assertions).
8. **Dokumentationsort:** Soll das Ergebnis zusätzlich zur Hilfe auch in der Feature-Dokumentation bzw. im Issue-Kommentar festgehalten werden, und werden gefundene Bruchstellen als eigene Folge-Issues ausgegliedert statt im selben Branch behoben?
