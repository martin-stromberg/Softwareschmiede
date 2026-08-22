← [Zurück zur Übersicht](index.md)

# Autonome Aufgaben — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle | Modul |
|------------|-----|-------|--------|
| `Aufgabe` | Entity | Represäntiert die Aufgabe, hält References zu Agenten und Session-State | Domain |
| `AutonomAufgabeKonfiguration` | Entity | Persistiert Konfiguration der Autonomen Aufgabe | Domain |
| `UnteragentSpezifikation` | Entity | Metadaten erzeugter Unteragenten | Domain |
| `SkillDefinition` | Entity | Versionierte Skill-Definitionen | Domain |
| `AutonomAufgabenInitialisierungsService` | Service | Orchestriert Erstellung von Arbeitsverzeichnis & Repository-Klon | Application |
| `ProjektleiterAgentService` | Service | Verwaltet Projektleiter-Agent-Lifecycle & Unteragenten-Orchestrierung | Application |
| `SessionManagementService` | Service | Manages Session-Pause/Resume, Token-Budget, Heartbeat | Application |
| `UnteragentGovernanceService` | Service | Erzwingt Governance-Regeln für Unteragenten (Infrastruktur vorbereitet) | Application |
| `AutonomAufgabeInitialisierungsDialogViewModel` | ViewModel | Bindung für Initialisierungs-Dialog | App (WPF) |
| `AutonomAufgabeDetailViewModel` | ViewModel | Bindung für Detail-View mit Kontroll-Buttons | App (WPF) |
| `SoftwareschmiededDbContext` | DbContext | Datenbankzugriff für Entities | Infrastructure |
| `ICliRunner` | Interface | CLI-Befehl-Ausführer (git, etc.) | Infrastructure |
| `IAgentRuntime` | Interface | Agent-Start/Stop-Infrastruktur | Infrastructure |
| Arbeitsverzeichnis | Dateisystem | Strukturierte Verzeichnisse für plan.md, progress.md, state.json | External |

## Abhängigkeiten

```
┌─ WPF UI-Ebene ────────────────────────────────────────────────────┐
│                                                                    │
│  AutonomAufgabeInitialisierungsDialogViewModel                    │
│  └─ AufgabeService                                                │
│     └─ AutonomAufgabenInitialisierungsService                     │
│        └─ SoftwareschmiededDbContext                              │
│        └─ ICliRunner                                              │
│                                                                    │
│  AutonomAufgabeDetailViewModel                                    │
│  └─ ProjektleiterAgentService                                     │
│     └─ IAgentRuntime                                              │
│     └─ SoftwareschmiededDbContext                                 │
│  └─ SessionManagementService                                      │
│     └─ IAgentRuntime                                              │
│     └─ SoftwareschmiededDbContext                                 │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘

┌─ Application-Ebene ────────────────────────────────────────────────┐
│                                                                     │
│  AutonomAufgabenInitialisierungsService                           │
│  ProjektleiterAgentService                                        │
│  SessionManagementService                                         │
│  UnteragentGovernanceService (Governance-Enforcement)             │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─ Domain-Ebene ──────────────────────────────────────────────────────┐
│                                                                      │
│  Aufgabe (Entity)                                                  │
│  AutonomAufgabeKonfiguration (Entity)                              │
│  UnteragentSpezifikation (Entity)                                  │
│  SkillDefinition (Entity)                                          │
│  AufgabeAusfuehrungsStatus, PersistenzModus, etc. (Enums)         │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘

┌─ Infrastructure-Ebene ──────────────────────────────────────────────┐
│                                                                      │
│  SoftwareschmiededDbContext                                        │
│  ICliRunner (Implementation)                                       │
│  IAgentRuntime (Implementation)                                    │
│  Dateisystem-Zugriff (I/O)                                        │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

## Datenfluss

### Initialisierung

```
Benutzer
    ↓
Dialog mit Konfiguration
    ↓
AutonomAufgabenInitialisierungsService.InitialisiereAsync()
    ├─ Arbeitsverzeichnis erstellen (Dateisystem)
    ├─ Repository klonen (ICliRunner)
    ├─ state.json schreiben (Dateisystem)
    ├─ permissions.json erstellen (Dateisystem)
    ├─ plan.md, progress.md, governance.md schreiben (Dateisystem)
    └─ AutonomAufgabeKonfiguration in DB speichern
```

### Agent-Start

```
Benutzer: "Start" klicken
    ↓
AutonomAufgabeDetailViewModel.StartCommand
    ↓
ProjektleiterAgentService.StarteAgenAsync()
    ├─ Skills laden (Dateisystem + DB)
    ├─ IAgentRuntime.StartAgentAsync() aufrufen
    │   ├─ Initialprompt
    │   ├─ Skill-Registry
    │   └─ Working Directory
    └─ AutonomAufgabeKonfiguration.ProjektleiterAgentId speichern (DB)
        
Projektleiter-Agent läuft ...
```

### Unteragenten-Orchestrierung

```
Projektleiter-Agent (erklärt Teilaufgaben)
    ↓
ProjektleiterAgentService.SteuereUnteragentAsync()
    ├─ Unteragenten-Verzeichnis erstellen (Dateisystem)
    ├─ Feature-Branch erzeugen (ICliRunner)
    ├─ Repository-Klon erstellen (ICliRunner)
    ├─ UnteragentSpezifikation in DB speichern
    └─ IAgentRuntime.StartAgentAsync() mit Task-Prompt aufrufen
    
Unteragent läuft & speichert Ergebnisse in tasks/task_XXX/
    ↓
task_report.md, task_changes.json, task_log.md
    ↓
Projektleiter liest Ergebnisse
    ↓
ProjektleiterAgentService.IntegriereErgebnisseAsync()
    ├─ plan.md aktualisieren (Dateisystem)
    ├─ progress.md aktualisieren (Dateisystem)
    ├─ state.json aktualisieren (Dateisystem)
    └─ UnteragentSpezifikation.Status → Abgeschlossen (DB)
```

### Session-Management

```
Parallel zur Ausführung:

Token-Monitor:
    ├─ Alle 10 Sekunden: Token-Verbrauch prüfen
    └─ Token-Limit erreicht?
        └─ SessionManagementService.PauseAufgabeBeiBudgetLimitAsync()
            ├─ IAgentRuntime.StopAgent() aufrufen
            ├─ AutonomAufgabeKonfiguration.SessionPauseUtc = now speichern (DB)
            └─ state.json aktualisieren (Dateisystem)

Heartbeat-Monitor:
    ├─ Alle 30 Sekunden: Heartbeat prüfen
    └─ Timeout überschritten?
        └─ SessionManagementService.PruefeAusfuehrungAsync()
            ├─ "Wurdest du unterbrochen?"-Prompt senden
            └─ Bei no-response: Aufgabe beenden (DB)
```

## Skalierung und Zuverlässigkeit

### Parallelisierung

- **Mehrere Unteragenten parallel**: Ja, das System unterstützt parallele Unteragenten-Ausführung
  - Koordination über `state.json` (Single Source of Truth)
  - Jeder Unteragent hat eigenen Scope (tasks/task_XXX/, branch, clone)
  - `AktiveUnteragenten`-Zähler in `AutonomAufgabeKonfiguration` trackst aktive Anzahl
  
- **Token-Sharing**: Token-Budget ist **hart** — wenn erste Unteragent 300k verbraucht und Budget ist 500k, bleibt 200k für nachfolgende
  
- **Branch-Management**: Jeder Unteragent arbeitet auf eigenem Branch → keine Merge-Konflikte zwischen parallelen Tasks

### Fehlertoleranz

- **Session-Pause bei Budget-Limit**: Agent wird sauber pausiert, Zustand wird in state.json & DB persistiert
- **Heartbeat-Überwachung**: Erkennt Agenten-Abstürze oder Netzwerk-Ausfälle
- **Governance-Enforcement**: `UnteragentGovernanceService` verhindert, dass Unteragenten ihre Grenzen überschreiten (Sicherheit)
- **Arbeitsverzeichnis-Struktur**: Falls state.json beschädigt ist, kann es aus DB-Entities und Dateisystem rekonstruiert werden

### Limits & Constraints

| Constraint | Standardwert | Konfigurierbar |
|-----------|--------------|---|
| Token-Budget pro Aufgabe | 500.000 | Ja (ini) |
| Laufzeitlimit | 480 min (8h) | Ja (ini) |
| Max. gleichzeitige Unteragenten | 5 | Ja (ini via permissions.json) |
| Max. Clones | 3 | Ja (hardcoded, könnte konfigurierbar sein) |
| Max. Feature-Branches | 10 | Ja (hardcoded, könnte konfigurierbar sein) |
| Heartbeat-Timeout | 300 sec | Ja (appsettings.json) |
| Arbeitsverzeichnis-Größe | Unbegrenzt | Nein (OS-Limit) |

### Ausfallszenarien

| Szenario | Handling |
|----------|----------|
| **Agent-Prozess stürzt ab** | Heartbeat-Timeout erkennt es, "Wurdest du unterbrochen?"-Prompt wird gesendet; keine Antwort → Beendet |
| **Arbeitsverzeichnis wird gelöscht** | Dateien (plan.md, progress.md, logs) gehen verloren, aber DB-Entities bleiben; Recovery möglich aber manuell |
| **Git-Klon schlägt fehl** | Fehler wird geworfen, Initialisierung bricht ab, Benutzer kann es erneut versuchen |
| **Permissions-Datei beschädigt** | Validierung während Agent-Start schlägt fehl, Fehlerlog wird ausgegeben |
| **Token-Limit wird überschritten** | SessionManagementService pausiert, state.json & DB werden aktualisiert, Benutzer kann fortsetzen |
| **Multiple Instanzen derselben Aufgabe** | Nicht unterstützt — Lock sollte implementiert werden (optional) |

## Integrationspunkte

### Mit bestehendem System

- **`Aufgabe`-Entity**: Neue Properties sind nullable, bestehende Logik bleibt unverändert
- **`AufgabeService`**: Neue Methode `ErzeugeAutonomAufgabeAsync()`, bestehende Methoden unbeeinflusst
- **Heartbeat-System**: `SessionManagementService` nutzt existierendes `LastHeartbeatUtc`, ergänzt nicht konkurrierendes System
- **DbContext**: Neue DbSets & Konfigurationen in `OnModelCreating()`, keine Änderungen an bestehenden Mappings

### Mit externen Systemen

- **Git/CLI**: über `ICliRunner` (Abstraktions-Interface)
- **Agent-Runtime**: über `IAgentRuntime` (Abstraktions-Interface)
- **Dateisystem**: Direkter I/O (Path.Combine, File.WriteAllText, etc.)

## Deployment & Betrieb

- **Feature-Flag**: `AutonomAufgaben.Enabled` in `appsettings.json` ermöglicht Deaktivierung
- **Konfiguration**: Globale Limits in `appsettings.json` können pro Deployment angepasst werden
- **Datenbank-Migrationen**: Drei neue Migrationen müssen angewendet werden
- **Monitoring**: Logs in `logs/agent.log` und `logs/cli.log` in jedem Arbeitsverzeichnis
