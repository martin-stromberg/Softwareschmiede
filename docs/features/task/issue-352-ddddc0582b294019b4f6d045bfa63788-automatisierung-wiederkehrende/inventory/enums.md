# Enums — SCM-Ereignis-Pipeline

## `ScmKontoModus`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs` (Zeilen 8–15)

| Wert | Bedeutung |
|------|-----------|
| `Benutzerkonto = 0` | Überwachung/Antworten unter dem Benutzer-Token; Trigger = Keyword, Owner-Regel „angemeldeter Benutzer = Owner" aktiv |
| `SeparatesKonto = 1` | Überwachung/Antworten unter dem Token des separaten Kontos (Notifications-Inbox); Trigger = frei stehende `@ViewerLogin`-Erwähnung, Autor muss Owner sein |

Der Modus gilt nur für Ereignispfade; Nicht-Ereignis-APIs bleiben Benutzer-Token-Pfade (Klassenkommentar Zeilen 3–7).

## `ScmReferenceKind`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeile 9)

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz — der Workflow-Erzeugungsfall; gelesene Quellen: `Description` + `IssueComment` |
| `PullRequest` | PR-Referenz (zusätzlich `ReviewComment`, `PullRequestReview`) |

## `ScmContentKind`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeile 12)

| Wert | Bedeutung |
|------|-----------|
| `Description` | Beschreibungstext der Referenz (Issue-Body) — **hier steht die `@softwarewerkstatt`-Erwähnung des Erzeugungs-Workflows**; von der `ViewerHasReacted`-Dedup ausgenommen |
| `IssueComment` | Issue-Kommentar |
| `ReviewComment` | PR-Zeilenkommentar |
| `PullRequestReview` | Veröffentlichtes PR-Review |

## `ScmTaskResolutionKind`

Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Zeilen 13–21)

| Wert | Bedeutung |
|------|-----------|
| `Assigned` | Genau eine Aufgabe gefunden oder angelegt |
| `Ambiguous` | Mehrere Aufgaben tragen dieselbe Referenz |
| `Unassigned` | Keine Aufgabe zuordenbar/anlegbar |

## `ScmResponseKind` / `ScmProviderFailureKind`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Zeilen 55, 80)

- `ScmResponseKind { Comment, Reaction, PullRequest }` — Antwortkanal-Arten, für die Issue-Erzeugung irrelevant.
- `ScmProviderFailureKind { Authentication, Permission, RateLimit, Temporary, Permanent }` — Fehlerklassen des Checkpoint-Backoffs; `Authentication`/`Permission`/404-`Permanent` werden im `SeparatesKonto`-Pfad als „Repository für Konto nicht erreichbar" isoliert.
