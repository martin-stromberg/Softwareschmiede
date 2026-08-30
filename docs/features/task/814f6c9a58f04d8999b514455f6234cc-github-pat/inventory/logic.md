# Logik-Klassen: Git-Branch-Erstellung

## `GitPluginBase<TPlugin>`
Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateBranchAsync(string localPath, string branchName, string? sourceBranchName = null, CancellationToken ct = default)` | public virtual | Erstellt einen neuen Git-Branch mit `git checkout -b`. Wenn `sourceBranchName` angegeben, wird von `origin/{sourceBranchName}` abgezweigt. |
| `CheckoutRemoteBranchAsync(string localPath, string branchName, CancellationToken ct = default)` | public virtual | Checked einen Remote-Branch mit `git checkout -b branchName --track origin/branchName` aus. |
| `CommitAsync(string localPath, string message, CancellationToken ct = default)` | public virtual | Erstellt einen Commit mit `git add .` und `git commit -m`. |
| `ResetAsync(string localPath, string resetType, string? targetRef, CancellationToken ct = default)` | public virtual | Setzt den HEAD auf einen Commit zurück mit `git reset`. |
| `RunGitAsync(IEnumerable<string> args, string? workingDirectory, CancellationToken ct = default, IDictionary<string, string>? environmentVariables = null)` | protected | Führt eine Git-Kommandozeile aus. |

**Implementierungsdetails der Basismethode (Zeilen 112-123):**

```csharp
public virtual async Task CreateBranchAsync(string localPath, string branchName, string? sourceBranchName = null, CancellationToken ct = default)
{
    var args = string.IsNullOrEmpty(sourceBranchName)
        ? new List<string> { "checkout", "-b", branchName }
        : new List<string> { "checkout", "-b", branchName, $"origin/{sourceBranchName}" };

    var result = await RunGitAsync(args, localPath, ct);
    if (!result.IsSuccess)
    {
        throw new InvalidOperationException($"git checkout -b fehlgeschlagen: {result.StdErr}");
    }
}
```

**Problematisches Verhalten (gemäß Anforderung):**
- Wenn `sourceBranchName` angegeben ist, wird Git die Kommandozeile `git checkout -b <branchName> origin/<sourceBranchName>` ohne explizit `--track` oder `--no-track` ausführen.
- Git's Standardverhalten (`branch.autoSetupMerge=true`) richtet dann automatisch ein Upstream-Tracking auf `origin/<sourceBranchName>` ein.
- Dies führt dazu, dass `git push` ohne Zielangabe die Commits direkt in den Basis-Branch pusht.

---

## `LocalDirectoryPlugin`
Datei: `src/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateBranchAsync(string localPath, string branchName, string? sourceBranchName = null, CancellationToken ct = default)` | public override | Überschreibung, die den Workspace-Pfad auflöst und dann `base.CreateBranchAsync()` aufruft. Siehe Zeilen 175-180. |

**Überreitungsimplementierung (Zeilen 175-180):**

```csharp
public override async Task CreateBranchAsync(string localPath, string branchName, string? sourceBranchName = null, CancellationToken ct = default)
{
    var workspacePath = ResolveWorkspacePath(localPath);
    await EnsureGitRepositoryAsync(workspacePath, ct);
    await base.CreateBranchAsync(workspacePath, branchName, sourceBranchName, ct);
}
```

**Konsequenz:** Die Änderung in der Basismethode wird automatisch von dieser Überreitungsimplementierung vererbt, da sie `base.CreateBranchAsync()` aufruft.

---

## `GitHubPlugin` und `BitBucketPlugin`
Dateien: 
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`

**Feststellung:** Keine Überreitungsimplementierung von `CreateBranchAsync` vorhanden. Beide Klassen nutzen die Basismethode aus `GitPluginBase<TPlugin>`.
