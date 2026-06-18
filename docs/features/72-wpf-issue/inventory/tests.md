# Tests

## Testklassen

### `AufgabeServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests.cs`

| Testmethode | Beschreibung |
|------------|--------------|
| `CreateAsync_ShouldCreateAufgabeWithStatusOffen_WhenCalledWithValidData` | Prüft, dass `CreateAsync` eine Aufgabe mit Status `Neu` erstellt |
| `CreateFromIssueAsync_ShouldCreateAufgabeWithIssueReferenz_WhenCalledWithValidIssue` | **Prüft, dass `CreateFromIssueAsync` eine Aufgabe mit `IssueReferenz` aus einem `Issue` erstellt** |
| `GetByProjektAsync_ShouldReturnAufgabenForProjekt_WhenAufgabenExist` | Prüft, dass alle Aufgaben eines Projekts zurückgegeben werden |
| `StartenAsync_ShouldSetStatusGestartetAndBranchName_WhenAufgabeExists` | Prüft, dass `StartenAsync` Status auf `Gestartet` setzt und Branch/Pfad speichert |

**Hinweis:** Der Test `CreateFromIssueAsync_ShouldCreateAufgabeWithIssueReferenz_WhenCalledWithValidIssue` validiert bereits:
- Dass `IssueReferenz` erstellt wird
- Dass `IssueNummer` korrekt gespeichert wird
- Dass `Milestone` korrekt gespeichert wird
- Dass Titel aus dem Issue übernommen wird

---

### `AufgabeServiceTests` (Integration)
Datei: `src/Softwareschmiede.IntegrationTests/Services/AufgabeServiceTests.cs`

| Testmethode | Beschreibung |
|------------|--------------|
| `CreateAsync_ShouldPersistAufgabe_WhenValidDataGiven` | Prüft Persistenz von `CreateAsync` über neuen Context |
| `StartenAsync_ShouldSetStatusInBearbeitungAndPersistBranchInfo_WhenAufgabeExists` | Prüft Persistenz von `StartenAsync` |
| `AbschliessenAsync_ShouldSetStatusAbgeschlossenAndClearBranchInfo_WhenAufgabeInBearbeitung` | Prüft Persistenz von `AbschliessenAsync` |

---

### `ProjectDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`

| Testmethode | Beschreibung |
|------------|--------------|
| `ProjektSpeichernAsync_ErstelltNeuesProjekt_WennIdLeer` | Prüft Neu-Anlage eines Projekts |
| `ProjektSpeichernAsync_AktualisiertBestehendesProjekt_WennIdVorhanden` | Prüft Update eines bestehenden Projekts |

**Hinweis:** Es gibt noch keine Tests für Issue-Laden oder Issue-zu-Aufgabe-Konvertierung im ViewModel.

---

### `TaskDetailViewModelTests`
Datei: `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`

Diese Testklasse setzt Mock-Objekte für:
- `AufgabeService`
- `ProtokollService`
- `KiAusfuehrungsService`
- `EntwicklungsprozessService`
- `PluginSelectionService`
- `IDialogService`
- Git- und KI-Plugins (Mocks)

**Hinweis:** Es gibt noch keine Tests für Issue-Zuweisung oder Issue-Browser-Integration im TaskDetailViewModel.

---

### `GitPluginBaseTests`
Datei: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`

Basisklasse-Tests für Git-Plugin-Funktionalität.

---

### `GitHubPluginTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`

Tests für das GitHub-Plugin, wahrscheinlich einschließlich Tests für `GetIssuesAsync`.

---

### `LocalDirectoryPluginTests`
Datei: `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests.cs`

Tests für das LocalDirectory-Plugin.

---

## Plugin-Implementierungen

### `GitHubPlugin`
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`

**`GetIssuesAsync` Implementierung (Zeile 316-334):**

```csharp
public override async Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default)
{
    _logger.LogInformation("Rufe Issues für Repository {RepositoryId} ab.", repositoryId);

    var result = await _cliRunner.RunAsync(
        "gh",
        ["issue", "list", "--repo", repositoryId, "--json", "number,title,body,labels,milestone", "--limit", "100"],
        null,
        GetGhEnvironment(),
        ct);

    if (!result.IsSuccess)
    {
        _logger.LogError("gh issue list fehlgeschlagen für {RepositoryId}: {StdErr}", repositoryId, result.StdErr);
        return [];
    }

    return ParseIssues(result.StdOut);
}
```

**Funktionsweise:**
- Nutzt `gh issue list` CLI-Befehl
- Lädt bis zu 100 Issues
- Parst JSON-Ausgabe und konvertiert in `Issue` Value Objects
- Enthält Error-Handling mit Grace-Degradation (leere Liste bei Fehler)

**Hinweis:** Lädt nur offene Issues (Default-Verhalten von `gh issue list`).

---

### `LocalDirectoryPlugin`
Datei: `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`

**`GetIssuesAsync` Implementierung (Zeile 122-123):**

```csharp
public override Task<IEnumerable<Issue>> GetIssuesAsync(string repositoryId, CancellationToken ct = default)
    => throw BuildNotSupported(nameof(GetIssuesAsync));
```

**Funktionsweise:**
- Wirft `NotImplementedException` (oder ähnlich)
- Issue-Funktionalität wird nicht unterstützt
