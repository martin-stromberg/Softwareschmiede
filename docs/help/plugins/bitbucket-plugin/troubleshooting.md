← [Zurück zur Übersicht](index.md)

# BitBucket-Plugin — Fehlerbehebung

## Repositories werden nicht angezeigt

**Symptom:** Beim Versuchen, ein Repository hinzuzufügen, zeigt die Liste keine Repositories an, obwohl welche in BitBucket vorhanden sind.

**Ursachen:**
- Workspace/Project Key ist falsch konfiguriert
- App Password hat nicht die erforderliche `repository:read`-Permission
- BitBucket-URL (Self-Hosted) ist falsch oder nicht erreichbar
- Netzwerk-Firewall blockiert die Verbindung

**Lösung:**
1. Öffne **Einstellungen** → **BitBucket-Plugin**
2. Prüfe den Workspace-Namen (Cloud):
   - Öffne **https://bitbucket.org/workspace** und wähle einen Workspace aus
   - Der Workspace-Name muss exakt konfiguriert werden
3. Prüfe das App Password auf Permissions:
   - Cloud: Gehe zu **Personal Settings** → **App Passwords** → Wähle das Passwort → Prüfe auf ☑ `repository:read`
   - Self-Hosted: Prüfe mit dem Administrator
4. Falls Self-Hosted: Prüfe die URL:
   - Öffne die BitBucket-URL im Browser und verifiziere, dass sie erreichbar ist
   - URL muss Basis-URL ohne Pfad sein: `https://bitbucket.company.com` (nicht `https://bitbucket.company.com:8080/bitbucket`)
5. Führe einen **Health-Check** aus (falls im UI verfügbar)
6. Speichere die Einstellungen erneut

## Health-Check fehlgeschlagen

**Symptom:** Im Plugin-Dialog wird der Status als „Verbindung fehlgeschlagen" oder ähnlich angezeigt.

**Ursachen:**
- BitBucket-Anmeldedaten sind falsch oder abgelaufen
- BitBucket-URL (Self-Hosted) ist nicht erreichbar
- Jira-Integration ist fehlkonfiguriert
- Netzwerk-Firewall blockiert die Verbindung

**Lösung:**

1. **Für Cloud:**
   - Prüfe Benutzername und App Password:
     - Öffne **https://bitbucket.org** und melde dich mit deinen Daten an
     - Falls der Login fehlschlägt, sind die Anmeldedaten falsch
   - App Password ist möglicherweise abgelaufen:
     - Gehe zu **Personal Settings** → **App Passwords**
     - Falls das Passwort fehlt, wurde es gelöscht oder ist abgelaufen — erstelle ein neues (siehe Installation)
   - Workspace-Name ist falsch:
     - Prüfe die Schreibweise (Groß-/Kleinschreibung) gegen die BitBucket-Workspace-URL

2. **Für Self-Hosted:**
   - BitBucket-URL ist nicht erreichbar:
     - Öffne die URL im Browser und vergewissere dich, dass sie lädt
     - Prüfe Firewall-Regeln und VPN-Verbindung
   - URL-Format ist falsch:
     - Entferne Pfade wie `/bitbucket`: `https://bitbucket.company.com:7990` (nicht `/rest/...`)
   - App Password ist ungültig:
     - Kontaktiere den BitBucket-Administrator, um ein neues Token zu erzeugen

3. **Falls Jira integriert:**
   - Jira-Anmeldedaten sind falsch:
     - Melde dich bei Jira an und verifiziere die E-Mail und das API Token
   - Jira-URL ist falsch:
     - Prüfe, dass die URL das Format `https://{workspace}.atlassian.net` hat (nicht `https://jira.company.com`)
   - Deaktiviere temporär die Jira-Integration:
     - Leere die Jira-URL-Felder und speichere
     - Versuche erneut einen Health-Check
     - Falls dieser erfolgreich ist, liegt das Problem bei Jira

## Pull Request konnte nicht erstellt werden

**Symptom:** Beim Erstellen eines Pull Requests wird eine Fehlermeldung angezeigt oder der PR wird nicht erstellt.

**Ursachen:**
- App Password hat nicht die `pullrequest:write`-Permission
- Standard-Branch ist nicht definiert oder falsch
- Repository existiert nicht oder API-Pfad ist falsch (Self-Hosted)
- Netzwerk-Fehler oder Server-Fehler

**Lösung:**
1. Prüfe die App-Password-Permissions:
   - Cloud: **Personal Settings** → **App Passwords** → Wähle Passwort → Prüfe auf ☑ `pullrequest:write`
   - Self-Hosted: Frag den Administrator
2. Prüfe den Default-Branch:
   - Öffne das Repository in BitBucket
   - Gehe zu **Repository Settings** → **Default Branch**
   - Der Default-Branch muss `main` oder `master` sein
3. Falls Self-Hosted:
   - Prüfe, dass der Project Key und Repository Slug korrekt sind
   - Öffne ein Repository in BitBucket und prüfe die URL: `https://bitbucket.company.com/projects/{PROJECT}/{repo}`
4. Prüfe die Aufgaben-Logs:
   - Öffne die Aufgabe und schaue in den **Aufgabenprotokoll**-Bereich
   - Kopiere die Fehlermeldung und prüfe, ob sie spezifische Hinweise gibt

## Repository Clone fehlgeschlagen

**Symptom:** Git-Befehle wie Clone, Pull oder Push schlagen fehl mit Authentifizierungs- oder Netzwerk-Fehlern.

**Ursachen:**
- Git ist nicht installiert
- netrc-Konfiguration ist falsch (Windows: `_netrc`, Linux/Mac: `.netrc`)
- SSH-Keys sind nicht konfiguriert (bei SSH-URLs)
- Netzwerk-Firewall blockiert Git-Operationen

**Lösung:**
1. Prüfe Git-Installation:
   - Öffne ein Terminal und führe `git --version` aus
   - Falls nicht gefunden, installiere Git von **https://git-scm.com**
2. Prüfe netrc-Datei:
   - Windows: `C:\Users\{username}\_netrc`
   - Linux/Mac: `~/.netrc`
   - Die Datei sollte folgende Einträge enthalten:
     ```
     machine bitbucket.org
     login {username}
     password {app_password}
     ```
   - Falls Self-Hosted:
     ```
     machine bitbucket.company.com
     login {username}
     password {app_password}
     ```
   - Falls die Datei fehlt oder falsch ist, führe einen Health-Check durch — das Plugin aktualisiert die netrc-Datei automatisch
3. Falls SSH-URLs verwendet werden:
   - Das Plugin unterstützt ausschließlich HTTPS-URLs. SSH-URLs (`git@bitbucket.org:...` oder `ssh://...`) werden abgelehnt.
   - Verwende die HTTPS-URL des Repositories (z.B. `https://bitbucket.org/workspace/repo.git`)
4. Netzwerk-Firewall:
   - Prüfe, dass Port 443 (HTTPS) oder 22 (SSH) offen ist
   - Falls hinter Proxy: Konfiguriere Git mit Proxy-Einstellungen

## Jira-Integration zeigt keine Issues

**Symptom:** Im Projekt werden keine Issues aus Jira angezeigt, obwohl die Jira-Integration konfiguriert ist.

**Ursachen:**
- Jira-Einstellungen sind nicht konfiguriert
- Jira Project Key ist falsch
- API Token ist ungültig oder hat nicht die erforderlichen Permissions
- Jira-Projekt hat keine Issues

**Lösung:**
1. Prüfe, dass alle Jira-Felder konfiguriert sind:
   - Öffne **Einstellungen** → **BitBucket-Plugin** → Reiter **Jira**
   - Prüfe, dass folgende Felder gefüllt sind:
     - **Jira Base URL** (z.B. `https://mycompany.atlassian.net`)
     - **Jira Project Key** (z.B. `MYAPP`)
     - **Jira Login E-Mail**
     - **Jira API Token**
2. Prüfe den Project Key:
   - Öffne Jira und navigiere zum Projekt
   - Der Project Key wird in der URL angezeigt: `https://mycompany.atlassian.net/projects/{KEY}`
   - Oder in den **Project Settings** unter **Details**
3. Prüfe den API Token:
   - Öffne Jira unter **Settings** → **API Tokens**
   - Falls das Token fehlt, wurde es gelöscht — erstelle ein neues (siehe Installation)
   - Vergewissere dich, dass das Token die `read:jira-work`-Permission hat
4. Prüfe das Projekt auf Issues:
   - Öffne das Jira-Projekt und suche mit `project={KEY}`
   - Falls keine Issues vorhanden sind, wird auch nichts angezeigt
5. Führe einen Health-Check durch:
   - Falls dieser fehlschlägt, sind die Jira-Einstellungen falsch

## Timeout beim Laden von Repositories

**Symptom:** Das Laden von Repositories dauert sehr lange oder wird unterbrochen mit einem Timeout-Fehler.

**Ursachen:**
- BitBucket-Server reagiert sehr langsam
- Viele Repositories im Workspace (Pagination-Problem)
- Netzwerk-Latenz oder Bandbreitenbeschränkung
- Workspace/Project enthält zu viele Repositories

**Lösung:**
1. Prüfe die BitBucket-Server-Performance:
   - Öffne die BitBucket-Weboberfläche und prüfe auf Performance-Probleme
   - Versuche, Repositories im Browser aufzulisten
2. Falls Self-Hosted:
   - Stelle sicher, dass der BitBucket-Server läuft und nicht überlastet ist
   - Frag den Administrator nach der Server-Performance
3. Reduziere die Anzahl der Repositories:
   - Das Plugin lädt alle Repositories eines Workspace/Projekts
   - Falls sehr viele vorhanden sind, erwäge, einen kleineren Workspace zu verwenden
4. Erhöhe das Timeout (falls möglich):
   - Dies ist in der aktuellen Version nicht konfigurierbar
   - Als Workaround: Nutze lokal geclonte Repositories und lade sie per **LocalDirectoryPlugin**

## Self-Hosted BitBucket: API-Fehler 401 Unauthorized

**Symptom:** Health-Check oder Repository-Laden schlägt mit `401 Unauthorized` fehl, obwohl die Anmeldedaten korrekt sind.

**Ursachen:**
- App Password ist ungültig oder abgelaufen
- Benutzer hat keine Permissions im Project/Repository
- LDAP- oder Authentifizierungs-Plugin in BitBucket ist fehlkonfiguriert

**Lösung:**
1. Prüfe das App Password:
   - Melde dich in BitBucket Self-Hosted an
   - Gehe zu **Personal Settings** → **HTTP Access Tokens** (oder **App Passwords**)
   - Prüfe, dass das Token existiert und nicht abgelaufen ist
   - Falls abgelaufen oder ungültig: Erstelle ein neues Token
2. Prüfe Benutzer-Permissions:
   - Gehe zu einem Repository → **Repository Settings** → **User and group access**
   - Vergewissere dich, dass dein Benutzer Zugriff hat
3. Starte einen curl-Test:
   - Öffne ein Terminal und führe aus:
     ```bash
     curl -u {username}:{password} https://bitbucket.company.com/rest/api/1.0/user
     ```
   - Falls dies 401 zurückgibt, sind Anmeldedaten/Permissions falsch
   - Falls erfolgreich (200), liegt das Problem in der Softwareschmiede-Konfiguration

## Self-Hosted BitBucket: API-Fehler 404 Not Found

**Symptom:** Health-Check oder Repository-Laden schlägt mit `404 Not Found` fehl.

**Ursachen:**
- BitBucket-URL ist falsch
- API-Endpunkt hat sich in einer anderen Version geändert
- Project Key oder Repository Slug existiert nicht

**Lösung:**
1. Prüfe die BitBucket-URL:
   - Öffne die URL im Browser: `https://bitbucket.company.com`
   - Die Seite sollte die BitBucket-Weboberfläche zeigen
   - Falls 404: Die URL ist falsch oder BitBucket ist nicht erreichbar
2. Prüfe die Project Key / Repository Slug:
   - Öffne ein Repository in BitBucket
   - Die URL sollte sein: `https://bitbucket.company.com/projects/{PROJECT}/repos/{REPO}`
   - Project Key und Repository Slug müssen exakt konfiguriert werden
3. Prüfe die API-Version:
   - Das Plugin nutzt API 1.0
   - Öffne `https://bitbucket.company.com/rest/api/1.0/user` im Browser (mit Basic Auth)
   - Falls 404: Möglicherweise ist API 1.0 nicht verfügbar (sehr alte BitBucket-Version)
   - Frag den Administrator nach der BitBucket-Version

## .netrc-Datei beschädigt oder falsch konfiguriert

**Symptom:** Git-Operationen schlagen mit `Invalid username or token` fehl, obwohl die Credentials korrekt sind.

**Ursachen:**
- `.netrc`-Datei ist beschädigt (ungültiges Format)
- `.netrc`-Datei hat falsche Permissions (sollte `0600` sein)
- `.netrc`-Eintrag wurde manuell bearbeitet und hat Syntax-Fehler
- Auf Windows: Git nutzt Credential Manager statt `.netrc`

**Lösung:**
1. **Datei löschen und neu generieren:**
   - Lösche die `.netrc`-Datei:
     - Windows: `C:\Users\{username}\_netrc`
     - Linux/Mac: `~/.netrc`
   - Führe einen Health-Check aus oder starte eine Git-Operation
   - Das Plugin erstellt die Datei automatisch neu

2. **Permissions prüfen (Linux/Mac):**
   ```bash
   ls -la ~/.netrc
   # Sollte zeigen: -rw------- (0600)
   chmod 600 ~/.netrc
   ```

3. **Format prüfen:**
   - Öffne die `.netrc`-Datei und prüfe, dass sie folgendes Format hat:
     ```
     machine bitbucket.org
     login {username}
     password {app_password}
     ```
   - Keine leeren Zeilen zwischen den Zeilen
   - Keine zusätzlichen Leerzeichen

4. **Windows Credential Manager prüfen:**
   - Öffne **Anmeldedaten-Manager** und lösche alle Bitbucket-Einträge
   - Dies zwingt Git, die `.netrc`-Datei zu verwenden

## Authentifizierung funktioniert, aber nur kurzzeitig

**Symptom:** Git-Operationen funktionieren manchmal, manchmal schlagen sie fehl mit Authentifizierungsfehler.

**Ursachen:**
- Credentials sind von Git gecacht und werden später durch neue überschrieben
- `.netrc`-Datei wird nicht konsistent gelesen
- Windows Credential Manager hat mehrere Einträge für den gleichen Host

**Lösung:**
1. **Gemischte Credentials bereinigen:**
   - Lösche die `.netrc`-Datei
   - Öffne **Anmeldedaten-Manager** und lösche alle Bitbucket-Einträge
   - Führe einen Health-Check aus
   - Versuche dann erneut

2. **Git Credential Cache leeren:**
   ```bash
   git credential reject
   host=bitbucket.org
   protocol=https
   # Drücke CTRL+D auf Linux/Mac oder CTRL+Z, Enter auf Windows
   ```

3. **Nur ein App Password verwenden:**
   - Stelle sicher, dass du nur ein gültiges App Password verwendest
   - Falls mehrere Passwörter erstellt wurden, lösche die alten und behalte nur das aktuelle

## Fehlermeldung: "Invalid username or token" bei Cloud-Clone

**Symptom:** `git clone` schlägt fehl mit der Meldung "fatal: Authentication failed" oder "Invalid username or token", obwohl die Credentials korrekt sind.

**Ursachen:**
- App Password hat keine erforderliche Permission (`repository:read`)
- E-Mail als Benutzername mit `@`-Zeichen wurde nicht korrekt URL-kodiert
- Credentials wurden nicht in die Clone-URL eingebettet

**Lösung:**
1. **App Password Permissions prüfen:**
   - Melde dich bei BitBucket Cloud an
   - Gehe zu **Personal Settings** → **App Passwords**
   - Klicke auf dein Passwort
   - Prüfe, dass ☑ `repository:read` aktiviert ist
   - Falls nicht, klicke **Edit** und aktiviere die Permission

2. **E-Mail-Benutzernamen URL-kodieren:**
   - Falls dein Username eine E-Mail ist (z.B. `martin@example.com`), wird das `@` als `%40` kodiert
   - Die Clone-URL sollte sein: `https://martin%40example.com:password@bitbucket.org/workspace/repo.git`
   - Das Plugin macht dies automatisch, aber stelle sicher, dass dein Username korrekt konfiguriert ist

3. **Credentials zurücksetzen:**
   - Lösche die `.netrc`-Datei und den Credential Manager (siehe oben)
   - Führe einen Health-Check aus
   - Versuche erneut zu klonen

## Fehlermeldung: "Authentication failed" bei Self-Hosted

**Symptom:** Git-Operationen gegen Self-Hosted schlagen fehl mit `Authentication failed`, obwohl die Credentials in der Cloud funktionieren.

**Ursachen:**
- Self-Hosted-Host in `.netrc` ist falsch
- Unterschiedliche App-Password-Formate zwischen Cloud und Self-Hosted
- Self-Hosted-Authentifizierung benötigt andere Permissions als Cloud

**Lösung:**
1. **Self-Hosted-Host in .netrc prüfen:**
   - Öffne die `.netrc`-Datei und prüfe, dass der Host korrekt ist:
     ```
     machine bitbucket.example.com  (NICHT: machine bitbucket.example.com:7990)
     login {username}
     password {app_password}
     ```
   - Der Host sollte nur Hostname und Port, aber nicht das Protokoll enthalten

2. **Self-Hosted-URL-Konfiguration prüfen:**
   - Öffne **Einstellungen** → **BitBucket-Plugin**
   - Prüfe, dass die **BitBucket-URL (Self-Hosted)** korrekt ist (z.B. `https://bitbucket.example.com`)
   - URL muss Basis-URL ohne Pfad sein

3. **API-Authentifizierung direkt testen:**
   ```bash
   curl -u {username}:{password} https://bitbucket.example.com/rest/api/1.0/user
   ```
   - Wenn dies funktioniert (HTTP 200), liegt das Problem bei Git oder `.netrc`
   - Wenn dies fehlschlägt (HTTP 401), sind die Credentials falsch

## Weitere Probleme

Falls das oben nicht aufgeführte Problem auftritt:

1. **Aufgabenprotokoll prüfen:**
   - Öffne die Aufgabe und schau in den **Aufgabenprotokoll**-Bereich
   - Dort sollte eine detaillierte Fehlermeldung stehen

2. **Health-Check durchführen:**
   - Öffne **Einstellungen** → **BitBucket-Plugin**
   - Klicke auf **Verbindung testen** (falls vorhanden)
   - Fehlermeldungen helfen oft, das Problem zu identifizieren

3. **Logs durchsuchen:**
   - Falls die App erweiterte Logs hat, prüfe diese auf `BitBucket`-bezogene Meldungen

4. **Kontakt mit Support:**
   - Wende dich an den BitBucket-Administrator (Self-Hosted)
   - Oder kontaktiere den Softwareschmiede-Support mit der Fehlermeldung und den oben durchgeführten Schritten
