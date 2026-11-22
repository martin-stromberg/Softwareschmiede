# Datenmodell — Token-relevante Entitäten und Value Objects

## Persistenzmodell

### `PluginConfiguration` — `src/Softwareschmiede/Domain/Entities/PluginConfiguration.cs`

EF-Core-Entität der Plugin-Konfiguration. Token-relevant: Sie persistiert **keinen** Token,
sondern nur dessen Speicherort.

| Member | Typ | Zeile | Bedeutung |
|--------|-----|-------|-----------|
| `Id` | `Guid` | 9 | Primärschlüssel |
| `PluginType` | `string` | 12 | z. B. „GitHub" |
| `PluginCategory` | `PluginCategory` (Enum) | 15 | Git/KI-Kategorie |
| `DisplayName` | `string` | 18 | Anzeigename |
| `CredentialStoreKey` | `string` | 21 | Schlüssel im Credential Store für den API-Token |
| `BaseUrl` | `string?` | 24 | Self-Hosted-Basis-URL |
| `Enabled` | `bool` | 27 | Aktivschalter |

Befund: Keine Längenbegrenzung des Tokens betroffen — die DB kennt den Token gar nicht.

### `PluginSettingField` — `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingField.cs`

Record, der ein Einstellungsfeld beschreibt (Zeilen 15–23): `Key`, `Label`,
`FieldType` (Default `Text`), `Placeholder`, `Description`, `IsRequired`, `EnumOptions`,
`DefaultValue`. Der Doc-Kommentar (Zeile 5) legt fest: „Der Schlüssel wird unter
`<PluginPrefix>.<Key>` im Credential Store gespeichert." Keine Validierungs- oder
Längenattribute vorhanden.

Die GitHub-Feldeklarationen stehen in `GitHubPlugin.GetSettingGroups`
(`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` Zeilen 63–111):

- `Token` (Zeilen 67–73): `PluginSettingFieldType.Secret`, Label „Personal Access Token",
  Placeholder `ghp_...`, `IsRequired: true`, Description verlangt Scopes `repo`, `read:org`,
  Code-Scanning-Alerts.
- `AgentToken` (Zeilen 74–80): `Secret`, Label „Personal Access Token des separaten Kontos",
  Placeholder `ghp_...`, `IsRequired: false`, Scopes `notifications` und `repo`.

### `PluginSettingEntry` — `src/Softwareschmiede.App/ViewModels/PluginSettingEntry.cs`

ViewModel-Hülle eines Felds (Zeilen 6–52): `Field`, `Value` (`string`, Zeilen 18–29),
`BoolValue`. `Value` trägt den rohen Eingabetext unverändert — keine Konvertierung,
kein Truncation. `PluginSettingGroupEntry` (Zeilen 55–69) gruppiert die Einträge.

## Ereignis-/Kontextmodell (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/`)

### `ScmEventRepository` — `ScmEvents.cs` Zeilen 16–19

`record ScmEventRepository(string ProviderPrefix, string Host, string RepositoryId,
string Owner, string Name, string? OwnerId, string ViewerId, string ViewerLogin,
bool CanWrite, ScmAccountMode Account = UserAccount, string? NumericRepositoryId = null)`.

Token-relevante Semantik: `ViewerId`/`ViewerLogin` sind die Identität des authentifizierten
Kontos (bei `ghs_` wäre das die Bot-Identität `<app-slug>[bot]`); `CanWrite` entscheidet
über alle Schreib-Gates; `Account` wählt den Credential-Key (siehe
[logic.md](logic.md) Abschnitt „Kontorouting"). Das Record enthält bewusst **keine**
Credentials (Doc-Kommentar Zeile 15).

### `ScmRepositoryContext` — `ScmRepositoryContext.cs` Zeilen 6–34

Record mit `GitRepositoryId` (Guid, Pflicht ≠ Empty), `PluginPrefix`, `RepositoryId`
(kanonische API-ID), `Account` (`ScmAccountMode`, Default `UserAccount`). Eingangsobjekt
von `IScmEventSource.GetEventRepositoryAsync`.

### `ScmEventObservation` — `ScmEvents.cs` Zeilen 32–54

Record einer beobachteten Textversion; token-relevant sind `AuthorId`/`AuthorLogin`,
`EditorId`/`EditorLogin` (Owner-/Trusted-Prüfung in `ScmEventValidationService`) und
`ViewerReactions`/`ViewerHasReacted` (Duplikaterkennung des Ereignis-Kontos).
Abgeleitete Member: `BodyHash` (SHA-256), `Revision`, `ContentKey`, `EventKey`.

### `ScmEventReference` / `ScmEventReadRequest` / `ScmEventBatch` — `ScmEvents.cs` Zeilen 22–29

Referenz (`Repository`, `Kind`, `Number`), Leseanfrage (`Reference`, `ContentKind`,
`SinceUtc`, `WatermarkUtc`) und Batch-Ergebnis.

### `ScmResponseRequest` / `ScmResponseResult` — `ScmEvents.cs` Zeilen 72–82

Antwortaktion mit `ActionId`, `Kind` (`ScmResponseKind`), `Body`, `Reaction`,
`HeadBranch`/`BaseBranch`, `Attachments`, `RemoteNodeId` u. a.; `Marker`
(`<!-- softwareschmiede:action:{ActionId} -->`, Zeile 78) als Reconcile-Anker.
`ScmResponseAttachment` (Zeile 63): `Path`, `Name`, `ContentType`, `Caption`.

### `ScmNotification` — `ScmEvents.cs` Zeilen 85–91

Roher Inbox-Eintrag des separaten Kontos; Konstante `MentionReason = "mention"` (Zeile 90).

### `ScmProviderException` — `ScmEvents.cs` Zeilen 97–114

Typisierte Provider-Ausnahme: `FailureKind` (`ScmProviderFailureKind`), `RetryAfterUtc`,
`OutcomeUncertain`. Credentialfrei per Konvention (Doc-Kommentar Zeile 96).

## Transportmodell (`plugins/Softwareschmiede.Plugin.GitHub/IGitHubEventApiClient.cs`)

| Record | Zeilen | Inhalt |
|--------|--------|--------|
| `GitHubEventApiRequest` | 16 | `Host`, `Method`, `Endpoint`, `JsonBody?` — credentialfrei; der Token kommt ausschließlich über den `tokenProvider` des Clients in den Header |
| `GitHubAssetUploadRequest` | 20 | `Host`, `RepositoryId` (numerisch), `Name`, `ContentType`, `Data` |
| `GitHubEventApiResponse` | 23 | `StatusCode`, `Body`, `Headers?` |

## CLI-Ergebnis

`CliResult` (`src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/`, verwendet von
`ICliRunner`/`CliRunner`): `ExitCode`, `StdOut`, `StdErr`, `IsSuccess` — trägt keinen Token,
aber `StdErr` kann Tokens enthalten (Maskierungspflicht bei `SanitizeSensitiveOutput`,
siehe [logic.md](logic.md)).

## Ergebnis-/Statusobjekte der Nicht-Ereignis-APIs

- `Issue`, `PullRequest`, `PullRequestStatusInfo`, `PullRequestWorkflowRunInfo`,
  `PullRequestCompletionResult`, `IssueCreateResult`, `IssueTemplateLoadResult`,
  `AvailableRepository`, `ScmAlert`, `RepositoryDirectoryEntry`,
  `RepositoryStructureLoadResult`, `PullRequestCheckoutSpec`, `GitActionCapabilities` —
  sämtlich credentialfrei; siehe `GitPluginBase`/`IGitPlugin`-Signaturen in
  [interfaces.md](interfaces.md).
