# Enums — Bestandsaufnahme

## `ScmKontoModus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs` (Z. 8–15)

| Wert | Bedeutung |
|------|-----------|
| `Benutzerkonto` (= 0) | Überwachung und Antworten unter dem Benutzer-Token; Trigger = Keyword, zusätzliche Regel „Viewer = Owner" |
| `SeparatesKonto` (= 1) | Überwachung und Antworten unter einem separaten Bot-Token (Notifications-Inbox); Trigger = frei stehende `@`-Erwähnung des Bot-Logins, Viewer-Owner-Regel entfällt |

Gilt ausschließlich für die Ereignispfade (Beobachtung, Zustellung, Ereignis-Antworten); die `github-actions`-Akteursprüfung sitzt in beiden Modi hinter derselben `SameIdentity`-Regel.

## `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 12)

| Wert | Bedeutung |
|------|-----------|
| `Description` | Issue-/PR-Beschreibung (Referenztext; von der Reaktionsregel ausgenommen) |
| `IssueComment` | Issue-/PR-Unterhaltungskommentar |
| `ReviewComment` | PR-Zeilenkommentar |
| `PullRequestReview` | Veröffentlichtes PR-Review |

## `ScmReferenceKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 9)

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz |
| `PullRequest` | Pull-Request-Referenz |

## `ScmTaskResolutionKind`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Z. 13–21)

| Wert | Bedeutung |
|------|-----------|
| `Assigned` | Genau eine Aufgabe gefunden oder angelegt |
| `Ambiguous` | Mehrere Aufgaben tragen dieselbe Referenz |
| `Unassigned` | Keine Aufgabe zuordenbar und keine anlegbar |

## `ScmProviderFailureKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 80)

| Wert | Bedeutung |
|------|-----------|
| `Authentication`, `Permission`, `RateLimit`, `Temporary`, `Permanent` | Fehlerklassen für gezielte Sperren und zeitgesteuerte Wiederholungen (für diese Anforderung nur indirekt relevant) |
