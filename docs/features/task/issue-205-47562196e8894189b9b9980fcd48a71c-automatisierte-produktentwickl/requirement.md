# Anforderung

## Fachliche Zusammenfassung

Das System wird um einen neuen Aufgabentyp "Autonome Aufgabe" erweitert, der eine vollständig automatisierte Projektentwicklung ermöglicht. Eine Autonome Aufgabe wird durch einen Hauptagenten (Projektleiter) gesteuert, der die Gesamtaufgabe in Teilaufgaben zerlegt, Unteragenten erzeugt und steuert, Skills verwaltet und Pull Requests vorbereitet. Das Feature umfasst ein Initialisierungsformular mit Konfigurationsoptionen (Projektbranch, Token-Budget, Laufzeitbegrenzung, Persistenz-Modus, Skill-Autogeneration), ein dediziertes Arbeitsverzeichnis mit strukturierten Unterverzeichnissen für Dokumentation, Repository-Klone, Aufgaben und Logs, eine Governance-Ebene für den Projektleiter mit festdefinierten Erlaubnissen und Grenzen, sowie automatische Mechanismen zur Wiederaufnahme nach Session-Limit-Pausen und Heartbeat-basierte Unterbrechungserkennung.

## Betroffene Klassen und Komponenten

### Datenmodell-Enums und Erweiterungen
- `Softwareschmiede.Domain.Enums.AufgabeAusfuehrungsStatus` (Erweiterung)
  - Neuer Wert: `AutonomAufgabe` — markiert eine Aufgabe als Autonome Aufgabe mit Projektleiter-Modus
  - Bestehende Werte: `NichtGestartet`, `LaufendesAgentenepaket`, etc.

- `Softwareschmiede.Domain.Enums.AufgabeStatus` (Überprüfung)
  - Bestehende Werte müssen auch für Autonome Aufgaben anwendbar sein oder neu definiert werden

### Datenmodell-Entitäten
- `Softwareschmiede.Domain.Entities.Aufgabe` (Erweiterung)
  - Neue Navigationseigenschaft: `AutonomAufgabeKonfiguration? AutonomKonfiguration { get; set; }` — Referenz zur Konfiguration der Autonomen Aufgabe (null für reguläre Aufgaben)
  - Neue Eigenschaft: `string? ProjektleiterAgentId { get; set; }` — ID des Projektleiter-Agenten (für Autonome Aufgaben)
  - Neue Eigenschaft: `DateTimeOffset? SessionPauseUtc { get; set; }` — Zeitstempel der letzten Session-Pause wegen Limits
  - Neue Eigenschaft: `int? AktiveUnteragenten { get; set; }` — Zahl der aktuell aktiven Unteragenten

- Neue Klasse: `Softwareschmiede.Domain.Entities.AutonomAufgabeKonfiguration`
  - `Guid Id { get; set; }` — Eindeutige ID
  - `Guid AufgabeId { get; set; }` — Foreign Key zur Aufgabe
  - `string ProjektBranchName { get; set; }` — Name des dedizierten Projektbranches
  - `string InitialPrompt { get; set; }` — Initialprompt für den Projektleiter
  - `string PermissionsJsonPfad { get; set; }` — Pfad zur permissions.json
  - `int TokenBudget { get; set; }` — Token-Budget für die Gesamtaufgabe
  - `int? TokenBudgetErweitert { get; set; }` — Optionales erweitertes Budget
  - `int LaufzeitLimitMinuten { get; set; }` — Nettozeit-Limit in Minuten
  - `string PersistenzmModus { get; set; }` — Enum-Wert: `Standard`, `SessionReset`, etc.
  - `bool SkillAutogeneration { get; set; }` — Flag: Skills automatisch generieren?
  - `string ArbeitsverzeichnispPfad { get; set; }` — Pfad zum Arbeitsverzeichnis
  - `Aufgabe Aufgabe { get; set; }` — Navigationseigenschaft

- Neue Klasse: `Softwareschmiede.Domain.Entities.UnteragentSpezifikation`
  - `Guid Id { get; set; }` — Eindeutige Unteragenten-ID
  - `Guid AutonomAufgabeId { get; set; }` — Foreign Key zur AutonomAufgabeKonfiguration
  - `string AgentId { get; set; }` — Agent-Identifier
  - `string TaskId { get; set; }` — Task-Identifier
  - `string AgentScope { get; set; }` — Geltungsbereich des Agenten (z.B. "feature-backend", "feature-frontend")
  - `string AgentPrompt { get; set; }` — Task-Prompt für den Agenten
  - `string AgentDirectory { get; set; }` — Pfad zum Agent-Arbeitsbereich (tasks/task_XXX/)
  - `string AgentBranch { get; set; }` — Git-Branch für diesen Agenten
  - `string AgentClone { get; set; }` — Pfad zum Clone für diesen Agenten (clones/repo_feature_X/)
  - `DateTimeOffset ErzeugungsDatum { get; set; }` — Erstellungszeitpunkt
  - `DateTimeOffset? AbschlussDatum { get; set; }` — Abschlusszeitpunkt (null wenn noch aktiv)
  - `string Status { get; set; }` — Enum: `Erzeugt`, `Ausgeführt`, `Abgeschlossen`, `Fehler`
  - `AutonomAufgabeKonfiguration AutonomAufgabe { get; set; }` — Navigationseigenschaft

- Neue Klasse: `Softwareschmiede.Domain.Entities.SkillDefinition`
  - `Guid Id { get; set; }` — Eindeutige ID
  - `Guid AutonomAufgabeId { get; set; }` — Foreign Key
  - `string SkillName { get; set; }` — Name des Skills (z.B. "projektleiter-v1")
  - `string SkillVersion { get; set; }` — Versionsnummer
  - `string SkillContent { get; set; }` — Markdown-Inhalt des Skills
  - `string SkillStatus { get; set; }` — Enum: `Entwurf`, `Review`, `Freigegeben`, `Archiviert`
  - `DateTimeOffset ErstellungsDatum { get; set; }`
  - `DateTimeOffset? FreigabeDatum { get; set; }`
  - `AutonomAufgabeKonfiguration AutonomAufgabe { get; set; }` — Navigationseigenschaft

### Logik-Services
- Neue Klasse: `Softwareschmiede.Application.Services.AutonomAufgabenInitialisierungsService`
  - Methode: `async Task<AutonomAufgabeKonfiguration> InitialisiereAsync(Aufgabe aufgabe, AutonomAufgabeInitialisierungsAnfrage anfrage, CancellationToken ct = default)` — Erstellt das Arbeitsverzeichnis, erzeugt den Repository-Klon, initialisiert state.json
  - Methode: `async Task ErstelleArbeitsverzeichnisStrukturAsync(string arbeitsverzeichnispPfad, CancellationToken ct = default)` — Erstellt die Verzeichnisstruktur mit plan.md, progress.md, state.json, governance.md, permissions.json, skills/, clones/, tasks/, logs/

- Neue Klasse: `Softwareschmiede.Application.Services.ProjektleiterAgentService`
  - Methode: `async Task<string> StarteAgenAsync(AutonomAufgabeKonfiguration konfiguration, CancellationToken ct = default)` — Startet den Projektleiter-Agenten mit InitialPrompt
  - Methode: `async Task SteuereUnteragentAsync(UnteragentSpezifikation unteragent, CancellationToken ct = default)` — Erzeugt und konfiguriert einen Unteragenten
  - Methode: `async Task IntegriereErgebnisseAsync(AutonomAufgabeKonfiguration konfiguration, UnteragentSpezifikation unteragent, CancellationToken ct = default)` — Integriert Unteragenten-Ergebnisse in plan.md, progress.md, state.json

- Neue Klasse: `Softwareschmiede.Application.Services.UnteragentGovernanceService`
  - Methode: `bool VerifiziereBerechtigung(UnteragentSpezifikation unteragent, string aktion, string zielPfad)` — Validiert, dass ein Unteragent nur in seinem eigenen Bereich arbeitet
  - Methode: `async Task ValidiereFehlerBedingungAsync(UnteragentSpezifikation unteragent, CancellationToken ct = default)` — Prüft auf Abbruchbedingungen (Tokenlimit, Rechtsverletzung, Laufzeitüberschreitung)

- Neue Klasse: `Softwareschmiede.Application.Services.SessionManagementService`
  - Methode: `async Task PauseAufgabeBeiBudgetLimitAsync(Aufgabe aufgabe, CancellationToken ct = default)` — Pausiert die Aufgabe und speichert SessionPauseUtc
  - Methode: `async Task SetzeFortAsync(Aufgabe aufgabe, CancellationToken ct = default)` — Setzt die Aufgabe nach Pause fort und sendet "weitermachen"-Prompt
  - Methode: `async Task<bool> PruefeAusfuehrungAsync(Aufgabe aufgabe, TimeSpan heartbeatTimeout, CancellationToken ct = default)` — Prüft mittels Heartbeat, ob die Ausführung unterbrochen wurde

- Erweiterung: `Softwareschmiede.Application.Services.AufgabeService`
  - Neue Methode: `async Task<AutonomAufgabeKonfiguration> ErzeugeAutonomAufgabeAsync(Aufgabe aufgabe, string initialprompt, CancellationToken ct = default)` — Wrapper für Initialisierung einer Autonomen Aufgabe

### UI-Komponenten / ViewModel
- Neue Klasse: `Softwareschmiede.App.ViewModels.AutonomAufgabeInitialisierungsDialogViewModel`
  - Properties für Formularfelder:
    - `string? SelectedProjectBranch { get; set; }`
    - `string InitialPrompt { get; set; }` — Textarea für Initialprompt
    - `PermissionsJsonOption SelectedPermissionsOption { get; set; }` — Enum: `Generate`, `Select`, `Existing`
    - `int TokenBudget { get; set; }`
    - `bool AllowTokenExtension { get; set; }`
    - `int RuntimeLimitMinutes { get; set; }`
    - `string SelectedPersistenceMode { get; set; }` — Enum: `Standard`, `SessionReset`, etc.
    - `bool AutoGenerateSkills { get; set; }`
  - Methode: `async Task BestaetigenAsync()` — Validiert Eingaben und ruft AutonomAufgabenInitialisierungsService auf
  - Methode: `void Abbrechen()` — Schließt den Dialog

- Neue Klasse: `Softwareschmiede.App.ViewModels.AutonomAufgabeDetailViewModel`
  - Properties:
    - `AutonomAufgabeKonfiguration Konfiguration { get; set; }`
    - `List<UnteragentSpezifikation> Unteragenten { get; set; }`
    - `List<SkillDefinition> Skills { get; set; }`
    - `string PlanContent { get; set; }` — Inhalt von plan.md
    - `string ProgressContent { get; set; }` — Inhalt von progress.md
    - `string GovernanceContent { get; set; }` — Inhalt von governance.md
  - Methode: `async Task LaedePlanAsync()` — Lädt plan.md aus dem Arbeitsverzeichnis
  - Methode: `async Task LaedeProgressAsync()` — Lädt progress.md
  - Methode: `async Task AktualisierePlanAsync(string content)` — Speichert plan.md

- Neue View: `Softwareschmiede.App.Views.AutonomAufgabeInitialisierungsDialog.xaml` — Dialog mit Formular
- Neue View: `Softwareschmiede.App.Views.AutonomAufgabeDetailView.xaml` — Detail-Panel für Autonome Aufgaben

### Tests
- Neue Test-Klasse: `Softwareschmiede.Tests.Application.Services.AutonomAufgabenInitialisierungsServiceTests`
  - Test: `InitialisiereAsync_ErzeugtArbeitsverzeichnis()` — Verifiziert Erstellung der Verzeichnisstruktur
  - Test: `InitialisiereAsync_ErzeugtRepositoryKlon()` — Verifiziert Repository-Klon im Unterverzeichnis clones/repo_main/

- Neue Test-Klasse: `Softwareschmiede.Tests.Application.Services.UnteragentGovernanceServiceTests`
  - Test: `VerifiziereBerechtigung_VerbietetAenderungenAusserhalArbeitsbereich()` — Prüft Governance-Enforcement
  - Test: `VerifiziereBerechtigung_VerbietetPullRequestErstellung()` — Prüft PR-Verbot für Unteragenten
  - Test: `VerifiziereBerechtigung_VerbietetSkillModifikation()` — Prüft Skill-Schutzmechanismus

- Neue Test-Klasse: `Softwareschmiede.Tests.Application.Services.SessionManagementServiceTests`
  - Test: `PauseAufgabeBeiBudgetLimit_SetztSessionPauseUtc()` — Verifiziert Pause-Zeitstempel
  - Test: `SetzeFort_SendetWeitermachenPrompt()` — Verifiziert Prompt-Absetzung nach Pause
  - Test: `PruefeAusfuehrung_ErkenntUnterbruch()` — Verifiziert Heartbeat-basierte Unterbrechungserkennung

- Neue E2E-Test-Klasse: `Softwareschmiede.Tests.E2E.E2E_AutonomAufgabenInitialisierung.cs`
  - Test: `CreateAndInitializeAutonomousTask_DisplaysInitializationDialog()` — Full-UI-Test für Dialoganzeige
  - Test: `CompleteInitialization_CreatesWorkingDirectory()` — E2E-Verifizierung der Verzeichniserstellung

## Implementierungsansatz

### 1. Datenbank-Migration
- Neue Tabellen: `AutonomAufgabeKonfigurationen`, `UnteragentSpezifikationen`, `SkillDefinitionen`
- Erweiterungen: `Aufgaben`-Tabelle (neue nullable Spalten für `ProjektleiterAgentId`, `SessionPauseUtc`, `AktiveUnteragenten`)
- Foreign Key: `AutonomAufgabeKonfigurationen.AufgabeId` → `Aufgaben.Id`

### 2. Arbeitsverzeichnis-Struktur
Bei Initialisierung wird unter einem benutzerdefinierten Pfad diese Struktur angelegt:
```
/autonomous-task/
    plan.md                      (Gesamtplan, von Projektleiter verwaltet)
    progress.md                  (Fortschrittsprotokoll, live aktualisiert)
    state.json                   (Maschinenzustand: task_id, branches, clones, agents, budget, etc.)
    governance.md                (Governance-Regeln für Projektleiter)
    permissions.json             (Berechtigungsprofil, unveränderbar durch Projektleiter)
    skills/
        skill_projektleiter_v1.md     (Hauptskill für Projektleiter)
        skill_projektleiter_review.md (Review-Dokumentation)
        archive/
            skill_xyz_v1.md           (Archivierte Skills)
    clones/
        repo_main/                (Hauptklon des Quellrepositories)
        repo_feature_1/           (Feature-Branch-Klone, erstellt vom Projektleiter)
    tasks/
        task_001/                 (Arbeitsbereich des Unteragenten 1)
            task_report.md
            task_changes.json
            task_log.md
        task_002/
    logs/
        cli.log                   (Befehlsprotokoll)
        agent.log                 (Agenten-Aktivitätslog)
```

### 3. State-Management (state.json)
Die `state.json` wird bei Initialisierung mit folgendem Schema erstellt:
- `task_id`: GUID der Aufgabe
- `project_branch`: Projektbranch-Name
- `initial_prompt`: Initialprompt für Projektleiter
- `permissions`: Berechtigungsprofil (Link zur permissions.json)
- `runtime`: Objekt mit `started_utc`, `net_minutes_used`, `net_minutes_limit`, `paused_utc`
- `governance`: Limits (max_subagents, max_clones, max_feature_branches, token_budget)
- `clones`: Array von Clone-Einträgen mit paths und branches
- `subagents`: Array von UnteragentSpezifikation-Objekten
- `skills`: Array mit Skill-Versionen und Status
- `progress`: Objekt mit phase, completion_percentage, last_updated_utc
- `pull_request`: Objekt mit status (z.B. "Geplant", "Vorbereitet", "Erstellt"), url (optional)
- `flags`: Objekt mit `allow_token_extension`, `skip_conpty_tests` (falls zutreffend)

### 4. Projektleiter-Agent-Steuerung
Der Hauptagent (Projektleiter) wird mit folgendem Eingabeformat gestartet:
- Initialprompt aus `AutonomAufgabeKonfiguration.InitialPrompt`
- Arbeitsverzeichnis als Arbeitskontext
- Hauptskill "projektleiter-v1.md" als verfügbare Skill
- Limits aus governance.md (Token, Laufzeit, Unteragenten-Zahl)
- Der Projektleiter kann innerhalb seiner governance-Grenzen:
  - plan.md/progress.md aktualisieren
  - Unteragenten erzeugen (mit Agent-Tool oder CLI-Kommando)
  - Skills definieren und versionieren
  - PRs vorbereiten (aber nicht mergen)

### 5. Unteragenten-Isolation
Jeder Unteragent wird mit einer `UnteragentSpezifikation` konfiguriert:
- Dediziertes Arbeitsverzeichnis (`tasks/task_XXX/`)
- Dedizierter Branch (`feature-unteragent-XXX`)
- Dedizierter Klon (`clones/repo_feature_XXX/`)
- Eingabe: agent_prompt, Skills, Task-Kontext
- Ausgabe: task_report.md, task_changes.json, task_log.md + Commits im Branch
- Governance-Check vor jeder Aktion (Schreiboperation nur im eigenen Bereich)

### 6. Session-Management & Heartbeat
- Bei Erreichung des Token-Budgets: `SessionManagementService.PauseAufgabeBeiBudgetLimitAsync()` speichert `SessionPauseUtc` und pausiert den Projektleiter
- Beim Anwendungsstart: Falls eine Autonome Aufgabe mit `SessionPauseUtc` und nicht erhöhtem Budget vorhanden ist, wird automatisch ein "weitermachen"-Prompt abgesetzt
- Heartbeat-Monitoring: `SessionManagementService.PruefeAusfuehrungAsync()` wird periodisch aufgerufen, um zu prüfen, ob die Ausführung unterbrochen wurde. Falls die Zeit seit dem letzten Heartbeat das Laufzeit-Limit überschreitet oder überschreitet und kein Session-Limit aktiv ist, wird ein "wurdest du unterbrochen?"-Prompt abgesetzt

### 7. Skills-Lifecycle
Der Hauptskill "Projektleiter" durchläuft:
1. **Bedarfserkennung** (bei Initialisierung)
2. **Skill-Entwurf** → skill_projektleiter_v1.md
3. **Review-Simulation** → skill_projektleiter_review.md (optional, intern)
4. **Freigabe** (Status: Freigegeben)
5. **Versionierung** (bei Updates → v2, v3, ...)
6. **Einsatz** (aktiv für Projektleiter-Agenten)
7. **Archivierung** (alte Versionen nach Abschluss)

### 8. Integrationsstrategie
Der Projektleiter integriert Unteragenten-Ergebnisse durch:
- Aufruf von `ProjektleiterAgentService.IntegriereErgebnisseAsync()` nach jedem Unteragenten-Abschluss
- Aktualisierung von plan.md (Teilaufgabenstatus)
- Aktualisierung von progress.md (Fortschritt, Meilensteine, Entscheidungen)
- Aktualisierung von state.json (subagents-Array)
- PR-Vorbereitung durch Sammlung aller Branch-Commits

## Konfiguration

### Initialisierungsformular (AutonomAufgabeInitialisierungsDialogViewModel)
Das System stellt ein Formular mit folgenden Konfigurationsoptionen bereit:
1. **Projektbranch**: Auswahlfeld (oder Textfeld für Neuerstellung) — Bestimmt den Gitbranch für die Gesamtaufgabe
2. **Initialprompt**: Textarea — Der Prompt für den Projektleiter
3. **Permissions**: Auswahlfeld (Generieren / Vorhandene Datei wählen / Vordefiniert) — permissions.json-Quelle
4. **Token-Budget**: Zahleneingabe (Standard: z.B. 500000) — Token für die Gesamtaufgabe
5. **Token-Erweiterung**: Checkbox — Darf der Anwender das Budget erhöhen?
6. **Laufzeitbegrenzung**: Zahleneingabe (Standard: z.B. 480 Minuten / 8 Stunden) — Nettozeit in Minuten
7. **Persistenz-Modus**: Auswahlfeld (Standard, SessionReset) — Verhalten bei Session-Unterbrechungen
8. **Skill-Autogeneration**: Checkbox — Skills automatisch aus Anforderungen generieren?

Die Konfigurationsoptionen werden in `AutonomAufgabeKonfiguration` persistiert und können später über das ViewModel angezeigt/bearbeitet werden.

### permissions.json Schema
```json
{
  "allowed_actions": [
    "read_files",
    "write_files_in_task_dir",
    "git_commit_in_feature_branch",
    "run_tests",
    "create_skill",
    "spawn_subagent",
    "manage_skills"
  ],
  "limits": {
    "max_subagents": 5,
    "max_clones": 3,
    "max_feature_branches": 10,
    "token_budget": 500000,
    "net_runtime_minutes": 480
  },
  "persistence": {
    "mode": "SessionReset",
    "auto_resume": true
  }
}
```

## Offene Fragen

1. **Agent-Spezifikation**: Wie wird der Projektleiter-Agent konkret als Klasse/Typ in der CLI/Runtime modelliert? Nutzt das System existierende Agenten-Infra oder wird eine neue Abstraktionsebene benötigt?

2. **Pull-Request-Erstellung**: Das Feature verbietet automatische Merges und nennt "PR vorbereiten" als Projektleiter-Aufgabe. Wird die PR-Erstellung manuell durch den Anwender ausgelöst oder durch den Projektleiter über ein spezielles Kommando?

3. **Heartbeat-Mechanismus**: Welcher Kanal wird für Heartbeats verwendet? Datenbankeinträge in `Aufgabe.LastHeartbeatUtc`, oder separate Heartbeat-Einträge in einer eigenen Tabelle?

4. **Session-Limit-Kalkulation**: Zählt nur "echte Agentenarbeit" (cli process running) zur Nettozeit, oder auch UI-Interaktion des Projektleiters in der Anwendung?

5. **Unteragenten-Kommunikation**: Wie empfangen Unteragenten die `UnteragentSpezifikation` und ihre Task-Prompts konkret? Über Dateien im Arbeitsverzeichnis, über CLI-Argumente, oder über Datenbankeinträge?

6. **Fehlerbehandlung bei Projektleiter-Abbruch**: Wenn der Projektleiter-Agent selbst abbricht (z.B. bei Unerwarteter Exception), soll das System automatisch versuchen, ihn neu zu starten, oder soll der Anwender manuell eingreifen?

7. **Skill-Autogeneration**: Wenn `SkillAutogeneration == true` ist, welcher Mechanismus wird verwendet, um Skills automatisch aus Anforderungen zu generieren? Ein spezialisierter Service oder ein CLI-Kommando?

8. **Validierung von permissions.json**: Wird die Datei beim Start validiert, oder nur, wenn sie modifiziert wird? Sollte eine Validierungsroutine in einem neuen Service implementiert werden?

9. **Parallelisierung**: Können mehrere Unteragenten gleichzeitig arbeiten, oder arbeiten sie sequenziell? Falls parallel, wie wird die Koordination in plan.md/progress.md gehandhabt?

10. **Abschlussartefakte**: Sollte der Abschlussbericht automatisch aus state.json und progress.md generiert werden, oder wird er vom Projektleiter manuell verfasst?
