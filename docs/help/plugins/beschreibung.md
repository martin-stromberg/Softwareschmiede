# Plugin-System — Beschreibung

## Zweck

Das Plugin-System trennt die Kernlogik der Softwareschmiede von den konkreten Integrationen. So kann die Anwendung unterschiedliche Git-Provider und KI-Tools nutzen, ohne dass die Kernanwendung geändert werden muss.

## Funktionsweise

Jedes Plugin implementiert `IPlugin` und gehört einem von zwei Typen an:

| Typ | Interface | Aufgabe |
|-----|-----------|---------|
| `SourceCodeManagement` | `IGitPlugin` | Klonen, Branchen, Committen, Pushen, Pull Requests |
| `DevelopmentAutomation` | `IKiPlugin` | KI starten, Agentenpaket deployen, Tests ausführen |

### Verfügbare Plugins

| Plugin | Typ | Beschreibung |
|--------|-----|--------------|
| `GitHubPlugin` | SCM | GitHub-Integration über GitHub CLI (`gh`) |
| `BitbucketPlugin` | SCM | BitBucket Cloud und Self-Hosted (Server/Data Center); Jira-Integration |
| `LocalDirectoryPlugin` | SCM | Arbeitet direkt auf einem lokalen Verzeichnis ohne Remote |
| `ClaudeCliPlugin` | KI | Claude Code CLI – nutzt `claude` CLI mit Stream-JSON-Output |
| `GitHubCopilotPlugin` | KI | GitHub Copilot CLI – nutzt `copilot` CLI |
| `KiSimulatorPlugin` | KI | Simulierter KI-Agent für Entwicklung und Tests |

### Einstellungen

Jedes Plugin deklariert Einstellungsgruppen (`PluginSettingGroup`) mit typisierten Feldern (`PluginSettingField`). Werte werden verschlüsselt im Windows Credential Store gespeichert unter dem Schlüssel `<PluginPrefix>.<FieldKey>`.

Das Standard-SCM-Plugin und das Standard-KI-Plugin werden in den App-Einstellungen hinterlegt und per `PluginDefaultSettingsService` verwaltet.

## Beispiele

- Für GitHub-Repositories wird `GitHubPlugin` als SCM gewählt; der GitHub-Token wird unter `Softwareschmiede.GitHub.Token` gespeichert.
- Für KI-Läufe mit Claude wird `ClaudeCliPlugin` gewählt; der Anthropic API Key wird optional als `ANTHROPIC_API_KEY`-Umgebungsvariable übergeben.

## Einschränkungen

- Plugins liegen als fest referenzierte Klassenbibliotheken vor; dynamisches Laden zur Laufzeit ist nicht implementiert.
- Pro Aufgabe kann immer nur ein KI-Plugin und ein SCM-Plugin gleichzeitig aktiv sein.
