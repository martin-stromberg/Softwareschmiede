# Enums — Bestandsaufnahme Assignee-Auslöser (Issue #474)

## `ScmReferenceKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 10)

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz |
| `PullRequest` | Pull-Request-Referenz |

Der Assignee-Auslöser der Anforderung gilt für beide Werte.

## `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 13)

| Wert | Bedeutung |
|------|-----------|
| `Description` | Beschreibungstext der Referenz — laut Anforderung der auslösende Text des assign-Auslösers |
| `IssueComment` | Issue-/PR-Kommentar |
| `ReviewComment` | PR-Review-Kommentar (nur PullRequest) |
| `PullRequestReview` | Veröffentlichte PR-Review (nur PullRequest) |

## `ScmAccountMode`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmAccountMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `UserAccount = 0` | Standard: repository-weite Enumeration, Keyword-Trigger |
| `SeparateAccount = 1` | Separates Konto: Notifications-Inbox, `@ViewerLogin`-Mention-Trigger — einziger Modus, in dem der Assignee-Auslöser gelten soll |

Persistiert unter `AppSettingService.ScmAccountModeKey` (`"scm.monitoring.accountmode"`).

## `ScmProviderFailureKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 94)

| Wert | Bedeutung |
|------|-----------|
| `Authentication` | Konto-Token ungültig — dauerhafte Sperre, Cache-Eviction |
| `Permission` | Fehlende Berechtigung — dauerhafte Sperre |
| `RateLimit` | Kontingent erschöpft — Sperr-gate |
| `Temporary` | Vorübergehend — Backoff |
| `Permanent` | Permanent — dauerhafte Sperre, außer bei `retryPermanent` (Repository-Auflösung) |

## `ScmResponseKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 60)

| Wert | Bedeutung |
|------|-----------|
| `Comment` | Kommentar-Antwort |
| `Reaction` | Reaktion (z. B. `eyes`-Empfang) |
| `PullRequest` | PR-Anlage |
| `IssueBodyUpdate` | Ersetzt ausschließlich die Issue-Beschreibung |

## `ScmTaskResolutionKind`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Z. 15-23)

| Wert | Bedeutung |
|------|-----------|
| `Assigned` | Genau eine Aufgabe gefunden oder angelegt |
| `Ambiguous` | Mehrere Aufgaben tragen dieselbe Referenz |
| `Unassigned` | Keine Aufgabe zuordenbar und keine anlegbar |
