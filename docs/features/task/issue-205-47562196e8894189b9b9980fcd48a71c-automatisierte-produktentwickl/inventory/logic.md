# Logik-Services

## `ProjektleiterAgentService`
Datei: `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StarteAgentAsync(konfiguration, ct)` | `public async Task<string>` | Startet den Projektleiter-Agenten mit dem Initialprompt aus der Konfiguration; generiert Skill-Datei; setzt `AusfuehrungsStatus = Aktiv` und `ProjektleiterAgentId`; gibt die neue Agent-ID zurück. **BEOBACHTUNG**: Führt nur DB-Buchhaltung durch, startet keinen echten CLI-Prozess. |
| `SteuereUnteragentAsync(unteragent, ct)` | `public async Task` | Erzeugt und konfiguriert einen Unteragenten: erstellt sein Arbeitsverzeichnis, den Feature-Branch und den Klon; persistiert die Spezifikation. |
| `IntegriereErgebnisseAsync(konfiguration, unteragent, ct)` | `public async Task` | Integriert die Ergebnisse eines abgeschlossenen Unteragenten in plan.md, progress.md und state.json; aktualisiert den UnteragentStatus auf Abgeschlossen. |
| `LadeKonfigurationAsync(autonomAufgabeId, ct)` | `private async Task<AutonomAufgabeKonfiguration>` | Lädt die AutonomAufgabeKonfiguration für die gegebene ID. |
| `PruefeGovernance(unteragent, konfiguration)` | `private void` | Prüft, dass das Arbeitsverzeichnis des Unteragenten innerhalb des erlaubten Bereichs der Autonomen Aufgabe liegt. |
| `PersistiereUnteragentAsync(unteragent, ct)` | `private async Task` | Markiert den Unteragenten als erzeugt und persistiert die Spezifikation. |

**Abhängigkeiten**: Nutzt `UnteragentGovernanceService`, `UnteragentGitProvisioningService`, `SoftwareschmiededDbContext`.

**Fehlende Methoden gemäß Anforderung**: 
- `StarteAgenNachAppNeustartAsync(aufgabeId)` — Automatischer Start beim App-Start mit Resume-Prompt
- `StoppeAgenExplizitAsync(aufgabeId)` — Setzt `ExplizitGestoppt = true` und ruft `KiAusfuehrungsService.StopCliAsync()` auf

## `KiAusfuehrungsService`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartCliAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct, startConfig, gitPlugin)` | `public async Task<CliProcessHandle>` | Startet einen CLI-Prozess für eine Aufgabe ohne Pseudo-Konsole; gibt das Handle zurück. |
| `StartWithPseudoConsoleAsync(aufgabeId, kiPlugin, localRepoPath, optionalParameters, ct, startConfig, gitPlugin)` | `public async Task<CliProcessHandle>` | Startet einen CLI-Prozess über die Windows Pseudo Console API; gibt das Handle zurück. Unterstützt optionale Parameter (z. B. Resume-Prompt). |
| `GetRunningProcess(aufgabeId)` | `public System.Diagnostics.Process?` | Gibt den laufenden Prozess für eine Aufgabe zurück, oder null wenn kein Prozess läuft. |
| `IsRunning(aufgabeId)` | `public bool` | Prüft, ob ein CLI-Prozess für die Aufgabe noch läuft. |
| `GetRunningCount()` | `public int` | Gibt die Anzahl laufender CLI-Prozesse zurück. |
| `GetPseudoConsoleSession(aufgabeId)` | `public PseudoConsoleSession?` | Gibt die PseudoConsoleSession für eine Aufgabe zurück, oder null wenn keine vorhanden. |
| `StopCliAsync(aufgabeId, ct)` | `public async Task` | Stoppt den laufenden CLI-Prozess für eine Aufgabe (SIGTERM → 5s → Kill). |

**Events**:
- `CliProcessStatusChanged` — Wird ausgelöst, wenn ein CLI-Prozess gestartet, gestoppt oder ein Fehler aufgetreten ist. Signatur: `Action<Guid, CliProcessStatus>?`
- `RunningCountChanged` — Wird ausgelöst, wenn sich die Anzahl laufender Prozesse ändert. Signatur: `Action<int, int>?` (nicht dokumentiert; gehört zu `IRunningAutomationStatusSource`)

**Beobachtung**: Die `optionalParameters` werden durch `kiPlugin.StartCliAsync()` verarbeitet. Es ist zu prüfen, ob alle Plugins diese Parameter korrekt an die CLI durchreichen.

## `SessionManagementService`
Datei: `src/Softwareschmiede/Application/Services/SessionManagementService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `PauseAufgabeBeiBudgetLimitAsync(aufgabe, ct)` | `public async Task` | Pausiert die Aufgabe wegen Erreichens des Token-Budgets: setzt `SessionPauseUtc` und aktualisiert state.json. |
| `SetzeFortAsync(aufgabe, ct)` | `public async Task` | Setzt die Aufgabe nach einer Session-Pause fort: generiert einen "Weitermachen"-Prompt; setzt `AusfuehrungsStatus = Aktiv`; löscht `SessionPauseUtc`. |
| `PruefeAusfuehrungAsync(aufgabe, heartbeatTimeout, ct)` | `public async Task<bool>` | Prüft mittels Heartbeat, ob die Ausführung unterbrochen wurde. Ist kein Session-Limit aktiv und der letzte Heartbeat älter als `heartbeatTimeout`, wird ein "Wurdest du unterbrochen?"-Prompt gesendet. |
| `ErstelleWeitermachenPrompt(konfiguration)` | `private static string` | Generiert einen "Weitermachen"-Prompt basierend auf der Konfiguration. |
| `AktualisierePausedUtcInStateJsonAsync(arbeitsverzeichnisPfad, pausedUtc, ct)` | `private async Task` | Aktualisiert `state.json` mit dem Pausierung-Zeitstempel. |

**Abhängigkeiten**: Nutzt `SoftwareschmiededDbContext`.

## `AufgabeRecoveryService`
Datei: `src/Softwareschmiede/Application/Services/AufgabeRecoveryService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScanForRecoveryCandidatesAsync(ct)` | `public async Task<IEnumerable<Guid>>` | Scannt alle Aufgaben nach Recovery-Kandidaten (aktiver Status, Ausführung aktiv, Heartbeat älter als 5 Minuten, kein CLI-Prozess, keine Autonome Aufgabe). |
| `RecoverManuellAsync(aufgabeId, ct)` | `public async Task` | Führt eine manuelle Recovery durch (nur aus `Gestartet` oder `Wartend`-Status). |

**Konstante**:
- `HeartbeatTimeoutMinutes` = 5 — Schwelle für abgelaufene Heartbeats

**Abhängigkeiten**: Nutzt `SoftwareschmiededDbContext`, `IRunningAutomationStatusSource`.

**Beobachtung**: Der Service ignoriert gezielt Autonome Aufgaben (`a.AutonomKonfiguration == null`) in der Recovery-Kandidaten-Abfrage, da Autonome Aufgaben durch den Projektleiter-Agenten selbst gesteuert werden.

## `AutonomAufgabeStartService`
Datei: `src/Softwareschmiede.App/Services/AutonomAufgabeStartService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StarteAsync(aufgabe, ct)` | `public async Task<AutonomAufgabeStartResult?>` | Orchestriert den Ablauf "Autonome Aufgabe initialisieren": öffnet den Initialisierungsdialog, lädt die aktualisierte Aufgabe und zeigt die Detail-Ansicht an; gibt ein `AutonomAufgabeStartResult` oder null (bei Abbruch) zurück. |

**Abhängigkeiten**: Nutzt `IServiceProvider`, `IDialogService`, `AufgabeService`.

**Beobachtung**: Der Service nutzt `ProjektleiterAgentService` und `SessionManagementService` aus dem DI-Container, ohne diese direkt zu injizieren.
