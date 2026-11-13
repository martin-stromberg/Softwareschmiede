# Enums

## `ScmKontoModus`

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Benutzerkonto = 0` | Überwachung und Antworten laufen unter dem Benutzer-Token; zugleich der Credential-Selektor für `GetAgentEnvironmentAsync` (über `CredentialKeyFor` → `GitHubTokenCredentialKey`) |
| `SeparatesKonto = 1` | Überwachung und Antworten laufen unter dem Token eines separaten Kontos (→ `GitHubAgentTokenCredentialKey`) |

Hinweis aus dem Enum-Kommentar: Nicht-Ereignis-APIs — darunter alle gh-/git-Schreib- und Arbeitskopien-Operationen — laufen bewusst unter dem Benutzerkonto; `GetGitEnvironment`/`GetGhEnvironment` nutzen daher immer `GitHubTokenCredentialKey`, nur `GetAgentEnvironmentAsync` ist kontomodus-abhängig.

Keine weiteren Enums sind für die Anforderung relevant (keine netrc-/Credential-bezogenen Enum-Werte im Bestand).
