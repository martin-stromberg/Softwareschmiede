# Interfaces und Contracts

## `IGitPlugin`
**Datei:** `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`

### Relevante Methoden für Diesen Issue

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateBranchAsync(string localPath, string branchName, string? sourceBranchName, CancellationToken ct)` | `localPath` — Lokaler Pfad des geklonten Repositories; `branchName` — Name des neuen Branches; `sourceBranchName` — Optionaler Basis-Branch (null = aktueller HEAD); `ct` — Cancellation Token | `Task` | **KRITISCH:** Legt einen neuen Branch im lokalen Klon an. Dies ist die Methode, die in `AutonomAufgabeInitialisierungsDialogViewModel.NeuenBranchAnlegenAsync()` (Zeile 344) aufgerufen wird und bei autonomen Aufgaben fehlschlägt, weil `localPath` null ist. |
| `GetRemoteBranchesAsync(string repositoryUrl, CancellationToken ct)` | `repositoryUrl` — URL des Repositories; `ct` — Cancellation Token | `Task<IEnumerable<string>>` | Listet alle Remote-Branches eines Repositories auf (ohne Klon) — wird in `LadeProjektBranchesAsync()` verwendet (Zeile 250). |
| `GetDefaultBranchAsync(string repositoryUrl, CancellationToken ct)` | `repositoryUrl` — URL des Repositories; `ct` — Cancellation Token | `Task<string>` | Ermittelt den Standard-Branch eines Repositories (z.B. "main" oder "master"). |

**Relevanz für Issue:**
- `CreateBranchAsync` ist die problematische Stelle: Sie setzt voraus, dass ein lokaler Klon bereits existiert.
- Bei autonomen Aufgaben sollte diese Methode nicht im Dialog aufgerufen werden, sondern erst nach dem Klon-Anlegen im Service.
- Die Dialog-Logik braucht möglicherweise eine andere Strategie, um neue Branch-Namen zu validieren (ohne Git-Operation).

---

## `ICliRunner`
**Datei:** Nicht vollständig in den Globs gefunden, aber verwendet in mehreren Services.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RunAsync(string command, IEnumerable<string> args, string? workingDirectory, IDictionary<string, string>? environmentVariables, CancellationToken ct)` | `command` — Befehl (z.B. "git"); `args` — Argumente als Collection; `workingDirectory` — Arbeitsverzeichnis; `environmentVariables` — Umgebungsvariablen; `ct` — Cancellation Token | `Task<CliResult>` | Führt einen CLI-Befehl aus und liefert Erfolgs-/Fehler-Information. |

**Verwendung in Diesem Issue:**
- In `UnteragentGitProvisioningService.ProvisioniereAsync()` (Zeile 34) wird dies verwendet, um `git branch` auszuführen.
- `AutonomAufgabenInitialisierungsService` könnte die gleiche Methode nutzen, um `git branch` im geklonten Repo auszuführen.

**Relevanz für Issue:**
- Dies ist der Low-Level-Mechanismus, um Git-Operationen durchzuführen.
- `UnteragentGitProvisioningService` zeigt das korrekte Muster für Branch-Erstellung via `_cliRunner`.

---

## `IPluginManager`
**Datei:** Nicht in den Globs gefunden, aber verwendet im ViewModel.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetSourceCodeManagementPlugins()` | Keine | `List<IGitPlugin>` | Liefert alle verfügbaren Git-Plugins. |

**Verwendung im ViewModel:**
- Zeile 296 in `AutonomAufgabeInitialisierungsDialogViewModel.ResolveGitPlugin()`.

**Relevanz für Issue:**
- Wird verwendet, um das passende Git-Plugin für die Remote-Branch-Abfrage zu finden.
- Für lokale Branch-Operationen wird das Plugin nicht benötigt; stattdessen wäre `ICliRunner` ausreichend.
