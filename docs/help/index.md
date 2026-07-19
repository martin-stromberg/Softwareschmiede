# Dokumentation

Übersicht über alle dokumentierten Funktionsbereiche der Softwareschmiede.

## Kernfunktionen

- [Aufgaben & KI-Entwicklungsprozess](aufgaben/index.md) — Aufgaben sind die Arbeitseinheiten der Softwareschmiede. Der optimierte Aufgabenworkflow kombiniert Repository-Klone und CLI-Start in einem Schritt über einen einzelnen „Starten"-Button; die KI-Plugin-Auswahl erfolgt per Dialog mit optionaler Speicherung als Projekt-Standard. Die Aufgabendetailansicht mit Ribbon-Menü bietet umfassende Verwaltung mit Status-abhängigem Content-Switching (Edit-, CLI-, Diff-Panels), eingebettetem Terminalfenster, Info-Toggle und automatischem CLI-Neustart. Die Navigationsmenü-Seitenleiste zeigt aktive Aufgaben als Kacheln mit KI-Ausführungsstatus an; der Status wird automatisch aktualisiert, ohne dass der Benutzer die Ansicht neu laden muss, und wird bei echten Statuswechseln mit dezenter Fade-Animation hervorgehoben. Dies ermöglicht schnellen Zugriff auf laufende Arbeiten mit durchgehend aktuellem Status.
- [Projekte](projekte/index.md) — Projekte sind die zentrale Organisationseinheit; jedes Projekt fasst ein oder mehrere Git-Repositories sowie zugehörige Aufgaben zusammen. Ein Suggestions-Panel auf der Projektübersicht zeigt unzugeordnete Repositories aus allen SCM-Plugins und ermöglicht schnelle Projekterstellung. Die Projektdetailansicht bietet umfassende Verwaltung mit Ribbon-Menü, Bearbeitung und Repository-Management.

## Anwendung

Eigenschaften und Präsentation der Softwareschmiede Desktopanwendung.

- [Programmsymbol](anwendung/index.md) — Visuelles Markenidentitäts-Symbol (Hammer-/Spitzhacken) der Softwareschmiede, angezeigt im Windows-Explorer, in der Taskleiste und im Fenster-Titel.
- [Versionsanzeige](anwendung/versions-anzeige.md) — Zeigt die aktuell installierte Programmversion dauerhaft in der Fußzeile der Navigations-Seitenleiste an, sodass Benutzer die Version auf einen Blick erkennen.
- [Programmupdate](programmupdate/index.md) — Aktualisiert die Anwendung über das GUI mit Sicherheitsprüfung und Fortschrittsanzeige. Vor dem Update wird überprüft, ob laufende CLI-Aufgaben das Update blockieren würden; der Benutzer wird ggf. gewarnt. Der Fortschrittsdialog zeigt den Status der Update-Vorbereitung (Download, Entpacken, Validierung) an und erlaubt dem Benutzer, den Prozess abzubrechen.

## Benutzeroberfläche

- [Dateiexplorer](dateiexplorer/index.md) — Durchsucht und inspiziert die lokale Dateistruktur des geklonten Repositories in zwei Modi: Standard (vollständiger Arbeitsbaum) und Vergleich (nur geänderte Dateien mit farblich hervorgehobenem Diff). Integriert in die Aufgabendetailansicht.
- [Dateisystem-Integration](dateisystem-integration/index.md) — Öffnet das Arbeitsverzeichnis im Standard-Dateiexplorer des Betriebssystems oder eine gefundene Visual-Studio-Solution direkt aus dem Ribbon der Aufgabendetailansicht heraus; bei mehreren Solutions wählt der Benutzer per Dialog aus.
- [Terminal-Integration](terminal/index.md) — KI-CLI-Tools (Claude, Codex, GitHub Copilot) werden über Windows Pseudo Console (ConPTY) gestartet und direkt in der WPF-Aufgabendetailansicht gerendert. Der VT100/ANSI-Output wird mit voller Farbunterstützung in einem benutzerdefinierten Control angezeigt; Tastatureingaben werden nativ an den Prozess weitergeleitet.
- [Diff-Anzeige](diff/index.md) — Zeigt die Unterschiede zwischen zwei Versionen einer Datei, integriert in die Aufgabendetailansicht.

## Systemverwaltung & Konfiguration

- [Einstellungen](einstellungen/index.md) — Bündelt alle systemweiten Konfigurationen: Plugin-Einstellungen, Arbeitsverzeichnis, Benachrichtigungen und Erscheinungsbild.
- [Plugin-System](plugins/index.md) — SCM- und KI-Plugins werden als separate .NET-Klassenbibliotheken bereitgestellt und per `PluginManager` zur Laufzeit entdeckt.
- [Stabilität & Fehlerbehandlung](stabilitaet/index.md) — Fängt Fehler an allen relevanten Stellen zentral ab und protokolliert sie, statt unkontrolliert abzustürzen: globale Exception-Handler, abgesicherte Fire-and-Forget-Aufrufe, geschützte Prozess-Event-Handler und zuverlässige Freigabe nativer ConPTY-Handles.

### KI-Plugins (Entwicklungsautomatisierung)

- [Claude CLI](plugins/index.md) — Claude CLI aus Aufgaben heraus starten
- [GitHub Copilot](plugins/index.md) — GitHub Copilot CLI aus Aufgaben heraus starten
- [Codex CLI](plugins/index.md) — Codex CLI aus Aufgaben heraus starten

### SCM-Plugins (Quellcodeverwaltung)

- [GitHub-Plugin](plugins/index.md) — GitHub.com Integration über GitHub CLI
- [BitBucket-Plugin](plugins/bitbucket-plugin/index.md) — BitBucket Cloud und Self-Hosted (Server/Data Center) mit optionaler Jira-Integration
- [Local Directory Plugin](plugins/index.md) — Lokale Git-Repositories ohne Remote
