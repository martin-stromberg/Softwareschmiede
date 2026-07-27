# GitHub- und Bitbucket-Plugins

## GitHub-Plugin

**Datei:** `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`

Das GitHub-Plugin nutzt `gh` und `git`. Authentifizierung laeuft ueber `GH_TOKEN` aus dem Credential Store.

### Issues lesen

`GetIssuesAsync()` normalisiert Repository-IDs und ruft:

```text
gh issue list --repo <owner/repo> --json number,title,body,labels,milestone --limit 100
```

Danach parst `ParseIssues()` die JSON-Antwort in `Issue`. Der Parser erwartet normale Issue-Felder und ist nicht fuer Alert-JSON geeignet.

Wichtig: Der `--json`-Parameter enthaelt aktuell nicht `url`, obwohl `ParseIssues()` optional `url` ausliest. Bestehende Tests enthalten `url`, der reale CLI-Aufruf vermutlich nicht. Das ist fuer die Alert-Anforderung kein Blocker, aber ein vorhandener Randbefund.

### Issues anlegen

`CreateIssueAsync()` ruft:

```text
gh issue create --repo <owner/repo> --title <title> --body <body>
```

Die Methode validiert Repository-ID und Titel, sanitizt Fehlerausgaben und parst die Issue-Nummer aus der Rueckgabe-URL. Sie liefert ein `IssueCreateResult.Success(Issue)` zurueck. Das ist der naheliegende technische Weg fuer die automatische Issue-Anlage aus einem Alert.

### Repository-Struktur

Das Plugin nutzt bereits `gh api`, z. B. fuer Issue-Templates und Git-Trees. Eine Alert-Implementierung kann daher konsistent ueber `gh api` erfolgen.

Naheliegender initialer Endpunkt fuer Code Scanning:

```text
gh api repos/<owner>/<repo>/code-scanning/alerts
```

Die benoetigten Token-Rechte muessen in der Planung explizit betrachtet werden. Die aktuelle GitHub-Plugin-Beschreibung nennt `repo` und `read:org`; fuer Code-Scanning-Alerts koennen zusaetzliche Security-Event-Rechte relevant sein.

## Bitbucket-Plugin

**Datei:** `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

Bitbucket liefert in `GetIssuesAsync()` Jira-Issues, sofern Jira konfiguriert ist. Die Implementierung baut eine Jira-JQL-Abfrage und mappt die Antwort auf `Issue`.

Die Anforderung grenzt Alert-Unterstuetzung fuer Jira/Bitbucket aus. Deshalb sollte Bitbucket unveraendert bleiben oder lediglich Default-Alert-Methoden aus einem erweiterten Contract erben, die leere Ergebnisse liefern.

## Plugin-Konsequenz

Eine Contract-Erweiterung darf bestehende Plugins nicht brechen. Geeignete Muster:

- neues optionales Interface, z. B. `IScmAlertProvider`
- Default-Methode auf `IGitPlugin`, z. B. `GetAlertsAsync()` mit leerer Liste
- gemeinsamer neuer Anforderungstyp plus Default-Implementierung, die `GetIssuesAsync()` in normale Anforderungen mappt

Wegen der fachlichen Regel "GitHub-Alerts werden als eigene Art von SCM-Anforderung behandelt" ist ein eigener Alert-Typ klarer als eine Erweiterung von `Issue`.

