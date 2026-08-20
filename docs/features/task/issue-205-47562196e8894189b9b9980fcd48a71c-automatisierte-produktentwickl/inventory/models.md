# Datenmodelle — Bestandsaufnahme

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

**Status der Anforderung:** Erweiterung erwartet, aber noch nicht implementiert.

### Existierende Properties:
| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe. |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts. |
| `GitRepositoryId` | `Guid?` | Optionale ID des verknüpften Git-Repositories. |
| `Titel` | `string` | Titel der Aufgabe. |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für den KI-Agenten. |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe. |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter Status der KI-Ausführung. |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe. |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories. |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets. |
| `AgentenName` | `string?` | Name des verwendeten Agenten. |
| `KiPluginPrefix` | `string?` | Prefix des für diese Aufgabe verwendeten KI-Plugins. |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe. |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum (null wenn noch nicht abgeschlossen). |
| `AktiveRunId` | `string?` | Optional: Aktive Lauf-ID einer KI-Ausführung. |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Optional: Zeitstempel des letzten Heartbeats. |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Optional: Zeitstempel des letzten echten CLI-Prozessstarts. |
| `LaufStatus` | `AufgabeLaufStatus?` | Optional: Laufzeit-Substatus der aktiven Ausführung. |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-relevante Statusänderungen. |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für den nächsten Prompt. |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt für den nächsten Prompt. |

### Navigationseigenschaften:
| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Projekt` | `Projekt` | Übergeordnetes Projekt. |
| `GitRepository` | `GitRepository?` | Verknüpftes Git-Repository. |
| `IssueReferenz` | `IssueReferenz?` | Verknüpfte Issue-Referenz. |
| `AlertReferenz` | `AlertReferenz?` | Verknüpfte Alert-Referenz. |
| `PullRequests` | `List<PullRequestReferenz>` | Verknüpfte Pull Requests. |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge des KI-Prozesses. |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für diese Aufgabe. |
| `Todos` | `List<Todo>` | To-Do-Elemente dieser Aufgabe. |

### Fehlende Properties (aus Anforderung):
- `AutonomAufgabeKonfiguration? AutonomKonfiguration` — Navigationseigenschaft zur Konfiguration der Autonomen Aufgabe
- `string? ProjektleiterAgentId` — ID des Projektleiter-Agenten
- `DateTimeOffset? SessionPauseUtc` — Zeitstempel der letzten Session-Pause wegen Limits
- `int? AktiveUnteragenten` — Zahl der aktuell aktiven Unteragenten

---

## `AutonomAufgabeKonfiguration`
**Status der Anforderung:** Entity ist nicht implementiert.

Diese neue Entity-Klasse ist komplett zu erstellen. Erforderliche Properties gemäß Anforderung:
- `Guid Id` — Eindeutige ID
- `Guid AufgabeId` — Foreign Key zur Aufgabe
- `string ProjektBranchName` — Name des dedizierten Projektbranches
- `string InitialPrompt` — Initialprompt für den Projektleiter
- `string PermissionsJsonPfad` — Pfad zur permissions.json
- `int TokenBudget` — Token-Budget für die Gesamtaufgabe
- `int? TokenBudgetErweitert` — Optionales erweitertes Budget
- `int LaufzeitLimitMinuten` — Nettozeit-Limit in Minuten
- `string PersistenzmModus` — Enum-Wert: `Standard`, `SessionReset`, etc.
- `bool SkillAutogeneration` — Flag: Skills automatisch generieren?
- `string ArbeitsverzeichnispPfad` — Pfad zum Arbeitsverzeichnis
- `Aufgabe Aufgabe` — Navigationseigenschaft

---

## `UnteragentSpezifikation`
**Status der Anforderung:** Entity ist nicht implementiert.

Diese neue Entity-Klasse ist komplett zu erstellen. Erforderliche Properties gemäß Anforderung:
- `Guid Id` — Eindeutige Unteragenten-ID
- `Guid AutonomAufgabeId` — Foreign Key zur AutonomAufgabeKonfiguration
- `string AgentId` — Agent-Identifier
- `string TaskId` — Task-Identifier
- `string AgentScope` — Geltungsbereich des Agenten (z.B. "feature-backend")
- `string AgentPrompt` — Task-Prompt für den Agenten
- `string AgentDirectory` — Pfad zum Agent-Arbeitsbereich (tasks/task_XXX/)
- `string AgentBranch` — Git-Branch für diesen Agenten
- `string AgentClone` — Pfad zum Clone für diesen Agenten (clones/repo_feature_X/)
- `DateTimeOffset ErzeugungsDatum` — Erstellungszeitpunkt
- `DateTimeOffset? AbschlussDatum` — Abschlusszeitpunkt (null wenn noch aktiv)
- `string Status` — Enum: `Erzeugt`, `Ausgeführt`, `Abgeschlossen`, `Fehler`
- `AutonomAufgabeKonfiguration AutonomAufgabe` — Navigationseigenschaft

---

## `SkillDefinition`
**Status der Anforderung:** Entity ist nicht implementiert.

Diese neue Entity-Klasse ist komplett zu erstellen. Erforderliche Properties gemäß Anforderung:
- `Guid Id` — Eindeutige ID
- `Guid AutonomAufgabeId` — Foreign Key
- `string SkillName` — Name des Skills (z.B. "projektleiter-v1")
- `string SkillVersion` — Versionsnummer
- `string SkillContent` — Markdown-Inhalt des Skills
- `string SkillStatus` — Enum: `Entwurf`, `Review`, `Freigegeben`, `Archiviert`
- `DateTimeOffset ErstellungsDatum` — Erstellungszeitpunkt
- `DateTimeOffset? FreigabeDatum` — Freigabezeitpunkt
- `AutonomAufgabeKonfiguration AutonomAufgabe` — Navigationseigenschaft
