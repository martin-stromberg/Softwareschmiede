# Tests: LocalDirectoryPlugin-Repositorystruktur-Tests

## Testklassen

### `LocalDirectoryPluginTests_GetRepositoryStructureAsync`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests_GetRepositoryStructureAsync.cs`

Spezialisierte Test-Klasse für die `GetRepositoryStructureAsync`-Funktionalität des `LocalDirectoryPlugin`.

#### Testmethoden

| Testmethode | Zweck | Status |
|------------|-------|--------|
| `GetRepositoryStructureAsync_ShouldReturnDirectories_UpToMaxDepth()` | Stellt sicher, dass Verzeichnisse bis zur konfigurierten Tiefe zurückgegeben werden; tiefere Verzeichnisse werden ausgeschlossen | ✓ Vorhanden (Zeilen 20–43) |
| `GetRepositoryStructureAsync_ShouldExcludeGitDirectory()` | Validiert, dass `.git`-Verzeichnisse und deren Inhalte ausgeschlossen werden | ✓ Vorhanden (Zeilen 45–65) |
| `GetRepositoryStructureAsync_ShouldReturnEmpty_ForNonExistentPath()` | Prüft, dass für nicht existierende Pfade eine leere Liste (ohne Exception) zurückgegeben wird | ✓ Vorhanden (Zeilen 67–76) |
| `GetRepositoryStructureAsync_ShouldReturnEmpty_ForEmptyUrl()` | Prüft, dass eine leere Repository-URL zu einer leeren Liste führt | ✓ Vorhanden (Zeilen 78–87) |
| `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledUpFront()` | Validiert, dass ein bereits vor dem Start abgebrochenes `CancellationToken` sofort eine `OperationCanceledException` wirft | ✓ Vorhanden (Zeilen 89–108) |
| **`GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal()`** | **Deckt den Abbruch *während* der laufenden Traversierung ab** (war vorher nicht getestet) — **FLAKY TEST** | ⚠️ Vorhanden (Zeilen 117–141) |
| `GetRepositoryStructureAsync_ShouldReturnEmpty_ForEmptyDirectory()` | Prüft, dass ein leeres Verzeichnis zu einer leeren Liste führt | ✓ Vorhanden (Zeilen 143–160) |

### Details zum Flaky Test: `GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal()`

**Zeilen 117–141:**
```csharp
[Fact]
public async Task GetRepositoryStructureAsync_ShouldThrow_WhenCancelledDuringTraversal()
{
    var root = Directory.CreateTempSubdirectory().FullName;
    try
    {
        // Großer, flacher Verzeichnisbaum, damit die Traversierung lange genug dauert, um den
        // Abbruch zuverlässig mitten in der Verarbeitung (statt vor dem Start) auszulösen.
        for (var i = 0; i < 3000; i++)  // ← ZEILE 124: TIMING-KRITISCHER WERT
        {
            Directory.CreateDirectory(Path.Combine(root, $"dir-{i:D5}"));
        }

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(5));  // ← ZEILE 131: ZU ENGES FENSTER (5ms)

        var act = () => sut.GetRepositoryStructureAsync(root, maxDepth: 2, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}
```

**Probleme:**
- **Zeile 124:** 3000 Verzeichnisse — unter CI-Last (GitHub Actions) kann die Traversierung dieser Menge schneller abgeschlossen sein als das 5ms-Fenster
- **Zeile 131:** 5 Millisekunden Delay ist zu klein; die Abbruch-Prüfung in `CollectDirectoryEntries()` (Zeile 345) wird möglicherweise nie erreicht, weil die Traversierung bereits abgeschlossen ist
- **Symptom:** Test schlägt sporadisch fehl, weil die erwartete `OperationCanceledException` nicht geworfen wird — stattdessen kehrt die Methode mit einer gefüllten `entries`-Liste zurück

**Abhängige Production-Code-Stelle:**
- `LocalDirectoryPlugin.GetRepositoryStructureLoadResultAsync()` (Zeilen 296–317)
- `LocalDirectoryPlugin.CollectDirectoryEntries()` (Zelines 319–368, Cancellation-Check Zeile 345)

### Hilfsmethoden

| Hilfsmethode | Zweck |
|-------------|-------|
| `CreateSut()` | Erstellt eine neue Instanz von `LocalDirectoryPlugin` mit Mock-Abhängigkeiten (`ICliRunner`, `ICredentialStore`) und `NullLogger` |

### Fixture/Temp-Daten

Alle Tests verwenden `Directory.CreateTempSubdirectory()` für isolierte Test-Verzeichnisse und löschen diese nach dem Test.
