# Bestandsaufnahme: GitHub-PAT-Token-Sicherheit und Separation

Analyse des bestehenden Codes bezüglich der Anforderung zur Separation der GitHub PAT-Authentifizierung: Der Token soll nicht mehr in Git-Remote-URLs eingebettet werden, sondern wird isoliert für GitHub-API-Operationen über Umgebungsvariablen und `.netrc` bereitgestellt.

---

## Zusammenfassung

### Was existiert bereits

- **Token-Speicherung (sicher):** Token wird im Windows Credential Store unter `Softwareschmiede.GitHub.Token` via `ICredentialStore` gespeichert
- **GitHub-API-Authentifizierung (richtig):** Token wird über `GH_TOKEN` Umgebungsvariable für `gh`-CLI-Befehle bereitgestellt (Methode `GetGhEnvironment()`)
- **Token-Sanitization:** Methode `SanitizeSensitiveOutput()` maskiert Token in Fehlermeldungen bereits
- **`.netrc`-Vorbereitung:** `ConfigureGitCredentialsAsync()` erstellt eine `.netrc`-Datei mit Credentials (Zeile 296-307)
- **UI-Feld:** Token-Konfiguration im UI unter "Authentifizierung" als Secret-Feld mit erforderlichem Status
- **Tests:** Umfangreiche Tests für GitHub-API-Operationen und Clone-/Push-/Pull-Operationen

### Was ist problematisch (muss geändert werden)

- **Token-Embedding in Clone-URL:** `CloneRepositoryAsync()` nutzt `BuildAuthenticatedCloneUrl()` um Token direkt in URL einzubetten (Zeile 757)
- **Token-Embedding in Remote-URL:** `ConfigureGitCredentialsAsync()` setzt Token in Remote-URL via `git remote set-url` ein (Zeile 324)
- **Token-Embedding in Remote-URL (bei Push/Pull):** `EnsureRemoteCredentialsAsync()` prüft und fügt Token zu Remote-URL hinzu wenn nicht vorhanden (Zeile 236)
- **`GetGitEnvironment()` unvollständig:** Rüstet `.netrc`-Pfad aus, übergibt aber **nicht** den Token selbst als Umgebungsvariable an `git`

### Kritische Stellen für die Anforderung

| Zeilen | Methode | Problem | Auswirkung |
|--------|---------|---------|-----------|
| 145-155 | `BuildAuthenticatedCloneUrl()` | Token wird in URL eingebettet | Clone-URL enthält Klartext-Token |
| 257-351 | `ConfigureGitCredentialsAsync()` | Remote-URL mit eingebettetem Token gesetzt | `.git/config` enthält `oauth2:{token}@` |
| 202-255 | `EnsureRemoteCredentialsAsync()` | URL wird mit Token aktualisiert wenn nicht vorhanden | Push/Pull-URLs enthalten Token |
| 116-139 | `GetGitEnvironment()` | Konfiguriert nur `.netrc`-Pfad, keine Token-Übergabe | `git` kann Token nicht via `GH_TOKEN` nutzen |
| 509 (Test) | `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()` | Test validiert Token-Embedding (!!!) | Test würde nach Änderung fehlschlagen |

---

## Details

- [Logik-Komponenten](inventory/logic.md) — `GitHubPlugin`, `PluginSettingsService`, Methoden, Token-Handling
- [Interfaces](inventory/interfaces.md) — `ICredentialStore`, `ICliRunner`, Verträge und Abhängigkeiten
- [Tests](inventory/tests.md) — Bestehende Test-Abdeckung, relevante Test-Cases, Lücken
- [Enums](inventory/enums.md) — `PluginSettingFieldType`, `PullRequestCompletionStrategy`, etc.

---

## Technische Details: Gegenwärtiger Authentifizierungs-Ablauf

### 1. Token-Speicherung
```
UI-Eingabe → PluginSettingsService → ICredentialStore → Windows Credential Store
                                                          unter "Softwareschmiede.GitHub.Token"
```

### 2. GitHub-API-Operationen (Beispiel: Issues abrufen)
```
GetIssuesAsync()
  → _credentialStore.GetCredential("Softwareschmiede.GitHub.Token")
  → GetGhEnvironment() → {"GH_TOKEN": "ghp_..."}
  → _cliRunner.RunAsync("gh", ["issue", "list", ...], null, {"GH_TOKEN": "..."})
  → Correct: Token als Umgebungsvariable
```

### 3. Repository-Kloning (PROBLEMATISCH)
```
CloneRepositoryAsync("https://github.com/owner/repo")
  → _credentialStore.GetCredential("Softwareschmiede.GitHub.Token")
  → BuildAuthenticatedCloneUrl(url, token)
  → Returns: "https://oauth2:ghp_...@github.com/owner/repo"
  → _cliRunner.RunAsync("git", ["clone", "https://oauth2:ghp_...@github.com/owner/repo", targetPath], ...)
  → Problem: Token sichtbar in CLI-Befehl und History
```

### 4. Push/Pull-Konfiguration (PROBLEMATISCH)
```
PushBranchAsync() → EnsureRemoteCredentialsAsync()
  → git config remote.origin.url → "https://github.com/owner/repo"
  → Wenn kein "@" in URL:
    → git remote set-url origin "https://oauth2:ghp_...@github.com/owner/repo"
  → Problem: Token wird in .git/config gespeichert
```

### 5. `.netrc`-Vorbereitung (TEILWEISE VORHANDEN)
```
ConfigureGitCredentialsAsync()
  → Erstellt ~/.netrc (Windows: ~/_netrc)
  → Inhalt:
      machine github.com
      login oauth2
      password ghp_...
      machine api.github.com
      login oauth2
      password ghp_...
  → Problem: Wird erstellt, aber `GetGitEnvironment()` übergibt keine `GH_TOKEN`, 
             daher muss git auf `.netrc` fallback
```

---

## Gegenwärtiger Stand der Umgebungsvariablen

### `GetGhEnvironment()` (für `gh`-CLI)
```csharp
{
    "GH_TOKEN": "ghp_..."
}
```
✓ Korrekt: Token über Umgebungsvariable für GitHub-CLI

### `GetGitEnvironment()` (für `git`-CLI)
```csharp
{
    "GIT_TERMINAL_PROMPT": "0",
    "GIT_SSH_COMMAND": "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null",
    "NETRC": "C:\\Users\\User\\_netrc"  // oder ~/.netrc auf Unix
}
```
✗ **Fehlt:** `GH_TOKEN` Übergabe an `git`! (Nur `.netrc`-Pfad)

---

## Konstanten und Konfiguration

| Konstante | Wert | Verwendung |
|-----------|------|-----------|
| `GitHubTokenCredentialKey` | `"Softwareschmiede.GitHub.Token"` | Schlüssel im `ICredentialStore` |
| `RepositoryUrlKey` | `"RepositoryUrl"` | Feld für Repository-URL Konfiguration |
| `RepositoryNameKey` | `"RepositoryName"` | Feld für Repository-Name (owner/repo) |

---

## Test-Erkenntnisse

### Bestehende Test-Abhängigkeiten vom Token-Embedding
Die folgenden Tests validieren aktuelle Token-Embedding-Verhalten. Sie müssen bei der Implementierung angepasst werden:

1. **`CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()`** (Zeile 484)
   - Erwartet: `a.Any(x => x.Contains("https://oauth2:token@github.com/test/repo", ...))`
   - Nach Änderung: Sollte `https://github.com/test/repo` (ohne Token) erwarten

2. **`PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()`** (Zeile 951)
   - Erwartet: Remote-URL mit `oauth2:token123@`
   - Nach Änderung: Sollte URL ohne Token und `GH_TOKEN` Umgebungsvariable erwarten

3. **`PullAsync_ShouldRunGitPull_WithConfiguredCredentials()`** (Zeile 1049)
   - Ähnlich wie Push-Test

### Positive Test-Erkenntnisse (funktionieren unverändert)
- Token-Sanitization bei Fehlern wird getestet und funktioniert
- GitHub-API-Tests nutzen bereits `GetGhEnvironment()` mit `GH_TOKEN`
- Cancellation-Handling ist robust
- Fehler-Mapping ist umfassend

---

## Abhängigkeitsübersicht

```
GitHubPlugin
├─ ICliRunner (private)
│  └─ Führt git/gh Befehle mit Umgebungsvariablen aus
├─ ICredentialStore (private)
│  └─ Speichert/liest Token unter "Softwareschmiede.GitHub.Token"
└─ ILogger<GitHubPlugin> (private)
   └─ Logging mit Token-Maskierung via SanitizeSensitiveOutput()

PluginSettingsService
└─ ICredentialStore (public)
   └─ Verwaltet alle Plugin-Einstellungen inklusive Token
```

---

## Offene Punkte (aus Anforderung)

1. **Authentifizierungsvariante:** Soll `GH_TOKEN` für lokale Git-Befehle genutzt werden (Variante A) oder nur `.netrc` (Variante B)?
   - **Gegenwärtig:** `.netrc` wird vorbereitet, aber `GH_TOKEN` wird nicht übergeben
   
2. **Benutzer-`gh cli` Authentifizierung:** Sollen lokale Repositories die bereits authentifizierte `gh cli` des Benutzers nutzen?
   - **Gegenwärtig:** Programm-Token wird immer verwendet, keine Fallback auf System-`gh`

3. **Rückwärtskompatibilität:** Sollten bestehende Repositories mit eingebettetem Token automatisch bereinigt werden?
   - **Gegenwärtig:** Nicht vorhanden; nur neue Klones und neue Push/Pull-Operationen können geändert werden

4. **Dokumentation:** Sollen neue Token-Sicherheits-Informationen in Dokumentation hinzugefügt werden?
   - **Gegenwärtig:** Keine spezifische Dokumentation zur Token-Separation vorhanden

5. **Windows-spezifische Aspekte:** `.netrc` auf Windows vs. `_netrc` - sollte standardisiert werden?
   - **Gegenwärtig:** Beides wird unterstützt (Zeile 135)

---

## Dateien mit Token-relevantem Code

| Datei | Relevanz | Status |
|-------|----------|--------|
| `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` | Zentral | Muss geändert werden |
| `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICredentialStore.cs` | Speicher-Abstraktio | Keine Änderung nötig |
| `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/ICliRunner.cs` | CLI-Abstraktio | Keine Änderung nötig |
| `src/Softwareschmiede/Application/Services/PluginSettingsService.cs` | Token-Verwaltung | Keine Änderung nötig |
| `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs` | Tests | Muss angepasst werden |
