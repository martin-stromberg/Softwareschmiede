# Tests zu CreateBranchAsync

## Testklassen

### `GitPluginBaseTests`
Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`

| Testmethode | Was wird getestet? | Beobachtung |
|-------------|-------------------|------------|
| `CreateBranchAsync_ShouldRunCheckoutMinusB()` (Zeilen 14-30) | Basismethode führt `git checkout -b <branchName>` aus (ohne Quellenangabe) | Erwartet: `["checkout", "-b", "feature/x"]` |
| `CreateBranchAsync_ShouldThrow_WhenGitCheckoutFails()` (Zeilen 32-50) | Basismethode wirft `InvalidOperationException` wenn `git checkout -b` fehlschlägt | Erwartet: `["checkout", "-b", "feature/x"]` |
| `CreateBranchAsync_ShouldPropagateCancellation()` (Zeilen 52-72) | Basismethode propagiert `CancellationToken` korrekt | Erwartet: `["checkout", "-b", "feature/x"]` |

**Fehlender Test:** Kein Test für `CreateBranchAsync` mit `sourceBranchName`-Parameter. Aktuell wird nur der Fall ohne Quellenangabe getestet. Ein Test mit `sourceBranchName="staging"` fehlt, der die Argument-Sequenz `["checkout", "-b", "feature/x", "origin/staging"]` erwarten würde.

---

### `GitHubPluginTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`

| Testmethode | Was wird getestet? | Beobachtung |
|-------------|-------------------|------------|
| `CreateBranchAsync_ShouldCallGitCheckoutMinusB_WhenCalled()` (Zeilen 617-640) | GitHubPlugin nutzt Basismethode korrekt | Prüft: `a.Contains("checkout") && a.Contains("-b")`, aber nicht die exakte Argument-Sequenz. |

**Beobachtung:** Der Test ist nicht so präzise wie die GitPluginBaseTests — er prüft nur das Vorhandensein von Flaggen, nicht die exakte Reihenfolge oder Abwesenheit von `--no-track`.

---

## Hilfsmethoden und Test-Fixtures

### `TestGitPlugin`
Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs` (Zeilen 252-275)

Eine interne Test-Konkretisierung von `GitPluginBase<TestGitPlugin>`, die verwendet wird, um die Basismethoden isoliert zu testen:

```csharp
private sealed class TestGitPlugin(ICliRunner cliRunner) : GitPluginBase<TestGitPlugin>(cliRunner)
{
    public override string PluginName => "Test";
    public override string PluginPrefix => "Test";
    public override PluginType PluginType => PluginType.SourceCodeManagement;
    // ... weitere erforderliche Implementierungen
}
```

Diese Hilfsklasse wird von allen GitPluginBaseTests verwendet, um die Basismethoden ohne Plugin-spezifische Abhängigkeiten zu testen.

---

## Test-Abdeckungslücke

**Zusammenfassung der fehlenden Abdeckung:**

1. **Kein Test für `CreateBranchAsync` mit `sourceBranchName`**: 
   - Sollte testen, dass die Methode `git checkout -b <branchName> origin/<sourceBranchName>` aufruft.
   - Laut Anforderung wird der Fix `--no-track` zu dieser Variante hinzufügen, aber der Test für die Original-Variante existiert noch nicht.

2. **Keine Tests in BitBucketPlugin oder LocalDirectoryPlugin spezifisch für CreateBranchAsync**:
   - LocalDirectoryPlugin hat eine Überreitungsimplementierung, aber keinen spezifischen Test dafür.
   - BitBucketPlugin nutzt die Basismethode, hat aber auch keinen spezifischen Test.

3. **Integration/E2E-Tests fehlen**:
   - Keine Tests, die verifizieren, dass das Upstream-Tracking nach `CreateBranchAsync` nicht gesetzt ist (oder nach dem Fix mit `--no-track` nicht gesetzt wird).
