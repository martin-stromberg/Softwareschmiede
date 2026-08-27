# Umsetzungsplan: GitHub-PAT – Token-Sicherheit und Separation

## Übersicht

Der GitHub Personal Access Token (PAT) wird derzeit direkt in Git-Remote-URLs eingebettet (`https://oauth2:{token}@github.com/owner/repo`), wodurch er in CLI-Logs, Umgebungsvariablen und `.git/config` sichtbar wird und kompromittiert werden kann. Der Plan sieht vor, die Authentifizierungsmechanismen zu separieren: Der Programm-Token wird künftig ausschließlich über die Umgebungsvariable `GH_TOKEN` für beide GitHub-API-Operationen (via `gh cli`) und lokale Git-Operationen (via `git`) bereitgestellt. URL-Embedding wird vollständig entfernt. Die bestehende `.netrc`-Vorbereitung wird als zusätzliche Fallback-Unterstützung beibehalten. Token-Sanitization über `SanitizeSensitiveOutput()` bleibt unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Authentifizierungsvariante für lokale Git-Operationen | **Variante A (bevorzugt):** `GH_TOKEN`-Umgebungsvariable für `git`-Befehle verwenden; `.netrc` als optionaler Fallback beibehalten | Standardisiert die Token-Übergabe auf eine Mechanismus (`GH_TOKEN`), nutzt das Verhalten von Git/`gh cli` automatisch. `.netrc` bleibt als Fallback erhalten, bricht bestehende Konfigurationen nicht. |
| Rückwärtskompatibilität für bestehende Repositories | Nur bei neuen Klones und neuen Push/Pull-Operationen sichere Methode nutzen; bestehende Repositories **nicht** automatisch bereinigen | Reduziert Komplexität und Fehlerrisiko. Befähigt Anwender, ihre bestehenden Clones manuell zu bereinigen, falls gewünscht. |
| System-`gh cli`-Authentifizierung als Fallback | **Nicht** unterstützen; Programm-Token wird immer genutzt | Vereinfacht Authentifizierungslogik und Fehlerbehandlung. Der Programm-Token ist der Single Source of Truth für Plugin-Operationen. |
| Windows-spezifische `.netrc`/`_netrc` Standardisierung | Beide Varianten weiterhin unterstützen; bestehende Logik beibehalten | Windows-Kompatibilität mit existierendem Code erhalten. |
| Dokumentation | Keine zusätzliche Dokumentation erforderlich; Änderungen sind transparent | Plugin-Funktionalität bleibt für Anwender identisch. Token wird nicht mehr in URLs sichtbar — das ist eine Sicherheitsverbesserung ohne Verhaltensänderung. |

## Programmabläufe

### Repository Klonen (Neue Implementierung)

1. `CloneRepositoryAsync()` wird aufgerufen mit Repository-URL (z. B. `https://github.com/owner/repo.git`)
2. Token wird aus `ICredentialStore` unter Schlüssel `"Softwareschmiede.GitHub.Token"` abgerufen
3. `git clone` wird **ohne** Token-Embedding in URL aufgerufen: `git clone https://github.com/owner/repo.git {targetPath}`
4. Umgebungsvariable `GH_TOKEN` wird in `GetGitEnvironment()` übergeben
5. `git` nutzt automatisch `GH_TOKEN` oder `.netrc` für Authentifizierung
6. Fehler werden via `SanitizeSensitiveOutput()` bereinigt bevor sie geloggt werden

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `ICliRunner`, `ICredentialStore`, `GetGitEnvironment()`

### Git Credentials Konfigurieren (Neue Implementierung)

1. `ConfigureGitCredentialsAsync()` wird aufgerufen mit lokalem Pfad und Repository-URL
2. `.netrc`-Datei wird erstellt/aktualisiert mit Credentials (bereits vorhanden, Zeile 296-307)
3. Remote-URL wird auf unauthentifizierte Variante gesetzt: `git remote set-url origin https://github.com/owner/repo.git`
4. `GetGitEnvironment()` wird vor allen `git`-Befehlen aufgerufen und übergibt `GH_TOKEN`
5. Keine weiteren Konfigurationsschritte nötig

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `ICliRunner`, `GetGitEnvironment()`

### Remote Credentials Sicherstellen (Neue Implementierung)

1. `EnsureRemoteCredentialsAsync()` wird vor Push/Pull aufgerufen
2. Remote-URL wird gelesen: `git config remote.origin.url`
3. Prüfung: Enthält URL noch einen Token (Pattern `oauth2:...@`)? Falls ja, wird er entfernt
4. Remote-URL wird auf unauthentifizierte Form normalisiert: `https://github.com/owner/repo.git`
5. `GH_TOKEN` wird über `GetGitEnvironment()` für Push/Pull bereitgestellt

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `ICliRunner`, `GetGitEnvironment()`

### GitHub API Operationen (Unverändert)

1. Methoden wie `GetIssuesAsync()`, `GetAlertsAsync()`, `CreateIssueAsync()` werden aufgerufen
2. Token wird aus `ICredentialStore` abgerufen
3. `GetGhEnvironment()` wird aufgerufen, gibt `{"GH_TOKEN": "ghp_..."}` zurück
4. `gh cli` Befehle werden mit dieser Umgebungsvariable ausgeführt
5. Token ist nicht in CLI-Befehlen sichtbar

Beteiligte Klassen/Komponenten: `GitHubPlugin`, `ICliRunner`, `ICredentialStore`, `GetGhEnvironment()`

## Neue Klassen

Keine. Alle Änderungen erfolgen in bestehenden Klassen.

## Änderungen an bestehenden Klassen

### `GitHubPlugin` (Klasse)

- **Geänderte Methoden:**
  - `CloneRepositoryAsync()` — Entfernt Token-Embedding via `BuildAuthenticatedCloneUrl()`. `git clone` wird mit unauthentifizierter URL aufgerufen; Token über `GH_TOKEN` Umgebungsvariable (via `GetGitEnvironment()`) bereitgestellt.
  - `ConfigureGitCredentialsAsync()` — Remote-URL wird auf unauthentifizierte Form gesetzt (`https://github.com/owner/repo.git`). `.netrc`-Datei bleibt als optionaler Fallback. Token-Embedding via `git remote set-url` wird entfernt.
  - `EnsureRemoteCredentialsAsync()` — Ändert sich zu einer **Prüfungs- und Normalisierungsfunktion**: Remote-URL wird gelesen, falls noch Token eingebettet ist, wird er entfernt und URL normalisiert. Keine neuen Tokens werden mehr in URLs eingebettet.
  - `GetGitEnvironment()` — Wird erweitert um Übergabe von `GH_TOKEN` Umgebungsvariable. Token wird aus `ICredentialStore` abgerufen und als `GH_TOKEN` in das Environment-Dictionary eingefügt.

- **Entfernte Methoden / Funktionalität:**
  - Indirekt: `BuildAuthenticatedCloneUrl()` wird **nicht** mehr aufgerufen (Methode selbst kann beibehalten werden für Rückwärtskompatibilität, wird aber nicht mehr genutzt).

- **Verhalten ändert sich:**
  - Token ist nicht mehr in URLs sichtbar.
  - Token ist nicht mehr in CLI-Befehlsargumenten sichtbar.
  - Git-Authentifizierung erfolgt über Umgebungsvariablen (`GH_TOKEN`) und `.netrc`, nicht über URL-Embedding.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Bestehende Token-Validierung (via `ICredentialStore` und `PluginSettingsService`) bleibt unverändert.

## Konfigurationsänderungen

Keine. Bestehende Plugin-Einstellung „Personal Access Token" bleibt unverändert (wird weiterhin via UI konfiguriert und im Windows Credential Store gespeichert unter `Softwareschmiede.GitHub.Token`).

## Seiteneffekte und Risiken

- **Git-Authentifizierung:** Push/Pull-Operationen sind abhängig von korrekter `GH_TOKEN`-Übergabe via `GetGitEnvironment()`. Falls diese nicht korrekt implementiert wird, schlagen Push/Pull fehl.
- **Bestehende Repositories:** Repositories, die bereits mit einem eingebetteten Token geklont wurden, behalten diesen Token in ihrer `.git/config`. Der Plan bereinigt diese nicht automatisch — Anwender müssen dies manuell tun oder neue Klones verwenden.
- **Rückwärtskompatibilität:** Bestehende automatisierte Workflows, die auf die vorherige URL-Embedding-Methode verlassen, könnten unerwartet brechen, falls sie die `.git/config` auslesen. Dies ist jedoch ein Sicherheitsrisiko und sollte nicht unterstützt werden.
- **Token-Masking:** Fehler-Ausgaben müssen weiterhin durch `SanitizeSensitiveOutput()` gefiltert werden, um sicherzustellen, dass Tokens nicht in Logs sichtbar sind (auch nicht via Umgebungsvariablen in der Fehlerausgabe).

## Umsetzungsreihenfolge

1. **`GetGitEnvironment()` erweitern um `GH_TOKEN` Übergabe**
   - Voraussetzungen: Keine — `GitHubPlugin` und `ICredentialStore` bestehen bereits.
   - Beschreibung: Methode `GetGitEnvironment(string? token)` wird geändert, um den Token aus `ICredentialStore` abzurufen (falls nicht übergeben) und als `GH_TOKEN` in das Environment-Dictionary einzufügen. Bestehende Keys (`GIT_TERMINAL_PROMPT`, `GIT_SSH_COMMAND`, `NETRC`) bleiben erhalten.

2. **`CloneRepositoryAsync()` anpassen — Token-Embedding entfernen**
   - Voraussetzungen: Schritt 1 abgeschlossen.
   - Beschreibung: `BuildAuthenticatedCloneUrl()` Aufruf wird entfernt. `git clone` wird mit unauthentifizierter URL aufgerufen. `GetGitEnvironment()` wird aufgerufen, um Umgebungsvariablen (inkl. `GH_TOKEN`) bereitzustellen.

3. **`ConfigureGitCredentialsAsync()` anpassen — Token-Embedding entfernen**
   - Voraussetzungen: Schritt 1 abgeschlossen.
   - Beschreibung: `git remote set-url` wird angepasst um unauthentifizierte URL zu nutzen (`https://github.com/owner/repo.git`). `.netrc`-Datei bleibt bestehen. `GetGitEnvironment()` wird aufgerufen.

4. **`EnsureRemoteCredentialsAsync()` anpassen — Zu Normalisierungsfunktion umbauen**
   - Voraussetzungen: Schritt 1 abgeschlossen.
   - Beschreibung: Methode wird zu einer Prüfungs- und Normalisierungsfunktion umgebaut: Remote-URL wird gelesen, falls noch Tokens eingebettet sind (`oauth2:...@` Pattern), werden diese entfernt und URL normalisiert. `GetGitEnvironment()` wird aufgerufen für Push/Pull.

5. **Tests anpassen — `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()`**
   - Voraussetzungen: Schritt 2 abgeschlossen.
   - Beschreibung: Test wird angepasst, um zu überprüfen, dass `git clone` mit unauthentifizierter URL aufgerufen wird (`https://github.com/test/repo` statt `https://oauth2:token@...`). `GH_TOKEN` Umgebungsvariable wird in Environment-Verifikation geprüft.

6. **Tests anpassen — `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()`**
   - Voraussetzungen: Schritt 4 abgeschlossen.
   - Beschreibung: Test wird angepasst, um zu überprüfen, dass Remote-URL unauthentifiziert ist (ohne `oauth2:token@`). `GH_TOKEN` Umgebungsvariable wird in Environment-Verifikation geprüft.

7. **Tests anpassen — `PullAsync_ShouldRunGitPull_WithConfiguredCredentials()`**
   - Voraussetzungen: Schritt 4 abgeschlossen.
   - Beschreibung: Test wird angepasst, um zu überprüfen, dass `GH_TOKEN` Umgebungsvariable übergeben wird, nicht URL-Embedding.

8. **Neue Test — Tokens werden nicht in Remote-URLs eingebettet**
   - Voraussetzungen: Schritte 2-4 abgeschlossen.
   - Beschreibung: Neuer Test `CloneRepositoryAsync_ShouldNotEmbedToken_InCloneUrl()` überprüft explizit, dass Clone-URL keinen Token enthält. Ähnliche Tests für `PushBranchAsync()` und `PullAsync()`.

9. **Neue Test — `EnsureRemoteCredentialsAsync()` bereinigt eingebettete Tokens**
   - Voraussetzungen: Schritt 4 abgeschlossen.
   - Beschreibung: Test überprüft, dass wenn Remote-URL einen eingebetteten Token enthält, dieser entfernt wird und URL normalisiert wird.

10. **Token-Sanitization validieren in allen Error-Paths**
    - Voraussetzungen: Schritte 2-4 abgeschlossen.
    - Beschreibung: Neue oder erweiterte Tests stellen sicher, dass `SanitizeSensitiveOutput()` in allen Error-Paths für `CloneRepositoryAsync()`, `PushBranchAsync()`, `PullAsync()` aufgerufen wird. Tests überprüfen, dass Token in Exception-Messages nicht im Klartext auftauchen.

11. **Build und vollständige Test-Suite ausführen**
    - Voraussetzungen: Alle Schritte 1-10 abgeschlossen.
    - Beschreibung: `dotnet build` und `dotnet test` mit Filter `Category!=OsInterface` ausführen, um sicherzustellen, dass alle Tests bestanden werden und keine Regressions-Fehler eingeführt wurden.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CloneRepositoryAsync_ShouldNotEmbedToken_InCloneUrl()` | `GitHubPluginTests` | Überprüft, dass `git clone` mit unauthentifizierter URL aufgerufen wird (kein `https://oauth2:token@` in Arguments) |
| `CloneRepositoryAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication()` | `GitHubPluginTests` | Überprüft, dass `GH_TOKEN` Umgebungsvariable an `git clone` übergeben wird |
| `EnsureRemoteCredentialsAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl()` | `GitHubPluginTests` | Überprüft, dass wenn Remote-URL einen eingebetteten Token (`oauth2:token@`) enthält, dieser entfernt wird |
| `EnsureRemoteCredentialsAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()` | `GitHubPluginTests` | Überprüft, dass Remote-URL normalisiert wird auf `https://github.com/owner/repo` |
| `PushBranchAsync_ShouldNotEmbedToken_InRemoteUrl()` | `GitHubPluginTests` | Überprüft, dass `git remote set-url` nicht mit Token versehene URL setzt |
| `PushBranchAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication()` | `GitHubPluginTests` | Überprüft, dass `GH_TOKEN` Umgebungsvariable an `git push` übergeben wird |
| `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()` | `GitHubPluginTests` | Überprüft, dass `git pull` mit `GH_TOKEN` Umgebungsvariable funktioniert, nicht mit URL-Embedding |
| `SanitizeSensitiveOutput_ShouldMaskToken_InPushErrorMessages()` | `GitHubPluginTests` | Überprüft, dass Token in `git push` Fehler-Output maskiert wird |
| `SanitizeSensitiveOutput_ShouldMaskToken_InPullErrorMessages()` | `GitHubPluginTests` | Überprüft, dass Token in `git pull` Fehler-Output maskiert wird |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()` | Verifikation auf unauthentifizierte URL anpassen (`https://github.com/test/repo` statt `https://oauth2:token@...`); `GH_TOKEN` Umgebungsvariable erwarten |
| `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()` | Verifikation auf unauthentifizierte Remote-URL anpassen (kein `oauth2:token@`); `GH_TOKEN` Umgebungsvariable erwarten |
| `PullAsync_ShouldRunGitPull_WithConfiguredCredentials()` | Verifikation auf `GH_TOKEN` Umgebungsvariable anpassen; URL-Embedding-Checks entfernen |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Benutzer klont ein Repository über die UI | `E2E_GitHubPlugin*.cs` (neue oder bestehende E2E-Testklasse) | Repository wird erfolgreich geklont; Token taucht nicht in `.git/config` oder CLI-Logs auf |
| Benutzer führt Push nach dem Klonen durch | `E2E_GitHubPlugin*.cs` | Push funktioniert mit `GH_TOKEN`-Authentifizierung; Token ist nicht in Remote-URL sichtbar |
| Benutzer führt Pull nach dem Klonen durch | `E2E_GitHubPlugin*.cs` | Pull funktioniert mit `GH_TOKEN`-Authentifizierung; Token ist nicht in Remote-URL sichtbar |
| Token wird nicht in Fehlermeldungen angezeigt | `E2E_GitHubPlugin*.cs` | Authentifizierungsfehler werden angezeigt, aber Token ist maskiert (als `oauth2:***@`) |

**Hinweis:** E2E-Tests müssen minimal sein (wie pro requirement.md festgelegt). Da die Funktionalität für Benutzer transparent bleibt (Push/Pull arbeitet wie zuvor), sollten E2E-Tests sich auf **kritische Sicherheits-Aspekte** konzentrieren: (1) Token ist nicht in `.git/config` oder CLI-Logs sichtbar, (2) Authentifizierung funktioniert trotz fehlenden URL-Embeddings.

Bestehende E2E-Tests, die Push/Pull testen, benötigen möglicherweise Anpassungen, falls sie explizit auf Token-Embedding in Remote-URLs prüfen — diese müssen entfernt oder angepasst werden.

## Offene Punkte

Keine. Alle fünf offenen Fragen aus der Anforderung wurden durch Designentscheidungen geklärt:

1. ✓ **Authentifizierungsvariante:** Variante A (`GH_TOKEN`-Umgebungsvariable) mit `.netrc` als optionalem Fallback — standardisiert und sicher.
2. ✓ **System-`gh cli`-Authentifizierung:** Nicht unterstützt — Programm-Token ist Single Source of Truth, vereinfacht Fehlerbehandlung.
3. ✓ **Rückwärtskompatibilität:** Nur bei neuen Klones — reduziert Komplexität und Fehlerrisiko.
4. ✓ **Dokumentation:** Nicht erforderlich — Änderungen sind transparent für Anwender, Plugin-Funktionalität ändert sich nicht.
5. ✓ **Windows `.netrc`/`_netrc` Standardisierung:** Beide weiterhin unterstützen — bestehende Kompatibilität erhalten.
