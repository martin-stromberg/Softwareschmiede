# Monitoring, DI und Tests

## Aktueller Zustand Hintergrundverarbeitung

Die WPF-App verwendet `Host.CreateDefaultBuilder` und registriert Services in `App.xaml.cs`. Es gibt Singletons und Scoped Services, aber keine vorhandene PR-spezifische Hintergrundverarbeitung.

Vorhandene zeit-/hintergrundnahe Muster:

- `CliProcessManager` nutzt Timer fuer Heartbeats und ist als Singleton registriert.
- `PromptZeitVersandService` nutzt `TimeProvider` und Timer fuer geplante Prompts.
- `AufgabeRecoveryService` bereinigt inkonsistente CLI-Laufzustaende.

Diese Muster passen fuer ein pollingbasiertes PR-Monitoring, solange DbContext-Zugriffe ueber Scopes laufen.

## Noetige Monitoring-Phasen

Die Anforderung braucht mindestens folgende Phasen:

1. PR erstellt und gespeichert.
2. Pre-Merge-Actions laufen.
3. Pre-Merge-Actions erfolgreich.
4. PR automatisch oder manuell abgeschlossen.
5. Post-Merge-Actions laufen.
6. Post-Merge-Actions erfolgreich oder fehlgeschlagen.
7. Blockiert oder Fehler, z. B. GitHub-API-Fehler, fehlende Berechtigung, Bypass erforderlich.

Eine explizite Enum fuer `MonitoringPhase` verhindert, dass Status aus PR- und Action-Daten implizit geraten wird.

## Service-Schnitt

Sinnvolle neue Services:

- `PullRequestReferenzService`
  - PR speichern
  - PRs je Aufgabe laden
  - Status und Workflow-Runs aktualisieren
- `PullRequestMonitoringService`
  - faellige PRs suchen
  - GitHub-Status abfragen
  - Auto-Abschluss bei erfolgreichen relevanten Actions pruefen
  - Post-Merge-Runs verfolgen
- optional `PullRequestAutoCompletionPolicy`
  - trennt fachliche Entscheidung von GitHub-API-Aufrufen

## DI

Registrierung in `src/Softwareschmiede.App/App.xaml.cs`:

- Scoped Persistenzservice.
- Singleton oder Hosted Service fuer Monitoring.
- Nutzung von `IServiceScopeFactory`, falls ein Singleton periodisch DbContext-basierte Services braucht.

Da `Microsoft.Extensions.Hosting` bereits aktiv ist, ist `AddHostedService` technisch moeglich. Bei WPF muss der Start/Stop-Pfad trotzdem sauber sein, weil `OnExit` den Host stoppt.

## Tests

Empfohlene Testabdeckung:

- Unit-Tests fuer Persistenzservice mit `TestDbContextFactory`.
- Unit-Tests fuer Monitoring-Entscheidungen ohne echte GitHub-Aufrufe.
- GitHubPlugin-Tests mit Mock-`ICliRunner` fuer:
  - PR-Status-JSON,
  - Workflow-Run-JSON,
  - Merge-/Approval-Aufrufe,
  - Fehlerfaelle und Sanitizing.
- ViewModel-Tests fuer:
  - PR-Tab-Sichtbarkeit,
  - PR-Laden,
  - Leer-/Fehlerzustand,
  - Reload nach PR-Erstellung.
- E2E/UI-Test optional fuer Navigation zum neuen PR-Inhaltsbereich.

## Build- und Migrationshinweise

Das Projekt verwendet `net10.0` bzw. `net10.0-windows10.0.17763.0` und `WarningsAsErrors=CS1591`. Neue public Typen, Properties und Methoden brauchen XML-Dokumentation.

Neue EF-Entities erfordern Migration und Snapshot-Update. Fuer Tests muessen neue Entities im InMemory-Kontext mit `EnsureCreated` funktionieren.

