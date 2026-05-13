# LocalDirectoryPlugin – Technische API-/Plugin-Dokumentation

## Zweck

`LocalDirectoryPlugin` ist eine `IGitPlugin`-Implementierung für lokale Verzeichnisse ohne Remote-Provider (kein GitHub/GitLab-API-Zugriff).

## Implementierung und Contract

- Contract: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- Implementierung: `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
- Plugin-Metadaten:
  - `PluginName`: `Local Directory`
  - `PluginPrefix`: `LocalDirectoryPlugin`
  - `PluginType`: `SourceCodeManagement`

## Unterstützte und nicht unterstützte IGitPlugin-Operationen

| IGitPlugin-Methode | Support | Verhalten |
|---|---|---|
| `CloneRepositoryAsync` | ✅ | Arbeitet je nach `WorkspaceMode` im Quellverzeichnis oder in einer kopierten Arbeitskopie. |
| `CreateBranchAsync` | ✅ | Stellt Git-Repository sicher und delegiert auf Git-Basislogik. |
| `CommitAsync` | ✅ | Stellt Git-Repository sicher und delegiert auf Git-Basislogik. |
| `ResetAsync` | ✅ | Stellt Git-Repository sicher und delegiert auf Git-Basislogik. |
| `CheckHealthAsync` | ✅ | Liefert immer `true`. |
| `GetIssuesAsync` | ❌ | `NotSupportedException` (keine Remote-Provider-Funktionen). |
| `PushBranchAsync` | ❌ | `NotSupportedException` (keine Remote-Provider-Funktionen). |
| `PullAsync` | ❌ | `NotSupportedException` (keine Remote-Provider-Funktionen). |
| `CreatePullRequestAsync` | ❌ | `NotSupportedException` (keine Remote-Provider-Funktionen). |
| `GetRemoteBranchesAsync` | ❌ | `NotSupportedException` (keine Remote-Provider-Funktionen). |
| `GetDefaultBranchAsync` | ❌ | `NotSupportedException` (keine Remote-Provider-Funktionen). |
| `CheckoutRemoteBranchAsync` | ❌ | `NotSupportedException` (keine Remote-Provider-Funktionen). |

## WorkspaceMode und Ablauf von `CloneRepositoryAsync`

### `InSourceDirectory`

- `sourcePath` wird direkt als Workspace verwendet.
- Vor dem Start wird ein dirty Workspace (`git status --porcelain`) abgelehnt.
- Existiert noch kein `.git`, ist `LocalDirectoryPlugin.ConfirmGitInitInSourceDirectory=true` zwingend erforderlich; sonst Abbruch.
- Falls `targetPath` ungleich `sourcePath` ist, wird im `targetPath` eine Pointer-Datei `.softwareschmiede-local-workspace` mit dem aufgelösten Workspace-Pfad geschrieben.

### `SeparateWorkingDirectory`

- `sourcePath` wird in ein separates Arbeitsverzeichnis kopiert.
- Das Ziel muss:
  - ungleich Quelle sein,
  - leer sein (falls vorhanden).
- Nach dem Kopieren wird ein Git-Repository sichergestellt und anschließend auf dirty Workspace geprüft.

## Settings und Guardrails (`LocalDirectoryPlugin.<Key>`)

| Key | Typ | Default | Wirkung |
|---|---|---|---|
| `WorkspaceMode` | Enum | `SeparateWorkingDirectory` | Modusauswahl: exakt `InSourceDirectory` oder `SeparateWorkingDirectory` (case-sensitive Parsing). Ungültige Werte fallen auf `SeparateWorkingDirectory` zurück. |
| `SourceDirectory` | String | – | Fallback-Quelle, wenn `repositoryUrl` beim Clone leer ist. |
| `WorkingDirectory` | String | – | Optionales Zielverzeichnis für `SeparateWorkingDirectory`; überschreibt `targetPath`. |
| `ConfirmGitInitInSourceDirectory` | Bool (`true`/`false`) | `false` | Erforderliche explizite Freigabe für `git init` im Quellverzeichnis, falls dort noch kein Git-Repository existiert. |
| `CopyTimeoutSeconds` | Integer | `600` | Timeout der Verzeichniskopie. Werte `< 1` oder ungültig fallen auf Default zurück. |
| `CopyMaxFiles` | Integer | `100000` | Maximale Dateianzahl je Kopiervorgang. Werte `< 1` oder ungültig fallen auf Default zurück. |
| `CopyMaxMegabytes` | Integer | `10240` | Maximale Datenmenge (MB) je Kopiervorgang. Werte `< 1` oder ungültig fallen auf Default zurück. |

## Weitere Sicherheits-/Stabilitätsregeln

- Quellverzeichnis muss existieren (`DirectoryNotFoundException` bei fehlendem Pfad).
- Symlinks/Reparse-Points werden beim Kopieren blockiert (`InvalidOperationException`).
- Bei Fehler/Abbruch während der Kopie wird das Zielverzeichnis vollständig aufgeräumt.
- Workspace-Auflösung für Folgeoperationen (`CreateBranchAsync`, `CommitAsync`, `ResetAsync`) nutzt:
  1. In-Memory-Mapping des Plugin-Objekts,
  2. Pointer-Datei `.softwareschmiede-local-workspace` (auch nach Plugin-Neuinstanz).

## Testabdeckung (relevante Nachweise)

- Unit: `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests.cs`
  - Bestätigungspflicht für `git init` in `InSourceDirectory`
  - Dirty-Workspace-Abbruch
  - Guardrails für `CopyMaxFiles` und `CopyMaxMegabytes`
  - Verzeichnisregeln (Quelle/Ziel verschieden, Ziel leer)
  - Fallbacks für `SourceDirectory` und `WorkingDirectory`
  - NotSupported-Verhalten für Remote-Methoden
- Integration: `src/Softwareschmiede.IntegrationTests/Infrastructure/Plugins/LocalDirectoryPluginIntegrationTests.cs`
  - End-to-End für Clone/Branch/Commit/Reset
  - Verhalten in `InSourceDirectory` und `SeparateWorkingDirectory`
  - Pointer-Datei-Verhalten bei Plugin-Neuinstanz
