← [Zurück zur Übersicht](index.md)

# GitHub-Ereignisüberwachung — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|------------|-----|-------|
| `ScmEventMonitoringService` | Hintergrunddienst | Pollt lokal verknüpfte Referenzen und dispatcht gespeicherte Einträge. |
| `GitHubPlugin` | SCM-Provider | Implementiert `IScmEventSource`, `IScmEventResponder` und `IScmAgentAccess`; liest GitHub-Inhalte, führt unterstützte Antworten aus und stellt Agenten die SCM-Anbindung als Prozessumgebung bereit. |
| `ScmEventStore` / `SoftwareschmiededDbContext` | Persistenz | Speichert Ereignisse, Snapshots, Checkpoints, Aktionen und Aufgaben-Leases. |
| `ScmEventOrchestrator` | Application Service | Prüft Aufgabenstatus und CLI, koordiniert Zustellung sowie Wiederaufnahme. |
| `IScmPromptSessionAdapter` / `ScmEventExchangeService` | CLI-Austausch | Prüft den Antwortkanal und korreliert Annahme, Aktionen und Abschluss mit Lauf und Versuch. |
| `ScmEventQueueViewModel` / `ScmEventQueueView` | Oberfläche | Zeigt Einträge und ermöglicht kontrollierte Queue-Aktionen. |

## Abhängigkeiten

GitHub-API-Aufrufe laufen prozesslos über HTTP (`GitHubHttpApiClient`/`HttpClient`) mit dem gespeicherten Token als `Authorization`-Header — ohne `gh`-Kindprozesse. `gh` bleibt nur für die drei ereignisgebundenen, seltenen Mutationen `CreateIssueAsync`, `CreatePullRequestAsync` und `CompletePullRequestAsync` sowie als optionales Werkzeug in Agenten-Sitzungen erforderlich; die lokale `git`-CLI wird für Klon-, Push- und Branch-Operationen weiterhin benötigt. Für KI-Ausführung werden die vorhandene Aufgaben-/CLI-Infrastruktur und ein unterstützter sicherer Austauschkanal benötigt. Bitbucket und Local Directory implementieren derzeit keine Ereignisquelle oder Ereignisantwort; der Monitor überspringt Provider ohne diese Fähigkeiten.

Der Kontomodus (`scm.monitoring.accountmode`, Standard `Benutzerkonto`) wählt pro Poll-Zyklus den Eingangskanal: Im `Benutzerkonto` enumeriert der Monitor die Repositories wie bisher; im `SeparatesKonto` liest er ausschließlich die Notifications-Inbox eines separaten GitHub-Kontos über die optionale Plugin-Fähigkeit `IScmNotificationSource` (`GET /notifications`, `reason = mention`) und leitet daraus Referenzen ab. Beide Konto-Pfade nutzen denselben HTTP-Client, getrennt nur durch den Credential-Selektor (`Softwareschmiede.GitHub.Token` bzw. `Softwareschmiede.GitHub.AgentToken`); das `Konto`-Feld auf `ScmEventRepository` propagiert die Wahl über Referenz, Beobachtung und Antwort.

Der CLI-Kanaladapter erkennt Codex CLI `v0.114.x`, Claude Code `v2.1.x` und Devin CLI `v3000.11.x` anhand ihrer Startausgabe — jeweils gleiches Major.Minor mit beliebigem Patch; Minor- und Major-Sprünge bleiben bewusst ungeprüft. Automatische Freigabe setzt bei Codex `workspace-write`, bei Claude `acceptEdits` und bei Devin `--permission-mode accept-edits`, `dangerous` oder `bypass` sowie jeweils eine sicher auflösbare effektive Konfiguration voraus. Danach muss die tatsächliche Dateitool-Umgebung Lesen, Schreiben und Umbenennen im privaten, rungebundenen Austauschkanal nachweisen. Eine erkannte Version außerhalb dieser Bereiche wird im Wartegrund mit CLI-Name und erkannter Version benannt und kann ausdrücklich bestätigt werden; die Dateitool-Kanalprobe bleibt dabei die eigentliche Sicherheitsgrenze. Unbekannte Promptformate, unbekannte Optionen, entgegenstehende Regeln oder nicht nachweisbarer Zugriff werden nicht als unterstützt behandelt und bieten keine Bestätigung an. Der KI-Simulator hat einen separaten SCM-Prozessmodus; seine normale Shell ist kein Antwortkanal. GitHub Copilot CLI wird vom Adapter nicht unterstützt.

## Datenfluss

GitHub-Texte werden mit Repository- und Versionsmetadaten gelesen, gegen lokale Referenzen abgeglichen und dauerhaft in die Inbox geschrieben. Nach Trigger- und Identitätsprüfung leitet der Orchestrator den vollständigen Ursprungstext an eine CLI mit nachgewiesenem Antwortkanal weiter. Empfangsreaktion, Wartehinweis und KI-Antwort werden als getrennte Remoteaktionen protokolliert. Kommentare und veröffentlichte PR-Reviews erhalten die 👀-Reaktion; für PR-Reviews wird sie über GraphQL auf dem Review-Node gesetzt. Ein fehlgeschlagener Empfangsreaktionsversuch erhält keinen alternativen Kommentar als Ersatz, kann aber unabhängig von der Reaktion einen Warte-Kommentar nach sich ziehen.

## Zuverlässigkeit

Eindeutige Schlüssel und Inhaltssnapshots verhindern doppelte lokale Aufträge für wiederholt gelesene Textstände. Aufgaben-Leases serialisieren Zustellungen derselben Aufgabe. Für Remoteaktionen wird keine verteilte Exactly-once-Garantie zugesagt: Bei nicht rekonstruierbarem Ergebnis wird der Zustand sichtbar ungewiss und ein möglicher Prompt-Neuversand verlangt eine bewusste Bestätigung.
