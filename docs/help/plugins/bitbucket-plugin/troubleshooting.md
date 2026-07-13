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

## Arbeitsverzeichnis-Auswahl zeigt nur manuelle Eingabe

**Symptom:** Nach Auswahl eines BitBucket-Repositories erscheint statt der Arbeitsverzeichnis-ComboBox ein
Textfeld, oder Unterverzeichnisse werden nicht zur Auswahl angeboten.

**Ursachen:**
- App Password hat keine ausreichende `repository:read`-Berechtigung
- Repository-URL kann nicht eindeutig als Bitbucket Cloud- oder Self-Hosted-Repository erkannt werden
- Bitbucket-API ist nicht erreichbar oder liefert einen Fehler
- Der konfigurierte Branch/Default-Branch ist nicht erreichbar
- Self-Hosted-Instanz blockiert die `browse`-API oder nutzt ein inkompatibles URL-/Projektformat

**Lösung:**
1. Prüfe, ob das Repository in BitBucket im Browser geöffnet werden kann.
2. Prüfe das App Password auf `repository:read`.
3. Falls Cloud:
   - Prüfe Workspace und Repository-Slug in der URL `https://bitbucket.org/{workspace}/{repo}`.
   - Prüfe, ob der Default-Branch existiert und lesbar ist.
4. Falls Self-Hosted:
   - Prüfe Project Key und Repository Slug gegen die Web-URL
     `https://bitbucket.company.com/projects/{PROJECT}/repos/{REPO}`.
   - Prüfe, ob die Browse-API erreichbar ist, z. B. über
     `https://bitbucket.company.com/rest/api/1.0/projects/{PROJECT}/repos/{REPO}/browse`.
5. Gib das Arbeitsverzeichnis bei Bedarf manuell als relativen Pfad ein, z. B. `src` oder `backend/api`.

> **Hinweis:** Das manuelle Textfeld ist ein gewollter Fallback. Die Projektanlage und -bearbeitung bleiben
> damit möglich, auch wenn die Remote-Verzeichnisstruktur gerade nicht geladen werden kann. Ein leeres
> Repository mit erfolgreich geladenem Root zeigt dagegen weiterhin die Auswahlbox mit nur `"."`.

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
   - Stelle sicher, dass der SSH-Agent läuft und SSH-Keys konfiguriert sind
   - Öffne ein Terminal und prüfe: `ssh -T git@bitbucket.org` (Cloud) oder `ssh -T git@bitbucket.company.com` (Self-Hosted)
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
