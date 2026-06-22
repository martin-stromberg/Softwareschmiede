# Bestandsaufnahme: BitBucket Plugin Integration und Konfiguration

Der bestehende `BitbucketPlugin` ist als SCM-Provider-Plugin implementiert und wird von `PluginManager` dynamisch geladen. Diese Bestandsaufnahme analysiert den aktuellen Code-Status bezogen auf die Anforderung zur vollständigen Integration, Settings-View-Korrektur und Self-Hosted-Konfiguration.

## Zusammenfassung

### Vorhanden
- **BitbucketPlugin-Klasse:** Vollständig implementiert mit allen Basis-Operationen (Clone, Push, Pull, PR-Erstellung, Issue-Abfrage)
- **2 Einstellungsgruppen:** "Authentifizierung" und "Jira" sind definiert
- **Settings-Binding in UI:** SettingsViewModel lädt Plugin-Einstellungen und bindet sie bidirektional an XAML-View
- **Plugin-Discovery:** PluginManager lädt BitbucketPlugin automatisch via Assembly Loading
- **Credential-Store-Integration:** ICredentialStore wird für Speicherung sensibler Daten verwendet
- **API-URL Hardcodierung:** Methoden nutzen aktuell `https://api.bitbucket.org` (Bitbucket Cloud Standard)

### Lücken / Nicht Vorhanden
- **3. Einstellungsgruppe "BitBucket-Hosting":** Nicht implementiert – neue Gruppe mit Hosting-Modus und URL-Feld fehlt
- **Self-Hosted-Unterstützung:** Keine Mechanik zur Umschaltung zwischen Cloud und Self-Hosted
- **Hilfsmethode GetBitbucketApiBaseUrl():** Nicht vorhanden – wird benötigt um API-URL dynamisch zu konstruieren
- **Credential-Keys für Hosting:** `BitbucketHostingModeKey` und `BitbucketSelfHostedUrlKey` nicht definiert
- **View-Rendering aller Gruppen:** View scheint alle Gruppen zu unterstützen (ItemsControl mit ItemsSource), aber nur 2 Gruppen existieren
- **Unit-Tests für BitbucketPlugin:** Keine spezifischen Tests vorhanden
- **Integration Tests für Settings-View:** Keine E2E-Tests für Settings-Rendering

## Details

- [Datenmodell](inventory/models.md)
- [Logik und Services](inventory/logic.md)
- [Interfaces und Contracts](inventory/interfaces.md)
- [Enums und Konstanten](inventory/enums.md)
- [Tests und Hilfsmethoden](inventory/tests.md)

## Architektur-Übersicht

### Plugin-Architektur
```
BitbucketPlugin (extends GitPluginBase<BitbucketPlugin>)
    ├── implements IGitPlugin
    ├── implements IPlugin
    └── uses: ICliRunner, ICredentialStore, ILogger
```

### Settings-Flow
```
SettingsView (XAML)
    ↓ DataContext: SettingsViewModel
    ├── LoadScmPluginSettings(plugin) → LadePluginEinstellungen()
    ├── konvertiert PluginSettingGroup → PluginSettingGroupEntry
    ├── ItemsControl bindet an SelectedScmPluginSettings
    └── SpeichernCommand → SpeicherePluginEinstellungen() → PluginSettingsService
```

### Plugin-Discovery (Startup)
```
App.xaml.cs: StartupAsync()
    ↓ ConfigureServices()
    ├── services.AddSingleton<PluginManager>()
    └── PluginManager: DiscoverPlugins()
        ├── Directory.GetFiles("plugins/", "*.dll")
        ├── AssemblyLoadContext.Default.LoadFromAssemblyPath()
        ├── reflection: typeof(IGitPlugin).IsAssignableFrom()
        └── BitbucketPlugin wird geladen + registriert
```

## Abhängigkeits-Graph (Relevant für Anforderung)

```
BitbucketPlugin
    ├── PluginSettingGroup (neues Feld nötig)
    │   └── PluginSettingField
    │       └── PluginSettingFieldType (Enum)
    │
    ├── ICredentialStore (neue Keys für Hosting-Modus)
    │
    ├── ICliRunner (für curl/git Aufrufe)
    │
    └── Methoden mit hardcodierter URL (müssen angepasst werden)
        ├── GetAvailableRepositoriesAsync() – nutzt /2.0/repositories/
        ├── CreatePullRequestAsync() – nutzt /2.0/repositories/{id}/pullrequests
        ├── CheckHealthAsync() – nutzt /2.0/user
        └── GetIssuesAsync() – nutzt Jira URL (nicht Bitbucket, keine Änderung nötig)

SettingsView (XAML)
    └── SettingsViewModel
        ├── SelectedScmPluginSettings (IReadOnlyList<PluginSettingGroupEntry>)
        └── ItemsControl mit DataTemplate (iteriert korrekt über alle Gruppen)

PluginManager
    └── DiscoverPlugins() lädt BitbucketPlugin automatisch
```

## Offene Punkte aus Anforderung

Diese werden durch die Bestandsaufnahme **nicht** beantwortet (benötigen Implementierung):

1. Settings-View-Rendering: Zeigt bereits alle Gruppen (kein Bug erkannt, nur 2 Gruppen existieren)
2. Self-Hosted-Modus: Neue Logik in BitbucketPlugin erforderlich
3. API-URL-Konfiguration: Neue Hilfsmethode `GetBitbucketApiBaseUrl()` erforderlich
4. Tests: Komplett neu zu schreiben
5. Projekt-Integration: Registrierung via PluginManager bereits vorhanden, `Softwareschmiede.slnx` muss BitBucket-Plugin-Projekt referenzieren

## Nächste Schritte (Für Implementierung)

1. Neue Credential-Keys in `BitbucketPlugin` definieren
2. Neue `PluginSettingGroup` "BitBucket-Hosting" zu `GetSettingGroups()` hinzufügen
3. Hilfsmethode `GetBitbucketApiBaseUrl()` implementieren
4. API-Aufrufe in Methoden auf dynamische URL umstellen
5. Unit-Tests schreiben (Cloud und Self-Hosted)
6. View-Rendering testen
7. `Softwareschmiede.slnx` aktualisieren falls nötig
