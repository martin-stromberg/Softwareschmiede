# Bestandsaufnahme: Enums (SCM-Ereignisverarbeitung)

## `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 12)

| Wert | Bedeutung |
|------|-----------|
| `Description` | Issue-/PR-Text — **aus der Anforderung ausgenommen** (erhält keine Empfangsreaktion, `ScmEventOrchestrator.cs` Z. 95) |
| `IssueComment` | Kommentar auf Issue/PR-Konversation — im Scope |
| `ReviewComment` | Zeilenkommentar eines PR-Reviews — im Scope |
| `PullRequestReview` | Review-Text — im Scope |

## `ScmReferenceKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 9)

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz |
| `PullRequest` | Pull-Request-Referenz |

## `ScmResponseKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 52)

| Wert | Bedeutung |
|------|-----------|
| `Comment` | Kommentar-Antwort |
| `Reaction` | Reaktions-Antwort — die Empfangsreaktion (`"eyes"`), die der anderen Instanz als Erkennungsmerkmal dient |
| `PullRequest` | PR-Anlage durch den Agenten |

## `ScmKontoModus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs` (Z. 8–15)

| Wert | Bedeutung |
|------|-----------|
| `Benutzerkonto = 0` | Ereignispfade laufen unter dem Benutzer-Token (repository-weite Enumeration) |
| `SeparatesKonto = 1` | Ereignispfade laufen unter dem Token eines separaten Kontos (Notifications-Inbox) |

Gilt ausschließlich für die Ereignispfade (Beobachtung, Zustellung, Ereignis-Antworten) — siehe Token-Grenze im Kommentar von `GitHubPlugin.ClientFor` (`GitHubPlugin.Events.cs` Z. 320–327).

## `ScmProviderFailureKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 77)

| Wert | Bedeutung |
|------|-----------|
| `Authentication` | Anmeldung ungültig/abgelaufen → `NextAttemptUtc = long.MaxValue` beim Ereignis |
| `Permission` | Fehlende Berechtigung → `long.MaxValue` |
| `RateLimit` | Rate-Limit → Backoff mit `RetryAfterUtc` |
| `Temporary` | Vorübergehender Fehler → Backoff |
| `Permanent` | Dauerhafter Fehler |

## `ScmTaskResolutionKind`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Z. 13–21)

| Wert | Bedeutung |
|------|-----------|
| `Assigned` | Genau eine Aufgabe gefunden/angelegt |
| `Ambiguous` | Mehrere Aufgaben tragen dieselbe Referenz |
| `Unassigned` | Keine Aufgabe zuordenbar und keine anlegbar |
