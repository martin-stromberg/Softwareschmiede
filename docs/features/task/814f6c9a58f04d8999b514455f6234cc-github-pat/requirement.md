# Anforderung – GitHub-PAT: Token-Sicherheit und Separation

**Aufgaben-ID:** 814f6c9a-58f0-4d89-99b5-14455f6234cc  
**Branch:** task/814f6c9a58f04d8999b514455f6234cc-github-pat  
**Erstellt:** 2026-08-27

## Fachliche Zusammenfassung

Das Programm speichert einen GitHub Personal Access Token (PAT) in seinen Einstellungen und nutzt ihn für API-Operationen. Aktuell wird dieser Token auch direkt in Git-Remote-URLs eingebettet, was das Risiko mit sich bringt, dass er in Logs, Fehlermeldungen, Prompts oder Dokumentation erscheint und dadurch kompromitiert wird. Die Anforderung ist, den Authentifizierungsmechanismus zu separieren: Das Programm soll seinen PAT isoliert für GitHub-API-Operationen nutzen (z. B. Issue-Abfrage via `gh api`), während das lokal geklonte Repository mit der Standard-`gh`-CLI-Authentifizierung arbeitet — ohne dass der Programm-Token jemals in URLs, Umgebungsvariablen-Übergaben oder sichtbaren Konfigurationen auftaucht.

## Betroffene Klassen und Komponenten

### Plugin-Architektur
- `GitHubPlugin` (plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs)
  - Methoden: `CloneRepositoryAsync()`, `ConfigureGitCredentialsAsync()`, `EnsureRemoteCredentialsAsync()`, `PushBranchAsync()`, `PullAsync()`
  - Token-Speicherung: `GitHubTokenCredentialKey = "Softwareschmiede.GitHub.Token"` über `ICredentialStore`
  - Problematische Stellen: `BuildAuthenticatedCloneUrl()`, URL-Embedding in `git remote set-url` Befehlen

### Infrastruktur und Services
- `ICredentialStore` (src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs)
  - Speichert Token im Windows Credential Store unter `Softwareschmiede.GitHub.Token`
- `ICliRunner` 
  - Führt `gh` und `git` CLI-Befehle aus, übergeben Umgebungsvariablen
- `PluginSettingsService` (src/Softwareschmiede/Application/Services/PluginSettingsService.cs)
  - Verwaltet Plugin-Konfiguration einschließlich Token-Speicherung

### Tests
- Bestehende Tests in `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
- Neue oder angepasste Tests für Token-Nicht-Embedding in URLs

## Implementierungsansatz

### Kernänderung: Trennung der Authentifizierungsmechanismen

1. **Programm-interner Token für GitHub-API-Calls (bleiben unverändert)**
   - Token wird in `Softwareschmiede.GitHub.Token` gespeichert
   - Wird über `GetGhEnvironment()` als `GH_TOKEN`-Umgebungsvariable an `gh` CLI übergeben (Methoden wie `GetIssuesAsync()`, `GetAlertsAsync()`, `CreateIssueAsync()`)
   - **Keine Änderung nötig** — diesen Mechanismus weiterverwenden

2. **Lokales Repository-Kloning ohne Token-Embedding in URLs**
   - **Entfernen:** URL-Embedding des Tokens in `CloneRepositoryAsync()`, `ConfigureGitCredentialsAsync()`, `EnsureRemoteCredentialsAsync()`
   - **Alternativ verwenden:**
     - Variante A (bevorzugt): Programm-Token über `GH_TOKEN`-Umgebungsvariable für `git clone` bereitstellen
       - `git` nutzt intern `GH_TOKEN` wenn gesetzt (via `credential.helper` oder direkt)
       - Remote-URL bleibt unauthentifiziert: `https://github.com/owner/repo`
       - Keine Credentials in `.git/config` oder Remote-URL
     - Variante B (Fallback): Nur `.netrc`-Datei verwenden (aktuell bereits teilweise in `ConfigureGitCredentialsAsync()` vorhanden)
       - `.netrc`-Datei wird mit Token gefüllt
       - `git` liest Credentials aus `.netrc` ohne URL-Embedding
   
3. **Token-Sichtbarkeitsprävention**
   - Methode `SanitizeSensitiveOutput()` (existiert bereits, Zeile 170) bleibt bestehen und wird weiterhin auf alle CLI-Fehlerausgaben angewendet
   - Keine Änderung in Log-Sanitization erforderlich

### Detaillierte Schritte

#### Schritt 1: `CloneRepositoryAsync()` anpassen
- **Vorher:** Token wird in `BuildAuthenticatedCloneUrl()` in die Clone-URL eingebettet
- **Nachher:** 
  - Clone-URL ohne Token: `git clone https://github.com/owner/repo.git`
  - Programm-Token wird über `GH_TOKEN`-Umgebungsvariable bereitgestellt
  - `git` nutzt `credential.helper` oder `GH_TOKEN` automatisch

#### Schritt 2: `ConfigureGitCredentialsAsync()` vereinfachen
- **Vorher:** 
  - URL-Embedding: `git remote set-url origin https://oauth2:{token}@github.com/...`
  - `.netrc`-Datei (als Backup)
- **Nachher:**
  - URL-Embedding **entfernen**
  - `.netrc`-Datei behalten als Authentifizierungsmechanismus (oder weiterhin über Umgebungsvariable)
  - Vereinfachte Konfiguration ohne Token-Sichtbarkeit

#### Schritt 3: `EnsureRemoteCredentialsAsync()` anpassen
- **Vorher:** URL wird mit eingebettetem Token aktualisiert
- **Nachher:** 
  - Nur prüfen, dass Remote-URL die richtige URL hat (ohne Token-Embedding)
  - Falls Credentials fehlen, `.netrc` aktualisieren oder Fallback auf `GH_TOKEN`-Umgebungsvariable verwenden

#### Schritt 4: `GetGitEnvironment()` erweitern
- Token wird weiterhin als `GH_TOKEN`-Umgebungsvariable übergeben (ist bereits teilweise vorhanden)
- Sicherstellen, dass alle `git`-Operationen diese Variable für Authentifizierung nutzen

### Abhängigkeiten und Events
- **Abhängigkeiten:** 
  - `ICliRunner` — für Umgebungsvariablen-Übergabe
  - `ICredentialStore` — für Token-Abruf
- **Keine neuen Events erforderlich** — existierende Plugin-Schnittstellen bleiben erhalten
- **Konfigurationskompatibilität:** Bestehende Credentials (Tokens in `Softwareschmiede.GitHub.Token`) bleiben gültig

## Konfiguration

- **Änderungen auf Benutzer-Ebene:** Minimal — existierende Token müssen nicht neu konfiguriert werden
- **Änderungen auf Programmebene:** 
  - Plugin-Einstellung „Personal Access Token" bleibt unverändert (wird weiterhin über UI in den Einstellungen konfiguriert und im Credential Store gespeichert)
  - **Optionale Erweiterung:** Neue Checkbox-Einstellung wie „Token-Sicherheit: GitHub-API-Operationen getrennt vom lokalen Repository halten" (zur Dokumentation der neuen Arbeitsweise) — aber nicht zwingend nötig, falls die Trennung transparent ist
- **Keine sensiblen Daten in URLs, Logs oder Prompts** — sicherstellen, dass die Implementierung durch `SanitizeSensitiveOutput()` validiert wird

## Offene Fragen

1. **Präferierte Authentifizierungsvariante für lokale Repositories:**
   - Soll der Programm-Token über `GH_TOKEN`-Umgebungsvariable bereitgestellt werden (Variante A)?
   - Oder soll ausschließlich die `.netrc`-Datei für lokale Git-Operationen genutzt werden (Variante B)?
   - Oder eine Kombination aus beiden (`.netrc` als Fallback)?

2. **Verhältnis zu `gh cli` Authentifizierung des Benutzers:**
   - Soll das lokale Repository die bereits im System authentifizierte `gh cli` des Benutzers nutzen (wenn vorhanden)?
   - Oder soll der Programm-Token immer zum Einsatz kommen?

3. **Rückwärtskompatibilität:**
   - Sollen bestehende Repositories, deren Remote-URL bereits einen eingebetteten Token hat, automatisch bereinigt werden?
   - Oder soll nur bei neuen Klones die sichere Methode genutzt werden?

4. **Dokumentation:**
   - Soll die neue Token-Sicherheitspraxis in `docs/help/plugins/beschreibung.md` oder `docs/help/plugins/bitbucket-plugin/...` dokumentiert werden?
   - Sollte ein Troubleshooting-Abschnitt für Token-bezogene Authentifizierungsfehler hinzugefügt werden?

5. **Windows-spezifische Aspekte:**
   - `.netrc`-Verhalten auf Windows (aktuell auch `_netrc` unterstützt) — soll dies beibehalten werden?
   - Oder sollte nur der `GH_TOKEN`-Umgebungsvariablen-Ansatz für Windows standardisiert werden?
