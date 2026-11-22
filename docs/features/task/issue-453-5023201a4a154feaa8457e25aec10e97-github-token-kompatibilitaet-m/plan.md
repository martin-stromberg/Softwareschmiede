# Umsetzungsplan: GitHub-Token-Kompatibilität mit `ghs_`-Installationstokens (Issue #453)

## Übersicht

Nachweis der Format-/Längenverträglichkeit der gesamten GitHub-Token-Kette (Einstellungs-UI → `ICredentialStore`/`WindowsCredentialStore` → `GitHubHttpApiClient`-Bearer-Header → `GH_TOKEN`-/`GIT_CONFIG_*`-Prozessumgebung → `gh`-/git-/Agenten-CLIs → Maskierung) für ~520-stellige `ghs_`-App-Installationstokens — mit einem synthetischen Token, da ein echtes nicht beschaffbar ist. Ergebnis: dokumentierte Aussage je Kettenglied in der Hilfe, festgelegtes Eintrags-Verhalten „warnen statt still akzeptieren" (`IDialogService.ConfirmDialog` beim Speichern) und dokumentierte funktionale Nichteignung (≈1h-Ablauf, `/user`-403, Bot-Viewer-Identität).

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Der benutzerseitig eingetragene GitHub-Authentifizierungs-Token als Credential auf dem gesamten internen Pfad — plus als Nebenartefakt die dokumentierte Kompatibilitätsaussage je Kettenglied. |
| Auslösende Ereignisse | Testläufe mit synthetischem `ghs_`-Token über die vorhandenen Testseams; produktiv neu: Nutzeraktion „Speichern" in den Einstellungen mit `ghs_`-Wert in einem `Secret`-Feld. |
| Erwartete Wirkung | Pro Kettenglied eine dokumentierte Aussage „verifiziert" oder „dokumentarisch bewertet"; ein Warn-Dialog bei `ghs_`-Eingabe; Hilfe-Abschnitt mit Token-Format-Aussage und Konsequenzen. |
| Umgang mit fehlenden Objekten | Testtoken: **erzeugen** — synthetisches ~520-stelliges `ghs_`-Token (JWT-artige Struktur, Zeichensatz `[A-Za-z0-9.\-_]`) als Test-Helfer. Ergebnisdokumentation: **erzeugen** in `docs/help/plugins/github-ereignisse/` (überlebt das Aufräumen; im Issue-Kommentar referenzierbar). `ScmE2ECredentialStore`: **wiederverwenden** (E2E-Credential-Seam). `WindowsCredentialStore`-Testeintrag: **erzeugen und verwerfen** (eigener Test-Schlüssel, `DeleteCredential` im `finally`). Echte GitHub App: nicht vorhanden — API-/CLI-Verhalten nur **dokumentarisch** bewerten. Legacy-`_netrc`: bestehende Bereinigung bleibt, keine Wiedereinführung. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein echter `ghs_`-Token, kein Live-Nachweis gegen `api.github.com`, `gh`-CLI oder `git` | Ein echtes Installationstoken erfordert eine installierte GitHub App (`POST /app/installations/{id}/access_tokens`), die nicht verfügbar ist; interne Schichten werden synthetisch verifiziert, das API-/CLI-Verhalten dokumentarisch anhand der GitHub-Dokumentation bewertet. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 2 | Keine funktionale `ghs_`-Unterstützung als Dauer-Credential (keine GitHub-App-Anbindung, keine Token-Ausstellung/-Erneuerung) | Die Anwendung ist reiner Token-Verbraucher; ein abgelaufenes `ghs_`-Token lässt sich ohne App-Credentials nicht erneuern. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 3 | Keine harte Ablehnung von `ghs_`-Eingaben — es wird **gewarnt**, nicht blockiert | Das Token ist technisch speicher- und transportierbar und für Kurztests denkbar legitim; die Warnung informiert über die funktionale Nichteignung, „Nein" bricht das Speichern ab. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 4 | Keine Formatvalidierung/Erkennung anderer Tokenformate (`ghp_`, `github_pat_`, `gho_` u. a.) | Ausdrückliche Anforderung; nur `ghs_` wird per Changelog-Regex `ghs_[A-Za-z0-9.\-_]{36,}` erkannt. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 5 | Kein Validierungs-/Warn-Hook im `PluginSettingField`-Vertrag | `PluginSettingField` ist ein deklarativer Record; ein Contract-Hook wäre eine über das Issue hinausgehende Vertragsänderung. Die Warnung wird hostseitig im `SettingsViewModel` umgesetzt. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 6 | `CheckHealthAsync`, `GetAvailableRepositoriesAsync`, `ResolveEventRepositoryRestAsync` bleiben unverändert | `CheckHealthAsync` hat keinen produktiven Aufrufer (nur `IAiPlugin.CheckHealthAsync` wird gerufen); die 403-Konsequenz für `ghs_` wird dokumentiert statt umgebaut. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 7 | `ScmEventValidationService`-Owner-Regel und `TrustedAutomationLogins` bleiben unverändert | Die Abweisung eines `<app-slug>[bot]`-Viewers ist korrektes Bestandsverhalten und die dokumentierte fachliche Konsequenz — kein zu behebender Fehler. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 8 | `EventFailureMessage`-Texte bleiben PAT-zentriert/generisch | Durch die Warnung beim Eintragen ist das generische Ablauf-Fehlerbild („GitHub-Anmeldung abgelaufen oder ungültig") hinreichend; kein tokenformat-sensitiver Hinweis nötig. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 9 | BitBucket-Plugin und GitHub Enterprise Server unverändert | Außerhalb des Issue-Scopes; GHE ist vom Formatwechsel laut Changelog nicht betroffen — GHE-Host-Varianten der Kette bleiben aber im Test-Nachweis erhalten. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |
| 10 | Keine DB- oder Konfigurationsänderung am Credential-Modell | `PluginConfiguration` persistiert nur `CredentialStoreKey`; Speicherung bleibt `ICredentialStore`/`WindowsCredentialStore`. | bestätigt (2026-11-22, Orchestrierung: direkte Ableitung der Anforderung bzw. nicht geforderter Zusatzumfang; finale Bestätigung via PR-Review) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `ghs_`-Erkennung — Ort | Host-seitig in `SettingsViewModel.SaveAsync` direkt nach `ValidateRequiredFields()`: Scan aller `PluginSettingFieldType.Secret`-Einträge von `_selectedPluginSettings` (genau die Werte, die `SavePluginSettingsAsync` persistiert). | `PluginSettingField` hat keinen Validierungshook (siehe Ausschluss 5); der Host trägt bereits die GitHub-spezifische Validierung `ValidateScmAccountMode` mit den Konstanten `GitHubPluginPrefix`/`GitHubAgentTokenFieldKey`. Der Scan über alle Secret-Felder deckt `Token` und `AgentToken` ohne Prefix-Kopplung ab und fängt Fehlpastes in anderen Plugin-Panels gleich mit — ein `ghs_`-Wert ist überall ein GitHub-Installationstoken. |
| `ghs_`-Erkennung — Pattern | `Regex.IsMatch` mit der Changelog-Regex `ghs_[A-Za-z0-9.\-_]{36,}` — wörtlich, ohne Verankerung/Verschärfung. | Ausdrückliche Anforderung: die UI-Erkennung darf nicht enger sein als die Changelog-Regex. Da die Konsequenz nur eine Rückfrage ist (keine Ablehnung), sind seltene False-Positives unkritisch. |
| Warn-UX | `IDialogService.ConfirmDialog` (`WpfDialogService` → `MessageBox` Ja/Nein + Warning-Icon): „Ja" → normaler Speicherpfad, „Nein" → `SaveAsync` bricht ab, nichts wird persistiert. | Einziges etabliertes „warnen-aber-erlauben"-Muster im Projekt (Referenz: `ProjectDetailViewModel`, `ScmEventQueueViewModel`); erzeugt keinen neuen Inline-Warn-Mechanismus; im E2E über die stabilen Win32-Automation-IDs IDYES/IDNO adressierbar (Muster `DeleteConfirmationDialogView`). |
| Zeitpunkt der Warnung | Beim Speichern, nicht bei der Eingabe (`PasswordChanged`). | Einmalige, kontrollierte Rückfrage statt wiederholter Unterbrechung während der Eingabe; der kritische Moment ist die Persistierung. |
| Funktionale Nichteignung | Dokumentarische Bewertung plus Pinning-Tests auf dem bestehenden Verhalten (Bot-Viewer → Verwerfungsregel; `/user`-403 → `false`/leere Liste). | Das Verhalten ist bereits korrekt implementiert; die Tests fixieren die dokumentierten Aussagen reproduzierbar, statt sie nur zu behaupten. |
| `WindowsCredentialStore`-Nachweis | Neuer `OsInterface`-Test mit echtem `WindowsCredentialStore` unter eigenem Test-Schlüssel (Roundtrip ~520 Zeichen + Überschreitung >2560 Byte → `InvalidOperationException`), Cleanup per `finally`-`DeleteCredential`. | Die 2560-Byte-Grenze (`CRED_MAX_CREDENTIAL_BLOB_SIZE`) ist die einzige Hartgrenze der Kette und der zentrale Punkt des Issues; ein echter Roundtrip ist belastbarer als eine Rechenaussage. `OsInterface` ist die vorgesehene Kategorie für OS-nahe Tests (separate Lane), und `CredentialStoreSnapshot`/`WpfTestBase` belegen, dass der echte Store im Testkontext nutzbar ist. |
| Ergebnisdokumentation | Neuer Abschnitt „Token-Formate" in `docs/help/plugins/github-ereignisse/installation.md` (Aussage je Kettenglied + Warnverhalten + Nichteignung) plus Eintrag in `troubleshooting.md`. | `installation.md` ist die Token-Einrichtungsseite; die Hilfe überlebt das Aufräumen des Feature-Verzeichnisses und ist aus dem Issue-Kommentar referenzierbar. |
| Feld-Hinweistexte | `Description` der Felder `Token`/`AgentToken` in `GitHubPlugin.GetSettingGroups` um einen `ghs_`-Hinweis ergänzen. | Der Hinweis ist schon vor dem ersten Speichern sichtbar (statischer Text unter dem Feld) und kostet keine Mechanik; ergänzt, ersetzt aber nicht die Warnung beim Speichern. |

## Programmabläufe

### Kettenglied-Nachweis mit synthetischem `ghs_`-Token

1. `TestTokens.GhsInstallationToken` (neuer Test-Helfer) liefert ein ~520-stelliges Token: Präfix `ghs_`, JWT-artige Struktur mit zwei Punkten, Zeichensatz `[A-Za-z0-9.\-_]`.
2. **UI-Eingabe → Einstellungs-Persistenz:** `PasswordBox` → `PluginSettingEntry.Value` → `PluginSettingsService.SetValue` — Nachweis E2E (Kettenglied 7 unten) plus `PluginSettingsServiceIntegrationTests`-Roundtrip über `InMemoryCredentialStore` (`SetValue`/`GetValue` Identität).
3. **Windows Credential Store:** `WindowsCredentialStore.SetCredential`/`GetCredential`/`DeleteCredential` Roundtrip mit dem ~520-Zeichen-Token unter einem Test-Schlüssel (`OsInterface`-Lane) — exakte Identität; zusätzlich ein >1280-UTF-16-Zeichen-Wert (>2560 Byte) → `InvalidOperationException` (dokumentiert die `CredWriteW`-Hartgrenze und das Fehlerbild).
4. **HTTP-Transport:** `GitHubHttpApiClient.SendAsync` über den `DelegateHandler`-Seam — `AuthenticationHeaderValue("Bearer", token)`, `Parameter` exakt gleich dem Token (JWT-Zeichensatz und Länge unbeanstandet).
5. **gh-/git-Prozessumgebung:** `GitHubPlugin.GetAgentEnvironmentAsync` sowie ein repräsentativer `ICliRunner`-Mock-Aufruf (z. B. `CloneRepositoryAsync` bzw. `CreateIssueAsync`) — `GH_TOKEN` exakt, `HasGitHubExtraHeader`-Seam prüft `GIT_CONFIG_VALUE_0` = `"AUTHORIZATION: basic " + Base64("x-access-token:" + token)` (Wert ~720–740 Zeichen).
6. **Maskierung:** `SanitizeSensitiveOutput` — Token literal und als `oauth2:<token>@`-Einbettung → `***`/`oauth2:***@`.
7. **UI↔Store-Roundtrip (E2E):** SCM-E2E-App → Einstellungen → Plugins → GitHub → `ghs_`-Token ins `Token`-Feld → Speichern → Warnung bestätigen → Panel neu wählen → Wert wieder geladen (durchläuft `ScmE2ECredentialStore` + `PluginSettingsService`).
8. **Dokumentarisch (kein Testbedarf, keine Längenlogik im Code):** `PasswordBox` (kein `MaxLength`), `PluginSettingEntry.Value`, `CliRunner.CreateProcess` (nur `ProcessStartInfo.Environment`, kein CLI-Argument), `AiExecutionService.WithScmAgentEnvironmentAsync` (reine Merge-Durchleitung), `PluginConfiguration` (persistiert nur `CredentialStoreKey`), echte `gh`-/git-CLI-Verträglichkeit und GitHub-API-Antworten — Letztere nur dokumentarisch bewertet (kein echtes Token).

Beteiligte Klassen/Komponenten: `TestTokens`, `PluginSettingsService`, `InMemoryCredentialStore`, `WindowsCredentialStore`, `GitHubHttpApiClient`, `GitHubPlugin` (`GetGhEnvironment`/`GetGitEnvironment`/`GetAgentEnvironmentAsync`/`AddGitHubHttpAuthHeader`/`SanitizeSensitiveOutput`), `ScmE2ECredentialStore`, `SettingsView` (E2E-View).

### `ghs_`-Warnung beim Speichern

1. Nutzer trägt einen `ghs_`-Wert in ein `PluginSettingFieldType.Secret`-Feld des geöffneten Plugin-Einstellungspanels ein (`PasswordBox` → `OnPasswordChanged` → `PluginSettingEntry.Value`).
2. `SettingsViewModel.SaveAsync` ruft nach `ValidateRequiredFields()` die neue Methode `ConfirmGitHubInstallationTokens()` auf.
3. Diese scannt alle `Secret`-Einträge von `_selectedPluginSettings`; bei einem Treffer von `GitHubInstallationTokenPattern` auf dem (getrimmten) Wert ruft sie `_dialogService.ConfirmDialog` mit den betroffenen Feld-Labels und der Begründung (GitHub-App-Installationstoken, läuft nach ~1 Stunde ab, liefert keine Benutzeridentität → als dauerhaftes Credential ungeeignet) auf.
4. „Ja" → `SaveAsync` läuft normal weiter (`SavePluginSettingsAsync` persistiert den Wert); „Nein" → Rückkehr ohne jede Persistierung und ohne Fehlerbanner (der Dialog selbst war die Rückmeldung).
5. Kein Treffer (z. B. `ghp_`-PAT) → kein Dialog, Speichern wie bisher.

Beteiligte Klassen/Komponenten: `SettingsViewModel`, `PluginSettingEntry`, `IDialogService`/`WpfDialogService`, `PluginSettingsService`.

### Ergebnisdokumentation

1. `docs/help/plugins/github-ereignisse/installation.md`: neuer Abschnitt „Unterstützte Token-Formate" — PAT als unterstütztes Format; `ghs_`-Installationstoken: technisch durchgängig (Aussage je Kettenglied in kompakter Tabelle), aber funktional ungeeignet (≈1h-Ablauf ohne Erneuerung, `GET /user`/`GET /user/repos` → 403 → leere Repository-Liste/fehlende Health-Prüfung, GraphQL-`viewer` = `<app-slug>[bot]`-Identität → Owner-Regel/`@ViewerLogin`-Trigger greifen nicht); Verhalten beim Eintragen: Warn-Dialog mit Bestätigung.
2. `docs/help/plugins/github-ereignisse/troubleshooting.md`: Eintrag — Anmeldefehler („GitHub-Anmeldung abgelaufen oder ungültig") etwa eine Stunde nach Eintrag eines `ghs_`-Tokens ist das erwartete Ablaufverhalten des Formats; Lösung: Personal Access Token verwenden.
3. Issue-Rückmeldung: die Aussage je Kettenglied wird im Issue-Kommentar mit Verweis auf den Hilfe-Abschnitt und die Testnamen festgehalten (Erstellung des Kommentars liegt beim Lifecycle-Schritt, nicht bei diesem Plan).

Beteiligte Klassen/Komponenten: — (Dokumentation).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `TestTokens` (`src/Softwareschmiede.Tests/Helpers/TestTokens.cs`) | statische Hilfsklasse | `GhsInstallationToken`: synthetisches ~520-stelliges Token (`ghs_`-Präfix, zwei Punkte/JWT-Struktur, Zeichensatz `[A-Za-z0-9.\-_]`); `OverlongCredentialValue`: Wert >1280 UTF-16-Zeichen für die `CredWriteW`-Grenzprobe. |
| `WindowsCredentialStoreTests` (`src/Softwareschmiede.Tests/Infrastructure/Services/WindowsCredentialStoreTests.cs`) | Testklasse (`OsInterface`-Kategorie) | Echter Roundtrip des ~520-Zeichen-Tokens und der Grenzfall >2560 Byte → `InvalidOperationException` am echten `WindowsCredentialStore`; eigener Test-Schlüssel mit `finally`-`DeleteCredential`. |
| `GhsTokenWarningDialogView` (`src/Softwareschmiede.Tests/E2E/Views/Dialogs/GhsTokenWarningDialogView.cs`) | `DialogView`-Subklasse | FlaUI-Zugriff auf den nativen MessageBox-Warndialog (Fenstertitel, `Confirm`/`Cancel` via AutomationId `6`/`7`, Screenshot-Nachweis). |

## Änderungen an bestehenden Klassen

### `SettingsViewModel` (ViewModel)

- **Neue Konstanten/Felder:** `GitHubInstallationTokenPattern` (`Regex`/`const` für `ghs_[A-Za-z0-9.\-_]{36,}`).
- **Neue Methoden:** `ConfirmGitHubInstallationTokens()` — scannt die `Secret`-Einträge von `_selectedPluginSettings`; Treffer → `_dialogService.ConfirmDialog` (Titel z. B. „GitHub-Installationstoken", Message mit Feld-Labels + Begründung + „Trotzdem speichern?"); Rückgabe `bool`.
- **Geänderte Methoden:** `SaveAsync` — `ConfirmGitHubInstallationTokens()` direkt nach `ValidateRequiredFields()` aufrufen; `false` → Abbruch vor jeder Persistierung.

### `GitHubPlugin` (Plugin)

- **Geänderte Methoden:** `GetSettingGroups` — `Description` der Felder `Token` und `AgentToken` um den Hinweis ergänzen, dass `ghs_`-App-Installationstokens zwar speicherbar, aber nach ~1 Stunde abgelaufen und ohne Benutzeridentität sind (als dauerhaftes Credential ungeeignet).

### `SettingsView` (E2E-Testview, `src/Softwareschmiede.Tests/E2E/Views/SettingsView.cs`)

- **Neue Methoden:** `SetPluginSecretValue(string automationName, string value)` — setzt ein `Secret`-Feld (`PasswordBox`) über das UIA-ValuePattern (Fallback: Tastatureingabe); `ClickSave()` ohne Warte auf die Erfolgsmeldung (für den Dialog-Pfad; bestehendes `SaveSettings` wartet auf „Settings gespeichert.").

### `ScmEventsE2ETests` (`src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs`)

- **Neue Tests:** Fact `ScmEvents_GitHubGhsTokenWarning_UiRoundtrip_E2E` — eigener App-Start im SCM-E2E-Modus (nur dort steht das `GitHubPlugin` in der Plugin-Liste; `PluginManager.IsAllowedInTestMode` lädt es im normalen Testmodus nicht). Schlankes Setup anstreben: `TestRoot` + `scm-config.json` + leeres `scm-scenario.json` + `simulator/`-Verzeichnis + `SOFTWARESCHMIEDE_TEST_DB_PATH` — ohne die 17-Workspace-Initialisierung der `ScmE2EFixture`; Fallback: volle Fixture.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Secret`-Felder der zu speichernden Plugin-Einstellungen (`Token`, `AgentToken` und jedes andere Secret-Feld des aktiven Panels) | `Regex.IsMatch` mit `ghs_[A-Za-z0-9.\-_]{36,}` auf dem getrimmten Wert → Warn-Dialog | Kein Validierungsfehler: nur eine bestätigbare Rückfrage; „Nein" bricht `SaveAsync` ohne Persistierung ab. Die Regel zieht den Wertebereich nicht enger als die Changelog-Regex. |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`SettingsViewModelTests`:** `CreateSut` nutzt `new Mock<IDialogService>().Object` — Moq-Default liefert `ConfirmDialog` = `false`; bestehende Save-Tests verwenden keine `ghs_`-Werte und bleiben grün. Für die neuen Warn-Tests muss der Mock zugänglich gemacht werden (Feld oder Rückgabeparameter von `CreateSut`).
- **`GitHubPlugin.GetSettingGroups`-Texte:** Description-Änderungen können bestehende Metadaten-Assertions betreffen (`PluginMetadata_ShouldExposeExpectedValues`, `GetSettingGroups_ContainsOptionalAgentTokenField` in `GitHubPluginTests`) — Assertions vor Änderung prüfen und ggf. anpassen.
- **`PluginManager.IsAllowedInTestMode`:** Das `GitHubPlugin` steht im normalen E2E-Testmodus nicht zur Verfügung — der E2E-Nachweis erfordert zwingend den SCM-E2E-Modus (`SOFTWARESCHMIEDE_SCM_TEST_CONFIG`), in dem `ScmE2EPluginManager` das echte Plugin bereitstellt. Ein nicht erkannter Modus würde das Szenario still unmöglich machen.
- **`PasswordBox` über UIA:** Das Setzen/Lesen eines ~520-Zeichen-Werts über das ValuePattern ist der übliche Weg; falls das Lesen in FlaUI nicht den Klartext liefert, erfolgt der Roundtrip-Nachweis alternativ über die Länge bzw. ein erneutes Speichern ohne erneuten Dialog.
- **`WindowsCredentialStoreTests`:** Schreibt ausschließlich unter einem eigenen Test-Schlüssel mit `finally`-`DeleteCredential` — kein Eingriff in produktive Credentials; läuft nur in der `OsInterface`-Lane (nicht in der stabilen Lane). Ist die `OsInterface`-Lane in der aktuellen Umgebung nicht ausführbar, wird die Aussage dokumentarisch geführt (2560-Byte-Grenze rechnerisch belegt, Fehlerbild aus dem Codepfad) und der Test trotzdem committed — er bleibt auf Dev-/CI-Maschinen wirksam.
- **Gefundene Bruchstellen:** Kleine, lokal begrenzte Fixes werden direkt in diesem Branch behoben; strukturelle Befunde (z. B. Validierungshook, Token-Erneuerungslogik) werden als Folge-Issues dokumentiert — wie im Issue vorgesehen.
- **Kein Risiko für laufende `Softwareschmiede.App.exe`-Instanzen** — keine Prozessbeendigung geplant.

## Umsetzungsreihenfolge

1. **Test-Helfer `TestTokens` anlegen**
   - Voraussetzungen: keine.
   - Beschreibung: `GhsInstallationToken` (~520 Z., JWT-Struktur) und `OverlongCredentialValue` (>1280 Z.) in `src/Softwareschmiede.Tests/Helpers/TestTokens.cs`.

2. **Kettenglied-Unit-/Integrationstests**
   - Voraussetzungen: Schritt 1; vorhandene Seams `InMemoryCredentialStore`, `DelegateHandler`, `Mock<ICliRunner>`/`HasGitHubExtraHeader`, `FakeGitHubEventApi`.
   - Beschreibung: `PluginSettingsServiceIntegrationTests` (Roundtrip), `GitHubHttpApiClientTests` (Bearer-Assertion), `GitHubPluginTests` (`GetAgentEnvironmentAsync`- und git-/gh-Env-Assertions, `SanitizeSensitiveOutput`).

3. **`WindowsCredentialStoreTests` (`OsInterface`)**
   - Voraussetzungen: Schritt 1; `OsInterfaceFact`-Attribut und `CredentialStoreSnapshot`-Muster vorhanden.
   - Beschreibung: Roundtrip ~520-Zeichen-Token + Überschreitung → `InvalidOperationException`, eigener Test-Schlüssel mit Cleanup.

4. **Pinning-Tests der funktionalen Bewertung**
   - Voraussetzungen: vorhandene Klassen `ScmEventValidationServiceTests`, `GitHubPluginTests`.
   - Beschreibung: Bot-Viewer (`<app-slug>[bot]`, nicht Owner) → Verwerfung im `UserAccount`-Modus; `CheckHealthAsync` 403 → `false`; `GetAvailableRepositoriesAsync` 403 → leere Liste (falls generisch bereits abgedeckt, bestehenden Test referenzieren statt duplizieren).

5. **UI-Warnung implementieren**
   - Voraussetzungen: keine (baut auf bestehendem `IDialogService`).
   - Beschreibung: `SettingsViewModel` (`GitHubInstallationTokenPattern`, `ConfirmGitHubInstallationTokens`, `SaveAsync`-Aufruf) plus `Description`-Ergänzungen in `GitHubPlugin.GetSettingGroups`.

6. **ViewModel-Tests der Warnung**
   - Voraussetzungen: Schritt 5; `_dialogService`-Mock zugänglich.
   - Beschreibung: Bestätigen → persistiert; Abbrechen → nichts persistiert; PAT → kein Dialog.

7. **E2E-Test des Warn- und Roundtrip-Flusses**
   - Voraussetzungen: Schritt 5; SCM-E2E-Modus, `DialogView`-Basis, `SettingsView`-Helper.
   - Beschreibung: `GhsTokenWarningDialogView`, `SetPluginSecretValue`/`ClickSave` in `SettingsView`, Fact `ScmEvents_GitHubGhsTokenWarning_UiRoundtrip_E2E` inkl. BoundingBox-/Sichtprüfungs-Screenshot des Dialogs.

8. **Hilfe-Dokumentation**
   - Voraussetzungen: Schritte 2–4 (Nachweise), Schritt 5 (festgelegtes Verhalten).
   - Beschreibung: Abschnitt „Token-Formate" in `installation.md` mit Aussage je Kettenglied; Eintrag in `troubleshooting.md`.

9. **Abschlussverifikation**
   - Voraussetzungen: alle vorherigen Schritte.
   - Beschreibung: `dotnet build Softwareschmiede.slnx`; stabile Lane `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test ... --filter "Category!=OsInterface"`; `OsInterface`-Lane separat für die neuen OS-Tests und den E2E-Fact; `dotnet format Softwareschmiede.slnx --verify-no-changes`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GhsInstallationToken`, `OverlongCredentialValue` | `TestTokens` (Hilfsklasse) | Synthetisches ~520-stelliges `ghs_`-Token (JWT-Struktur) bzw. >1280-Zeichen-Wert für die Grenzprobe. |
| `SetValue_GhsInstallationToken_RoundtripsUnchanged` | `PluginSettingsServiceIntegrationTests` | `SetValue`/`GetValue` über `InMemoryCredentialStore` liefert das Token unverändert (Kettenglied Einstellungs-Persistenz). |
| `SendAsync_GhsInstallationToken_BearerHeaderCarriesExactToken` | `GitHubHttpApiClientTests` | `Authorization: Bearer` `Parameter` ist exakt das ~520-stellige Token (Kettenglied HTTP-Transport). |
| `GetAgentEnvironmentAsync_GhsToken_ProvidesGhAndGitAuth` | `GitHubPluginTests` | `GH_TOKEN` exakt + `HasGitHubExtraHeader` mit ~740-stelligem `GIT_CONFIG_VALUE_0` (Kettenglieder Agent-/git-Umgebung). |
| `CloneRepositoryAsync_GhsToken_PassesEnvironmentToCli` (oder `CreateIssueAsync`-Variante) | `GitHubPluginTests` | `ICliRunner`-Mock empfängt `GH_TOKEN`/extraheader unverändert (Kettenglied gh-/git-CLI-Aufruf). |
| `SanitizeSensitiveOutput_GhsToken_IsMasked` | `GitHubPluginTests` | Literal- und `oauth2:…@`-Maskierung des ~520-stelligen Tokens. |
| `SetCredential_GhsInstallationToken_RoundtripsExact` | `WindowsCredentialStoreTests` (`OsInterface`) | Echter `CredWriteW`/`CredReadW`-Roundtrip unter Test-Schlüssel (Kettenglied OS-Credential-Store, ~1040 Byte < 2560). |
| `SetCredential_OverlongValue_ThrowsInvalidOperation` | `WindowsCredentialStoreTests` (`OsInterface`) | >2560-Byte-Wert → `InvalidOperationException` mit Win32-Fehler (dokumentiert Hartgrenze + Fehlerbild). |
| `GetRejection_BotViewerNotOwner_IsRejected` | `ScmEventValidationServiceTests` | `<app-slug>[bot]`-Viewer, der nicht Owner ist, wird im `UserAccount`-Modus verworfen (funktionale Nichteignung). |
| `CheckHealthAsync_Forbidden_ReturnsFalse` | `GitHubPluginTests` | `GET /user` 403 → `false` (pins die `/user`-403-Konsequenz für `ghs_`). |
| `GetAvailableRepositoriesAsync_Forbidden_ReturnsEmptyList` | `GitHubPluginTests` | `GET /user/repos` 403 → leere Liste + Warnung (nur ergänzen, falls der 403-Fall nicht generisch abgedeckt ist). |
| `Save_GhsToken_ConfirmDialogConfirmed_Persists` | `SettingsViewModelTests` | `ConfirmDialog` aufgerufen; „Ja" → Token landet im `InMemoryCredentialStoreForSettings`. |
| `Save_GhsToken_ConfirmDialogCancelled_PersistsNothing` | `SettingsViewModelTests` | „Nein" → kein `SetCredential`-Aufruf, keine Settings-Persistierung. |
| `Save_PatValue_NoConfirmDialog` | `SettingsViewModelTests` | `ghp_`-Wert → `ConfirmDialog` wird nicht aufgerufen. |
| `Confirm`/`Cancel` | `GhsTokenWarningDialogView` (E2E-Helfer) | IDYES/IDNO-Zugriff auf den nativen Warndialog. |
| `SetPluginSecretValue`, `ClickSave` | `SettingsView` (E2E-Helfer) | `PasswordBox` per UIA setzen; Speichern ohne Erfolgswarte auslösen. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `SettingsViewModelTests.CreateSut` | `Mock<IDialogService>` muss für die neuen Warn-Tests zugänglich sein (Feld/Parameter); alle bestehenden Save-Tests laufen mit Mock-Default (`ConfirmDialog` = `false`) weiter, da kein `ghs_`-Wert verwendet wird. |
| `GitHubPluginTests.PluginMetadata_ShouldExposeExpectedValues` / `GetSettingGroups_ContainsOptionalAgentTokenField` | Prüfen, ob `Description`-Texte der Felder `Token`/`AgentToken` assertiert werden; bei Änderung der Beschreibungen ggf. anpassen. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | `ghs_`-Token in das GitHub-`Token`-Feld eintragen → Speichern → Warndialog bestätigen → Erfolgsmeldung → Panel neu wählen → Wert geladen; anschließend `ghs_`-Eingabe → Dialog abbrechen → kein Speichern; PAT-Eingabe → kein Dialog. | `ScmEventsE2ETests.ScmEvents_GitHubGhsTokenWarning_UiRoundtrip_E2E` (`E2E_ScmEvents.cs`) + `GhsTokenWarningDialogView` + `SettingsView.SetPluginSecretValue` | Festgelegtes Eintrags-Verhalten „warnen statt still akzeptieren" über den kompletten UI→Credential-Store-Pfad | Der Warn-Dialog ist eine benutzerseitige Interaktion (native MessageBox) — nur FlaUI kann Auslösen, Sichtbarkeit und Bestätigung am echten Fenster nachweisen; der Roundtrip über `ScmE2ECredentialStore` belegt die vollständige UI-Kette. |
| Pflicht | BoundingBox-/Screenshot-Nachweis des Warndialogs (Dialog innerhalb der Bildschirmgrenzen, Ausschnitt für die Sichtprüfung der Abnahme-Screenshots in Schritt 10). | Derselbe Fact (`Capture()` des Dialogfensters, Dateiablage im Fixture-`TestRoot`). | Geometrische Assertion des Dialogs | CI-erzwungener Layout-/Sichtnachweis statt aufmerksamkeitsabhängiger Prüfung. |

Bestehende E2E-Tests, die angepasst werden müssen: **Keine** (neuer Fact in bestehender Klasse; `WpfTestBase.ManagedCredentialKeys` muss nicht erweitert werden — der SCM-E2E-Modus nutzt den prozessgebundenen `ScmE2ECredentialStore` statt des OS-Stores).

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: `ghs_`-Token eintragen, warnen, bestätigen, speichern, wieder laden | Nutzer klickt „Speichern" in den Einstellungen | SCM-E2E-App läuft; GitHub-Plugin-Panel geöffnet; `Token`-Feld leer bzw. Fixture-Token | `ghs_`-Token in `PasswordBox` eintragen → Speichern → Dialog „GitHub-Installationstoken" erscheint → „Ja" → „Settings gespeichert." → Panel neu wählen | Warn-Dialog erscheint; nach Bestätigung Erfolgsmeldung; beim erneuten Öffnen ist der Wert geladen (Credential-Roundtrip) | E2E `ScmEvents_GitHubGhsTokenWarning_UiRoundtrip_E2E` + Screenshot des Dialogs |
| 2 | `ghs_`-Eingabe abgebrochen | „Nein" im Warndialog | Wie #1 | `ghs_`-Token eintragen → Speichern → Dialog → „Nein" | Kein „Settings gespeichert.", nichts persistiert (Panel-Neuwahl zeigt den alten Wert) | E2E (selber Fact) + `SettingsViewModelTests.Save_GhsToken_ConfirmDialogCancelled_PersistsNothing` |
| 3 | PAT-Eingabe ohne Warnung | Nutzer speichert `ghp_`-Wert | Wie #1 | PAT eintragen → Speichern | Direkt „Settings gespeichert." ohne Dialog | E2E (selber Fact) + `Save_PatValue_NoConfirmDialog` |
| 4 | Credential-Store-Grenze | Test schreibt >1280 UTF-16-Zeichen | `WindowsCredentialStore` verfügbar (Windows) | `SetCredential` mit `OverlongCredentialValue` | `InvalidOperationException` mit Win32-Fehler; ~520-Zeichen-Token dagegen erfolgreich im Roundtrip | `WindowsCredentialStoreTests` (`OsInterface`-Lane) |
| 5 | Funktionale Nichteignung dokumentiert | API-Bewertung/Pinning-Tests | — | Bot-Viewer-Verwerfung, `/user`-403 → `false`, `/user/repos`-403 → leere Liste nachweisen; Ablauf-/Viewer-/Endpunkt-Bewertung in der Hilfe festhalten | Neue Hilfe-Abschnitte; Pinning-Tests grün | `ScmEventValidationServiceTests`, `GitHubPluginTests`, `installation.md`/`troubleshooting.md` |
| 6 | Ablauf-Fehlerbild (Regression) | Token läuft nach ~1h ab (401) | `ghs_`-Token gespeichert (gedacht) | API-Aufruf → 401 → `Authentication` → `EvictResolutions` + generischer Hinweistext | Bestehendes Fehlerbild tritt planmäßig ein (bereits abgedeckt, dokumentiert) | Bestehende `GitHubEventSourceTests.Failures_AreTypedAndSanitized`/`AccessFailures_ExplainTokenSettingsAndRecovery` + Hilfe-Eintrag |

## Offene Punkte

Keine.
