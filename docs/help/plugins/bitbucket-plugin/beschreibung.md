← [Zurück zur Übersicht](index.md)

# BitBucket-Plugin — Beschreibung

## Zweck

Das BitBucket-Plugin verbindet die Softwareschmiede mit Git-Repositories in **BitBucket Cloud** und **BitBucket Server/Data Center**. Es ermöglicht:

- Auflistung von Repositories in einem Workspace (Cloud) oder Projekt (Self-Hosted)
- Repository-Klone mit HTTP-Authentifizierung (App Password)
- Branch-Management (Pull, Push, Erstellung)
- Pull-Request-Erstellung
- Remote-Abruf der Repository-Verzeichnisstruktur für die Arbeitsverzeichnis-Auswahl
- Git-basierte Repository-Operationen (Clone, Pull, Push)
- Integration mit Jira für Issue-Tracking

## Funktionsweise

### Cloud vs. Self-Hosted

Das Plugin erkennt automatisch, ob BitBucket Cloud oder eine Self-Hosted-Installation verwendet wird:

| Aspekt | Cloud (bitbucket.org) | Self-Hosted |
|--------|----------------------|-------------|
| **API-Basis-URL** | `https://api.bitbucket.org` | Benutzerdefiniert (z.B. `https://bitbucket.example.com`) |
| **API-Version** | 2.0 | 1.0 |
| **Repository-Pfad** | `/2.0/repositories/{workspace}/{repo}` | `/rest/api/1.0/projects/{projectKey}/repos/{repoSlug}` |
| **Authentifizierung** | App Password (gleich wie Self-Hosted) | App Password |
| **Clone-URL-Format** | `https://bitbucket.org/{workspace}/{repo}.git` | `https://{host}/scm/{projectKey}/{repo}.git` |

### Hosting-Modus-Auswahl

Der **Hosting-Modus** wird in den Plugin-Einstellungen konfiguriert:

- **Cloud** (Standard): Nutzt die offizielle BitBucket-Cloud-API bei `api.bitbucket.org`. Dies ist die Standard-Einstellung.
- **Self-Hosted**: Nutzt eine eigene BitBucket Server/Data Center-Installation mit benutzerdefinierter URL.

### Authentifizierung

Beide Modi verwenden **App Passwords** (Token):

1. **Cloud**: App Password wird unter **Personal Settings** → **App Passwords** erstellt
2. **Self-Hosted**: Admin erstellt oder Benutzer erstellt ein App Password (abhängig von Bitbucket Server-Version)

Das App Password wird in den Plugin-Einstellungen eingegeben und benötigt mindestens folgende Permissions:
- `repository:read` — Repositories lesen
- `repository:write` — Branches, PRs schreiben
- `pullrequest:write` — Pull Requests erstellen

### Jira-Integration

Das Plugin kann optional mit **Jira Cloud** integriert werden, um Issues zu laden und auf Repository-Seiten anzuzeigen:

- **Jira URL**: `https://{workspace}.atlassian.net`
- **Jira Project Key**: Eindeutiger Projektschlüssel (z.B. `MYAPP`)
- **Jira Login**: E-Mail für Jira API
- **Jira API Token**: Persönlicher Jira API Token

Die Jira-Integration ist unabhängig vom BitBucket-Hosting-Modus; dieselbe Jira-Cloud wird für beide BitBucket-Instanzen genutzt.

### Arbeitsverzeichnis-Auswahl

Bei der Repository-Zuweisung und beim Bearbeiten des Arbeitsverzeichnisses lädt das BitBucket-Plugin die
Verzeichnisstruktur des ausgewählten Remote-Repositories. Dadurch können Benutzer ein Unterverzeichnis
analog zum GitHub-Plugin direkt aus einer Auswahlbox wählen.

Im Cloud-Modus nutzt das Plugin die Bitbucket-Source-API und folgt paginierten Antworten über den
`next`-Link. Im Self-Hosted-Modus nutzt es die `browse`-API von Bitbucket Server/Data Center und baut die
Struktur wegen fehlender rekursiver Tiefenabfrage levelweise bis zur konfigurierten Maximal-Tiefe auf.

Ein erfolgreicher Abruf ohne Unterverzeichnisse ist ein gültiger Zustand: Die Auswahl enthält dann nur
`"."` für das Repository-Root. Technische Fehler, fehlende Berechtigungen, nicht erreichbare APIs oder
unparsebare Repository-URLs werden als Fehlerstatus gemeldet. Die Projektanlage oder -bearbeitung wird
dann nicht blockiert; der Dialog zeigt stattdessen ein Textfeld für die manuelle Eingabe eines relativen
Arbeitsverzeichnisses.

## Beispiele

### Beispiel 1: Cloud-Setup

```
Hosting-Modus: Cloud
Workspace: martin-stromberg
Username: martin@example.com
App Password: xxxx-xxxx-xxxx-xxxx
Jira URL: https://mycompany.atlassian.net
Jira Project: MYAPP
```

Das Plugin lädt Repositories aus `https://api.bitbucket.org/2.0/repositories/martin-stromberg` und erstellt PRs über die Cloud-API.

### Beispiel 2: Self-Hosted-Setup

```
Hosting-Modus: Self-Hosted
BitBucket URL: https://bitbucket.company.com
Workspace/Project: MYPROJ
Username: developer@company.com
App Password: xxxx-xxxx-xxxx-xxxx
```

Das Plugin lädt Repositories aus `https://bitbucket.company.com/rest/api/1.0/projects/MYPROJ/repos` und erstellt PRs über die Self-Hosted-API.

### Beispiel 3: Self-Hosted mit Port

```
BitBucket URL: https://bitbucket.company.com:7990
```

Ports werden in der BitBucket-URL unterstützt.

## Einschränkungen

1. **Repository-Discovery**: Das Plugin listet nur Repositories im konfigurierten Workspace (Cloud) oder Projekt (Self-Hosted) auf. Repositories aus anderen Workspaces sind nicht sichtbar.

2. **Jira-Integration**: Jira wird nur bei aktivierter Jira-Konfiguration abgefragt. Falls die Verbindung fehlschlägt, wird der Health-Check gemeldet, aber die BitBucket-Funktionen funktionieren weiterhin.

3. **Self-Hosted-API-Kompatibilität**: Das Plugin unterstützt BitBucket Server/Data Center mit REST API 1.0. Ältere Versionen mit API 0.x werden nicht unterstützt.

4. **URL-Format**: Die Self-Hosted-URL muss die Basis-URL ohne Pfad sein (z.B. `https://bitbucket.example.com`, nicht `https://bitbucket.example.com/bitbucket`).

5. **Git-Clone-URLs**: Das Plugin konstruiert Clone-URLs aus der konfigurierten Basis-URL. Falls Git SSH-Keys statt HTTP-Auth verwendet werden, muss der SSH-Agent entsprechend konfiguriert sein.

## Vergleich mit GitHub-Plugin

| Feature | BitBucket Cloud | BitBucket Self-Hosted | GitHub |
|---------|-----------------|----------------------|--------|
| **Hosting-Modi** | 1 (Cloud) | 1 (Self-Hosted) | 1 (GitHub.com) |
| **Jira-Integration** | Ja | Ja | Nein |
| **Authentifizierung** | App Password | App Password | Token (GitHub CLI) |
| **API-Version** | 2.0 | 1.0 | v3 / GraphQL |
| **Repository-Namespace** | Workspace | Project Key | User/Org |
| **Arbeitsverzeichnis-Auswahl** | Remote-Struktur über Source-API | Remote-Struktur über Browse-API | Remote-Struktur über Git-Trees-API |

Das GitHub-Plugin verwendet die GitHub CLI (`gh`), während das BitBucket-Plugin `curl` für API-Aufrufe und `git` für Repository-Operationen nutzt.
