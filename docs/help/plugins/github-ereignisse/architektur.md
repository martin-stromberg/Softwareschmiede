← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|------------|-----|-------|
| `ScmEventMonitoringService` | Hintergrunddienst | Pollt lokal verknüpfte Referenzen und dispatcht gespeicherte Einträge. |
| `GitHubPlugin` | SCM-Provider | Implementiert `IScmEventSource` und `IScmEventResponder`; liest GitHub-Inhalte und führt unterstützte Antworten aus. |
| `ScmEventStore` / `SoftwareschmiededDbContext` | Persistenz | Speichert Ereignisse, Snapshots, Checkpoints, Aktionen und Aufgaben-Leases. |
| `ScmEventOrchestrator` | Application Service | Prüft Aufgabenstatus und CLI, koordiniert Zustellung sowie Wiederaufnahme. |
| `IScmPromptSessionAdapter` / `ScmEventExchangeService` | CLI-Austausch | Prüft den Antwortkanal und korreliert Annahme, Aktionen und Abschluss mit Lauf und Versuch. |
| `ScmEventQueueViewModel` / `ScmEventQueueView` | Oberfläche | Zeigt Einträge und ermöglicht kontrollierte Queue-Aktionen. |

## Abhängigkeiten

GitHub-API-Aufrufe laufen über `gh` und die gespeicherte GitHub-Anmeldung. Für KI-Ausführung werden die vorhandene Aufgaben-/CLI-Infrastruktur und ein unterstützter sicherer Austauschkanal benötigt. Bitbucket und Local Directory implementieren derzeit keine Ereignisquelle oder Ereignisantwort; der Monitor überspringt Provider ohne diese Fähigkeiten.

Der CLI-Kanaladapter erkennt Codex CLI `v0.114.0`, Claude Code `v2.1.0` und Devin CLI `v3000.11.92` anhand ihrer Startausgabe. Automatische Freigabe setzt bei Codex `workspace-write`, bei Claude `acceptEdits` und bei Devin `--permission-mode accept-edits`, `dangerous` oder `bypass` sowie jeweils eine sicher auflösbare effektive Konfiguration voraus. Danach muss die tatsächliche Dateitool-Umgebung Lesen, Schreiben und Umbenennen im privaten, rungebundenen Austauschkanal nachweisen. Andere Versionen, unbekannte Optionen, entgegenstehende Regeln oder nicht nachweisbarer Zugriff werden nicht als unterstützt behandelt. Der KI-Simulator hat einen separaten SCM-Prozessmodus; seine normale Shell ist kein Antwortkanal. GitHub Copilot CLI wird vom Adapter nicht unterstützt.

## Datenfluss

GitHub-Texte werden mit Repository- und Versionsmetadaten gelesen, gegen lokale Referenzen abgeglichen und dauerhaft in die Inbox geschrieben. Nach Trigger- und Identitätsprüfung leitet der Orchestrator den vollständigen Ursprungstext an eine CLI mit nachgewiesenem Antwortkanal weiter. Empfangsreaktion, Wartehinweis und KI-Antwort werden als getrennte Remoteaktionen protokolliert. Kommentare und veröffentlichte PR-Reviews erhalten die 👀-Reaktion; für PR-Reviews wird sie über GraphQL auf dem Review-Node gesetzt. Ein fehlgeschlagener Empfangsreaktionsversuch erhält keinen alternativen Kommentar als Ersatz, kann aber unabhängig von der Reaktion einen Warte-Kommentar nach sich ziehen.

## Zuverlässigkeit

Eindeutige Schlüssel und Inhaltssnapshots verhindern doppelte lokale Aufträge für wiederholt gelesene Textstände. Aufgaben-Leases serialisieren Zustellungen derselben Aufgabe. Für Remoteaktionen wird keine verteilte Exactly-once-Garantie zugesagt: Bei nicht rekonstruierbarem Ergebnis wird der Zustand sichtbar ungewiss und ein möglicher Prompt-Neuversand verlangt eine bewusste Bestätigung.
