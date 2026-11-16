# Enums

## `ScmKontoModus`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Enums/ScmKontoModus.cs` (Z. 8–15)

| Wert | Bedeutung |
|------|-----------|
| `Benutzerkonto = 0` | Ereignisüberwachung/-antworten unter dem Benutzer-Token (`GitHubTokenCredentialKey`). |
| `SeparatesKonto = 1` | Ereignispfade unter dem Token des separaten Kontos (`GitHubAgentTokenCredentialKey`). |

Wird an `IScmAgentAccess.GetAgentEnvironmentAsync` übergeben und im `GitHubPlugin` via `CredentialKeyFor` auf den Credential-Key abgebildet — wählt das Credential, nicht den Host. XML-Doc (Z. 4–7) beschreibt die Token-Grenze: Nicht-Ereignis-Pfade (inkl. aller git-Transport-Operationen via `GetGitEnvironment`) laufen bewusst unter dem Benutzerkonto.
