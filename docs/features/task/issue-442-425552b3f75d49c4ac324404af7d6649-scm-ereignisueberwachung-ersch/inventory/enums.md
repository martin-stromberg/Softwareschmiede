# Enums — Bestandsaufnahme Issue #442

## `ScmProviderFailureKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:94`

| Wert | Bedeutung |
|------|-----------|
| `Authentication` | Token ungültig/abgelaufen → Checkpoint dauerhaft (`long.MaxValue`) bis „Erneut prüfen" |
| `Permission` | Fehlende Berechtigung → dauerhaft gesperrt; im Inbox-Modus zudem als „Repository unsichtbar" isoliert (`IsInaccessible`) |
| `RateLimit` | Kontingent erschöpft (REST: 429 oder 403 + `X-RateLimit-Remaining: 0`/`Retry-After`; GraphQL: `RATE_LIMITED`-Fehlertyp) → Backoff über `RetryAfterUtc`; **nicht** von `IsInaccessible` abgedeckt |
| `Temporary` | Transiente Fehler (5xx, Transport) → Backoff |
| `Permanent` | Dauerhafte Quellfehler (z. B. 404) → dauerhaft gesperrt, außer `retryPermanent: true` (Auflösungs-Checkpoints) |

## `ScmAccountMode`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmAccountMode.cs:8`

| Wert | Bedeutung |
|------|-----------|
| `UserAccount = 0` | Ereignispfade unter dem Benutzer-Token (`Softwareschmiede.GitHub.Token` → `_eventApiClient`) |
| `SeparateAccount = 1` | Ereignispfade unter dem Token des separaten Kontos (`Softwareschmiede.GitHub.AgentToken` → `_agentEventApiClient`); Notifications-Inbox statt Repository-Enumeration |

## `ScmReferenceKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:10`

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz |
| `PullRequest` | Pull-Request-Referenz |

## `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:13`

| Wert | Bedeutung |
|------|-----------|
| `Description` | Beschreibungstext der Referenz (GraphQL) |
| `IssueComment` | Issue-/PR-Kommentare (REST-Liste + GraphQL-Node) |
| `ReviewComment` | PR-Zeilenkommentare |
| `PullRequestReview` | Veröffentlichte PR-Reviews (PENDING ausgenommen) |

## `ScmResponseKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs:60`

| Wert | Bedeutung |
|------|-----------|
| `Comment` | Kommentar-POST |
| `Reaction` | Reaktions-POST (bzw. GraphQL `addReaction` bei `PullRequestReview`) |
| `PullRequest` | PR-Anlage-POST |
| `IssueBodyUpdate` | Beschreibungs-PATCH (Titel unberührt) — Erweiterung nur am Ende erlaubt (JSON-Kompatibilität) |
