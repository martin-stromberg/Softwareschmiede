# Anforderungsübersetzung: BitBucket Cloud Support

## Fachliche Zusammenfassung

Das `BitbucketPlugin` soll Git-Operationen (insbesondere `git clone`) gegen Bitbucket Cloud unterstützen. Gegenwärtig funktioniert die Implementierung für Self-Hosted Bitbucket Server/Data Center korrekt, schlägt aber bei Bitbucket Cloud mit Authentifizierungsfehlern fehl ("Invalid username or token"). Das Plugin muss die Authentifizierungsmechanismen so konfigurieren, dass sowohl Self-Hosted- als auch Cloud-Instanzen unterstützt werden. Das Problem liegt darin, dass die Credentials nicht korrekt an Git weitergegeben werden und die `.netrc`-basierte Authentifizierung nicht mit Bitbucket Cloud kompatibel ist.

## Betroffene Klassen und Komponenten

### Logikklassen / Services
- `BitbucketPlugin` (Hauptimplementierung in `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`)
  - Methode `GetGitEnvironment()` – Umgebungsvariablen für `git` Befehle
  - Methode `GetGitHttpAuthArgs()` – HTTP Basic Auth für Self-Hosted
  - Methode `BuildAuthenticatedCloneUrl()` – Einbettung von Credentials in die Clone-URL
  - Methode `CloneRepositoryAsync()` – Repository-Klonierung
  - Methode `PullAsync()` und weitere Git-Operationen

### Interfaces / Abstraktion
- `GitPluginBase<BitbucketPlugin>` – Basisklasse mit abstrakten Methoden
- `ICredentialStore` – Credential-Verwaltung
- `ICliRunner` – Ausführung von CLI-Befehlen

### Konfiguration / Enums
- `BitbucketHostingModeKey` – Enum-ähnliche Unterscheidung ("Cloud" vs. "SelfHosted")
- `BitbucketAppPasswordKey`, `BitbucketUserKey` – Credential-Keys für Authentifizierung

## Implementierungsansatz

### Root Cause
Die gegenwärtige Implementierung nutzt für Self-Hosted `.netrc`-Einträge, welche von Git/curl für Basic Auth verwendet werden. Bei Bitbucket Cloud funktioniert dies nicht, da:
1. Bitbucket Cloud strikte Authentifizierungsanforderungen hat
2. Die `.netrc`-Datei möglicherweise nicht korrekt generiert oder gelesen wird
3. Token-basierte Authentifizierung (OAuth2 oder App Password) anders behandelt werden muss

### Lösungsansatz

1. **Differenzierung nach Hosting-Modus**
   - Self-Hosted: Weiterhin `.netrc` + `git -c http.extraheader=...` oder URL-embedding
   - Cloud: Token-basierte Authentifizierung via `GIT_CREDENTIAL_HELPER` oder direktes URL-embedding mit `oauth2:` Benutzer

2. **Git-Authentifizierung für Cloud**
   - Embedded Credentials in Clone-URL: `https://oauth2:{appPassword}@bitbucket.org/{workspace}/{repo}.git`
   - ODER: `GIT_CREDENTIAL_HELPER` Umgebungsvariable setzen auf ein einfaches Script
   - Terminal-Prompts deaktivieren: `GIT_TERMINAL_PROMPT=0` (bereits vorhanden)

3. **Methoden-Änderungen**
   - `GetGitEnvironment()`: Conditional Logic nach `HostingMode` hinzufügen
   - `BuildAuthenticatedCloneUrl()`: Ggf. Änderung des Benutzernamens für Cloud-Mode (von `user:appPassword` zu `oauth2:appPassword`)
   - `CloneRepositoryAsync()`: Sicherstellen, dass die richtige Authentifizierungsmethode verwendet wird

4. **Robustheit**
   - Fehlerbehandlung für Authentifizierungsfehler verfeinern
   - Logging verbessern für Debugging-Zwecke
   - Test für beide Hosting-Modi

## Konfiguration

Das Feature ist bereits über die bestehende Konfigurationsebene im Plugin abgedeckt:
- `GetSettingGroups()` enthält bereits die Gruppe "BitBucket-Hosting" mit dem Enum `HostingMode` (Cloud/SelfHosted)
- Credentials werden über `ICredentialStore` verwaltet
- Keine zusätzlichen Konfigurationsfelder notwendig, da alle erforderlichen Einstellungen bereits definiert sind

## Offene Fragen

1. **Authentifizierungsmethode für Cloud**: Soll der Username bei Cloud-Mode weiterhin `oauth2` sein, oder gibt es eine andere Best-Practice für Bitbucket Cloud App Passwords?

2. **Backward-Compatibility**: Müssen die Änderungen an `BuildAuthenticatedCloneUrl()` die Self-Hosted-Instanzen noch vollständig unterstützen, oder kann die URL-Struktur geändert werden?

3. **Fehlerdetails**: Gibt es zusätzliche Logs oder Debugging-Informationen, die bei Authentifizierungsfehlern ausgegeben werden sollten?

4. **Git Credential Helper**: Sollte ein Custom-Credential-Helper implementiert werden, oder ist die Direct-URL-embedding-Methode ausreichend?

5. **Token Format**: Bitbucket Cloud App Passwords unterscheiden sich möglicherweise vom Format bei Self-Hosted – gibt es spezifische Anforderungen oder Besonderheiten?

6. **HTTPS vs. SSH**: Ist SSH-Authentifizierung für Bitbucket Cloud erforderlich, oder genügt HTTPS?
