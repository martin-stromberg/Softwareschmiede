# Enums — Token-relevante Aufzählungen

## `PluginSettingFieldType` — `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`

Steuert Template-Auswahl und Speicherweg der Einstellungsfelder:

| Wert | Zeile | Bedeutung |
|------|-------|-----------|
| `Text` | 7 | Einzeiliger Text (TextBox) |
| `Secret` | 10 | Maskiertes Token-Feld → `PasswordBox`, Credential-Store-Speicherung |
| `Url` | 13 | URL-Eingabe |
| `Integer` | 16 | Ganzzahl |
| `Boolean` | 19 | Checkbox |
| `Enum` | 22 | ComboBox mit `EnumOptions` |
| `FilePath` | 25 | Pfad + Dateidialog |
| `CommandLineParameters = 8` | 30 | CLI-Parameter-Feld (Wert 7 reserviert) |

Die GitHub-Token-Felder `Token`/`AgentToken` sind `Secret`.

## `ScmAccountMode` — `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmAccountMode.cs`

| Wert | Zeile | Bedeutung |
|------|-------|-----------|
| `UserAccount = 0` | 11 | Ereignispfade unter dem Benutzer-Token (`Softwareschmiede.GitHub.Token`) |
| `SeparateAccount = 1` | 14 | Ereignispfade unter `…AgentToken` (Notifications-Inbox) |

Der Modus steuert `ClientFor`/`CredentialKeyFor` (`GitHubPlugin.Events.cs`
Zeilen 448–460) und die Trigger-Regel in `ScmEventValidationService.GetRejection`.
Doc-Hinweis (Zeilen 4–7): Nicht-Ereignis-APIs und gh/git laufen **immer** unter dem
Benutzerkonto (Token-Grenze).

## `ScmProviderFailureKind` — `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` Zeile 94

| Wert | Auslöser (GitHub) | Wirkung |
|------|-------------------|---------|
| `Authentication` | HTTP 401, GraphQL `UNAUTHORIZED`, fehlendes AgentToken | `EvictResolutions` (gesamtes Konto), PAT-Hinweistext |
| `Permission` | HTTP 403 (ohne Rate-Limit), GraphQL `FORBIDDEN`, `!CanWrite`-Gates, Viewer-/Repo-Identitätswechsel | `EvictResolutions` (Client+Host), PAT-Hinweistext |
| `RateLimit` | HTTP 429 oder 403 mit `X-RateLimit-Remaining: 0`/`Retry-After`, GraphQL `RATE_LIMITED` | `_rateLimitGates`-Sperre bis `RetryAfterUtc` bzw. 60-s-Fallback |
| `Temporary` | ≥500, Transportfehler, unvollständige Antworten | wiederholbar; REST-Fallback der Auflösung |
| `Permanent` | 404/NOT_FOUND, Validierungsfehler, Host-Fehler | nicht wiederholbar (außer Auflösung per `retryPermanent`-Backoff) |

## `ScmReferenceKind` / `ScmContentKind` / `ScmResponseKind` — `ScmEvents.cs`

- `ScmReferenceKind` (Zeile 10): `Issue`, `PullRequest`.
- `ScmContentKind` (Zeile 13): `Description`, `IssueComment`, `ReviewComment`,
  `PullRequestReview` — steuert u. a. die `ViewerHasReacted`-Regel.
- `ScmResponseKind` (Zeile 60): `Comment`, `Reaction`, `PullRequest`,
  `IssueBodyUpdate` (Anhängen neuer Werte nur am Ende — JSON-Persistenz).

## `PluginType` — `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PluginType.cs`

`SourceCodeManagement`, `DevelopmentAutomation`, `Ide` — `GitHubPlugin.PluginType`
ist `SourceCodeManagement`.

## `PullRequestCompletionStrategy` / `PullRequestMergeMethod` — `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/`

Enum-Optionen der Plugin-Einstellungsfelder (nicht token-relevant, aber derselben
Einstellungsgruppe „Pull Requests"):

- `PullRequestCompletionStrategy`: `Merge`, `AutoMerge`, `ApprovalOnly`.
- `PullRequestMergeMethod`: `Merge`, `Squash`, `Rebase`.
