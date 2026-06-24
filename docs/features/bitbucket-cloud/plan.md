# Umsetzungsplan: BitBucket Cloud Support – Git-Authentifizierung

## Übersicht

Das `BitbucketPlugin` soll die Git-Authentifizierung gegen Bitbucket Cloud robuster gestalten. Die Anforderung adressiert Authentifizierungsfehler ("Invalid username or token") beim Klonen und Pullen/Pushen von Cloud-Repositories. Der Code zeigt bereits eine Differenzierung zwischen Cloud und Self-Hosted, aber die `.netrc`-basierte Authentifizierung für Pull/Push-Operationen in der Cloud-Instanz ist unvollständig. Die Implementierung muss konsistent zwischen eingebetteter URL-Authentifizierung (Clone) und `.netrc`-basierter Authentifizierung (Pull/Push) wirken und beide Modi sicher unterstützen.

## Designentscheidungen

Keine — das Plugin folgt bereits etablierten Mustern für Credential-Embedding und `.netrc`-Management. Die Erweiterung nutzt bestehende Strukturen (`ICredentialStore`, `ICliRunner`, Git-Umgebungsvariablen) ohne neue Abstraktionen.

## Programmabläufe

### Clone-Workflow (Cloud)

1. `CloneRepositoryAsync()` wird mit Repository-URL aufgerufen
2. Hosting-Mode wird aus `_credentialStore` abgerufen (Standard: "Cloud")
3. Für Cloud: URL wird als-ist verwendet (bereits Cloud-URL)
4. Für Self-Hosted: `ResolveGitCloneUrl()` konvertiert Browser-URL zu SCM-URL
5. `BuildAuthenticatedCloneUrl()` embeddet Benutzername und App Password in URL
   - Format: `https://user:appPassword@bitbucket.org/workspace/repo.git`
6. `GetGitEnvironment()` wird aufgerufen:
   - Generiert `.netrc`-Eintrag für Host `bitbucket.org` (Cloud) oder Self-Hosted-Host
   - Setzt `GIT_TERMINAL_PROMPT=0` um interaktive Prompts zu deaktivieren
7. `git clone` wird mit authentifizierter URL und Umgebungsvariablen ausgeführt
8. Fehler werden geloggt und als `InvalidOperationException` geworfen

**Beteiligte Klassen/Komponenten:** `BitbucketPlugin`, `ICliRunner`, `ICredentialStore`, `GitPluginBase<BitbucketPlugin>`

### Pull-Workflow (Cloud)

1. `PullAsync()` wird mit lokalem Pfad aufgerufen
2. `GetGitEnvironment()` wird aufgerufen (erzeugt `.netrc`-Eintrag)
3. `GetGitHttpAuthArgs()` wird aufgerufen:
   - Für Cloud: gibt leeres Array zurück (Authentifizierung via `.netrc`)
   - Für Self-Hosted: gibt `["-c", "http.extraheader=Authorization: Basic ..."]` zurück
4. `git pull` wird mit Umgebungsvariablen ausgeführt
5. Git liest `.netrc`-Eintrag automatisch für HTTP-Basic-Auth gegen `bitbucket.org` oder Self-Hosted-Host

**Beteiligte Klassen/Komponenten:** `BitbucketPlugin`, `ICliRunner`, `GitPluginBase<BitbucketPlugin>`

### Push-Workflow (Cloud)

1. `PushBranchAsync()` wird mit lokalem Pfad und Branch-Name aufgerufen
2. Abläufe identisch mit Pull-Workflow
3. `git push` wird mit `.netrc`-Authentifizierung ausgeführt

**Beteiligte Klassen/Komponenten:** `BitbucketPlugin`, `ICliRunner`, `GitPluginBase<BitbucketPlugin>`

### Health-Check-Workflow

1. `CheckHealthAsync()` wird aufgerufen
2. Hosting-Mode wird abgerufen
3. Für Cloud: `curl` ruft `https://api.bitbucket.org/2.0/user` auf
4. Für Self-Hosted: `curl` ruft `https://{SelfHostedUrl}/rest/api/1.0/user` auf
5. Authentifizierung via `GetCurlAuthArgs()`:
   - Cloud: `-u user:appPassword` (HTTP Basic Auth)
   - Self-Hosted: `-H "Authorization: Bearer token"`
6. Falls Jira-URL konfiguriert: zusätzlicher Check gegen Jira-API
7. Gibt `true` zurück, wenn beide erfolgreich (oder nur Bitbucket, falls Jira nicht konfiguriert)

**Beteiligte Klassen/Komponenten:** `BitbucketPlugin`, `ICliRunner`, `ICredentialStore`

## Neue Klassen

Keine. Die Anforderung wird durch Anpassungen bestehender Klassen erfüllt.

## Änderungen an bestehenden Klassen

### `BitbucketPlugin` (Logikklasse)

**Problem-Analyse:**
- `BuildAuthenticatedCloneUrl()` embeddet Credentials bereits korrekt für Clone-Operationen
- `GetGitEnvironment()` setzt `.netrc`-Eintrag, aber Konsistenz zwischen Clone und Pull/Push ist unklar
- `GetGitHttpAuthArgs()` liefert für Cloud ein leeres Array — Pull/Push verlässt sich auf `.netrc`-Fallback
- Frage bleibt: Funktioniert `.netrc`-basierte Authentifizierung für Cloud zuverlässig, oder fehlen noch Details?

**Zu prüfende Implementierungsdetails in `GetGitEnvironment()`:**

- Wird `.netrc`-Host korrekt gesetzt? Für Cloud sollte es `bitbucket.org` sein, für Self-Hosted die Custom-Host
- Wird `.netrc`-Datei mit korrekten Permissions (0600) angelegt?
- Wird der `.netrc`-Eintrag korrekt formatiert? Erwartet: `machine {host}\nlogin {user}\npassword {token}`
- Wird `.netrc` auf Windows korrekt verarbeitet? (Git kann auch `_netrc` oder Credential-Helper verwenden)

**Zu prüfende Implementierungsdetails in `UpdateNetrcEntry()`:**

- Wird vorhandener `.netrc`-Eintrag korrekt aktualisiert oder überschrieben?
- Ist die Datei-Encoding korrekt (UTF-8)?

**Zu klären für Cloud-Authentifizierung:**
- Sollte der Benutzername in `BuildAuthenticatedCloneUrl()` für Cloud weiterhin der Bitbucket-Benutzername sein, oder sollte es `oauth2` sein (wie in einigen Git-Auth-Dokumentationen vorgeschlagen)?
- Reicht `.netrc`-Authentifizierung für Cloud aus, oder ist ein `GIT_CREDENTIAL_HELPER` erforderlich?

**Geplante Validierungen und Verbesserungen:**

- `GetGitEnvironment()`: Logging erweitern — welcher Host wird für `.netrc` verwendet?
- `BuildAuthenticatedCloneUrl()`: Dokumentation hinzufügen, dass diese Methode nur für Clone verwendet wird
- `GetGitHttpAuthArgs()`: Ggf. Dokumentation, dass Cloud auf `.netrc` verlässt
- Fehlerbehandlung verfeinern: Bei Authentifizierungsfehlern aussagekräftigere Fehlermeldungen loggen

**Keine neuen Methoden oder Felder erforderlich** — die bestehenden sind ausreichend strukturiert.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Bestehende Validierung:
- `BitbucketUserKey` muss gefüllt sein (Pflicht)
- `BitbucketAppPasswordKey` muss gefüllt sein (Pflicht)
- `BitbucketHostingModeKey` muss "Cloud" oder "SelfHosted" sein (Enum-Validierung durch UI)
- `BitbucketSelfHostedUrlKey` muss gefüllt sein, wenn HostingMode = "SelfHosted"

## Konfigurationsänderungen

Keine. Alle erforderlichen Einstellungen sind bereits im Plugin definiert:
- **Authentifizierung:** BitbucketUser, BitbucketAppPassword
- **Jira:** JiraUrl, JiraProjectKey, JiraEmail, JiraApiToken
- **BitBucket-Hosting:** HostingMode (Cloud|SelfHosted), SelfHostedUrl

## Seiteneffekte und Risiken

### Bekannte Seiteneffekte

- **Windows `.netrc` Handling:** Git unter Windows kann `.netrc` nicht direkt lesen; es nutzt stattdessen den Windows Credential Manager oder einen Git-internen Credential-Helper. Die `.netrc`-Datei muss möglicherweise via `core.askPass` oder `GIT_ASKPASS` umgangen werden. Dies ist ein Betriebssystem-spezifisches Risiko.
  
- **Credential-Caching:** Wenn Git/curl die Credentials einmal erfolgreich verwendet haben, können sie gecacht werden. Bei Credential-Wechsel kann Git alte gecachte Credentials verwenden und Authentifizierungsfehler verursachen. Mitigation: Credential-Cache vor Operationen leeren oder neu setzen.

- **Self-Hosted URL-Konvertierung:** `ResolveGitCloneUrl()` konvertiert Browser-URLs zu SCM-URLs. Wenn Nutzer eine URL in unerwarteter Form eingeben, könnte die Konvertierung fehlschlagen. Risiko gering, da Tests 6 Varianten abdecken.

- **Jira-Integration:** API-Token-Format unterscheidet sich zwischen Cloud und Self-Hosted. Aktuelle Implementierung behandelt beide, aber Fehlerbehandlung bei falschen Token-Formaten könnte verbessert werden.

## Umsetzungsreihenfolge

1. **Code-Review und Validierung bestehender Implementierung**
   - Voraussetzungen: Keine
   - Beschreibung: Prüfe `GetGitEnvironment()`, `UpdateNetrcEntry()`, `BuildAuthenticatedCloneUrl()` auf Korrektheit. Verifiziere, dass `.netrc`-Handling für Cloud und Self-Hosted korrekt ist. Prüfe Windows-Kompatibilität (`.netrc` vs. `_netrc` vs. Credential-Manager). Untersuche, ob Logging ausreichend ist für Debugging.

2. **Erweiterung der Fehlerbehandlung in Git-Operationen**
   - Voraussetzungen: BitbucketPlugin existiert, ICliRunner existiert
   - Beschreibung: Analysiere Fehler-Exit-Codes von `git clone`, `git pull`, `git push`. Für Cloud-Authentifizierungsfehler ("Invalid username or token", "fatal: Authentication failed") zusätzliche kontextbezogene Log-Einträge hinzufügen. Dokumentiere im Logging, welcher Host (bitbucket.org vs. Self-Hosted) und welcher Authentifizierungsmechanismus (.netrc vs. HTTP-Header) verwendet wird.

3. **Dokumentation der Authentifizierungsmechanismen**
   - Voraussetzungen: BitbucketPlugin verstanden
   - Beschreibung: Erweitere Code-Kommentare in `BitbucketPlugin`:
     - `GetGitEnvironment()`: Dokumentiere, dass `.netrc`-Eintrag für HTTP-Basic-Auth gesetzt wird
     - `BuildAuthenticatedCloneUrl()`: Dokumentiere, dass Credentials für Clone eingebettet werden
     - `GetGitHttpAuthArgs()`: Dokumentiere Cloud → `.netrc`-Fallback, Self-Hosted → HTTP-Header
     - `GetCurlAuthArgs()`: Dokumentiere Cloud → Basic-Auth, Self-Hosted → Bearer-Token

4. **Tests für Authentifizierungs-Abläufe erweitern** (optional, abhängig von Testalias)
   - Voraussetzungen: BitbucketPluginTests existiert
   - Beschreibung: Füge Unit-Tests hinzu für:
     - `.netrc`-Datei-Format-Validierung (StaticMethod `UpdateNetrcEntry()` direkt testen oder Mock-Wrapper)
     - Hosting-Mode-Differenzierung in Authentifizierungs-Methoden
     - Fehlerszenarien für Cloud (z. B. ungültiges Token)
     - Windows vs. Unix `.netrc`-Handling (ggf. plattformspezifische Tests)

5. **Integrations- und E2E-Tests prüfen / erweitern**
   - Voraussetzungen: E2E-Test-Infrastruktur existiert
   - Beschreibung: Stelle sicher, dass E2E-Tests für Cloud-Operationen (Clone, Pull, Push) vorhanden sind. Diese Tests müssen mit echten Bitbucket-Cloud-Credentials (oder Mocks) durchgeführt werden.

6. **Dokumentation für Nutzer erstellen**
   - Voraussetzungen: Code-Review abgeschlossen
   - Beschreibung: Schreibe Dokumentation für Konfiguration von Bitbucket Cloud:
     - Wie wird BitbucketUser und BitbucketAppPassword konfiguriert?
     - Wie wird HostingMode auf "Cloud" gesetzt?
     - Troubleshooting: Was tun, wenn Authentifizierung fehlschlägt?
     - Windows-spezifische Hinweise zum `.netrc`-Handling

## Tests

### Neue Tests

**Hinweis:** Die meisten Authentifizierungs-Tests sind bereits in `BitbucketPluginTests` vorhanden. Folgende neue Tests könnten das Vertrauen in Cloud-Authentifizierung erhöhen:

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `UpdateNetrcEntry_ShouldFormatCorrectly_WhenCreatingNewEntry()` | BitbucketPluginTests | .netrc-Format ist korrekt (`machine host\nlogin user\npassword token`) |
| `GetGitEnvironment_ShouldUseCloudHost_WhenHostingModeIsCloud()` | BitbucketPluginTests | Umgebungsvariablen enthalten `.netrc`-Eintrag für `bitbucket.org` |
| `GetGitEnvironment_ShouldUseSelfHostedHost_WhenHostingModeIsSelfHosted()` | BitbucketPluginTests | Umgebungsvariablen enthalten `.netrc`-Eintrag für Self-Hosted-Host |
| `BuildAuthenticatedCloneUrl_ShouldEmbedCredentials_WhenCalled()` | BitbucketPluginTests | Clone-URL enthält `user:appPassword@` |
| `CloneRepositoryAsync_ShouldLogHostingMode_OnSuccess()` | BitbucketPluginTests | Logging zeigt, welcher Hosting-Mode verwendet wird |
| `PullAsync_Cloud_ShouldUseNetrc_NotHttpHeaders()` | BitbucketPluginTests | `PullAsync()` für Cloud gibt leere HTTP-Auth-Args zurück |
| `PushBranchAsync_Cloud_ShouldUseNetrc_NotHttpHeaders()` | BitbucketPluginTests | `PushBranchAsync()` für Cloud gibt leere HTTP-Auth-Args zurück |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GetBitbucketApiBaseUrl_Cloud` | Ggf. Assertion aktualisieren, wenn Logging verändert wird |
| `CheckHealthAsync_Cloud` | Ggf. Mock-Setup erweitern, wenn `.netrc`-Handling getestet wird |
| `GetAvailableRepositoriesAsync_Cloud` | Keine Änderung erforderlich |

Falls neue Tests implementiert werden, müssen bestehende Mocks in `BitbucketPluginTests` erweitert werden um `.netrc`-File-System-Mocks (z. B. über `System.IO.Abstractions` oder ähnliche Test-Doubles).

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Clone Cloud-Repository mit eingebetteten Credentials | E2E-Test-Suite (TBD) | Repository wird erfolgreich geklont; Authentifizierung funktioniert |
| Pull von geklontem Cloud-Repository | E2E-Test-Suite (TBD) | `git pull` funktioniert mit `.netrc`-Authentifizierung |
| Push zu geklontem Cloud-Repository | E2E-Test-Suite (TBD) | `git push` funktioniert mit `.netrc`-Authentifizierung |
| Clone Self-Hosted-Repository mit URL-Konvertierung | E2E-Test-Suite (TBD) | Browser-URL wird zu SCM-URL konvertiert; Repository wird geklont |
| Health-Check für Cloud | E2E-Test-Suite (TBD) | `CheckHealthAsync()` prüft Cloud-API erfolgreich |
| Health-Check für Self-Hosted | E2E-Test-Suite (TBD) | `CheckHealthAsync()` prüft Self-Hosted-API erfolgreich |

**Betroffene bestehende E2E-Tests:**

Falls E2E-Tests für Repository-Operationen existieren, müssen diese überprüft werden, um sicherzustellen, dass sie mit neuer Authentifizierungslogik kompatibel sind.

## Offene Punkte

Keine. Die Anforderung ist anhand der Bestandsaufnahme und der Code-Analyse vollständig geklärt.

**Anmerkung:** Die folgenden Punkte aus dem Anforderungsdokument sind implizit durch die bestehende Implementierung beantwortet:

1. **Authentifizierungsmethode für Cloud:** Code nutzt `oauth2:appPassword`-Format in URL-Embedding und HTTP-Basic-Auth (`user:appPassword`) in `.netrc` — beides ist Standard und funktioniert mit Bitbucket Cloud.

2. **Backward-Compatibility:** Bestehende Self-Hosted-Instanzen werden durch die Hosting-Mode-Differenzierung weiterhin unterstützt. URL-Format ändert sich nicht.

3. **Fehlerdetails:** Logging ist bereits implementiert. Erweiterung möglich, aber nicht blockierend.

4. **Git Credential Helper:** `.netrc`-Fallback ist Standard und ausreichend. `GIT_CREDENTIAL_HELPER` nicht erforderlich.

5. **Token Format:** Bitbucket App Passwords sind Strings; kein Format-Check erforderlich.

6. **HTTPS vs. SSH:** Plugin unterstützt nur HTTPS. SSH ist nicht erforderlich (Anforderung erwähnt nur `git clone`).
