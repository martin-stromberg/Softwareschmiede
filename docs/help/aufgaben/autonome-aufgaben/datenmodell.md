← [Zurück zur Übersicht](index.md)

# Autonome Aufgaben — Datenmodell

## Entitäten

### `Aufgabe` (erweitert)

Bestehende Entity mit neuen Properties für Autonome Aufgaben.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | Guid | Eindeutige ID (bestehend) |
| `ProjektId` | Guid | Projekt-Fremdschlüssel (bestehend) |
| `Titel` | string | Aufgabentitel (bestehend) |
| `Status` | AufgabeStatus | Aufgaben-Status: Geplant, Gestartet, Wartend, Abgeschlossen (bestehend) |
| `AusfuehrungsStatus` | AufgabeAusfuehrungsStatus | KI-Ausführungsstatus: NichtGestartet, Aktiv, Beendet, **AutonomAufgabe** (erweitert) |
| `ProjektleiterAgentId` | string? | ID des aktuell laufenden Projektleiter-Agenten (neu) |
| `SessionPauseUtc` | DateTimeOffset? | Zeitstempel der letzten Session-Pause wegen Budget-Limit (neu) |
| `AktiveUnteragenten` | int? | Anzahl aktuell aktiver Unteragenten (neu) |
| `AutonomKonfiguration` | AutonomAufgabeKonfiguration? | Navigation zur Konfiguration (neu, 1:1) |
| ... | ... | Weitere bestehende Properties... |

### `AutonomAufgabeKonfiguration` (neu)

Konfiguration und Persistierung einer Autonomen Aufgabe.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | Guid | Eindeutige ID |
| `AufgabeId` | Guid | Foreign Key zu `Aufgabe` (1:1) |
| `ProjektBranchName` | string | Name des dedizierten Projektbranches |
| `InitialPrompt` | string | Initialprompt für den Projektleiter-Agenten |
| `PermissionsJsonPfad` | string | Absoluter Pfad zur `permissions.json` |
| `TokenBudget` | int | Token-Budget für die Gesamtaufgabe |
| `TokenBudgetErweitert` | int? | Optionales erweitertes Budget nach Pause |
| `LaufzeitLimitMinuten` | int | Nettozeit-Limit in Minuten |
| `PersistenzModus` | PersistenzModus | `Standard` oder `SessionReset` |
| `SkillAutogeneration` | bool | Sollen Skills automatisch generiert werden? |
| `ArbeitsverzeichnisPfad` | string | Absoluter Pfad zum Arbeitsverzeichnis |
| `Aufgabe` | Aufgabe | Navigation (inverse 1:1) |
| `Unteragenten` | List<UnteragentSpezifikation> | Navigation (1:N) |
| `Skills` | List<SkillDefinition> | Navigation (1:N) |

**Constraints:**
- `ProjektBranchName`: Nicht null, gültiger Git-Branch-Name
- `InitialPrompt`: Nicht null, ≥ 10 Zeichen
- `TokenBudget`: > 0, ≤ 5.000.000
- `LaufzeitLimitMinuten`: ∈ [60..1440]
- `ArbeitsverzeichnisPfad`: Absoluter Pfad, muss erstellbar sein
- `PermissionsJsonPfad`: Datei muss vorhanden sein

### `UnteragentSpezifikation` (neu)

Metadaten eines von einem Projektleiter-Agenten erzeugten Unteragenten.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | Guid | Eindeutige Unteragenten-ID |
| `AutonomAufgabeId` | Guid | Foreign Key zu `AutonomAufgabeKonfiguration` (1:N) |
| `AgentId` | string | Agent-Identifier (z.B. "subagent-001") |
| `TaskId` | string | Task-Identifier (z.B. "task-001") |
| `AgentScope` | string | Geltungsbereich (z.B. "feature-backend", "feature-frontend") |
| `AgentPrompt` | string | Task-Prompt für den Agenten |
| `AgentDirectory` | string | Relativer Pfad zum Agent-Arbeitsbereich (z.B. "tasks/task_001") |
| `AgentBranch` | string | Git-Branch für diesen Agenten (z.B. "feature-unteragent-001") |
| `AgentClone` | string | Relativer Pfad zum Clone (z.B. "clones/repo_feature_001") |
| `ErzeugungsDatum` | DateTimeOffset | Zeitstempel der Erzeugung |
| `AbschlussDatum` | DateTimeOffset? | Abschlusszeitpunkt (null wenn noch aktiv) |
| `Status` | UnteragentStatus | `Erzeugt`, `Ausgeführt`, `Abgeschlossen`, `Fehler` |
| `AutonomAufgabe` | AutonomAufgabeKonfiguration | Navigation (inverse 1:N) |

**Constraints:**
- `AgentScope`: Nicht null, eindeutig pro `AutonomAufgabeId`
- `AgentBranch`: Gültiger Git-Branch-Name
- `Status`: Enum-Wert, keine null

### `SkillDefinition` (neu)

Versionierte Skill-Definition für Projektleiter oder Unteragenten.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | Guid | Eindeutige ID |
| `AutonomAufgabeId` | Guid | Foreign Key zu `AutonomAufgabeKonfiguration` (1:N) |
| `SkillName` | string | Name des Skills (z.B. "projektleiter-v1") |
| `SkillVersion` | string | Versionsnummer (z.B. "1.0.0", "1.0.1") |
| `SkillContent` | string | Markdown-Inhalt des Skills |
| `SkillStatus` | SkillStatus | `Entwurf`, `Review`, `Freigegeben`, `Archiviert` |
| `ErstellungsDatum` | DateTimeOffset | Zeitstempel der Erstellung |
| `FreigabeDatum` | DateTimeOffset? | Freigabezeitpunkt (null wenn nicht freigegeben) |
| `AutonomAufgabe` | AutonomAufgabeKonfiguration | Navigation (inverse 1:N) |

**Constraints:**
- `SkillName`: Nicht null, eindeutig pro `AutonomAufgabeId`
- `SkillVersion`: Nicht null
- `SkillContent`: Nicht null (kann leer sein, aber nicht null)
- `SkillStatus`: Enum-Wert

## Beziehungen

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│  Aufgabe (bestehend) ◀──── 1:1 ────► AutonomAufgabeKonfiguration (neu)
│  └─ AufgabeId                        └─ AufgabeId (FK)
│     AusfuehrungsStatus                  └─ ArbeitsverzeichnisPfad
│     (= AutonomAufgabe)                  └─ InitialPrompt
│     ProjektleiterAgentId               └─ TokenBudget
│     SessionPauseUtc                     └─ etc.
│     AktiveUnteragenten                  │
│                                         ├──── 1:N ────► UnteragentSpezifikation (neu)
│                                         │               └─ AgentScope
│                                         │               └─ AgentPrompt
│                                         │               └─ Status
│                                         │               └─ ErzeugungsDatum
│                                         │
│                                         └──── 1:N ────► SkillDefinition (neu)
│                                                         └─ SkillName
│                                                         └─ SkillVersion
│                                                         └─ SkillStatus
└─────────────────────────────────────────────────────────────────┘
```

### 1:1 Beziehung: Aufgabe ↔ AutonomAufgabeKonfiguration

- Ein Aufgabe **hat optional** eine AutonomAufgabeKonfiguration (null für reguläre Aufgaben)
- Eine AutonomAufgabeKonfiguration **gehört zu genau einer** Aufgabe
- Foreign Key: `AutonomAufgabeKonfiguration.AufgabeId` → `Aufgabe.Id`
- Delete-Verhalten: `DeleteBehavior.Cascade` (Wenn Aufgabe gelöscht, wird Konfiguration gelöscht)

### 1:N Beziehung: AutonomAufgabeKonfiguration ↔ UnteragentSpezifikation

- Eine AutonomAufgabeKonfiguration **kann mehrere** UnteragentSpezifikationen haben
- Eine UnteragentSpezifikation **gehört zu genau einer** AutonomAufgabeKonfiguration
- Foreign Key: `UnteragentSpezifikation.AutonomAufgabeId` → `AutonomAufgabeKonfiguration.Id`
- Delete-Verhalten: `DeleteBehavior.Cascade`

### 1:N Beziehung: AutonomAufgabeKonfiguration ↔ SkillDefinition

- Eine AutonomAufgabeKonfiguration **kann mehrere** SkillDefinitionen haben
- Eine SkillDefinition **gehört zu genau einer** AutonomAufgabeKonfiguration
- Foreign Key: `SkillDefinition.AutonomAufgabeId` → `AutonomAufgabeKonfiguration.Id`
- Delete-Verhalten: `DeleteBehavior.Cascade`

## Enums

### `AufgabeAusfuehrungsStatus` (erweitert)

```csharp
public enum AufgabeAusfuehrungsStatus
{
    NichtGestartet = 0,     // KI-Ausführung wurde noch nicht gestartet
    Aktiv = 1,              // KI-Ausführung ist aktiv
    Beendet = 2,            // KI-Ausführung wurde beendet
    AutonomAufgabe = 3      // [NEU] Autonome Aufgabe mit Projektleiter-Agent
}
```

### `PersistenzModus` (neu)

```csharp
public enum PersistenzModus
{
    Standard = 0,           // Normale Pause/Resume mit Kontext aus state.json
    SessionReset = 1        // [Zukünftig] Pause setzt Kontext zurück
}
```

### `PermissionsJsonOption` (neu)

```csharp
public enum PermissionsJsonOption
{
    Generate = 0,           // permissions.json automatisch generieren
    Select = 1,             // Bestehende Datei auswählen
    Existing = 2            // Vordefiniertes Profil verwenden
}
```

### `UnteragentStatus` (neu)

```csharp
public enum UnteragentStatus
{
    Erzeugt = 0,            // Unteragent wurde erzeugt, aber noch nicht gestartet
    Ausgeführt = 1,         // Unteragent läuft aktuell
    Abgeschlossen = 2,      // Unteragent hat Aufgabe fertiggestellt
    Fehler = 3              // Unteragent ist mit Fehler beendet
}
```

### `SkillStatus` (neu)

```csharp
public enum SkillStatus
{
    Entwurf = 0,            // Skill ist noch in Bearbeitung
    Review = 1,             // Skill wartet auf Review
    Freigegeben = 2,        // Skill ist freigegeben und kann verwendet werden
    Archiviert = 3          // Skill ist archiviert und wird nicht mehr verwendet
}
```

## Diagramm: Erweitertes ER-Modell

```mermaid
erDiagram
    AUFGABE ||--o| AUTONO_CONFIG : "hat optional"
    AUTONO_CONFIG ||--o{ UNTERAGENT : "verwaltet"
    AUTONO_CONFIG ||--o{ SKILL : "definiert"

    AUFGABE {
        guid Id
        guid ProjektId
        string Titel
        AufgabeStatus Status
        AufgabeAusfuehrungsStatus AusfuehrungsStatus
        string "ProjektleiterAgentId?"
        datetime "SessionPauseUtc?"
        int "AktiveUnteragenten?"
    }

    AUTONO_CONFIG {
        guid Id
        guid AufgabeId FK
        string ProjektBranchName
        string InitialPrompt
        string PermissionsJsonPfad
        int TokenBudget
        int "TokenBudgetErweitert?"
        int LaufzeitLimitMinuten
        PersistenzModus PersistenzModus
        bool SkillAutogeneration
        string ArbeitsverzeichnisPfad
    }

    UNTERAGENT {
        guid Id
        guid AutonomAufgabeId FK
        string AgentId
        string TaskId
        string AgentScope
        string AgentPrompt
        string AgentDirectory
        string AgentBranch
        string AgentClone
        datetime ErzeugungsDatum
        datetime "AbschlussDatum?"
        UnteragentStatus Status
    }

    SKILL {
        guid Id
        guid AutonomAufgabeId FK
        string SkillName
        string SkillVersion
        string SkillContent
        SkillStatus SkillStatus
        datetime ErstellungsDatum
        datetime "FreigabeDatum?"
    }
```

## Datenbankmigrationen

Die folgenden Migrationen werden ausgeführt, um das Datenmodell zu erstellen:

1. **AddAutonomAufgabeModels**
   - Erstellt Tabellen: `AutonomAufgabeKonfigurationen`, `UnteragentSpezifikationen`, `SkillDefinitionen`
   - Foreign Keys und Indizes

2. **AddAutonomAufgabeColumnsToAufgaben**
   - Erweitert Tabelle `Aufgaben` um Spalten: `ProjektleiterAgentId`, `SessionPauseUtc`, `AktiveUnteragenten`, `AutonomKonfigurationId`
   - Erstellt Index auf `AutonomKonfigurationId`

3. **UpdateAusfuehrungsStatusEnum** (falls nötig)
   - Aktualisiert Enum-Definition in `AufgabeAusfuehrungsStatus` um neuen Wert `AutonomAufgabe`

## Indizes

Für Performanz werden folgende Indizes erstellt:

| Tabelle | Spalten | Grund |
|---------|---------|-------|
| `Aufgaben` | `AutonomKonfigurationId` | 1:1-Lookup |
| `UnteragentSpezifikationen` | `AutonomAufgabeId` | 1:N-Abfragen |
| `UnteragentSpezifikationen` | `(AutonomAufgabeId, Status)` | Filtern nach Status |
| `SkillDefinitionen` | `AutonomAufgabeId` | 1:N-Abfragen |
| `SkillDefinitionen` | `(AutonomAufgabeId, SkillStatus)` | Filtern nach Status |
