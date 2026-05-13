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
| `PushBranchAsync` | ✅ (nur `SeparateWorkingDirectory`) | Kein klassischer `git push`: **Push-Sync** als Dateisynchronisation `WorkingDirectory -> SourceDirectory` (inkl. **Delete-Sync** über `git status --porcelain`). |
| `PullAsync` | ✅ (nur `SeparateWorkingDirectory`) | Kein Merge-Workflow: **Pull-Sync** als Dateisynchronisation `SourceDirectory -> WorkingDirectory` (ff-only/rebase-äquivalentes Verhalten ohne Merge-Commit). |
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

- Das Ziel muss:
  - ungleich Quelle sein,
  - leer sein (falls vorhanden).
- Danach entscheidet das Plugin deterministisch (Git-Workflow-Fallback):
  1. Quelle ist Git-Repository → `git clone source target`
  2. Quelle ist kein Git-Repository + `ConfirmGitInitInSourceDirectory=true` → **git-init-Fallback**: `git init` in Quelle, danach `git clone`
  3. Quelle ist kein Git-Repository + `ConfirmGitInitInSourceDirectory!=true` → Copy-Fallback (`CopyDirectoryWithGuardrails`), kein Clone
- Bei Clone-Fehlern wird das Zielverzeichnis aufgeräumt.

## Git-Workflow-Fallback im `SeparateWorkingDirectory`-Modus

### Pull-Sync (`PullAsync`)

- Führt **keinen Merge** aus und erstellt keine Merge-Commits.
- Verhalten ist als **ff-only/rebase-äquivalente Policy** dokumentiert: Änderungen werden deterministisch aus dem Quellverzeichnis synchronisiert.
- Vor der Synchronisation muss das Arbeitsverzeichnis sauber sein (`git status --porcelain` leer), sonst Abbruch mit `InvalidOperationException` (z. B. bei lokalen Änderungen/Konfliktlage).
- Es wird ein Hinweis geloggt, dass kein Merge durchgeführt wird.
- **Warning:** Bei Konflikt- oder manuellem Eingriffsbedarf muss die Situation außerhalb von `PullAsync` bereinigt werden (z. B. manuelle Auflösung/Commit/Reset), danach Pull erneut starten.

### Push-Sync (`PushBranchAsync`)

- Führt **keinen Remote-Push** aus.
- Stattdessen werden Dateien vom Arbeitsverzeichnis ins Quellverzeichnis synchronisiert (`WorkingDirectory -> SourceDirectory`, overwrite aktiv).
- Das entspricht einer lokalen Datei-Synchronisation und nicht einem klassischen `git push origin <branch>`.

### Delete-Sync

- Gelöschte/umbenannte Pfade werden über `git status --porcelain` im Arbeitsverzeichnis erkannt.
- Status mit `D`, `T` und Rename-Status (`R`, Altpfad aus `old -> new`) werden als Löschkandidaten interpretiert.
- Die erkannten Pfade werden im Quellverzeichnis entfernt (Datei oder Verzeichnis), sodass Löschungen konsistent synchronisiert sind.

## Settings und Guardrails (`LocalDirectoryPlugin.<Key>`)

| Key | Typ | Default | Wirkung |
|---|---|---|---|
| `WorkspaceMode` | Enum | `SeparateWorkingDirectory` | Modusauswahl: exakt `InSourceDirectory` oder `SeparateWorkingDirectory` (case-sensitive Parsing). Ungültige Werte fallen auf `SeparateWorkingDirectory` zurück. |
| `SourceDirectory` | String | – | Fallback-Quelle, wenn `repositoryUrl` beim Clone leer ist. |
| `ConfirmGitInitInSourceDirectory` | Bool (`true`/`false`) | `false` | Erforderliche explizite Freigabe für `git init` im Quellverzeichnis, falls dort noch kein Git-Repository existiert. |
| `CopyTimeoutSeconds` | Integer | `600` | Timeout der Verzeichniskopie. Werte `< 1` oder ungültig fallen auf Default zurück. |
| `CopyMaxFiles` | Integer | `100000` | Maximale Dateianzahl je Kopiervorgang. Werte `< 1` oder ungültig fallen auf Default zurück. |
| `CopyMaxMegabytes` | Integer | `10240` | Maximale Datenmenge (MB) je Kopiervorgang. Werte `< 1` oder ungültig fallen auf Default zurück. |

> Hinweis: Ein plugin-spezifisches `WorkingDirectory`-Setting existiert nicht mehr.  
> Das Zielverzeichnis für `SeparateWorkingDirectory` stammt aus dem vom Prozess übergebenen `targetPath`
> (auf Basis von `repositories.workdir` + `softwareschmiede/<aufgabeId>`).

## Weitere Sicherheits-/Stabilitätsregeln

- Quellverzeichnis muss existieren (`DirectoryNotFoundException` bei fehlendem Pfad).
- Symlinks/Reparse-Points werden beim Kopieren blockiert (`InvalidOperationException`).
- Bei Fehler/Abbruch während der Kopie wird das Zielverzeichnis vollständig aufgeräumt.
- Push in `SeparateWorkingDirectory` nutzt Push-Sync (lokale Dateisynchronisation inkl. Delete-Sync), keinen Remote-Push.
- Workspace-Auflösung für Folgeoperationen (`CreateBranchAsync`, `CommitAsync`, `ResetAsync`) nutzt:
  1. In-Memory-Mapping des Plugin-Objekts,
  2. Pointer-Datei `.softwareschmiede-local-workspace` (auch nach Plugin-Neuinstanz).

## Grenzen, bekannte Einschränkungen und nächste Schritte

- Kein Remote-Provider-Contract: kein `git push`/`git pull` zu Remotes, keine PR-/Issue-API.
- Push/Pull im `SeparateWorkingDirectory` sind bewusst lokale Datei-Synchronisationen.
- Pull läuft nur auf sauberem Workspace; bei `uncommitted changes` wird hart abgebrochen.
- Bekannter Restpunkt: Ein expliziter UI-Bestätigungsfluss vor Pull ist fachlich vorgesehen, aber nicht Teil der aktuellen automatisierten Testabdeckung.

## Testabdeckung (relevante Nachweise)

- Unit: `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests.cs`
  - Bestätigungspflicht für `git init` in `InSourceDirectory`
  - Dirty-Workspace-Abbruch
  - Guardrails für `CopyMaxFiles` und `CopyMaxMegabytes`
  - Strategiematrix für `SeparateWorkingDirectory` (`clone`, `init+clone`, `copy`)
  - Push-Sync und Delete-Sync über `git status --porcelain`
  - Pull-Sync ohne Merge (nur bei sauberem Workspace)
  - Verzeichnisregeln (Quelle/Ziel verschieden, Ziel leer)
  - Fallback für `SourceDirectory` und Default-Auflösung des Zielpfads via `targetPath`
  - NotSupported-Verhalten für Remote-Methoden
- Integration: `src/Softwareschmiede.IntegrationTests/Infrastructure/Plugins/LocalDirectoryPluginIntegrationTests.cs`
  - End-to-End für Clone/Branch/Commit/Reset
  - Verhalten in `InSourceDirectory` und `SeparateWorkingDirectory`
  - Nicht-Git-Szenarien mit `git-init`-Opt-in und Copy-Fallback
  - Pointer-Datei-Verhalten bei Plugin-Neuinstanz
