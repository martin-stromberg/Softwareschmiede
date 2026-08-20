# Umsetzungsplan: Automatisierte Produktentwicklung mit autonomen Aufgaben

## Übersicht

Das System wird um einen neuen Aufgabentyp „Autonome Aufgabe" erweitert, der eine vollständig automatisierte Projektentwicklung unter Kontrolle eines Projektleiter-Agenten ermöglicht. Der Projektleiter zerlegt Aufgaben in Teilaufgaben, erzeugt und verwaltet Unteragenten, orchestriert Skills und bereitet Pull Requests vor. Die Umsetzung umfasst drei neue Datenmodell-Entitäten, vier neue Service-Klassen, zwei ViewModels und zwei XAML-Views, Enum-Erweiterungen, Entity-Erweiterungen der bestehenden `Aufgabe`-Klasse sowie umfangreiche Unit- und E2E-Tests. Die Implementierung nutzt bestehende Infrastrukturen (Heartbeat-System, Branch-Management, DbContext-Patterns) und integriert sich nahtlos in den Lifecycle der regulären Aufgaben.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Konfigurationspersistierung** | Dedizierte `AutonomAufgabeKonfiguration`-Entity mit 1:1-Beziehung zu `Aufgabe` | Ermöglicht saubere Separation der Autonome-Aufgaben-Konfiguration von regulären Aufgaben; nullable Navigation in `Aufgabe` signalisiert optional Feature; minimale Änderungen an existierender Aufgabe-Logik. |
| **Working Directory State** | `state.json` im Arbeitsverzeichnis als Single Source of Truth für Laufzeitstatus | Ermöglicht Projektleiter-Agent direkten Zugriff ohne Datenbank-Roundtrips; Datei-basierte Struktur passt zu CLI/Agent-Fokus; Redundanz mit DB-Entities aber akzeptabel für Resilienz (Neustart kann state.json neu synchronisieren). |
| **Unteragenten-Isolation** | `UnteragentSpezifikation`-Entity + `UnteragentGovernanceService` für Permission-Checks | Explizite Governance-Enforcement verhindert Unteragenten-Übergriffe; Entity-basierte Spec ermöglicht Audit-Trail und Recovery. |
| **Skill-Management** | Dedizierte `SkillDefinition`-Entity mit Status-Enum (`Entwurf`, `Review`, `Freigegeben`, `Archiviert`) | Versionierung und Lifecycle-Management auf DB-Ebene; ermöglicht Rollback und Audit; Skills als Dokumente mit Content-Feld. |
| **Session Management** | Zusätzliche Felder in `Aufgabe` (`SessionPauseUtc`, `AktiveUnteragenten`) + `SessionManagementService` | Minimale Erweiterung existierender Entity; Service nutzt bestehende Heartbeat-Infrastruktur. |
| **Enum-Erweiterung** | Neuer Wert `AutonomAufgabe` in `AufgabeAusfuehrungsStatus` zur Markierung des Betriebsmodus | Bestehender Enum für Ausführungsmodus; `AutonomAufgabe` signalisiert Projektleiter-Steuerung vs. direkter Agentensteuerung. |
| **Repository-Klone** | Pfade in `AutonomAufgabeKonfiguration` und `UnteragentSpezifikation`; Klone unter `clones/`-Subverzeichnis | Nutzt bestehende Clone-Management-Patterns; Hierarchie separiert Hauptklon von Feature-Klonen. |
| **Governance-Durchsetzung** | Imperativer Service (`UnteragentGovernanceService`) statt deklarativer Policies | Passt zu existierenden Service-Patterns; explizite Methoden für Berechtigungsprüfung vor kritischen Operationen. |

---

## Programmabläufe

### Initialisierung einer Autonomen Aufgabe

1. Benutzer startet Initialisierungsdialog für eine reguläre Aufgabe oder neue Aufgabe
2. ViewModel (`AutonomAufgabeInitialisierungsDialogViewModel`) zeigt Formular mit: Projektbranch, Initialprompt, Permissions-Quelle, Token-Budget, Laufzeitbegrenzung, Persistenz-Modus, Skill-Autogeneration
3. Benutzer füllt Formular aus und klickt „Bestätigen"
4. ViewModel validiert Eingaben und ruft `AufgabeService.ErzeugeAutonomAufgabeAsync(aufgabe, initialprompt)` auf
5. `AufgabeService` erstellt `AutonomAufgabeKonfiguration`-Instanz und delegiert an `AutonomAufgabenInitialisierungsService.InitialisiereAsync()`
6. `AutonomAufgabenInitialisierungsService` führt aus:
   - Erstellt Arbeitsverzeichnis-Struktur via `ErstelleArbeitsverzeichnisStrukturAsync()`
   - Klont Repository in `clones/repo_main/`
   - Generiert `state.json` mit Initialkonfiguration
   - Erstellt `permissions.json` (generiert oder kopiert)
   - Erstellt Basis-`plan.md` und `progress.md`
   - Erstellt `governance.md`
7. `AutonomAufgabeKonfiguration` wird in Datenbank persistiert
8. Aufgabe-Entity wird auf `AusfuehrungsStatus = AutonomAufgabe` gesetzt
9. Dialog schließt, Aufgabe zeigt Detail-View mit Kontroll-Schaltflächen (Start, Stop, Resume)

Beteiligte Klassen/Komponenten: `AutonomAufgabeInitialisierungsDialogViewModel`, `AutonomAufgabenInitialisierungsService`, `AufgabeService`, `SoftwareschmiededDbContext`

### Start des Projektleiter-Agenten

1. Benutzer klickt „Start" in der Autonome-Aufgabe-Detail-View
2. ViewModel ruft `ProjektleiterAgentService.StarteAgenAsync(konfiguration)` auf
3. `ProjektleiterAgentService` liest Konfiguration und state.json
4. Lädt Projektleiter-Skill aus `skills/skill_projektleiter_v1.md`
5. Erzeugt Agenten mit:
   - InitialPrompt aus `AutonomAufgabeKonfiguration.InitialPrompt`
   - Arbeitsverzeichnis als Kontext
   - Skill-Registry als verfügbare Skills
   - Governance-Limits aus `governance.md`
6. Agent wird gestartet (CLI oder Agent SDK)
7. Aufgabe-Entity: `AusfuehrungsStatus` → `Aktiv`, `AktiveRunId` wird gesetzt

Beteiligte Klassen/Komponenten: `ProjektleiterAgentService`, `AufgabeService`, Dateisystem-Zugriff für state.json

### Erstellung und Steuerung eines Unteragenten

1. Projektleiter-Agent erkennt Teilaufgabe und ruft (intern oder via CLI-Kommando) `ProjektleiterAgentService.SteuereUnteragentAsync(unteragentSpec)` auf
2. Service erstellt Arbeitsverzeichnis `tasks/task_XXX/`
3. Erstellt Feature-Branch `feature-unteragent-XXX`
4. Klont Repository in `clones/repo_feature_XXX/`
5. Erstellt `UnteragentSpezifikation`-Entity mit:
   - AgentId, TaskId, AgentScope, AgentPrompt
   - Pfade zu Directory, Branch, Clone
   - Status = `Erzeugt`, ErzeugungsDatum = jetzt
6. Persistiert Spezifikation in Datenbank
7. Startet Unteragenten mit:
   - Task-Prompt aus `UnteragentSpezifikation.AgentPrompt`
   - Arbeitsverzeichnis `tasks/task_XXX/`
   - Governance-Checks via `UnteragentGovernanceService` (Isolationsbereich, max Operationen)
8. Unteragent führt Aufgabe aus, speichert Ergebnisse in `task_report.md`, `task_changes.json`, `task_log.md`
9. Agent committet zu Feature-Branch

Beteiligte Klassen/Komponenten: `ProjektleiterAgentService`, `UnteragentGovernanceService`, Dateisystem, Git-CLI/Bibliothek

### Integrationn von Unteragenten-Ergebnissen

1. Unteragent signalisiert Abschluss (via state.json oder direkter Rückruf)
2. Projektleiter-Agent oder Projektleiter-Service ruft `ProjektleiterAgentService.IntegriereErgebnisseAsync(konfiguration, unteragent)` auf
3. Service:
   - Liest `task_report.md`, `task_changes.json` aus Unteragenten-Verzeichnis
   - Aktualisiert `plan.md` mit Status der Teilaufgabe
   - Aktualisiert `progress.md` mit Fortschritt, Meilensteine, Entscheidungen
   - Aktualisiert `state.json`: subagents-Array, completed tasks
   - Aktualisiert `UnteragentSpezifikation` in DB: Status → `Abgeschlossen`, AbschlussDatum = jetzt
4. Bei PR-Vorbereitung: sammelt alle Branch-Commits und bereitet PR-Beschreibung vor (keine automatischen Merges)

Beteiligte Klassen/Komponenten: `ProjektleiterAgentService`, Dateisystem, Git-CLI

### Session-Pause bei Budget-Limit

1. Während Projektleiter-Agent läuft, überwacht `SessionManagementService` Token-Verbrauch und Laufzeitbudget
2. Bei Erreichen des Token-Limits:
   - `SessionManagementService.PauseAufgabeBeiBudgetLimitAsync(aufgabe)` wird aufgerufen
   - Aufgabe-Entity: `SessionPauseUtc` = jetzt, `AusfuehrungsStatus` → `Beendet` (oder `Wartend` je nach Kontext)
   - state.json wird aktualisiert: `runtime.paused_utc = now`
   - Projektleiter-Agent wird unterbrochen (Halt-Signal)
3. Benutzer kann später den Agent mit erweitertem Budget neu starten (falls `AllowTokenExtension` true)

Beteiligte Klassen/Komponenten: `SessionManagementService`, `AufgabeService`

### Wiederaufnahme nach Session-Pause

1. Benutzer klickt „Resume" in Detail-View (oder automatisch beim App-Start, falls `auto_resume = true`)
2. ViewModel ruft `SessionManagementService.SetzeFortAsync(aufgabe)` auf
3. Service:
   - Liest `SessionPauseUtc` aus Aufgabe-Entity
   - Generiert "Weitermachen"-Prompt mit Kontext aus state.json, plan.md, progress.md
   - Setzt `SessionPauseUtc` → null, `AusfuehrungsStatus` → `Aktiv`
   - Startet Projektleiter-Agent neu mit Weiterführungs-Prompt
4. Agent setzt Arbeit fort

Beteiligte Klassen/Komponenten: `SessionManagementService`, `ProjektleiterAgentService`, Dateisystem

### Heartbeat-Überwachung und Unterbrechungserkennung

1. Während Projekt läuft, wird in regelmäßigen Intervallen (z. B. alle 30 Sekunden) `SessionManagementService.PruefeAusfuehrungAsync(aufgabe, heartbeatTimeout)` aufgerufen
2. Service prüft:
   - `Aufgabe.LastHeartbeatUtc`: Zeit seit letztem Heartbeat
   - Falls Zeit > `heartbeatTimeout` UND kein Session-Limit aktiv: Agente könnte unterbrochen sein
3. Falls Verdacht auf Unterbrechung:
   - Generiert "Wurdest du unterbrochen?"-Prompt
   - Sendet Prompt an Projektleiter-Agent
   - Agent antwortet (bestätigt Weiterlauf oder warnt vor Fehler)
4. Falls Agent antwortet nicht: `AusfuehrungsStatus` → `Beendet`, Fehlerlog

Beteiligte Klassen/Komponenten: `SessionManagementService`, `AufgabeService`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AutonomAufgabeKonfiguration` | Datenmodellklasse (Entity) | Persistiert Konfiguration einer Autonomen Aufgabe (Branch, Token-Budget, Laufzeitlimit, Arbeitsverzeichnis, etc.) |
| `UnteragentSpezifikation` | Datenmodellklasse (Entity) | Persistiert Metadaten eines Unteragenten (ID, Scope, Branch, Pfade, Status, Erstellungs-/Abschlussdatum) |
| `SkillDefinition` | Datenmodellklasse (Entity) | Persistiert Skill-Definitionen mit Versionierung und Lifecycle-Status (`Entwurf`, `Review`, `Freigegeben`, `Archiviert`) |
| `AutonomAufgabenInitialisierungsService` | Service-Klasse | Orchestriert Erstellung des Arbeitsverzeichnisses, Repository-Klons und Initialisierung von state.json |
| `ProjektleiterAgentService` | Service-Klasse | Verwaltet Projektleiter-Agent-Lifecycle, Unteragenten-Erzeugung und Integrationt von Ergebnissen |
| `UnteragentGovernanceService` | Service-Klasse | Erzwingt Governance-Regeln: Isolationsbereich, Permission-Checks, Fehlervalidierung |
| `SessionManagementService` | Service-Klasse | Verwaltet Session-Pause/Resume, Token-Budget, Heartbeat-Monitoring, Unterbrechungserkennung |
| `AutonomAufgabeInitialisierungsDialogViewModel` | ViewModel (WPF) | Bindung für Initialisierungsdialog: Projektbranch, Initialprompt, Permissions, Token-Budget, Laufzeitlimit, Persistenz-Modus, Skill-Autogeneration |
| `AutonomAufgabeDetailViewModel` | ViewModel (WPF) | Bindung für Detail-View: Konfiguration anzeigen, plan.md/progress.md/governance.md laden, Start/Stop/Resume-Kontrollen |
| `AutonomAufgabeInitialisierungsDialog` | XAML-View | Dialogfenster mit Formular für Konfiguration |
| `AutonomAufgabeDetailView` | XAML-View | Detail-Panel mit Anzeige und Steuerung einer Autonomen Aufgabe |

---

## Änderungen an bestehenden Klassen

### `Aufgabe` (Entity)

- **Neue Eigenschaften:**
  - `AutonomAufgabeKonfiguration? AutonomKonfiguration { get; set; }` — Navigation zu Autonome-Aufgaben-Konfiguration (null für reguläre Aufgaben)
  - `string? ProjektleiterAgentId { get; set; }` — ID des aktuell laufenden Projektleiter-Agenten
  - `DateTimeOffset? SessionPauseUtc { get; set; }` — Zeitstempel der letzten Session-Pause wegen Budget-Limit
  - `int? AktiveUnteragenten { get; set; }` — Anzahl aktuell aktiver Unteragenten (für UI-Status)

### `AufgabeAusfuehrungsStatus` (Enum)

- **Neue Enum-Werte:**
  - `AutonomAufgabe` — Markiert eine Aufgabe als Autonome Aufgabe mit Projektleiter-Agent-Modus (zusätzlich zu existierenden `NichtGestartet`, `Aktiv`, `Beendet`)

### `AufgabeService` (Service)

- **Neue Methoden:**
  - `async Task<AutonomAufgabeKonfiguration> ErzeugeAutonomAufgabeAsync(Aufgabe aufgabe, string initialprompt, CancellationToken ct = default)` — Wrapper-Methode, die Initialisierung einer Autonomen Aufgabe orchestriert (delegiert an `AutonomAufgabenInitialisierungsService`)

### `SoftwareschmiededDbContext` (DbContext)

- **Neue Registrierungen:**
  - `DbSet<AutonomAufgabeKonfiguration> AutonomAufgabeKonfigurationen { get; set; }`
  - `DbSet<UnteragentSpezifikation> UnteragentSpezifikationen { get; set; }`
  - `DbSet<SkillDefinition> SkillDefinitionen { get; set; }`
- **Neue Konfigurationen in `OnModelCreating`:**
  - 1:1-Beziehung zwischen `Aufgabe` und `AutonomAufgabeKonfiguration` (Foreign Key `AufgabeId`)
  - 1:N-Beziehung zwischen `AutonomAufgabeKonfiguration` und `UnteragentSpezifikation`
  - 1:N-Beziehung zwischen `AutonomAufgabeKonfiguration` und `SkillDefinition`

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddAutonomAufgabeModels` | Neue Tabellen: `AutonomAufgabeKonfigurationen`, `UnteragentSpezifikationen`, `SkillDefinitionen` | Erstellt drei neue Tabellen für Autonome Aufgaben, Unteragenten und Skills |
| `AddAutonomAufgabeColumnsToAufgaben` | Tabelle `Aufgaben`: neue Spalten `AutonomKonfigurationId` (FK), `ProjektleiterAgentId`, `SessionPauseUtc`, `AktiveUnteragenten` | Erweitert `Aufgaben`-Tabelle mit Foreign Key zu Konfiguration und neuen Tracking-Feldern |
| `AddAutonomAufgabeToAusfuehrungsStatus` | Enum-Spalte `AusfuehrungsStatus` in `Aufgaben` | Neuer Enum-Wert `AutonomAufgabe` (Datenmigration nicht erforderlich, nur Enum-Definition) |

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AutonomAufgabeKonfiguration.ProjektBranchName` | Nicht leer, gültiger Git-Branch-Name | `ArgumentException` |
| `AutonomAufgabeKonfiguration.InitialPrompt` | Nicht leer, min. 10 Zeichen | `ArgumentException` |
| `AutonomAufgabeKonfiguration.TokenBudget` | > 0, max. 5.000.000 | `ArgumentException` |
| `AutonomAufgabeKonfiguration.LaufzeitLimitMinuten` | > 0, min. 60, max. 1440 (24h) | `ArgumentException` |
| `AutonomAufgabeKonfiguration.ArbeitsverzeichnispPfad` | Absolute Pfad, Verzeichnis muss erstellbar sein | `DirectoryAccessException`, `ArgumentException` |
| `UnteragentSpezifikation.AgentScope` | Nicht leer, eindeutig in `AutonomAufgabe` | `ArgumentException` |
| `UnteragentSpezifikation.AgentBranch` | Gültiger Git-Branch-Name | `ArgumentException` |
| `permissions.json` (Datei) | Datei vorhanden, gültiges JSON, erforderliche Properties: `allowed_actions`, `limits` | `FileNotFoundException`, `JsonException` |
| `state.json` (Datei) | Nach Initialisierung: gültiges JSON mit erforderlichen Top-Level-Keys (`task_id`, `runtime`, `governance`, `clones`, `subagents`) | `JsonException` |
| Unteragenten-Dateizugriff | Nur in `tasks/task_XXX/` erlaubt (via `UnteragentGovernanceService.VerifiziereBerechtigung`) | Datei-Schreiboperation abgebrochen, Fehlerlog |

---

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `AutonomAufgaben:Enabled` | bool | `true` | Feature-Flag zum Aktivieren/Deaktivieren von Autonomen Aufgaben |
| `AutonomAufgaben:DefaultTokenBudget` | int | 500000 | Standardbudget für neue Autonome Aufgaben |
| `AutonomAufgaben:DefaultRuntimeLimitMinutes` | int | 480 | Standard-Laufzeitlimit (8 Stunden) |
| `AutonomAufgaben:WorkingDirectoryBase` | string | `{AppData}/AutonomAufgaben` | Basis-Verzeichnis für Arbeitsverzeichnisse der Autonomen Aufgaben |
| `AutonomAufgaben:HeartbeatTimeoutSeconds` | int | 300 | Timeout in Sekunden für Heartbeat-Unterbrechungserkennung |
| `AutonomAufgaben:MaxConcurrentSubagents` | int | 5 | Maximale Anzahl gleichzeitig laufender Unteragenten pro Autonome Aufgabe |
| `AutonomAufgaben:SkillAutoGenerationEnabled` | bool | `false` | Standard für automatische Skill-Generierung |

---

## Seiteneffekte und Risiken

- **Heartbeat-System:** `SessionManagementService` ergänzt bestehende Heartbeat-Logik mit zusätzlichen Prüfungen; bestehende `UpdateHeartbeatAsync`-Aufrufe sind nicht betroffen, aber neue Heartbeat-Semantik (Unterbrechungserkennung) wird parallel aufgebaut. Tests müssen verifizieren, dass reguläre Aufgaben keine Seiteneffekte sehen.

- **`AufgabeService.StartenAsync` und Zustandsübergänge:** Neue Aufgaben-Properties könnten von bestehenden Methoden gelesen werden; jedoch sind sie alle nullable und haben sinnvolle Defaults (null = keine Autonome Aufgabe). Bestehende Tests sollten unbeeinflusst bleiben, da sie reguläre Aufgaben testen.

- **DbContext-Initialisierung:** Neue Entities müssen im `OnModelCreating` registriert werden; Migrations müssen in korrekter Reihenfolge ausgeführt werden. Risk: Datenbank-Versioning und Rollback-Szenarien müssen berücksichtigt werden.

- **Arbeitsverzeichnis-Dateien:** `state.json`, `plan.md`, `progress.md` sind externe Artefakte. Synchronisation zwischen DB-Entities und Dateisystem ist kritisch; fehlende Synchronisation kann zu Inkonsistenzen führen (z. B. wenn Datei gelöscht wird). Mitigierung: Migrationen und Recovery-Logik implementieren.

- **Agent-Lifecycle:** Projektleiter und Unteragenten sind extern verwaltete Prozesse (CLI-Befehle oder Agent-SDK-Aufrufe). Fehlerbehandlung und Timeout-Management sind kritisch. Keine Änderung an bestehender Aufgaben-Verwaltung, aber neue Service-Abhängigkeiten auf CLI/Agent-Infra.

- **Session-Pause & Resume:** Neue Properties `SessionPauseUtc` in `Aufgabe` signalisieren Paused-Zustand; bestehende Logik, die nur auf `Status` und `AusfuehrungsStatus` prüft, sieht Pause nicht. Risk: ViewModel/UI muss explizit auf `SessionPauseUtc` checken.

- **Governance-Enforcement:** `UnteragentGovernanceService` wertet absolute Pfade aus; Sicherheitsrisiko, falls Pfad-Normalisierung nicht robust ist (z. B. symlinks, `..`-Navigation). Mitigierung: Strenge Pfad-Validierung und Canonicalization.

---

## Umsetzungsreihenfolge

### Phase 1: Datenmodell-Grundlage

1. **Enum-Wert `AutonomAufgabe` zu `AufgabeAusfuehrungsStatus` hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: Öffne `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`, füge neuen Wert `AutonomAufgabe` am Ende ein

2. **Entity `AutonomAufgabeKonfiguration` erstellen**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Klasse in `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs` mit allen Properties gemäß Anforderung (Id, AufgabeId, ProjektBranchName, InitialPrompt, PermissionsJsonPfad, TokenBudget, TokenBudgetErweitert, LaufzeitLimitMinuten, PersistenzmModus, SkillAutogeneration, ArbeitsverzeichnispPfad, Navigation `Aufgabe`)

3. **Entity `UnteragentSpezifikation` erstellen**
   - Voraussetzungen: `AutonomAufgabeKonfiguration` muss existieren
   - Beschreibung: Neue Klasse in `src/Softwareschmiede/Domain/Entities/UnteragentSpezifikation.cs` mit allen Properties gemäß Anforderung (Id, AutonomAufgabeId, AgentId, TaskId, AgentScope, AgentPrompt, AgentDirectory, AgentBranch, AgentClone, ErzeugungsDatum, AbschlussDatum, Status, Navigation `AutonomAufgabe`)

4. **Entity `SkillDefinition` erstellen**
   - Voraussetzungen: `AutonomAufgabeKonfiguration` muss existieren
   - Beschreibung: Neue Klasse in `src/Softwareschmiede/Domain/Entities/SkillDefinition.cs` mit allen Properties gemäß Anforderung (Id, AutonomAufgabeId, SkillName, SkillVersion, SkillContent, SkillStatus, ErstellungsDatum, FreigabeDatum, Navigation `AutonomAufgabe`)

5. **Entity `Aufgabe` mit neuen Properties erweitern**
   - Voraussetzungen: `AutonomAufgabeKonfiguration` muss existieren
   - Beschreibung: Öffne `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`, füge vier neue nullable Properties hinzu: `AutonomAufgabeKonfiguration? AutonomKonfiguration`, `string? ProjektleiterAgentId`, `DateTimeOffset? SessionPauseUtc`, `int? AktiveUnteragenten`

6. **DbContext konfigurieren**
   - Voraussetzungen: Alle neuen Entities müssen existieren, `Aufgabe` muss erweitert sein
   - Beschreibung: Öffne `src/Softwareschmiede/Infrastructure/Persistence/SoftwareschmiededDbContext.cs`, registriere drei neue `DbSet`s und konfiguriere Beziehungen in `OnModelCreating`: 1:1-Beziehung zwischen `Aufgabe` und `AutonomAufgabeKonfiguration` mit `OnDelete(DeleteBehavior.Cascade)`, 1:N-Beziehungen für Unteragenten und Skills

7. **Datenbank-Migrationen erstellen**
   - Voraussetzungen: DbContext-Konfiguration muss abgeschlossen sein
   - Beschreibung: Füge drei Migrationen ein: (a) AddAutonomAufgabeModels (neue Tabellen), (b) AddAutonomAufgabeColumnsToAufgaben (neue Spalten in `Aufgaben`), (c) UpdateAusfuehrungsStatusEnum (Enum-Erweiterung, falls EF Core dies separate Migration erfordert)

### Phase 2: Core-Services

8. **Service `AutonomAufgabenInitialisierungsService` implementieren**
   - Voraussetzungen: Alle Entities müssen existieren, DbContext muss konfiguriert sein, Migrationen sollten ausgeführt sein (oder zumindest in Quellcode vorhanden)
   - Beschreibung: Neue Klasse in `src/Softwareschmiede/Application/Services/AutonomAufgabenInitialisierungsService.cs`. Implementiere:
     - `InitialisiereAsync(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage, CancellationToken ct)` — Hauptmethode, die Arbeitsverzeichnis, Repository-Klon und Initialisierung koordiniert
     - `ErstelleArbeitsverzeichnisStrukturAsync(string arbeitsverzeichnispPfad, CancellationToken ct)` — Erstellt Verzeichnisstruktur und Initialdateien (plan.md, progress.md, state.json, governance.md, permissions.json, Subdirectories)
     - Helper-Methoden für Repository-Kloning (wenn nicht bereits in anderer Klasse vorhanden)

9. **Service `UnteragentGovernanceService` implementieren**
   - Voraussetzungen: `UnteragentSpezifikation` muss existieren
   - Beschreibung: Neue Klasse in `src/Softwareschmiede/Application/Services/UnteragentGovernanceService.cs`. Implementiere:
     - `VerifiziereBerechtigung(UnteragentSpezifikation unteragent, string aktion, string zielPfad)` — Prüft, ob Aktion im Scope des Unteragenten erlaubt ist
     - `ValidiereFehlerBedingungAsync(UnteragentSpezifikation unteragent, CancellationToken ct)` — Prüft Abbruchbedingungen (Tokenlimit, Laufzeitüberschreitung, etc.)

10. **Service `SessionManagementService` implementieren**
    - Voraussetzungen: `AufgabeService` muss existieren, `Aufgabe` muss neue Properties haben
    - Beschreibung: Neue Klasse in `src/Softwareschmiede/Application/Services/SessionManagementService.cs`. Implementiere:
      - `PauseAufgabeBeiBudgetLimitAsync(Aufgabe aufgabe, CancellationToken ct)` — Pausiert Aufgabe, setzt `SessionPauseUtc`, aktualisiert state.json
      - `SetzeFortAsync(Aufgabe aufgabe, CancellationToken ct)` — Setzt Aufgabe fort, generiert "Weitermachen"-Prompt
      - `PruefeAusfuehrungAsync(Aufgabe aufgabe, TimeSpan heartbeatTimeout, CancellationToken ct)` — Heartbeat-basierte Unterbrechungserkennung

### Phase 3: Agent-Management

11. **Service `ProjektleiterAgentService` implementieren**
    - Voraussetzungen: `AutonomAufgabenInitialisierungsService`, `UnteragentGovernanceService`, `SessionManagementService` müssen existieren
    - Beschreibung: Neue Klasse in `src/Softwareschmiede/Application/Services/ProjektleiterAgentService.cs`. Implementiere:
      - `StarteAgenAsync(AutonomAufgabeKonfiguration konfiguration, CancellationToken ct)` — Startet Projektleiter-Agent mit Initialprompt und Skills
      - `SteuereUnteragentAsync(UnteragentSpezifikation unteragent, CancellationToken ct)` — Erzeugt und konfiguriert Unteragenten
      - `IntegriereErgebnisseAsync(AutonomAufgabeKonfiguration konfiguration, UnteragentSpezifikation unteragent, CancellationToken ct)` — Integriert Ergebnisse in plan.md, progress.md, state.json

12. **Methode `ErzeugeAutonomAufgabeAsync` zu `AufgabeService` hinzufügen**
    - Voraussetzungen: `AutonomAufgabenInitialisierungsService` muss existieren, `AufgabeService` muss vorhanden sein
    - Beschreibung: Öffne `src/Softwareschmiede/Application/Services/AufgabeService.cs`, füge Methode hinzu:
      ```
      async Task<AutonomAufgabeKonfiguration> ErzeugeAutonomAufgabeAsync(
          Aufgabe aufgabe, 
          string initialprompt, 
          CancellationToken ct = default)
      ```
      Delegiert an `AutonomAufgabenInitialisierungsService.InitialisiereAsync()`

### Phase 4: UI — ViewModels

13. **ViewModel `AutonomAufgabeInitialisierungsDialogViewModel` implementieren**
    - Voraussetzungen: `AufgabeService`, `AutonomAufgabenInitialisierungsService` müssen existieren
    - Beschreibung: Neue Klasse in `src/Softwareschmiede/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModel.cs` (WPF). Implementiere:
      - Properties für Formularfelder: `SelectedProjectBranch`, `InitialPrompt`, `SelectedPermissionsOption`, `TokenBudget`, `AllowTokenExtension`, `RuntimeLimitMinutes`, `SelectedPersistenceMode`, `AutoGenerateSkills`
      - `BestaetigenAsync()` — Validiert, ruft `AufgabeService.ErzeugeAutonomAufgabeAsync()` auf
      - `Abbrechen()` — Schließt Dialog

14. **ViewModel `AutonomAufgabeDetailViewModel` implementieren**
    - Voraussetzungen: `AufgabeService`, `ProjektleiterAgentService`, `SessionManagementService` müssen existieren
    - Beschreibung: Neue Klasse in `src/Softwareschmiede/App/ViewModels/AutonomAufgabeDetailViewModel.cs` (WPF). Implementiere:
      - Properties: `Konfiguration`, `Unteragenten`, `Skills`, `PlanContent`, `ProgressContent`, `GovernanceContent`
      - `LaedePlanAsync()`, `LaedeProgressAsync()`, `LaedeGovernanceAsync()` — Laden Dateien aus Arbeitsverzeichnis
      - `AktualisierePlanAsync(string content)` — Speichert Änderungen
      - `StarteAgenAsync()`, `StoppeAgenAsync()`, `ResumeAgenAsync()` — Kontroll-Methoden

### Phase 5: UI — XAML-Views

15. **XAML-View `AutonomAufgabeInitialisierungsDialog.xaml` erstellen**
    - Voraussetzungen: ViewModel muss existieren
    - Beschreibung: Neue XAML-Datei in `src/Softwareschmiede/App/Views/AutonomAufgabeInitialisierungsDialog.xaml`. Layout:
      - Gruppierte Eingabefelder (Projektbranch-Auswahlfeld oder Textfeld)
      - Textarea für InitialPrompt
      - Combobox für Permissions-Quelle
      - NumberBox für Token-Budget und Laufzeitlimit
      - Checkboxes für Token-Erweiterung und Skill-Autogeneration
      - Buttons: Bestätigen, Abbrechen
      - Validierungsfehlermeldungen

16. **XAML-View `AutonomAufgabeDetailView.xaml` erstellen**
    - Voraussetzungen: ViewModel muss existieren
    - Beschreibung: Neue XAML-Datei in `src/Softwareschmiede/App/Views/AutonomAufgabeDetailView.xaml`. Layout:
      - Tabbed Interface mit Tabs: „Konfiguration", „Plan", „Fortschritt", „Governance", „Skills", „Unteragenten"
      - Konfiguration-Tab: Read-only Anzeige der Einstellungen
      - Plan/Fortschritt/Governance-Tabs: TextBox mit Datei-Inhalt (optional editierbar)
      - Unteragenten-Tab: DataGrid oder ListBox mit Status, Dates
      - Control-Buttons: Start, Stop, Resume
      - Status-Anzeige: aktive Unteragenten, Budget-Verbrauch, Laufzeit

### Phase 6: Tests — Unit-Tests

17. **Test-Klasse `AutonomAufgabenInitialisierungsServiceTests` erstellen**
    - Voraussetzungen: `AutonomAufgabenInitialisierungsService` muss implementiert sein, Test-Infrastruktur (Mocks, Fixtures) muss vorhanden sein
    - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/Application/Services/AutonomAufgabenInitialisierungsServiceTests.cs`. Tests:
      - `InitialisiereAsync_ErzeugtArbeitsverzeichnis()` — Verifiziert Verzeichnisstruktur
      - `InitialisiereAsync_ErzeugtRepositoryKlon()` — Verifiziert Repository-Klon
      - `InitialisiereAsync_ErzeugtStateJson()` — Verifiziert state.json-Erstellung
      - `InitialisiereAsync_ErzeugtPermissionsJson()` — Verifiziert permissions.json

18. **Test-Klasse `UnteragentGovernanceServiceTests` erstellen**
    - Voraussetzungen: `UnteragentGovernanceService` muss implementiert sein
    - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/Application/Services/UnteragentGovernanceServiceTests.cs`. Tests:
      - `VerifiziereBerechtigung_ErlaubtZugriffAufEigenenBereich()` — Positiv-Test
      - `VerifiziereBerechtigung_VerbietetAenderungenAusserhalbArbeitsbereich()` — Negativ-Test
      - `VerifiziereBerechtigung_VerbietetPullRequestErstellung()` — PR-Verbot
      - `VerifiziereBerechtigung_VerbietetSkillModifikation()` — Skill-Schutz

19. **Test-Klasse `SessionManagementServiceTests` erstellen**
    - Voraussetzungen: `SessionManagementService` muss implementiert sein
    - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/Application/Services/SessionManagementServiceTests.cs`. Tests:
      - `PauseAufgabeBeiBudgetLimit_SetztSessionPauseUtc()` — Verifiziert Pause-Zeitstempel
      - `PauseAufgabeBeiBudgetLimit_AktualisieertStateJson()` — Verifiziert state.json-Update
      - `SetzeFort_SendetWeitermachenPrompt()` — Verifiziert Prompt-Generierung
      - `PruefeAusfuehrung_ErkenntUnterbruch()` — Verifiziert Heartbeat-Erkennung

20. **Test-Klasse `ProjektleiterAgentServiceTests` erstellen**
    - Voraussetzungen: `ProjektleiterAgentService` muss implementiert sein, andere Services müssen existieren
    - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/Application/Services/ProjektleiterAgentServiceTests.cs`. Tests:
      - `StarteAgenAsync_StartetAgentMitInitialprompt()` — Verifiziert Agent-Start
      - `SteuereUnteragentAsync_ErzeugtUnteragentSpezifikation()` — Verifiziert Unteragenten-Erzeugung
      - `IntegriereErgebnisseAsync_AktualisieertPlanMdUndProgressMd()` — Verifiziert Integrationn

21. **Test-Klasse `AutonomAufgabeInitialisierungsDialogViewModelTests` erstellen**
    - Voraussetzungen: ViewModel muss existieren, Test-Infrastruktur für MVVM (ICommand-Mocks, etc.)
    - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeInitialisierungsDialogViewModelTests.cs`. Tests:
      - `BestaetigenAsync_ValidatesInputsAndCallsService()` — Verifiziert Validierung und Service-Aufruf
      - `BestaetigenAsync_FailsOnInvalidTokenBudget()` — Negativ-Test
      - `Abbrechen_ClosesDialog()` — Verifiziert Abbruch

22. **Test-Klasse `AutonomAufgabeDetailViewModelTests` erstellen**
    - Voraussetzungen: ViewModel muss existieren
    - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/App/ViewModels/AutonomAufgabeDetailViewModelTests.cs`. Tests:
      - `LaedePlanAsync_LaedesDateiausArbeitsverzeichnis()` — Verifiziert Datei-Laden
      - `StarteAgenAsync_CallsProjektleiterAgentService()` — Verifiziert Agent-Start
      - `AktualisierePlanAsync_SpeichertAenderungen()` — Verifiziert Datei-Speicherung

### Phase 7: Tests — E2E-Tests

23. **E2E-Test-Klasse `E2E_AutonomAufgabenInitialisierung` erstellen**
    - Voraussetzungen: ViewModel und View müssen existieren, E2E-Test-Infrastruktur (WpfTestBase, etc.) muss vorhanden sein
    - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenInitialisierung.cs`. Tests:
      - `CreateAutonomousTask_DisplaysInitializationDialog()` — Full-UI-Test für Dialog-Anzeige (benutzerdefinierten Projekt, Auswahl Menu, Dialog öffnen)
      - `CompleteInitialization_CreatesWorkingDirectory()` — E2E-Verifizierung Verzeichniserstellung (Dialog ausfüllen, Bestätigen, Dateisystem-Prüfung)
      - `DetailView_DisplaysConfiguration()` — E2E-Verifizierung Detail-View-Anzeige nach Initialisierung

24. **E2E-Test-Klasse `E2E_AutonomAufgabenAgentExecution` erstellen**
    - Voraussetzungen: E2E-Infrastruktur, Services müssen funktional sein
    - Beschreibung: Neue Klasse in `src/Softwareschmiede.Tests/E2E/E2E_AutonomAufgabenAgentExecution.cs`. Tests:
      - `StartProjectManagerAgent_AgentIsRunning()` — E2E-Verifizierung Agent-Start (Dialog ausfüllen, Bestätigen, Start-Button klicken, UI-Status zeigt aktive Agenten)
      - `CreateSubagent_SubagentSpecificationIsPersisted()` — E2E-Verifizierung Unteragenten-Erzeugung
      - `SessionPause_PausesProjectManager()` — E2E-Verifizierung Session-Pause bei Budget-Limit

### Phase 8: Integration und Feinschliff

25. **Integration mit bestehender `AufgabeService` testen**
    - Voraussetzungen: Alle Services müssen implementiert sein
    - Beschreibung: Führe bestehende `AufgabeService`-Tests aus, verifiziere, dass keine Regressions entstanden sind. Falls Tests fehlschlagen: debugge und behebe Integrationsprobleme.

26. **Konfigurationseinträge hinzufügen und testen**
    - Voraussetzungen: `appsettings.json` oder Konfigurationsklasse muss vorhanden sein
    - Beschreibung: Öffne Konfigurationsdatei (`appsettings.json` oder `AppSettings.cs`), füge Einträge gemäß Tabelle hinzu. Verifiziere, dass Konfiguration korrekt geladen wird (Unit-Test mit Mock-IConfiguration).

27. **Migrationen überprüfen und ggf. korrigieren**
    - Voraussetzungen: Alle Migrationen müssen erstellt sein
    - Beschreibung: Führe `dotnet ef database update` aus, verifiziere, dass Datenbank-Schema korrekt ist. Falls Fehler: behebe Migration-Skripte.

28. **Dokumentation aktualisieren**
    - Voraussetzungen: Alle Implementierungen müssen abgeschlossen sein
    - Beschreibung: Schreibe Dokumentation für:
      - Neue Services (Zweck, öffentliche API)
      - Neue Entities (Beziehungen, Constraints)
      - Arbeitsverzeichnis-Struktur und state.json-Schema
      - Governance-Regeln und Permissions
      - Benutzer-Dokumentation für UI (Initialisierungsdialog, Detail-View)

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `InitialisiereAsync_ErzeugtArbeitsverzeichnis` | `AutonomAufgabenInitialisierungsServiceTests` | Verzeichnisstruktur (plan.md, progress.md, state.json, governance.md, permissions.json, skills/, clones/, tasks/, logs/) wird erstellt |
| `InitialisiereAsync_ErzeugtRepositoryKlon` | `AutonomAufgabenInitialisierungsServiceTests` | Repository-Klon im `clones/repo_main/`-Verzeichnis vorhanden |
| `InitialisiereAsync_ErzeugtStateJson` | `AutonomAufgabenInitialisierungsServiceTests` | state.json mit korrektem Schema und Initialwerten vorhanden |
| `InitialisiereAsync_ErzeugtPermissionsJson` | `AutonomAufgabenInitialisierungsServiceTests` | permissions.json mit Berechtigungen und Limits vorhanden |
| `VerifiziereBerechtigung_ErlaubtZugriffAufEigenenBereich` | `UnteragentGovernanceServiceTests` | Positiv-Fall: Zugriff auf `tasks/task_XXX/` ist erlaubt |
| `VerifiziereBerechtigung_VerbietetAenderungenAusserhalbArbeitsbereich` | `UnteragentGovernanceServiceTests` | Negativ-Fall: Zugriff auf `clones/` ist verboten |
| `VerifiziereBerechtigung_VerbietetPullRequestErstellung` | `UnteragentGovernanceServiceTests` | Negativ-Fall: PR-Erstellung ist verboten für Unteragenten |
| `VerifiziereBerechtigung_VerbietetSkillModifikation` | `UnteragentGovernanceServiceTests` | Negativ-Fall: Skill-Modifikation ist verboten |
| `ValidiereFehlerBedingungAsync_ErkenntTokenLimitVerletzung` | `UnteragentGovernanceServiceTests` | Abbruchbedingung: Token-Limit überschritten wird erkannt |
| `PauseAufgabeBeiBudgetLimit_SetztSessionPauseUtc` | `SessionManagementServiceTests` | Aufgabe-Entity.SessionPauseUtc wird gesetzt |
| `PauseAufgabeBeiBudgetLimit_AktualisieertStateJson` | `SessionManagementServiceTests` | state.json.runtime.paused_utc wird aktualisiert |
| `SetzeFort_SendetWeitermachenPrompt` | `SessionManagementServiceTests` | "Weitermachen"-Prompt wird generiert und an Agent gesendet |
| `PruefeAusfuehrung_ErkenntUnterbruch` | `SessionManagementServiceTests` | Heartbeat-Timeout wird erkannt, "Wurdest du unterbrochen?"-Prompt wird gesendet |
| `StarteAgenAsync_StartetAgentMitInitialprompt` | `ProjektleiterAgentServiceTests` | Projektleiter-Agent wird mit InitialPrompt gestartet |
| `SteuereUnteragentAsync_ErzeugtUnteragentSpezifikation` | `ProjektleiterAgentServiceTests` | UnteragentSpezifikation wird in DB persistiert, Dateien erstellt |
| `IntegriereErgebnisseAsync_AktualisieertPlanMdUndProgressMd` | `ProjektleiterAgentServiceTests` | plan.md und progress.md werden mit Unteragenten-Ergebnissen aktualisiert |
| `BestaetigenAsync_ValidatesInputsAndCallsService` | `AutonomAufgabeInitialisierungsDialogViewModelTests` | Validierung läuft, Service wird aufgerufen, Dialog schließt |
| `BestaetigenAsync_FailsOnInvalidTokenBudget` | `AutonomAufgabeInitialisierungsDialogViewModelTests` | Validierungsfehler für ungültiges Token-Budget |
| `Abbrechen_ClosesDialog` | `AutonomAufgabeInitialisierungsDialogViewModelTests` | Dialog wird geschlossen ohne Service-Aufruf |
| `LaedePlanAsync_LaedesDateiausArbeitsverzeichnis` | `AutonomAufgabeDetailViewModelTests` | plan.md wird aus Dateisystem geladen |
| `StarteAgenAsync_CallsProjektleiterAgentService` | `AutonomAufgabeDetailViewModelTests` | ProjektleiterAgentService.StarteAgenAsync wird aufgerufen |
| `AktualisierePlanAsync_SpeichertAenderungen` | `AutonomAufgabeDetailViewModelTests` | Änderungen werden in plan.md persistiert |
| `CreateAutonomousTask_DisplaysInitializationDialog` | `E2E_AutonomAufgabenInitialisierung` | Initialisierungsdialog wird angezeigt (UI-Element vorhanden, Formularfelder sichtbar) |
| `CompleteInitialization_CreatesWorkingDirectory` | `E2E_AutonomAufgabenInitialisierung` | Nach Bestätigung: Arbeitsverzeichnis mit Struktur vorhanden, Dateien existieren |
| `DetailView_DisplaysConfiguration` | `E2E_AutonomAufgabenInitialisierung` | Nach Initialisierung: Detail-View zeigt Konfiguration korrekt |
| `StartProjectManagerAgent_AgentIsRunning` | `E2E_AutonomAufgabenAgentExecution` | Projektleiter-Agent wird gestartet, UI zeigt aktiven Status |
| `CreateSubagent_SubagentSpecificationIsPersisted` | `E2E_AutonomAufgabenAgentExecution` | Unteragenten-Erstellung erstellt DB-Eintrag und Dateien |
| `SessionPause_PausesProjectManager` | `E2E_AutonomAufgabenAgentExecution` | Bei Budget-Limit wird Agent pausiert, UI zeigt Pause-Status |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AufgabeServiceTests` (alle bestehenden Tests) | Potenzielle Integrationsprobleme: neue Properties in `Aufgabe` (nullable) sollten nicht zu Fehlern führen, aber Assertions auf Aufgabe-Instanzen müssen verifizieren, dass neue Properties null sind. Tests, die `CreateAsync`, `UpdateAsync`, `SetStatusAsync` testen, sollten mit regulären (nicht-autonomen) Aufgaben durchgeführt werden. |
| Alle bestehenden E2E-Tests (z. B. `E2E_TaskManagement`, `E2E_TaskExecution`) | Keine direkte Anpassung erforderlich, solange diese Tests reguläre Aufgaben verwenden. Jedoch: Heartbeat-Logik wird durch `SessionManagementService` ergänzt; bestehende Tests mit Heartbeat-Calls sollten verifizieren, dass keine Seiteneffekte entstehen. |

Falls keine regressiven Fehler entstehen, sind die Tests unverändert ausführbar.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| **Initialisierungs-Dialog Anzeige** | `E2E_AutonomAufgabenInitialisierung` | Dialog ist sichtbar, alle Formularfelder sind vorhanden und ausgefüllt werden können |
| **Arbeitsverzeichnis-Erstellung** | `E2E_AutonomAufgabenInitialisierung` | Nach Initialisierung existiert Verzeichnis mit korrekter Struktur (plan.md, progress.md, state.json, governance.md, permissions.json, subdirectories) |
| **Repository-Klon** | `E2E_AutonomAufgabenInitialisierung` | Repository wird in `clones/repo_main/` geklont |
| **Detail-View Anzeige** | `E2E_AutonomAufgabenInitialisierung` | Nach Initialisierung zeigt Detail-View Konfiguration, plan.md, progress.md, governance.md korrekt an |
| **Projektleiter-Agent Start** | `E2E_AutonomAufgabenAgentExecution` | Agent wird gestartet, UI zeigt Status „Läuft" oder ähnlich, process/agent vorhanden |
| **Unteragenten-Erstellung** | `E2E_AutonomAufgabenAgentExecution` | Unteragent wird erzeugt: Verzeichnis `tasks/task_XXX/` vorhanden, UnteragentSpezifikation in DB, Branch `feature-unteragent-XXX` existiert |
| **Session-Pause bei Budget-Limit** | `E2E_AutonomAufgabenAgentExecution` | Bei erreichtem Token-Limit wird Agent pausiert, UI zeigt Pause-Status, SessionPauseUtc gesetzt, state.json aktualisiert |
| **Resume nach Pause** | `E2E_AutonomAufgabenAgentExecution` | Nach Resume wird Agent mit Weiterführungs-Prompt neu gestartet, Arbeit setzt sich fort |
| **Heartbeat-Unterbrechungserkennung** | `E2E_AutonomAufgabenAgentExecution` | Wenn Agent keine Heartbeats sendet, wird "Wurdest du unterbrochen?"-Prompt gesendet |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine — bestehende E2E-Tests verwenden reguläre Aufgaben und sind nicht betroffen. | — |

Falls Heartbeat-Tests bestehen, müssen diese verifizieren, dass neue Session-Management-Logik keine Seiteneffekte auf reguläre Aufgaben hat.

---

## Offene Punkte

Keine — alle Anforderungen und Detailfragen wurden durch die Anforderung, Bestandsaufnahme und Designdiskussionen geklärt. Die Implementierung kann unmittelbar mit Phase 1 beginnen.

---

## Notizen zur Implementierung

### Kritische Abhängigkeiten

1. **DbContext-Konfiguration vor Services:** DbContext muss vollständig konfiguriert sein, bevor Services die neuen Entities verwenden.
2. **Migrationen vor Tests:** Datenbank-Schema muss migriert sein, bevor Unit-Tests mit echten DbContext-Instanzen laufen.
3. **Agent-Lifecycle-Integration:** `ProjektleiterAgentService` und `SessionManagementService` müssen mit existierender Agent-Infrastruktur (CLI oder Agent SDK) abgestimmt sein. Sicherstellen, dass Prozess-Management (Start, Stop, Pause) mit bestehenden APIs kompatibel ist.
4. **Dateisystem-Zugriffe:** `AutonomAufgabenInitialisierungsService` und `ProjektleiterAgentService` führen Dateisystem-Operationen durch; Fehlerbehandlung und Pfad-Validierung sind kritisch.

### Bekannte Risiken und Mitigationen

1. **Datenbank-Inkonsistenz:** Synchronisation zwischen DB-Entities und Arbeitsverzeichnis-Dateien kann auseinanderlaufen. **Mitigierung:** Implementiere Recovery-Logik, die state.json zur Basis der Wahrheit macht.
2. **Agent-Prozess-Zombie:** Projektleiter-Agent könnte sich aufhängen. **Mitigierung:** Timeout-Mechanismen und Signal-Handler für graceful shutdown.
3. **Pfad-Injection:** Unteragenten-Governance könnte durch Pfad-Traversal-Attacken umgangen werden. **Mitigierung:** Strikte Pfad-Normalisierung und Whitelist der erlaubten Verzeichnisse.
4. **Heartbeat-False-Positives:** Legitimale, längere Pausen könnten als Unterbruch interpretiert werden. **Mitigierung:** Konfigurierbare Heartbeat-Timeout-Werte und Logging für Debuggung.

### Testing-Strategie

- **Unit-Tests:** Alle Services mit Mocks für Datenbankzugriff, Dateisystem, Agent-Infrastruktur.
- **Integration-Tests:** Services mit echtem DbContext (in-memory Database), Dateisystem-Operationen mit Temp-Verzeichnissen.
- **E2E-Tests:** Full-Stack mit UI, Datenbank, Dateisystem, verifyifying complete workflows.
- **Regression-Tests:** Bestehende `AufgabeService`-Tests müssen grün bleiben.
