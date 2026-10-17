# Repository-/Aufgaben-Datenmodell

Zurück: [../inventory.md](../inventory.md)

## `GitRepository`

Datei: `src/Softwareschmiede/Domain/Entities/GitRepository.cs`

| Eigenschaft | Typ | Zweck |
|-------------|-----|-------|
| `Id` | `Guid` | PK |
| `ProjektId` | `Guid` | FK — „als Projekt erfasst" = Datensatz in `db.GitRepositories` |
| `PluginTyp` | `string` | z. B. `Softwareschmiede.GitHub` (PluginPrefix); Monitor matcht auf `PluginPrefix` oder `PluginName` |
| `RepositoryUrl` | `string` | Lokale Alias-URL (HTTPS/SSH/`owner/repo`) — Eingabe für `ScmRepositoryContext.RepositoryId` → `ParseEventRepository` |
| `RepositoryName` | `string` | Anzeigename |
| `Aktiv` | `bool` (default `true`) | Aktivierungsschalter; heute nur über Aufgaben indirekt geprüft |
| `DefaultSourceBranchName` | `string?` | Start-Branch (für Monitoring irrelevant) |
| `Projekt` | `Projekt` | Navigation |

DbSet: `db.GitRepositories` (`SoftwareschmiededDbContext.cs:28`). Repositories ohne Aufgabe sind aktuell **nicht** in der Beobachtungsliste, obwohl sie alle Daten (`PluginTyp`, `RepositoryUrl`, `Aktiv`) für die Auflösung tragen.

## `Aufgabe`-Referenzen

- `IssueReferenz`: `AufgabeId`, `IssueNummer` → `ScmReferenceKind.Issue`, Alias = `repository.RepositoryUrl`.
- `PullRequestReferenz`: `AufgabeId`, `RepositoryId`, `PullRequestNumber`, `Provider`, `Rolle` → `ScmReferenceKind.PullRequest`, Alias = `reference.RepositoryId` (kann von `RepositoryUrl` abweichen!).

## Zuordnung Referenz → Aufgabe (heute)

`ScmEventMonitoringService` gruppiert `resolved` nach kanonischer Referenzidentität; `taskIds.Length == 1` → Aufgabe zugeordnet, sonst `null` → `AcceptBatchAsync` schreibt „Mehrdeutige Zuordnung zu Aufgaben". Eine Referenz ohne Aufgabe fällt heute gar nicht in `resolved` — mit repo-weiter Enumeration landet sie im selben `aufgabeId == null`-Pfad (verhaltenstechnisch „nicht zuordenbar", bewusste Zwischenstufe).

## Plugin-Zuordnung

Monitor prüft `string.Equals(plugin.PluginPrefix, x.Repository.PluginTyp) || string.Equals(plugin.PluginName, x.Repository.PluginTyp)` (`ScmEventMonitoringService.cs:46-47`). Für repo-weite Repos gilt dieselbe Match-Logik; Nicht-GitHub-Repos haben kein `IScmEventSource`-Plugin und werden implizit übersprungen.
