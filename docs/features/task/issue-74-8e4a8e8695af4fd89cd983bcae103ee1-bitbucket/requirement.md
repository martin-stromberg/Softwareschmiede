# Anforderung 74: BitBucket Plugin Integration und Konfiguration

## Fachliche Zusammenfassung

Das neu erstellte `BitbucketPlugin` wird vollständig in das Projekt integriert. Dabei werden drei Hauptprobleme adressiert:

1. **Settings-Darstellung**: Das Plugin definiert zwei `PluginSettingGroup`-Objekte („Authentifizierung" und „Jira"), aber in der Settings-View wird nur die erste Gruppe angezeigt. Dies muss korrigiert werden, damit beide Gruppen in der UI sichtbar sind.

2. **BitBucket-Hosting-Option**: Aktuell ist hardcodiert, dass BitBucket vom Anbieter gehostet wird (api.bitbucket.org). Es muss konfigurierbar sein, ob BitBucket Cloud (Anbieter) oder Self-Hosted (eigene URL) verwendet wird. Bei Self-Hosted muss eine abweichende URL für die API eingegeben werden.

3. **Projekt-Integration**: Das Plugin muss vollständig in das Projekt integriert werden (Registrierung, Tests, Dokumentation, Abhängigkeiten).

## Betroffene Klassen und Komponenten

### Datenmodellklassen / Value Objects
- **`BitbucketPlugin`** (bestehend, erweitern):
  - Neue `PluginSettingGroup`: „BitBucket-Hosting" mit Auswahl (Cloud/Self-Hosted) und URL-Feld.
  - Neue konstante Credential-Keys für Self-Hosted URL.
  - Anpassung der API-URLs in `GetAvailableRepositoriesAsync`, `CreatePullRequestAsync`, `GetIssuesAsync` – sollen je nach Hosting-Modus unterschiedliche Base-URLs verwenden.

### UI-Komponenten
- **Settings-View** (bestehend, fix):
  - `PluginSettingGroup`-Rendering muss alle Gruppen (nicht nur erste) anzeigen.
  - Bei mehreren Gruppen sollte jede in einer eigenen Card/Section mit Überschrift dargestellt werden.

### Services / Interfaces
- **`ICredentialStore`** (bestehend):
  - Wird für neue Credential-Keys genutzt (BitBucket-Hosting-Modus, Self-Hosted URL).

### Enums / Constants
- Ggf. neues Enum `BitbucketHostingMode` (Cloud, SelfHosted) oder String-basierte Konfiguration („cloud" vs. „self-hosted").

### Tests
- **`BitbucketPluginTests`** (neue Tests oder Erweiterung):
  - Test für Cloud-Modus: API-URLs verwenden `api.bitbucket.org`.
  - Test für Self-Hosted-Modus: API-URLs verwenden konfigurierte URL.
  - Test: `GetSettingGroups()` gibt exakt 3 Gruppen zurück (Authentifizierung, Jira, BitBucket-Hosting).
  - Test: Beide Setting-Gruppen sind über View korrekt dargestellt.

### Dokumentation
- Feature-Dokumentation für BitBucket Plugin (welche Features unterstützt, Unterschiede zu GitHub).
- Setup-Anleitung für BitBucket Cloud und Self-Hosted.
- README-Update mit BitBucket als neue SCM-Alternative.

## Implementierungsansatz

### Schritt 1: Settings-View-Fix
- Prüfen, wie `PluginSettingGroup`-Sammlung in der View gebunden wird.
- Falls nur `[0]` angesteuert wird, Binding auf vollständige Liste korrigieren.
- Eventuell Loop/ItemsControl hinzufügen, um alle Gruppen zu rendern.

### Schritt 2: BitBucket-Hosting-Konfiguration
- Neue `PluginSettingGroup` „BitBucket-Hosting" mit zwei Feldern hinzufügen:
  - Dropdown/Radio: „Hosting-Modus" (Cloud oder Self-Hosted).
  - Text-Feld: „BitBucket URL" (nur sichtbar/erforderlich wenn Self-Hosted).
  - Beispiel-URLs in Placeholders.

- Im Plugin-Code:
  - Credential-Keys definieren: `BitbucketHostingModeKey`, `BitbucketSelfHostedUrlKey`.
  - In Methoden, die API aufrufen (`GetAvailableRepositoriesAsync`, `CreatePullRequestAsync`, etc.):
    - Neue Hilfsmethode `GetBitbucketApiBaseUrl()` – prüft Hosting-Modus, gibt korrekte URL zurück.
    - API-URLs dynamisch konstruieren statt hardcodiert.

### Schritt 3: Methoden-Anpassung
- **`GetAvailableRepositoriesAsync`**:
  - Aktuell: `$"https://api.bitbucket.org/2.0/repositories/{workspace}?pagelen=100"`
  - Neu: `$"{GetBitbucketApiBaseUrl()}/2.0/repositories/{workspace}?pagelen=100"`

- **`CreatePullRequestAsync`**:
  - Aktuell: `$"https://api.bitbucket.org/2.0/repositories/{repositoryId}/pullrequests"`
  - Neu: `$"{GetBitbucketApiBaseUrl()}/2.0/repositories/{repositoryId}/pullrequests"`

- **`CheckHealthAsync`**:
  - Aktuell: `"https://api.bitbucket.org/2.0/user"`
  - Neu: `$"{GetBitbucketApiBaseUrl()}/2.0/user"`

- **`GetIssuesAsync`**:
  - Prüfen, ob Jira-Integration bereits vorhanden (ja, hat JiraUrl als Credential).
  - Keine Änderung erforderlich (nutzt Jira URL, nicht BitBucket URL).

### Schritt 4: Plugin-Registrierung
- BitBucket-Plugin muss in der Abhängigkeitsinjektion registriert sein (DI Container).
- Verifizieren: `services.AddScoped<IPlugin, BitbucketPlugin>()` oder ähnlich.
- Falls nicht vorhanden: Registrierung hinzufügen.

### Schritt 5: Projekt-Integration prüfen
- BitBucket-Plugin in `Softwareschmiede.slnx` referenziert (`.csproj` Import)?
- Build-Ausgabe: `.dll` wird in Output-Verzeichnis kopiert?
- Plugin-Discovery: `.dll` wird beim Starten geladen?
- Settings-UI: BitBucket-Plugin erscheint in Anwendungseinstellungen?

### Abhängigkeiten
- `BitbucketPlugin` abhängig von: `ICliRunner`, `ICredentialStore`, `ILogger<BitbucketPlugin>` (bereits vorhanden).
- Settings-View abhängig von: `PluginSettingGroup`, `PluginSettingField` (bereits vorhanden).
- DI-Container: Registrierung im Startup/Konfiguration.

## Konfiguration

### Benutzer-Konfiguration (Settings)
1. **Authentifizierung** (Gruppe 1):
   - BitBucket Username: Text-Feld.
   - App Password (Token): Secret-Feld.
   - Workspace: Text-Feld.

2. **Jira-Integration** (Gruppe 2):
   - Jira Base URL.
   - Jira Project Key.
   - Jira Login E-Mail.
   - Jira API Token.

3. **BitBucket-Hosting** (Gruppe 3, neu):
   - Hosting-Modus: Dropdown mit Optionen „Cloud" (Standard), „Self-Hosted".
   - BitBucket URL (Self-Hosted): Text-Feld für `https://bitbucket.example.com` (nur sichtbar wenn Self-Hosted).
   - Optional: Verifizierungs-Button „Verbindung testen".

### Code-Konfiguration
- Credential-Keys:
  ```csharp
  private const string BitbucketHostingModeKey = "Softwareschmiede.Bitbucket.HostingMode"; // "cloud" oder "self-hosted"
  private const string BitbucketSelfHostedUrlKey = "Softwareschmiede.Bitbucket.SelfHostedUrl"; // z.B. "https://bitbucket.example.com"
  ```

## Offene Fragen

1. **Settings-View Implementierung**: Wo ist die View, die `PluginSettingGroup`-Listen rendert? Welches Framework (WPF, Blazor, andere)?

2. **Self-Hosted BitBucket – Authentifizierung**: Für Self-Hosted wird weiterhin Username + App Password verwendet? Oder unterscheidet sich die Authentifizierungsmethode?

3. **Jira-Integration in BitBucket-Plugin**: Warum ist Jira im BitBucket-Plugin konfigurierbar? Ist dies Standard, oder sollte Jira ein separates Plugin sein?

4. **API-Versionen**: Gelten die gleichen BitBucket-API-Versionen (2.0) auch für Self-Hosted-Installationen, oder können diese abweichen?

5. **Fallback-Verhalten**: Falls die Self-Hosted-URL nicht erreichbar ist, soll auf Cloud fallback erfolgen oder Fehler anzeigen?

6. **Repository-Link-Felder**: Die `GetRepositoryLinkFields()` zeigen Placeholders für `bitbucket.org`. Müssen diese auch für Self-Hosted-Instanzen angepasst werden?

7. **Health-Check**: Der Health-Check prüft Bitbucket + Jira. Soll dieser auch die Erreichbarkeit der BitBucket-URL (Self-Hosted) prüfen?

8. **Git-Operationen (clone, pull, push)**: Nutzen diese ebenfalls die BitBucket-URL? Falls ja, müssen auch `GetGitEnvironment()` und `BuildAuthenticatedCloneUrl()` angepasst werden.
