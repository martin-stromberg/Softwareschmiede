# Dokumentation

Übersicht über alle dokumentierten Funktionsbereiche der Softwareschmiede.

## Kernfunktionen

- [Aufgaben & KI-Entwicklungsprozess](aufgaben/index.md) — Aufgaben sind die Arbeitseinheiten der Softwareschmiede; jede Aufgabe beschreibt eine Entwicklungsanforderung, die ein KI-CLI-Tool in einem isolierten Git-Branch bearbeitet.
- [Projekte](projekte/index.md) — Projekte sind die zentrale Organisationseinheit; jedes Projekt fasst ein oder mehrere Git-Repositories sowie zugehörige Aufgaben zusammen. Die Projektdetailansicht bietet umfassende Verwaltung mit Ribbon-Menü, Bearbeitung und Repository-Management.

## Benutzeroberfläche

- [CLI-Fenster-Einbettung](terminal/index.md) — Das CLI-Fenster des KI-Tools wird via Win32 `SetParent` direkt in die WPF-Aufgabendetailansicht eingebettet.
- [Diff-Anzeige](diff/index.md) — Zeigt die Unterschiede zwischen zwei Versionen einer Datei, integriert in die Aufgabendetailansicht.

## Systemverwaltung & Konfiguration

- [Einstellungen](einstellungen/index.md) — Bündelt alle systemweiten Konfigurationen: Plugin-Einstellungen, Arbeitsverzeichnis, Benachrichtigungen und Erscheinungsbild.
- [Plugin-System](plugins/index.md) — SCM- und KI-Plugins werden als separate .NET-Klassenbibliotheken bereitgestellt und per `PluginManager` zur Laufzeit entdeckt.
