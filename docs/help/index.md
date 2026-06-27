# Dokumentation

Übersicht über alle dokumentierten Funktionsbereiche der Softwareschmiede.

## Kernfunktionen

- [Aufgaben & KI-Entwicklungsprozess](aufgaben/index.md) — Aufgaben sind die Arbeitseinheiten der Softwareschmiede. Der optimierte Aufgabenworkflow kombiniert Repository-Klone und CLI-Start in einem Schritt über einen einzelnen „Starten"-Button; die KI-Plugin-Auswahl erfolgt per Dialog mit optionaler Speicherung als Projekt-Standard. Die Aufgabendetailansicht mit Ribbon-Menü bietet umfassende Verwaltung mit Status-abhängigem Content-Switching (Edit-, CLI-, Diff-Panels), eingebettetem Terminalfenster, Info-Toggle und automatischem CLI-Neustart.
- [Projekte](projekte/index.md) — Projekte sind die zentrale Organisationseinheit; jedes Projekt fasst ein oder mehrere Git-Repositories sowie zugehörige Aufgaben zusammen. Ein Suggestions-Panel auf der Projektübersicht zeigt unzugeordnete Repositories aus allen SCM-Plugins und ermöglicht schnelle Projekterstellung. Die Projektdetailansicht bietet umfassende Verwaltung mit Ribbon-Menü, Bearbeitung und Repository-Management.

## Benutzeroberfläche

- [CLI-Fenster-Einbettung](terminal/index.md) — Das CLI-Fenster des KI-Tools wird via Win32 `SetParent` direkt in die WPF-Aufgabendetailansicht eingebettet.
- [Diff-Anzeige](diff/index.md) — Zeigt die Unterschiede zwischen zwei Versionen einer Datei, integriert in die Aufgabendetailansicht.

## Systemverwaltung & Konfiguration

- [Einstellungen](einstellungen/index.md) — Bündelt alle systemweiten Konfigurationen: Plugin-Einstellungen, Arbeitsverzeichnis, Benachrichtigungen und Erscheinungsbild.
- [Plugin-System](plugins/index.md) — SCM- und KI-Plugins werden als separate .NET-Klassenbibliotheken bereitgestellt und per `PluginManager` zur Laufzeit entdeckt.

### KI-Plugins (Entwicklungsautomatisierung)

- [Claude CLI](plugins/index.md) — Claude CLI aus Aufgaben heraus starten
- [GitHub Copilot](plugins/index.md) — GitHub Copilot CLI aus Aufgaben heraus starten
- [Codex CLI](plugins/index.md) — Codex CLI aus Aufgaben heraus starten

### SCM-Plugins (Quellcodeverwaltung)

- [GitHub-Plugin](plugins/index.md) — GitHub.com Integration über GitHub CLI
- [BitBucket-Plugin](plugins/bitbucket-plugin/index.md) — BitBucket Cloud und Self-Hosted (Server/Data Center) mit optionaler Jira-Integration
- [Local Directory Plugin](plugins/index.md) — Lokale Git-Repositories ohne Remote
