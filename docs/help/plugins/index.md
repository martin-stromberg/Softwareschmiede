# Plugin-System

Die Softwareschmiede nutzt ein Plugin-System, das SCM-Plugins (Quellcodeverwaltung) und KI-Plugins (Entwicklungsautomatisierung) zur Laufzeit laden kann. KI-Plugins wie Claude CLI, GitHub Copilot, Codex CLI und der KI-Simulator werden als separate .NET-Klassenbibliotheken bereitgestellt und per `PluginManager` entdeckt.

Jedes Plugin kann einzeln aktiviert oder deaktiviert werden — deaktivierte Plugins verschwinden aus allen Auswahllisten und werden automatisch ausgeblendet. Bei genau einem aktiven Plugin einer Kategorie wird dieses automatisch ohne Auswahl verwendet.

## Inhalt

- [Beschreibung](beschreibung.md)
- [Plugin-Aktivierung und -Deaktivierung](aktivierung.md)
- [Business Rules](business-rules.md)
- [Technischer Ablauf](ablauf-technisch.md)
- [API](api.md)
- [Installation & Konfiguration](installation.md)
