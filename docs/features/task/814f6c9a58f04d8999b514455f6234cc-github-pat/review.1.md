# Plan-Review: GitHub-PAT-Token-Sicherheit und Separation

**Datum:** 30. August 2026  
**Branch:** task/814f6c9a58f04d8999b514455f6234cc-github-pat  
**Status:** Offene Aufgaben vorhanden

---

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Die Implementierung setzt den Plan zu ~95% um. Alle kritischen Code-Änderungen wurden vorgenommen, Unit-Tests wurden vollständig angepasst und erweitert. E2E-Tests fehlen jedoch noch gemäß Plan-Anforderung.

---

## Umgesetzte Planelemente

### Implementierung (GitHubPlugin.cs)

- [x] **`GetGitEnvironment()` erweitert um `GH_TOKEN` Übergabe**
  - Token wird aus `ICredentialStore` abgerufen
  - Token wird als `GH_TOKEN` in Umgebungsvariablen-Dictionary eingefügt
  - `.netrc`-Pfad wird weiterhin als optionaler Fallback konfiguriert
  - Implementierung: Zeilen 117-140

- [x] **`CloneRepositoryAsync()` angepasst — Token-Embedding entfernt**
  - `git clone` wird mit unauthentifizierter URL aufgerufen
  - Token wird ausschließlich über `GetGitEnvironment(token)` bereitgestellt
  - `BuildAuthenticatedCloneUrl()` wird nicht mehr aufgerufen
  - Implementierung: Zeilen 730-765

- [x] **`ConfigureGitCredentialsAsync()` angepasst — Token-Embedding entfernt**
  - `.netrc`-Datei wird erstellt/aktualisiert mit Credentials
  - Remote-URL wird auf unauthentifizierte Form gesetzt (`https://github.com/owner/repo.git`)
  - `GetGitEnvironment(token)` wird vor allen `git`-Befehlen aufgerufen
  - Implementierung: Zeilen 247-339

- [x] **`EnsureRemoteCredentialsAsync()` angepasst — Zu Normalisierungsfunktion umbauen**
  - Remote-URL wird gelesen
  - Neue Regex `EmbeddedTokenPattern` erkennt `oauth2:...@` Patterns
  - Eingebettete Tokens werden entfernt und URL normalisiert
  - Alte Logik zum Hinzufügen von Tokens entfernt
  - Implementierung: Zeilen 203-245

- [x] **Konstante `EmbeddedTokenPattern` hinzugefügt**
  - Regex zur Erkennung von eingebetteten Tokens: `oauth2:[^@\s]+@`
  - Kompatibel mit Case-Insensitivity und vorcompiliert für Performance
  - Zeile 20

### Unit-Tests (GitHubPluginTests.cs)

**Bestehende Tests angepasst:**

- [x] `CloneRepositoryAsync_ShouldCallGitClone_WhenCalled()` — angepasst
  - Verifiziert unauthentifizierte URL `https://github.com/test/repo`
  - Verifiziert `GH_TOKEN` in Umgebungsvariablen
  - Zeile 484-506

- [x] `PushBranchAsync_ShouldConfigureRemoteUrlWithToken_BeforePush()` — angepasst
  - Verifiziert, dass Legacy-Repositories mit eingebettetem Token bereinigt werden
  - Verifiziert neue URL ohne Token: `https://github.com/owner/repo.git`
  - Verifiziert `GH_TOKEN` in Push-Umgebung
  - Zeile 999-1044

- [x] `PullAsync_ShouldRunGitPull_WithConfiguredCredentials()` — angepasst
  - Verifiziert `GH_TOKEN` Umgebungsvariable bei Pull
  - Zeile 1237-1263

**Neue Tests hinzugefügt:**

- [x] `CloneRepositoryAsync_ShouldNotEmbedToken_InCloneUrl()` — Neu
  - Überprüft, dass `oauth2:` nicht in Clone-Argumenten vorhanden ist
  - Überprüft, dass Token nicht in Clone-Argumenten auftaucht
  - Zeile 510-534

- [x] `CloneRepositoryAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication()` — Neu
  - Überprüft explizit, dass `GH_TOKEN` korrekt übergeben wird
  - Zeile 538-560

- [x] `EnsureRemoteCredentialsAsync_ShouldRemoveEmbeddedToken_FromRemoteUrl()` — Neu
  - Überprüft, dass wenn Remote-URL einen eingebetteten Token hat, dieser entfernt wird
  - Zeile 1163-1196

- [x] `EnsureRemoteCredentialsAsync_ShouldNormalizeRemoteUrl_ToUnauthenticatedForm()` — Neu
  - Überprüft URL-Normalisierung auf unauthentifizierte Form
  - Zeile 1200-1233

- [x] `PushBranchAsync_ShouldNotEmbedToken_InRemoteUrl()` — Neu
  - Überprüft, dass kein `set-url` aufgerufen wird, wenn URL bereits unauthentifiziert ist
  - Zeile 1048-1074

- [x] `PushBranchAsync_ShouldPassGhTokenEnvironmentVariable_ForAuthentication()` — Neu
  - Überprüft `GH_TOKEN` bei Push
  - Zeile 1078-1104

- [x] `PullAsync_ShouldNotRequireEmbeddedToken_InRemoteUrl()` — Neu
  - Überprüft, dass Pull ohne eingebetteten Token funktioniert
  - Zeile 1267-1290 (teilweise gelesen)

- [x] `SanitizeSensitiveOutput_ShouldMaskToken_InPushErrorMessages()` — Neu
  - Überprüft, dass Token in Push-Fehlern maskiert wird
  - Verifiziert `oauth2:***@` statt Klartext
  - Zeile 1328-1352

- [x] `SanitizeSensitiveOutput_ShouldMaskToken_InPullErrorMessages()` — Neu
  - Überprüft, dass Token in Pull-Fehlern maskiert wird
  - Zeile 1356-1380

---

## Offene Aufgaben

### E2E-Tests fehlen (Plan-Anforderung)

- [ ] **E2E-Test: Benutzer klont ein Repository über die UI**
  - **Szenario:** Repository wird erfolgreich geklont; Token taucht nicht in `.git/config` oder CLI-Logs auf
  - **Testdatei:** `E2E_GitHubPlugin*.cs` (noch nicht vorhanden)
  - **Grund:** Wurde im Plan unter "E2E-Tests (Pflicht)" als erforderlich deklariert (Plan, Zeile 171-183)
  - **Auswirkung:** Kritische Sicherheits-Aspekte sind nicht durch automatisierte E2E-Tests validiert

- [ ] **E2E-Test: Benutzer führt Push nach dem Klonen durch**
  - **Szenario:** Push funktioniert mit `GH_TOKEN`-Authentifizierung; Token ist nicht in Remote-URL sichtbar
  - **Testdatei:** `E2E_GitHubPlugin*.cs` (noch nicht vorhanden)

- [ ] **E2E-Test: Benutzer führt Pull nach dem Klonen durch**
  - **Szenario:** Pull funktioniert mit `GH_TOKEN`-Authentifizierung; Token ist nicht in Remote-URL sichtbar
  - **Testdatei:** `E2E_GitHubPlugin*.cs` (noch nicht vorhanden)

- [ ] **E2E-Test: Token wird nicht in Fehlermeldungen angezeigt**
  - **Szenario:** Authentifizierungsfehler werden angezeigt, aber Token ist maskiert (als `oauth2:***@`)
  - **Testdatei:** `E2E_GitHubPlugin*.cs` (noch nicht vorhanden)

---

## Hinweise

### Implementierungs-Qualität

- **Code:** Hochwertig. Alle Änderungen sind minimal, gezielt und folgen dem Plan exakt.
- **Regex-Pattern:** `EmbeddedTokenPattern` ist korrekt implementiert und optimiert (Compiled, IgnoreCase)
- **Fehlerbehandlung:** Token-Sanitization in allen Error-Paths vorhanden (Push, Pull, Clone)
- **Rückwärtskompatibilität:** `.netrc`-Fallback wird beibehalten; alte `.git/config` Einträge werden normalisiert

### Test-Coverage

- **Unit-Tests:** Umfassend. Alle 9 neuen Tests sind vorhanden, bestehende Tests wurden korrekt angepasst.
- **Deckung:** 
  - ✓ Token wird nicht mehr in URLs eingebettet
  - ✓ `GH_TOKEN` wird korrekt übergeben
  - ✓ Eingebettete Tokens werden erkannt und entfernt
  - ✓ Token wird in Fehlern maskiert
  - ✗ E2E-Deckung fehlt

### Abhängigkeiten und Voraussetzungen

- Keine neuen Interfaces oder Klassen erforderlich
- Keine Datenbankmigrationen erforderlich
- Keine Konfigurationsänderungen erforderlich
- Bestehende `ICredentialStore` und `ICliRunner` Interfaces ausreichend

### Risikenminderung

1. **Legacy-Repositories:** Mit eingebettetem Token werden automatisch normalisiert bei nächstem Push/Pull
2. **Token-Sicherheit:** Tokens werden nicht mehr in CLI-Befehlen oder `.git/config` sichtbar
3. **Fehlerbehandlung:** `SanitizeSensitiveOutput()` sichert ab, dass Tokens nicht in Exceptions auftauchen

### Empfehlung für E2E-Tests

Laut CLAUDE.md sollen E2E-Tests "minimal" sein und sich auf "kritische Sicherheits-Aspekte" konzentrieren. Für diesen Task könnten E2E-Tests auf folgende Punkte reduziert werden:

1. **Sicherheits-Validierung:** Token ist nicht in `.git/config` oder CLI-Logs sichtbar
2. **Funktionalitäts-Validierung:** Push/Pull funktioniert mit `GH_TOKEN`-Authentifizierung

Diese zwei Tests als eine konsolidierte E2E-Testmethode hätten minimale Laufzeit und maximale Deckung der Anforderung.

---

## Zusammenfassung

Die **Kern-Implementierung ist vollständig und korrekt**. Alle 10 Schritte aus der Umsetzungsreihenfolge (Plan, Zeile 101-145) wurden umgesetzt:

1. ✓ `GetGitEnvironment()` erweitert
2. ✓ `CloneRepositoryAsync()` angepasst
3. ✓ `ConfigureGitCredentialsAsync()` angepasst
4. ✓ `EnsureRemoteCredentialsAsync()` angepasst
5. ✓ Tests angepasst (`CloneRepositoryAsync`)
6. ✓ Tests angepasst (`PushBranchAsync`)
7. ✓ Tests angepasst (`PullAsync`)
8. ✓ Neue Tests hinzugefügt (Token-Embedding)
9. ✓ Neue Tests hinzugefügt (Token-Normalisierung)
10. ✓ Token-Sanitization validiert

**Offene Arbeit:**
- Schritt 11: Build und vollständige Test-Suite — noch zu verifizieren
- **E2E-Tests:** Plan-Anforderung noch nicht erfüllt
