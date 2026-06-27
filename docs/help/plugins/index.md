# Plugin-System

Die Softwareschmiede nutzt ein Plugin-System, das SCM-Plugins (Quellcodeverwaltung) und KI-Plugins (Entwicklungsautomatisierung) zur Laufzeit laden kann. KI-Plugins wie Claude CLI, GitHub Copilot, Codex CLI und der KI-Simulator werden als separate .NET-Klassenbibliotheken bereitgestellt und per `PluginManager` entdeckt.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [API](api.md)
- [Installation & Konfiguration](installation.md)
