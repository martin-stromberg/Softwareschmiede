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
| `CodexPlugin` | KI | Codex CLI – nutzt `codex` CLI aus der Anwendung heraus |
| `KiSimulatorPlugin` | KI | Simulierter KI-Agent für Entwicklung und Tests |

### Einstellungen

Jedes Plugin deklariert Einstellungsgruppen (`PluginSettingGroup`) mit typisierten Feldern (`PluginSettingField`). Werte werden verschlüsselt im Windows Credential Store gespeichert unter dem Schlüssel `<PluginPrefix>.<FieldKey>`.

Das Standard-SCM-Plugin und das Standard-KI-Plugin werden in den App-Einstellungen hinterlegt und per `PluginDefaultSettingsService` verwaltet.

## Beispiele

- Für GitHub-Repositories wird `GitHubPlugin` als SCM gewählt; der GitHub-Token wird unter `Softwareschmiede.GitHub.Token` gespeichert.
- Für KI-Läufe mit Claude wird `ClaudeCliPlugin` gewählt; der Anthropic API Key wird optional als `ANTHROPIC_API_KEY`-Umgebungsvariable übergeben.
- Für KI-Läufe mit Codex wird `CodexPlugin` gewählt; optional kann ein absoluter Pfad zur `codex`-Executable unter `Softwareschmiede.Codex.ExecutablePath` gespeichert werden. Zusätzliche Codex-Argumente werden unter `Softwareschmiede.Codex.CommandLineParameters` gespeichert und nur verwendet, wenn sie vom Anwender gesetzt wurden. Automatische Defaults werden für diesen Wert nicht übernommen.

### Aktivierung und Deaktivierung

Jedes Plugin kann in den Einstellungen (Tab „Plugins") einzeln aktiviert oder deaktiviert werden. Der Aktivierungsstatus wird persistiert und bleibt nach Anwendungsneust bestehen. Neue oder erstmals entdeckte Plugins sind standardmäßig aktiviert. Deaktivierte Plugins:

- Erscheinen nicht in der Plugin-Auswahl von Projekten und Aufgaben
- Werden nicht als Standard-Plugin vorgeschlagen
- Sind nicht in der KI-Plugin-Auswahl von neuen Aufgaben verfügbar

Wichtig: Es muss stets mindestens ein Plugin je Kategorie (SCM oder KI) aktiv bleiben. Das Deaktivieren des letzten aktiven Plugins einer Kategorie wird durch Validierung verhindert.

**Single-Plugin-Verhalten:** Wenn genau ein Plugin einer Kategorie aktiv ist, wird es automatisch ohne Auswahldialog verwendet. Der Plugin-Selector wird in diesem Fall ausgeblendet.

## Einschränkungen

- Plugins liegen als fest referenzierte Klassenbibliotheken vor; dynamisches Laden zur Laufzeit ist nicht implementiert.
- Pro Aufgabe kann immer nur ein KI-Plugin und ein SCM-Plugin gleichzeitig aktiv sein.
- Der globale Aktivierungsstatus ist nicht pro Projekt konfigurierbar; er gilt für die gesamte Anwendung.
