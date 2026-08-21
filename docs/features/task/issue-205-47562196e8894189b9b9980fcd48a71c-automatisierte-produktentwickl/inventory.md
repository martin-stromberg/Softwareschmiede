# Bestandsaufnahme: Autonome Aufgaben mit Projektleiter-Agent

Bestandsaufnahme des Projektcodes bezogen auf die Anforderung zur Implementierung von Autonomen Aufgaben (Automated Task Orchestration mit Projektleiter-Agent, Unteragenten-Governance, Session-Management und Skills-Lifecycle).

## Zusammenfassung

### Was ist vorhanden:
- **`Aufgabe` Entity:** Grundstruktur für Aufgaben mit Status-Management, Heartbeat-Tracking und Branch-Verwaltung existiert bereits
- **`AufgabeService`:** Umfangreicher Service für Aufgaben-Lebenszyklusverwaltung (CRUD, Status-Übergänge, Heartbeat, aktive Läufe)
- **Enums:** `AufgabeStatus` mit 5 Werten (Neu, Gestartet, Wartend, Beendet, Archiviert) ist vorhanden; `AufgabeAusfuehrungsStatus` mit 3 Werten existiert
- **DbContext:** `SoftwareschmiededDbContext` registriert existierende Entities; neue Entities sind nicht registriert
- **Tests:** Umfangreiche Test-Abdeckung für `AufgabeService` (mehrere Testklassen) und andere Services vorhanden

### Was fehlt vollständig:
- **Drei neue Datenmodell-Entitäten:** `AutonomAufgabeKonfiguration`, `UnteragentSpezifikation`, `SkillDefinition`
- **Vier neue Service-Klassen:** `AutonomAufgabenInitialisierungsService`, `ProjektleiterAgentService`, `UnteragentGovernanceService`, `SessionManagementService`
- **Zwei neue ViewModels:** `AutonomAufgabeInitialisierungsDialogViewModel`, `AutonomAufgabeDetailViewModel`
- **Zwei neue XAML-Views:** `AutonomAufgabeInitialisierungsDialog.xaml`, `AutonomAufgabeDetailView.xaml`
- **Enum-Erweiterung:** Neuer Wert `AutonomAufgabe` in `AufgabeAusfuehrungsStatus` fehlt
- **Entity-Erweiterung:** Neue Properties in `Aufgabe` (AutonomKonfiguration, ProjektleiterAgentId, SessionPauseUtc, AktiveUnteragenten) fehlen
- **Neue Test-Klassen:** Acht Test-Klassen für Autonome Aufgaben-Feature sind nicht vorhanden

### Kritische Dependencies:
- `AufgabeService` muss erweitert werden um Methode `ErzeugeAutonomAufgabeAsync()`
- DbContext muss neue Entities und deren Konfigurationen registrieren
- Migrationen sind erforderlich für die neuen Tabellen

### Besonderheiten:
- Der Enum-Wert `AufgabeStatus.Wartend` könnte für Session-Pause-Status der Autonomen Aufgaben wiederverwendet werden
- Bestehende Heartbeat-Infrastruktur (`LastHeartbeatUtc`, `UpdateHeartbeatAsync`) kann als Basis für Session-Management genutzt werden
- Branch- und Klonpfad-Management in `Aufgabe` existiert bereits und kann für Repository-Klone der Autonomen Aufgaben genutzt werden

---

## Details

### [Datenmodelle](inventory/models.md)
- **`Aufgabe`** — Existiert; Erweiterung erforderlich
- **`AutonomAufgabeKonfiguration`** — Neu zu implementieren
- **`UnteragentSpezifikation`** — Neu zu implementieren
- **`SkillDefinition`** — Neu zu implementieren

### [Logik-Services](inventory/logic.md)
- **`AufgabeService`** — Existiert; eine neue Methode erforderlich
- **`AutonomAufgabenInitialisierungsService`** — Neu zu implementieren
- **`ProjektleiterAgentService`** — Neu zu implementieren
- **`UnteragentGovernanceService`** — Neu zu implementieren
- **`SessionManagementService`** — Neu zu implementieren

### [Enums](inventory/enums.md)
- **`AufgabeAusfuehrungsStatus`** — Existiert; Erweiterung um `AutonomAufgabe` erforderlich
- **`AufgabeStatus`** — Existiert; Überprüfung auf Anwendbarkeit erforderlich

---

## Abhängigkeits-Zusammenfassung

### Existing Infrastructure, die wiederverwendet werden kann:
1. **`Aufgabe` Entity** — Basis für Autonome Aufgaben-Integration
2. **`AufgabeService`** — Bestehende CRUD- und Lebenszyklusmethoden
3. **Heartbeat-System** — `LastHeartbeatUtc`, `UpdateHeartbeatAsync`, `GetHeartbeatAgeMinutesAsync`
4. **Branch & Clone Management** — `BranchName`, `LokalerKlonPfad`
5. **DbContext Infrastruktur** — Etabliertes Pattern für Entity-Konfiguration

### Neue Dependencies erforderlich:
1. **Repository-Kloning-Service** — Für Hauptklon und Feature-Branch-Klone
2. **Arbeitsverzeichnis-Manager** — Für Verzeichnisstruktur-Erstellung
3. **Agent-Lifecycle-Manager** — Für Projektleiter und Unteragenten
4. **Skill-Registry** — Für Skill-Verwaltung und Versionierung
5. **Token-Budget-Manager** — Für Budget-Tracking und Limits
6. **Governance-Enforcer** — Für Unteragenten-Isolation und Permission-Checks

---

## Implementierungs-Reihenfolge (empfohlen)

1. **Phase 1: Datenmodell-Grundlage**
   - Neue Entities (`AutonomAufgabeKonfiguration`, `UnteragentSpezifikation`, `SkillDefinition`)
   - Enum-Erweiterung (`AufgabeAusfuehrungsStatus.AutonomAufgabe`)
   - Entity-Erweiterung (`Aufgabe`-Properties)
   - DbContext-Registrierung

2. **Phase 2: Core-Services**
   - `AutonomAufgabenInitialisierungsService`
   - `SessionManagementService`
   - `UnteragentGovernanceService`

3. **Phase 3: Agent-Management**
   - `ProjektleiterAgentService`
   - `AufgabeService.ErzeugeAutonomAufgabeAsync()`

4. **Phase 4: UI**
   - ViewModels
   - XAML Views

5. **Phase 5: Tests**
   - Unit-Tests für Services
   - E2E-Tests für UI-Flows
