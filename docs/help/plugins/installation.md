# Plugin-System — Installation und Konfiguration

## Voraussetzungen

| Plugin | Voraussetzung |
|--------|---------------|
| `GitHubPlugin` | GitHub CLI (`gh`) installiert und eingeloggt |
| `LocalDirectoryPlugin` | Git installiert |
| `ClaudeCliPlugin` | Claude Code CLI (`claude`) installiert; optionaler Anthropic API Key |
| `GitHubCopilotPlugin` | GitHub Copilot CLI (`copilot`) installiert und eingeloggt |
| `KiSimulatorPlugin` | Keine — nur für Tests |

## Plugin-Verzeichnis

Jedes Plugin muss im "plugin"-Verzeichnis der Anwendung abgelegt sein. Der `PluginManager` sucht sowohl nach DLLsdirekt im Verzeichnis als auch in den Unterordnern der ersten Ebene.

Mit dem Kompilieren des Anwendungsprojekts werden die im Projekt enthaltenenen Plugins in das Buildverzeichnis kopiert, so dass sie auch ind er ENtwicklungsumgebung autmatisch verfügbar sind.

## Plugin-Einstellungen konfigurieren

Plugin-Einstellungen werden in der **Einstellungsseite** verwaltet:

1. In der Seitenleiste auf **Einstellungen** klicken.
2. Plugin in der Liste wählen.
3. Felder ausfüllen (z.B. Token, URL).
4. Auf **Speichern** klicken — Werte werden im Windows Credential Store abgelegt.

### Einstellungsfelder je Plugin

**GitHubPlugin**

| Feld | Beschreibung |
|------|--------------|
| Token | GitHub Personal Access Token (PAT) mit `repo`-Scope |

**ClaudeCliPlugin**

| Feld | Beschreibung |
|------|--------------|
| Anthropic API Key | Wird als `ANTHROPIC_API_KEY`-Umgebungsvariable übergeben. Optional, wenn Claude CLI bereits über OAuth angemeldet ist. |

## Standard-Plugin festlegen

In den **Einstellungen** lässt sich das Standard-SCM-Plugin und das Standard-KI-Plugin auswählen:

- Registerkarte **Quellcodeverwaltung** → Abschnitt „Standard SCM-Plugin"
- Registerkarte **KI** → Abschnitt „Standard KI-Plugin"

Diese Auswahl gilt als Fallback, wenn eine Aufgabe kein explizites Plugin vorgibt.

## Überprüfung

- Über `IKiPlugin.CheckHealthAsync` kann geprüft werden, ob das CLI erreichbar ist (z.B. `claude --version`).
- Fehler beim Plugin-Aufruf erscheinen im Aufgabenprotokoll.
