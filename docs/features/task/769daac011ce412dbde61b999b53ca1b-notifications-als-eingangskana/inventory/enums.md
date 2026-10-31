# Enums: GitHub-Notifications als Eingangskanal

## `ScmReferenceKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 8)

| Wert | Bedeutung |
|------|-----------|
| `Issue` | Issue-Referenz |
| `PullRequest` | Pull-Request-Referenz |

## `ScmContentKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 11)

Getrennte ID-Namensräume der beobachteten Texte (je ein Checkpoint je Referenz).

| Wert | Bedeutung |
|------|-----------|
| `Description` | Issue-/PR-Beschreibung (Ort, an dem eine `@mention` im Body läge) |
| `IssueComment` | Kommentar (Issues und PRs) |
| `ReviewComment` | Review-Kommentar (nur PR) |
| `PullRequestReview` | Review (nur PR) |

## `ScmResponseKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 50)

| Wert | Bedeutung |
|------|-----------|
| `Comment` | Kommentar-Antwort |
| `Reaction` | Reaktion (z. B. `eyes`-Empfangsbestätigung) |
| `PullRequest` | Pull-Request-Anlage als Antwortaktion |

## `ScmProviderFailureKind`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 66)

| Wert | Bedeutung |
|------|-----------|
| `Authentication` | Anmeldedaten ungültig/abgelaufen → Sperre (`NextAttemptUtc = long.MaxValue`) |
| `Permission` | Berechtigung fehlt → Sperre |
| `RateLimit` | Rate-Limit → Retry mit `RetryAfterUtc` |
| `Temporary` | Vorübergehend → Backoff-Retry |
| `Permanent` | Dauerhaft → Sperre |

## `ScmBackmergeModus`
Datei: `src/Softwareschmiede/Domain/Enums/ScmBackmergeModus.cs`

Vorlage für den neuen Kontomodus-Enum (wird als Zahl unter `AppEinstellungService.ScmBackmergeModeKey` persistiert).

| Wert | Bedeutung |
|------|-----------|
| `Deaktiviert = 0` | Kein automatischer Merge |
| `Global = 1` | Backmerge-PRs in allen Projekt-Repositories mergen |
| `Projektbezogen = 2` | Nur in Projekten mit aktivierter Projektoption |

## `ScmTaskResolutionKind`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Z. 13–21)

| Wert | Bedeutung |
|------|-----------|
| `Assigned` | Genau eine Aufgabe gefunden oder angelegt |
| `Ambiguous` | Mehrere Aufgaben tragen dieselbe Referenz |
| `Unassigned` | Keine Aufgabe zuordenbar/anlegbar |

## `PluginSettingFieldType`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Text` | Einzeiliger Text |
| `Secret` | Maskiertes Token-Feld — Typ des bestehenden `Token`-Felds und des geplanten zweiten PAT-Felds |
| `Url` | URL-Eingabe |
| `Integer` | Ganzzahl |
| `Boolean` | Checkbox |
| `Enum` | Feste Auswahl (`EnumOptions`) |
| `FilePath` | Dateipfad mit Dialog |
| `CommandLineParameters = 8` | CLI-Parameter (Wert 7 reserviert) |

## `PullRequestProvider`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/PullRequestProvider.cs`

| Wert | Bedeutung |
|------|-----------|
| `GitHub = 0` | GitHub Pull Request |
| `BitbucketCloud = 1` | Bitbucket Cloud |
| `BitbucketServerDataCenter = 2` | Bitbucket Server/Data Center |

## Nicht vorhanden
Es existiert **kein** Enum für den Kontomodus (`Benutzerkonto`/`SeparatesKonto`) und kein Enum/Value Object für Notification-`reason`-Werte.
