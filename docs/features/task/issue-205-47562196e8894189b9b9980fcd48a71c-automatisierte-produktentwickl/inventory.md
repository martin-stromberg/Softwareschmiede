# Bestandsaufnahme: Autonome Aufgaben – Echte CLI-Ausführung und UI-Integration

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezüglich der Anforderung zur echten CLI-Ausführung autonomer Aufgaben, Session-Resume nach App-Neustart und UI-Integration. Der Fokus liegt auf der Erfassung, was bereits vorhanden ist, und wo Lücken zu den Anforderungen bestehen.

## Zusammenfassung

**Bereits vorhanden:**
- ✅ `KiAusfuehrungsService` mit `StartCliAsync()` und `StartWithPseudoConsoleAsync()` für echte CLI-Prozessstartzahl
- ✅ `CliProcessStatusChanged`-Event zur Statusverfolgung von CLI-Prozessen
- ✅ `IKiPlugin.SupportsSessionContinuation()` Interface-Methode für Plugin-Support-Abfrage
- ✅ `SessionManagementService` mit `PauseAufgabeBeiBudgetLimitAsync()` und `SetzeFortAsync()` für Session-Resume
- ✅ `AufgabeRecoveryService` mit Heartbeat-basierter Wiederherstellung für reguläre Aufgaben
- ✅ `AutonomAufgabeDetailViewModel` mit StartCommand, StopCommand, ResumeCommand
- ✅ `ProjektleiterAgentService.StarteAgentAsync()` für Agent-Initialisierung
- ✅ `AutonomAufgabeStartService` für Dialog-Orchestrierung
- ✅ `AutonomAufgabeKonfiguration` Datenmodell mit InitialPrompt, SessionPauseUtc, etc.

**Lücken zu den Anforderungen:**
- ❌ `AutonomAufgabeKonfiguration.ExplizitGestoppt`-Flag (für explizites Stoppen durch Nutzer) nicht vorhanden → Datenbank-Migration erforderlich
- ❌ `ProjektleiterAgentService.StarteAgenNachAppNeustartAsync()` Methode nicht vorhanden (automatischer Start beim App-Startup)
- ❌ `ProjektleiterAgentService.StoppeAgenExplizitAsync()` Methode nicht vorhanden (explizites Stoppen mit CLI-Integration)
- ❌ Kein `AppStartupAutonomAufgabenRecoveryService` oder ähnliches für automatisches Recovery nach App-Neustart vorhanden
- ❌ `ProjektleiterAgentService.StarteAgentAsync()` startet aktuell keinen echten CLI-Prozess, nur DB-Operationen
- ❌ `AutonomAufgabeDetailViewModel.CliIsRunning`-Property nicht vorhanden (bindbare Eigenschaft für CLI-Laufzeitstatus)
- ❌ Event-Listener zwischen `KiAusfuehrungsService.CliProcessStatusChanged` und ViewModel nicht vorhanden
- ❌ TaskDetailView.xaml: Visibility-Binding zur Ausblendung regulärer Aufgaben-Buttons bei autonomen Aufgaben nicht vorhanden
- ❌ Tests für die geplanten Methoden nicht vorhanden

**Kritische Erkenntnisse:**
1. **CLI-Integration bereits vorhanden**: Der Service `KiAusfuehrungsService` bietet bereits `StartWithPseudoConsoleAsync()` mit `optionalParameters`, die für Resume-Prompts genutzt werden können.
2. **Plugin-Support für Session-Continuation**: `IKiPlugin.SupportsSessionContinuation()` ist definiert, es ist aber unklar, welche Plugins dies implementieren.
3. **Recovery für autonome Aufgaben geplant, nicht implementiert**: `AufgabeRecoveryService` schließt gezielt Autonome Aufgaben aus (`a.AutonomKonfiguration == null`), was bedeutet, dass der automatische Recovery-Mechanismus nach App-Neustart noch fehlt.
4. **Keine CliStoppService-Klasse**: Die Anforderung nennt einen `CliStoppService`, aber dieser existiert nicht. Die Stop-Logik gehört zu `KiAusfuehrungsService.StopCliAsync()`.

## Details

### [Datenmodell](inventory/models.md)
Analyse der Datenmodellklassen `AutonomAufgabeKonfiguration` und `Aufgabe`:
- `AutonomAufgabeKonfiguration`: 17 Eigenschaften vorhanden, aber `ExplizitGestoppt`-Flag fehlt
- `Aufgabe`: 23 Eigenschaften mit Lifecycle- und Heartbeat-Tracking

### [Logik-Services](inventory/logic.md)
Übersicht der Logikklassen und deren Methoden:
- `ProjektleiterAgentService`: 6 Methoden, aber nur DB-Buchhaltung; keine CLI-Integration und keine Auto-Resume-Methoden
- `KiAusfuehrungsService`: 7 öffentliche Methoden für CLI-Start/Stop mit Event-Support
- `SessionManagementService`: 5 Methoden für Session-Pause/Resume und Heartbeat-Prüfung
- `AufgabeRecoveryService`: 2 Methoden für Crash-Recovery (nur reguläre Aufgaben)
- `AutonomAufgabeStartService`: Orchestriert Dialog und ViewModel-Erstellung

### [Interfaces](inventory/interfaces.md)
Definition der Plugin- und Automation-Status-Interfaces:
- `IKiPlugin.SupportsSessionContinuation()` für Plugin-Capability-Abfrage
- `IRunningAutomationStatusSource` für Status-Tracking
- `IGitPlugin` für Repository-Pfad-Auflösung

### [Enumerationen](inventory/enums.md)
Bestehende Enums für Status-Verwaltung:
- `AufgabeAusfuehrungsStatus`: 3 Werte (NichtGestartet, Aktiv, Beendet)
- `AufgabeStatus`: 5 Werte (Neu, Gestartet, Wartend, Beendet, Archiviert)
- `CliProcessStatus`: 3 Werte (Gestartet, Gestoppt, Fehler) — lokal in KiAusfuehrungsService definiert

### [Tests](inventory/tests.md)
Bestandsaufnahme existierender Testklassen und Hilfsmethoden:
- `ProjektleiterAgentServiceTests`: Test für `StarteAgentAsync()` vorhanden, aber keine Tests für geplante Methoden
- `SessionManagementServiceTests`, `AufgabeRecoveryServiceTests`, `AutonomAufgabeDetailViewModelTests`: Vorhanden
- Fehlende E2E-Tests für neue Anforderungen

## Implementierungsblocken und Abhängigkeiten

1. **Datenbank-Migration**: `ExplizitGestoppt`-Flag muss zu `AutonomAufgabeKonfiguration` hinzugefügt werden, bevor die neuen Methoden implementiert werden können.

2. **CLI-Integration in `ProjektleiterAgentService.StarteAgentAsync()`**: Muss erweitert werden, um `KiAusfuehrungsService.StartWithPseudoConsoleAsync()` aufzurufen mit:
   - `kiPlugin`: Aus `Aufgabe.KiPluginPrefix` aufgelöst
   - `optionalParameters`: InitialPrompt beim Erststart, Resume-Prompt beim Neustart

3. **App-Startup-Recovery**: Neuer Service oder Aufbauerweiterung erforderlich, um beim App-Start alle Autonomen Aufgaben mit `ExplizitGestoppt == false` und `AusfuehrungsStatus == Aktiv` automatisch neu zu starten.

4. **ViewModel-Bindungen**: `AutonomAufgabeDetailViewModel` muss:
   - `CliIsRunning`-Property hinzufügen
   - Event-Listener für `KiAusfuehrungsService.CliProcessStatusChanged` abonnieren

5. **UI-Visibility-Binding**: `TaskDetailViewModel` muss `IsAutonomAufgabe`-Property implementieren, um reguläre Aufgaben-Buttons auszublenden.

## Verweise zu Anforderungspunkten

| Anforderungspunkt | Status | Findet sich in |
|-------------------|--------|--|
| Persistenz-Flag `ExplizitGestoppt` | ❌ Fehlt | — |
| `ProjektleiterAgentService.StarteAgentAsync()` mit CLI | ✅ Teilweise (DB only) | [logic.md](inventory/logic.md#ProjektleiterAgentService) |
| `ProjektleiterAgentService.StarteAgenNachAppNeustartAsync()` | ❌ Fehlt | — |
| `ProjektleiterAgentService.StoppeAgenExplizitAsync()` | ❌ Fehlt | — |
| `AppStartupAutonomAufgabenRecoveryService` | ❌ Fehlt | — |
| `KiAusfuehrungsService` mit `optionalParameters` | ✅ Vorhanden | [logic.md](inventory/logic.md#KiAusfuehrungsService) |
| `IKiPlugin.SupportsSessionContinuation()` | ✅ Vorhanden | [interfaces.md](inventory/interfaces.md) |
| `SessionManagementService.SetzeFortAsync()` | ✅ Vorhanden | [logic.md](inventory/logic.md#SessionManagementService) |
| UI-Visibility für autonome Aufgaben | ❌ Fehlt | — |
| Tests für neue Methoden | ❌ Fehlt | [tests.md](inventory/tests.md) |

## Nächste Schritte

1. Datenbank-Migration für `ExplizitGestoppt`-Flag erstellen
2. `ProjektleiterAgentService.StarteAgentAsync()` um echten CLI-Start mit `KiAusfuehrungsService` erweitern
3. `ProjektleiterAgentService.StarteAgenNachAppNeustartAsync()` implementieren
4. `ProjektleiterAgentService.StoppeAgenExplizitAsync()` implementieren
5. App-Startup-Recovery-Service erstellen oder `AufgabeRecoveryService` für Autonome Aufgaben erweitern
6. ViewModel-Bindungen implementieren
7. UI-Visibility-Logic hinzufügen
8. Unit- und E2E-Tests schreiben
