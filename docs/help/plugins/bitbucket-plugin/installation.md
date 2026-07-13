← [Zurück zur Übersicht](index.md)

# BitBucket-Plugin — Installation und Konfiguration

## Voraussetzungen

| Komponente | Erforderlich | Beschreibung |
|-----------|-----------|--------------|
| Git | Ja | Für `git clone`, `git pull`, `git push` erforderlich |
| curl | Ja | Für API-Aufrufe erforderlich |
| BitBucket Cloud oder Self-Hosted | Ja | Repository-Host |
| App Password | Ja | Für Authentifizierung erzeugt (siehe unten) |
| Jira Cloud (optional) | Nein | Nur für Issue-Tracking erforderlich |

## Einrichtung BitBucket Cloud

### Schritt 1: App Password erstellen

1. Melde dich in BitBucket Cloud an unter **https://bitbucket.org**
2. Klicke auf dein **Profilbild** → **Personal Settings** (unten links)
3. Navigiere zu **App Passwords** (unter „Security")
4. Klicke auf **Create app password**
5. Gib einen Namen ein, z.B. `Softwareschmiede`
6. Setze folgende Permissions:
   - ☑ `repository:read`
   - ☑ `repository:write`
   - ☑ `pullrequest:write`
7. Klicke **Create** — kopiere das generierte Passwort sofort in die Zwischenablage (wird danach nicht mehr angezeigt)

### Schritt 2: Workspace ermitteln

1. Gehe zu **https://bitbucket.org/workspace**
2. Wähle einen Workspace aus — der **Workspace-Name** wird später benötigt (z.B. `martin-stromberg`)
3. Alternativ: In der Repository-URL `https://bitbucket.org/{workspace}/{repo}` ist der erste Teil der Workspace

### Schritt 3: Plugin-Einstellungen konfigurieren

1. Öffne die Softwareschmiede-App und navigiere zu **Einstellungen**
2. Wähle **BitBucket-Plugin** aus der Plugin-Liste
3. Fülle folgende Felder aus:

   | Feld | Wert |
   |------|------|
   | **Hosting-Modus** | `Cloud` |
   | **BitBucket Username** | E-Mail oder Benutzername (z.B. `martin@example.com`) |
   | **App Password** | Das in Schritt 1 generierte Passwort |
   | **BitBucket Workspace** | Der Workspace-Name (z.B. `martin-stromberg`) |

4. Klicke auf **Speichern** — die Werte werden verschlüsselt im Windows Credential Store gespeichert

### Schritt 4: Health-Check durchführen

1. Klicke im Plugin-Dialog auf **Verbindung testen** (falls vorhanden)
2. Falls erfolgreich: Grüner Status
3. Falls fehlgeschlagen: Prüfe Benutzername, App Password und Workspace-Namen

## Einrichtung BitBucket Self-Hosted

### Schritt 1: App Password erstellen (Administrator/Benutzer)

Die Erstellung ist abhängig von der Bitbucket Server/Data Center-Version:

**Bitbucket 5.0+:**
1. Melde dich am Self-Hosted BitBucket an
2. Klicke auf dein **Profilbild** → **Personal Settings**
3. Navigiere zu **App Passwords** oder **HTTP Access Tokens**
4. Erstelle einen neuen Token mit Permissions:
   - ☑ `repository:read`
   - ☑ `repository:write`
   - ☑ `pullrequest:write`
5. Kopiere das generierte Token

**Ältere Versionen:**
- Konsultiere die Administrator-Dokumentation deiner Bitbucket-Installation

### Schritt 2: BitBucket-URL ermitteln

1. Öffne deine Bitbucket Self-Hosted-Installation im Browser (z.B. `https://bitbucket.company.com`)
2. Die URL muss die **Basis-URL ohne Pfad** sein:
   - ✓ `https://bitbucket.company.com`
   - ✓ `https://bitbucket.company.com:7990`
   - ✗ `https://bitbucket.company.com/bitbucket` (falsches Format)

### Schritt 3: Plugin-Einstellungen konfigurieren

1. Öffne die Softwareschmiede-App und navigiere zu **Einstellungen**
2. Wähle **BitBucket-Plugin** aus der Plugin-Liste
3. Fülle folgende Felder aus:

   | Feld | Wert |
   |------|------|
   | **Hosting-Modus** | `Self-Hosted` |
   | **BitBucket URL (Self-Hosted)** | Basis-URL (z.B. `https://bitbucket.company.com`) |
   | **BitBucket Username** | Benutzername (z.B. `developer@company.com`) |
   | **App Password** | Das in Schritt 1 generierte Token |
   | **BitBucket Workspace** | Project Key (z.B. `MYPROJ`) |

4. Klicke auf **Speichern**

### Schritt 4: Health-Check durchführen

1. Klicke im Plugin-Dialog auf **Verbindung testen** (falls vorhanden)
2. Falls erfolgreich: Grüne Status
3. Falls fehlgeschlagen: Prüfe URL, Benutzername und Token

## Optionale Jira-Integration

Falls dein Projekt auch Jira nutzt, kannst du Issues direkt in der Softwareschmiede anzeigen:

### Schritt 1: Jira API Token erstellen

1. Melde dich in Jira Cloud an unter **https://{workspace}.atlassian.net**
2. Klicke auf dein **Profilbild** → **Settings**
3. Navigiere zu **API Tokens** (oder **Personal Access Tokens**)
4. Klicke auf **Create API token**
5. Gib einen Namen ein (z.B. `Softwareschmiede`)
6. Kopiere das generierte Token

### Schritt 2: Plugin-Jira-Einstellungen konfigurieren

Im BitBucket-Plugin-Dialog, Reiter **Jira**:

| Feld | Wert |
|------|------|
| **Jira Base URL** | `https://{workspace}.atlassian.net` (z.B. `https://mycompany.atlassian.net`) |
| **Jira Project Key** | Eindeutiger Projekt-Schlüssel (z.B. `MYAPP`) |
| **Jira Login E-Mail** | Die E-Mail des Jira-Kontos, das das Token erstellt hat |
| **Jira API Token** | Das in Schritt 1 generierte Token |

Klicke auf **Speichern**.

## Credential-Keys

Intern speichert das Plugin Werte unter folgenden Keys im Windows Credential Store:

| Key | Beschreibung |
|-----|--------------|
| `Softwareschmiede.Bitbucket.Username` | BitBucket-Benutzername |
| `Softwareschmiede.Bitbucket.AppPassword` | BitBucket App Password |
| `Softwareschmiede.Bitbucket.Workspace` | Workspace (Cloud) oder Project Key (Self-Hosted) |
| `Softwareschmiede.Bitbucket.HostingMode` | `Cloud` oder `SelfHosted` |
| `Softwareschmiede.Bitbucket.SelfHostedUrl` | Basis-URL der Self-Hosted-Installation |
| `Softwareschmiede.Bitbucket.JiraUrl` | Jira Base URL (optional) |
| `Softwareschmiede.Bitbucket.JiraProjectKey` | Jira Project Key (optional) |
| `Softwareschmiede.Bitbucket.JiraEmail` | Jira Login E-Mail (optional) |
| `Softwareschmiede.Bitbucket.JiraApiToken` | Jira API Token (optional) |

## Überprüfung

### Repositories werden nicht angezeigt

1. Öffne ein Projekt und versuche, ein **neues Repository hinzuzufügen**
2. Klicke auf **Repositories laden**
3. Falls keine Repositories angezeigt werden:
   - Prüfe, dass der Workspace/Project Key korrekt ist
   - Versuche den Health-Check aus den Plugin-Einstellungen
   - Prüfe die App-Password-Permissions (siehe oben)

### Health-Check fehlgeschlagen

1. Prüfe die Plugin-Einstellungen auf Tippfehler
2. Prüfe, dass das App Password noch gültig ist (nicht abgelaufen oder gelöscht)
3. Falls Self-Hosted: Prüfe, dass die BitBucket-URL erreichbar ist (HTTPS, Port, Firewall)
4. Falls Jira: Prüfe, dass die Jira-Einstellungen korrekt sind (oder deaktiviere Jira-Integration temporär)

### Pull Requests können nicht erstellt werden

1. Prüfe, dass das App Password die `pullrequest:write`-Permission hat
2. Versuche eine manuelle Health-Check durchführen
3. Falls Self-Hosted: Prüfe, dass die API-Version unterstützt wird (1.0+)

## API-Endpunkte (Referenz)

Die Plugin-APIs sind intern und nicht öffentlich exponiert. Jedoch nutzt das Plugin folgende BitBucket-API-Endpunkte:

**Cloud:**
- `GET /2.0/repositories/{workspace}` — Repositories auflisten
- `GET /2.0/repositories/{workspace}/{repo_slug}/src/{branch}/` — Verzeichnisstruktur für die Arbeitsverzeichnis-Auswahl laden
- `GET /2.0/user` — Authentifizierung prüfen (Health-Check)
- `POST /2.0/repositories/{workspace}/{repo}/pullrequests` — PR erstellen

**Self-Hosted:**
- `GET /rest/api/1.0/projects/{projectKey}/repos` — Repositories auflisten
- `GET /rest/api/1.0/projects/{projectKey}/repos/{repoSlug}/browse/{path}` — Verzeichnisstruktur für die Arbeitsverzeichnis-Auswahl laden
- `GET /rest/api/1.0/user` — Authentifizierung prüfen (Health-Check)
- `POST /rest/api/1.0/projects/{projectKey}/repos/{repoSlug}/pull-requests` — PR erstellen

**Jira:**
- `GET /rest/api/3/search?jql=...` — Issues mit JQL-Filter suchen
- `GET /rest/api/3/myself` — Authentifizierung prüfen (Health-Check)
