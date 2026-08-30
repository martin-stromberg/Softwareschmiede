# Strukturierte Anforderungsübersetzung: Git-Branch-Erstellung ohne Upstream-Tracking

## Fachliche Zusammenfassung

Die Methode `GitPluginBase.CreateBranchAsync` erzeugt beim Checkout eines Task-Branches aus einem Basis-Branch ungewollt ein implizites Git-Upstream-Tracking auf den Basis-Branch (z. B. `origin/staging`). Das Git-Standardverhalten `branch.autoSetupMerge` richtet dieses Tracking automatisch ein, wenn weder `--track` noch `--no-track` explizit angegeben wird. Dies führt dazu, dass externe Git-Operationen (z. B. einfaches `git push` ohne Ziel) die Commits fälschlicherweise direkt in den Basis-Branch statt in einen neuen Task-Branch pushen. Der Bugfix soll `--no-track` zum `git checkout -b`-Aufruf hinzufügen, um dieses Tracking explizit zu deaktivieren.

## Betroffene Klassen und Komponenten

### Direkt zu ändernde Artefakte
- `Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
  - Methode `CreateBranchAsync(string localPath, string branchName, string? sourceBranchName = null, CancellationToken ct = default)` (Zeilen 112–123)

### Plugins (auf Überschreibungen prüfen)
- `Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` — Keine bekannte Override; nutzt Basismethode
- `Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs` — Keine bekannte Override; nutzt Basismethode (Prüfung erforderlich)
- `Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs` — Überschreibt `CreateBranchAsync`, ruft aber `base.CreateBranchAsync()` auf; Änderung wird dadurch automatisch vererbt

### Bestehende Tests (Anpassung erforderlich)
- `Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`
  - Test `CreateBranchAsync_ShouldRunCheckoutMinusB()` — Erwartet `["checkout", "-b", "feature/x"]` (ohne Quelle)
  - Test `CreateBranchAsync_ShouldThrow_WhenGitCheckoutFails()` — Erwartet `["checkout", "-b", "feature/x"]` (ohne Quelle)
  - Test `CreateBranchAsync_ShouldPropagateCancellation()` — Erwartet `["checkout", "-b", "feature/x"]` (ohne Quelle)
  - **Fehlender Test:** Test für `CreateBranchAsync` mit `sourceBranchName`-Parameter (aktuell nicht vorhanden)

- `Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
  - Test `CreateBranchAsync_ShouldCallGitCheckoutMinusB_WhenCalled()` — Prüft `checkout` und `-b`, aber nicht konkrete Argument-Sequenzen; ggf. keine Änderung erforderlich

## Implementierungsansatz

### Codeänderung in GitPluginBase.CreateBranchAsync
Die Logik in Zeilen 114–116 wird erweitert:

**Aktuell:**
```csharp
var args = string.IsNullOrEmpty(sourceBranchName)
    ? new List<string> { "checkout", "-b", branchName }
    : new List<string> { "checkout", "-b", branchName, $"origin/{sourceBranchName}" };
```

**Nach dem Fix:**
```csharp
var args = string.IsNullOrEmpty(sourceBranchName)
    ? new List<string> { "checkout", "-b", branchName }
    : new List<string> { "checkout", "-b", branchName, "--no-track", $"origin/{sourceBranchName}" };
```

### Begründung
- Wenn **keine** `sourceBranchName` angegeben ist: `git checkout -b <branchName>` — Erzeugt einen lokalen Branch aus dem aktuellen HEAD ohne Upstream-Tracking (bereits korekt).
- Wenn **eine** `sourceBranchName` angegeben ist: `git checkout -b <branchName> --no-track origin/<sourceBranchName>` — Erzeugt einen Branch, der von `origin/<sourceBranchName>` abgezweigt wird, jedoch **kein** Upstream-Tracking auf diesen Branch erhält.

### Abhängigkeiten
- Nur Änderung in der Git-Argument-Liste erforderlich; kein neues Interface oder Service nötig
- `RunGitAsync()` wird unverändert aufgerufen; keine Änderung der Fehlerbehandlung erforderlich

## Konfiguration

Entfällt — das Verhalten wird durch die Git-Kommandozeilenflag gesteuert und erfordert keine neue Konfiguration.

## Offene Fragen und Prüfschritte

1. **Prüfung BitBucket-Plugin:** Existiert eine `CreateBranchAsync`-Override in `BitBucketPlugin`? Falls ja, ist die Anpassung dort auch notwendig.
2. **Test-Abdeckung für sourceBranchName:** Der Test `CreateBranchAsync_ShouldRunCheckoutMinusB()` prüft den Fall *ohne* `sourceBranchName`. Ein Test für den Fall *mit* `sourceBranchName` existiert anscheinend noch nicht und sollte hinzugefügt werden (Argument-Sequenz: `["checkout", "-b", "feature/x", "--no-track", "origin/staging"]`).
3. **Integration-Tests:** Sollten E2E-Integrationstests (z. B. `LocalDirectoryPluginIntegrationTests`) überprüft werden, um zu bestätigen, dass das Upstream-Tracking nach der Änderung nicht mehr automatisch gesetzt wird?
4. **Dokumentation:** Ist eine Aktualisierung von Inline-Kommentaren oder Dokumentation zu Git-Befehlen erforderlich?
5. **App-interne Push-Methoden:** `GitHubPlugin.PushBranchAsync` und `BitBucketPlugin.PushBranchAsync` verwenden bereits `git push --set-upstream origin <branchName>`, was das Tracking korrekt setzt. Keine Änderung dort erforderlich, aber Bestätigung empfohlen.
