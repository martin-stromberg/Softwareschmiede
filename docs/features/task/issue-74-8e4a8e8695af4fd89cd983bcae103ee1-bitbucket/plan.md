# Umsetzungsplan: BitBucket Plugin Integration und Konfiguration (Issue #74)

## 1. Zusammenfassung

Dieses Vorhaben erstellt eine vollständige Integration des BitBucket-Plugins in das Softwareschmiede-Projekt mit folgenden Hauptzielen:

1. **Self-Hosted BitBucket-Unterstützung**: Implementierung einer neuen `PluginSettingGroup` "BitBucket-Hosting" mit umschaltbarer Konfiguration (Cloud vs. Self-Hosted). Bei Self-Hosted-Mode kann eine benutzerdefinierte URL eingegeben werden.

2. **Dynamische API-URL-Konstruktion**: Erstellung einer Hilfsmethode `GetBitbucketApiBaseUrl()`, die je nach Hosting-Modus (Cloud oder Self-Hosted) die korrekte API-Basis-URL zurückgibt. Alle API-Aufrufe werden von hardcodierten URLs auf diese dynamische Methode umgestellt.

3. **Credential-Storage für neue Einstellungen**: Definition neuer Credential-Keys für Hosting-Modus und Self-Hosted-URL.

4. **Umfassende Testabdeckung**: Schreibung von Unit-Tests für beide Hosting-Modi und Verifizierung der Settings-View-Rendering.

5. **Projekt-Integration**: Sicherstellung, dass BitBucket-Plugin vollständig in die Lösung integriert ist (Registrierung, Abhängigkeiten, Plugin-Discovery).

## 2. Betroffene Dateien

### Zu ändernde Dateien

| Datei | Änderung | Begründung |
|-------|----------|-----------|
| `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs` | Hauptimplementierung | Neue Credential-Keys, neue PluginSettingGroup, Hilfsmethode GetBitbucketApiBaseUrl(), Anpassung aller API-Aufrufe, Self-Hosted-URL-Unterstützung für Git-Operationen |
| `plugins/Softwareschmiede.Plugin.BitBucket/Softwareschmiede.Plugin.BitBucket.csproj` | Abhängigkeiten prüfen | Ggf. Test-Framework hinzufügen (xUnit/Moq) |
| `Softwareschmiede.slnx` | Projekt-Referenzen | BitBucket-Plugin-Projekt muss referenziert sein (falls nicht bereits geschehen) |
| `src/Softwareschmiede.App/Softwareschmiede.App.csproj` | Abhängigkeiten | BitBucket-Plugin als Build-Abhängigkeit hinzufügen |

### Neue Dateien

| Datei | Inhalt | Begründung |
|-------|--------|-----------|
| `plugins/Softwareschmiede.Plugin.BitBucket/Tests/BitbucketPluginTests.cs` | Unit-Tests | Tests für Cloud- und Self-Hosted-Modi, Settings-Group-Tests, Git-Operationen-Tests |
| `docs/features/bitbucket-plugin/README.md` | Feature-Dokumentation | Setup-Anleitung, Unterschiede Cloud vs. Self-Hosted, Troubleshooting |

### Zu überprüfende Dateien (kein direkter Change nötig)

| Datei | Grund |
|-------|-------|
| `src/Softwareschmiede.App/Views/SettingsView.xaml` | Verifizierung: Bindet alle PluginSettingGroups korrekt (bereits implementiert) |
| `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` | Verifizierung: Konvertiert alle Gruppen korrekt |
| `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs` | Verifizierung: Plugin wird geladen und registriert |

## 3. Architektur-Entscheidungen (basierend auf Antworten)

### 3.1 Hosting-Modus Enum

Verwende typsicheres Enum `BitbucketHostingMode` mit Werten:
- `Cloud` (Standard)
- `SelfHosted`

Intern wird dieses Enum für ICredentialStore als String serialisiert:
```csharp
private enum BitbucketHostingMode
{
    Cloud,
    SelfHosted
}
```

Serialisierung: `BitbucketHostingMode.Cloud.ToString()` → `"Cloud"` oder `"cloud"` (konsistent halten).

### 3.2 API-Pfad-Struktur (Cloud vs. Self-Hosted)

**BitBucket Cloud** (api.bitbucket.org):
- Basis-URL: `https://api.bitbucket.org`
- Repositories-Pfad: `/2.0/repositories/{workspace}/{repo}`
- Beispiel: `https://api.bitbucket.org/2.0/repositories/martin-stromberg/myrepo`

**BitBucket Self-Hosted** (Server / Data Center):
- Basis-URL: `https://your-bitbucket.example.com`
- Repositories-Pfad: `/rest/api/1.0/projects/{projectKey}/repos/{repositorySlug}`
- Beispiel: `https://your-bitbucket.example.com/rest/api/1.0/projects/MY/repos/myrepo`

**Implementierung**: Die Methode `GetBitbucketApiBaseUrl()` gibt nur die Basis-URL zurück. Die Pfad-Struktur wird in den aufrufenden Methoden berücksichtigt (nicht in der Hilfsmethode).

### 3.3 Self-Hosted URL-Format

Self-Hosted URL wird als vollständige Basis-URL gespeichert (z.B. `https://bitbucket.example.com` oder `https://bitbucket.example.com:7990`). Ports und Protokolle werden unterstützt.

### 3.4 Bedingte Sichtbarkeit

Das Feld "SelfHostedUrl" ist **immer sichtbar** in der UI (kein bedingter Mechanismus). Dies vereinfacht die Implementierung und die UX:
- Bei Cloud-Modus: Feld ist sichtbar, aber nicht relevant (Benutzer lässt es leer oder kopiert alte URL).
- Bei Self-Hosted-Modus: Feld ist sichtbar und Benutzer gibt URL ein.
- Validierung erfolgt beim Speichern oder Health-Check (nicht in der View).

### 3.5 Fallback-Verhalten

**Kein Fallback auf Cloud**. Falls die konfigurierte Self-Hosted-URL nicht erreichbar ist oder ungültig konfiguriert ist, wird ein Fehler angezeigt. Dies verhindert unerwartete Verhaltenswechsel und macht die Konfiguration transparent für Benutzer.

### 3.6 Health-Check für Self-Hosted

Der Health-Check wird erweitert, um auch die Self-Hosted-URL-Erreichbarkeit zu prüfen:
- Cloud-Modus: Prüfe `https://api.bitbucket.org/2.0/user` (wie bisher)
- Self-Hosted-Modus: Prüfe `{SelfHostedUrl}/rest/api/1.0/user` (mit Authentifizierung)

### 3.7 Git-Operationen bei Self-Hosted

Die Methoden `GetGitEnvironment()` und `BuildAuthenticatedCloneUrl()` müssen beide Cloud- und Self-Hosted-URLs unterstützen:

1. **GetGitEnvironment()**: Die `.netrc`-Datei muss den korrekten Host enthalten:
   - Cloud: `machine bitbucket.org`
   - Self-Hosted: `machine bitbucket.example.com` (aus Self-Hosted-URL extrahiert)

2. **BuildAuthenticatedCloneUrl()**: Funktioniert bereits Host-agnostisch (verwendet URI-Builder), aber muss mit unterschiedlichen Hosts umgehen können.

3. **RepositoryUrl-Handling**: Repository-URLs müssen in beiden Modi funktionieren:
   - Cloud: `https://bitbucket.org/workspace/repo` oder `git@bitbucket.org:workspace/repo`
   - Self-Hosted: `https://bitbucket.example.com/scm/project/repo` oder `git@bitbucket.example.com:project/repo`

## 4. Umsetzungsschritte

### Phase 1: Enum und Credential-Keys (BitBucketPlugin.cs)

**Schritt 1.1**: Neue Enum `BitbucketHostingMode` definieren
```csharp
private enum BitbucketHostingMode
{
    Cloud,
    SelfHosted
}
```

**Schritt 1.2**: Neue Konstanten für Hosting-Modus und Self-Hosted-URL definieren
- `BitbucketHostingModeKey = "Softwareschmiede.Bitbucket.HostingMode"` (Wert: "Cloud" oder "SelfHosted")
- `BitbucketSelfHostedUrlKey = "Softwareschmiede.Bitbucket.SelfHostedUrl"` (Wert: z.B. "https://bitbucket.example.com")

### Phase 2: Neue PluginSettingGroup "BitBucket-Hosting" (BitBucketPlugin.cs)

**Schritt 2.1**: Neue `PluginSettingGroup` zu `GetSettingGroups()` hinzufügen (nach der Jira-Gruppe)

Gruppe "BitBucket-Hosting" mit zwei Feldern:
1. **HostingMode** (Enum mit Dropdown):
   - Key: "HostingMode"
   - Label: "Hosting-Modus"
   - FieldType: `PluginSettingFieldType.Enum`
   - EnumOptions: `["Cloud", "SelfHosted"]`
   - Placeholder: "Cloud"
   - Description: "Cloud nutzt api.bitbucket.org, Self-Hosted eine eigene URL."
   - IsRequired: true
   - Default: "Cloud" (via Fallback in GetBitbucketApiBaseUrl())

2. **SelfHostedUrl** (Text-Feld):
   - Key: "SelfHostedUrl"
   - Label: "BitBucket URL (Self-Hosted)"
   - FieldType: `PluginSettingFieldType.Url`
   - Placeholder: "https://bitbucket.example.com"
   - Description: "Nur erforderlich wenn Hosting-Modus auf Self-Hosted gesetzt. Basis-URL ohne Pfad."
   - IsRequired: false (weil nur bei Self-Hosted relevant, aber View zeigt immer an)

**Ergebnis**: `GetSettingGroups()` gibt jetzt 3 Gruppen statt 2 zurück (Authentifizierung, Jira, BitBucket-Hosting).

### Phase 3: Hilfsmethode für API-URL-Konstruktion

**Schritt 3.1**: Private Hilfsmethode `GetBitbucketApiBaseUrl()` implementieren

```csharp
private string GetBitbucketApiBaseUrl()
{
    var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";
    
    if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
    {
        var selfHostedUrl = _credentialStore.GetCredential(BitbucketSelfHostedUrlKey);
        if (string.IsNullOrWhiteSpace(selfHostedUrl))
        {
            throw new InvalidOperationException("Self-Hosted URL ist nicht konfiguriert.");
        }
        return selfHostedUrl.TrimEnd('/');
    }
    
    // Default: Cloud
    return "https://api.bitbucket.org";
}
```

**Schritt 3.2**: Private Hilfsmethode `GetBitbucketApiPath()` für Pfad-Struktur

Da die API-Pfade zwischen Cloud und Self-Hosted unterschiedlich sind, wird eine zusätzliche Hilfsmethode benötigt, die den Pfad für verschiedene Ressourcen zurückgibt:

```csharp
private string GetBitbucketRepositoriesPath(string workspace)
{
    var hostingMode = _credentialStore.GetCredential(BitbucketHostingModeKey) ?? "Cloud";
    
    if (hostingMode.Equals("SelfHosted", StringComparison.OrdinalIgnoreCase))
    {
        // Self-Hosted: /rest/api/1.0/projects/{projectKey}/repos/{repositorySlug}
        // workspace wird als projectKey interpretiert
        return $"/rest/api/1.0/projects/{workspace}/repos";
    }
    
    // Cloud: /2.0/repositories/{workspace}
    return $"/2.0/repositories/{workspace}";
}
```

### Phase 4: API-Aufrufe auf dynamische URLs umstellen

**Schritt 4.1**: Methode `GetAvailableRepositoriesAsync()` anpassen

- Cloud (alt): `$"https://api.bitbucket.org/2.0/repositories/{workspace}?pagelen=100"`
- Cloud (neu): `$"{GetBitbucketApiBaseUrl()}/2.0/repositories/{workspace}?pagelen=100"`
- Self-Hosted (neu): `$"{GetBitbucketApiBaseUrl()}/rest/api/1.0/projects/{workspace}/repos"`

**Schritt 4.2**: Methode `CreatePullRequestAsync()` anpassen

- Cloud: `$"{GetBitbucketApiBaseUrl()}/2.0/repositories/{repositoryId}/pullrequests"`
- Self-Hosted: `$"{GetBitbucketApiBaseUrl()}/rest/api/1.0/projects/{projectKey}/repos/{repositorySlug}/pull-requests"`

**Schritt 4.3**: Methode `CheckHealthAsync()` anpassen

- Cloud: `$"{GetBitbucketApiBaseUrl()}/2.0/user"`
- Self-Hosted: `$"{GetBitbucketApiBaseUrl()}/rest/api/1.0/user"`

**Schritt 4.4**: Methode `GetGitEnvironment()` anpassen (Self-Hosted-URL-Support)

Modifiziere die `.netrc`-Datei-Konstruktion, um den korrekten Host zu extrahieren:

```csharp
private IDictionary<string, string> GetGitEnvironment()
{
    var user = _credentialStore.GetCredential(BitbucketUserKey);
    var appPassword = _credentialStore.GetCredential(BitbucketAppPasswordKey);

    var env = new Dictionary<string, string>
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_SSH_COMMAND"] = "ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null"
    };

    if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(appPassword))
    {
        var netrcPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            OperatingSystem.IsWindows() ? "_netrc" : ".netrc");

        // Extrahiere Host aus API-Base-URL
        var baseUrl = GetBitbucketApiBaseUrl();
        var uri = new Uri(baseUrl);
        var host = uri.Host;

        var netrcContent = $@"machine {host}
login {user}
password {appPassword}
";

        File.WriteAllText(netrcPath, netrcContent);
    }

    return env;
}
```

**Schritt 4.5**: Methode `GetIssuesAsync()` — keine Änderung erforderlich

Diese nutzt Jira URL (nicht BitBucket), daher keine Anpassung für Self-Hosted BitBucket nötig.

**Schritt 4.6**: Placeholder in `GetRepositoryLinkFields()` aktualisieren (optional)

Dokumentation hinzufügen, dass sowohl Cloud- als auch Self-Hosted-URLs unterstützt werden.

### Phase 5: Mock-Strategie für Tests

**Schritt 5.1**: Hilfsmethode für Test-Setup dokumentieren

Für Unit-Tests wird eine Mock-Konfiguration pro Szenario verwendet:

```csharp
// Beispiel: Cloud-Modus Mock
var mockCredentialStore = new Mock<ICredentialStore>();
mockCredentialStore.Setup(c => c.GetCredential(BitbucketHostingModeKey))
    .Returns("Cloud");

// Beispiel: Self-Hosted-Modus Mock
var mockCredentialStore = new Mock<ICredentialStore>();
mockCredentialStore.Setup(c => c.GetCredential(BitbucketHostingModeKey))
    .Returns("SelfHosted");
mockCredentialStore.Setup(c => c.GetCredential(BitbucketSelfHostedUrlKey))
    .Returns("https://bitbucket.example.com");
```

Keine Integrationstests gegen externe Systeme. Nur lokal mockbare Tests.

### Phase 6: Projekt-Integration verifizieren

**Schritt 6.1**: Prüfen, ob `Softwareschmiede.slnx` das BitBucket-Plugin-Projekt referenziert
- Falls nicht: Projekt-Referenz hinzufügen

**Schritt 6.2**: Prüfen, ob BitBucket-Plugin als Abhängigkeit in `Softwareschmiede.App.csproj` eingebunden ist
- Falls nötig: ProjectReference hinzufügen
- Oder: Plugin wird über PluginManager dynamisch geladen (kein direkter Reference nötig, aber Build muss Projekt referenzieren)

**Schritt 6.3**: Sicherstellen, dass Plugin-DLL im Output-Verzeichnis verfügbar ist
- Build-Output: `bin/Debug/net10.0/Softwareschmiede.Plugin.BitBucket.dll`
- PluginManager sucht im `plugins/`-Verzeichnis

### Phase 7: Unit-Tests schreiben

**Schritt 7.1**: Test-Projekt oder Test-Klasse erstellen
- Datei: `plugins/Softwareschmiede.Plugin.BitBucket/Tests/BitbucketPluginTests.cs`
- Framework: xUnit + Moq

**Schritt 7.2**: Tests für Cloud-Modus
- Test: `GetBitbucketApiBaseUrl()` gibt `"https://api.bitbucket.org"` zurück wenn Hosting-Modus == "Cloud" oder nicht gesetzt
- Test: `GetAvailableRepositoriesAsync()` konstruiert Cloud-API-URL korrekt
- Test: `CreatePullRequestAsync()` nutzt Cloud-API-URL mit Pfad `/2.0/repositories/{id}/pullrequests`
- Test: `CheckHealthAsync()` prüft `https://api.bitbucket.org/2.0/user` für Cloud
- Test: `GetGitEnvironment()` setzt `.netrc` mit Host `bitbucket.org` für Cloud

**Schritt 7.3**: Tests für Self-Hosted-Modus
- Mock: `ICredentialStore` mit Hosting-Modus == "SelfHosted" und URL == "https://bitbucket.example.com"
- Test: `GetBitbucketApiBaseUrl()` gibt "https://bitbucket.example.com" zurück
- Test: `GetAvailableRepositoriesAsync()` konstruiert Self-Hosted-API-URL korrekt mit Pfad `/rest/api/1.0/projects/{workspace}/repos`
- Test: `CreatePullRequestAsync()` nutzt Self-Hosted-API-URL
- Test: `CheckHealthAsync()` prüft `https://bitbucket.example.com/rest/api/1.0/user` für Self-Hosted
- Test: `GetGitEnvironment()` setzt `.netrc` mit korrekt extrahiertem Host für Self-Hosted
- Test: Fehler bei fehlender Self-Hosted-URL wird korrekt gehandhabt

**Schritt 7.4**: Tests für Settings-Groups
- Test: `GetSettingGroups()` gibt exakt 3 Gruppen zurück
- Test: Gruppen heißen "Authentifizierung", "Jira", "BitBucket-Hosting"
- Test: 3. Gruppe "BitBucket-Hosting" enthält exakt 2 Felder (HostingMode, SelfHostedUrl)
- Test: HostingMode-Feld hat EnumOptions `["Cloud", "SelfHosted"]`
- Test: SelfHostedUrl-Feld ist optional (IsRequired = false)

**Schritt 7.5**: Edge-Case Tests
- Test: Self-Hosted-URL mit Port (z.B. `https://bitbucket.example.com:7990`)
- Test: Self-Hosted-URL mit trailing Slash wird korrekt behandelt (gekürzt)
- Test: Leere oder ungültige Self-Hosted-URL führt zu Fehler bei API-Aufruf
- Test: Repository-Links funktionieren mit beiden URL-Formaten

### Phase 8: Dokumentation

**Schritt 8.1**: Feature-Dokumentation erstellen
- Datei: `docs/features/bitbucket-plugin/README.md`
- Inhalt:
  - Überblick: BitBucket als SCM-Provider
  - Setup für Cloud: Authentifizierung, Jira-Integration
  - Setup für Self-Hosted: URL-Konfiguration, Authentifizierung (identisch)
  - Unterschiede Cloud vs. Self-Hosted: API-Pfade, Port-Unterstützung
  - Troubleshooting: Häufige Fehler, Health-Check-Bedeutung
  - Beispiel: Self-Hosted-URL mit Port

**Schritt 8.2**: Credential-Keys dokumentieren
- Ggf. Datei: `docs/configuration/credential-keys.md`
- Neue Keys eintragen:
  - `Softwareschmiede.Bitbucket.HostingMode` (Wert: "Cloud" oder "SelfHosted")
  - `Softwareschmiede.Bitbucket.SelfHostedUrl` (Wert: Basis-URL, optional bei Cloud)

**Schritt 8.3**: README aktualisieren
- Projekt-Root: `README.md`
- Hinzufügen: BitBucket als neue SCM-Alternative erwähnen

### Phase 9: Validierung und Testing

**Schritt 9.1**: Build durchführen
- Lösung bauen: `dotnet build`
- Fehlerfreiheit prüfen

**Schritt 9.2**: Unit-Tests ausführen
- `dotnet test`
- Alle Tests müssen bestehen

**Schritt 9.3**: Anwendung starten und manuell testen
- App starten
- Settings-View öffnen
- Prüfen: BitBucket-Plugin mit 3 Gruppen angezeigt (Authentifizierung, Jira, BitBucket-Hosting)
- Hosting-Modus testen: Cloud ↔ Self-Hosted umschalten
- Self-Hosted-URL eingeben und speichern
- Health-Check durchführen (bei Cloud und Self-Hosted)
- Repositories laden für Cloud und Self-Hosted
- Repository-URLs validieren

**Schritt 9.4**: Repository-Operationen testen (optional)
- Repository klonen (Cloud und Self-Hosted)
- Branch erstellen, pushen
- Pull Request erstellen

## 5. Offene Punkte

Keine — alle Punkte aus der Anforderung sind geklärt.

## 6. Jira-Integration im BitBucket-Plugin

**Architektonische Begründung**: BitBucket trennt SCM und Issue-Tracking. Die Softwareschmiede-App verbindet beides bewusst für Repository-Aufgabenlisten. Das BitBucket-Plugin hat daher sowohl BitBucket Cloud/Self-Hosted- als auch Jira-Konfiguration, weil die App Repository-Issue-Listen (aus Jira) für BitBucket-Repositories anzeigt. Dies ist eine bewusste architektonische Entscheidung.

**Zukünftige Option**: Falls BitBucket Server/Data Center native Issue-Tracking hat (Jira Integration), könnte diese Option in Zukunft ergänzt werden. Derzeit bleibt die Jira-Konfiguration im BitBucket-Plugin.

## 7. Migration bestehender Installationen

**Keine Migration erforderlich**. Bestehende Einstellungen werden nicht automatisch umgestellt:
- Neue Benutzer erhalten Default-Werte (Cloud-Modus)
- Bestehende BitBucket-Cloud-Benutzer funktionieren weiterhin, da Cloud der Default ist
- Benutzer mit Self-Hosted müssen manuell die URL konfigurieren (einmalig in Settings)

Dies ist einfach zu verstehen und vermeidet unerwartete Verhaltenswechsel.
