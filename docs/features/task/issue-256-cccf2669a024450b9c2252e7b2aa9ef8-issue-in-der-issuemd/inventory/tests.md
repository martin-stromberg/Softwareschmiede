# Tests

## Testklassen

### `EntwicklungsprozessServiceTests`
Datei: `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`

Tests, die `CreateIssueFileAsync` direkt oder indirekt abdecken:

| Testmethode | Was wird getestet? |
|---|---|
| `CreateIssueFileAsync_ShouldCreateIssueFileWithCorrectContent_WhenAufgabeExists` | `issue.md` wird im richtigen Verzeichnis erstellt; Inhalt enthält Aufgabentitel, Aufgaben-ID und Branch-Name |
| `CreateIssueFileAsync_ShouldUseFallbackText_WhenAnforderungsBeschreibungIsNullOrEmpty` | Fallback-Text `[Keine Anforderungsbeschreibung verfügbar]` wird verwendet, wenn `AnforderungsBeschreibung` null oder leer ist |
| `CreateIssueFileAsync_ShouldLogWarning_WhenFileCreationFails` | Schreibfehler auf die `issue.md` (readonly-Datei) führt zu einem Log-Warning, wirft aber keine Exception |
| `ProzessStartenAsync_ShouldWriteIssueFileAndGitignoreIntoWorkingDirectory_WhenWorkingDirectoryConfigured` | `issue.md` und `.gitignore` werden ins konfigurierte Unterverzeichnis (`WorkingDirectoryRelativePath`) geschrieben, nicht in den Repository-Root |
| `ProzessStartenAsync_ShouldContinue_WhenIssueFileCreationFails` | Der Gesamtprozess läuft weiter, auch wenn `CreateIssueFileAsync` fehlschlägt |
| `ProzessStartenAsync_ShouldCreateIssueBranch_WhenAufgabeHasIssueReference` | Wenn eine `IssueReferenz` mit Nummer gesetzt ist, wird `IssueNummer` im Branch-Namen verwendet; die Datei-Seite der Issue-Referenz wird dabei **nicht** getestet |
| `ProzessStartenUndCliStartenAsync_ShouldStartTaskWithoutIssueReference_WhenSingleProjectRepositoryExists` | Aufgabe ohne Issue-Referenz wird korrekt gestartet; `IssueReferenz` ist `null` nach dem Start |

Tests, die `IssueReferenz` in anderen Kontexten einsetzen:

| Testmethode | Was wird getestet? |
|---|---|
| `PullRequestErstellenAsync_ShouldAppendClosingDirective_WhenLegacyPathHasIssueReference` | Der PR-Body wird um eine Closing-Direktive ergänzt, wenn eine `IssueReferenz` gesetzt ist |

**Noch nicht vorhandene Tests (Lücke laut Anforderung):**
- `CreateIssueFileAsync_ShouldIncludeIssueReference_WhenIssueReferenzIsSet`
- `CreateIssueFileAsync_ShouldNotIncludeIssueReference_WhenIssueReferenzIsNull`

## Hilfsmethoden

### `EntwicklungsprozessServiceTests` (private Hilfsmethoden in der Testklasse)

| Methode | Beschreibung |
|---|---|
| `SetupCloneWithDirectoryCreation(string?)` | Konfiguriert `_gitPluginMock` so, dass das Klon-Verzeichnis tatsächlich auf der Festplatte angelegt wird; gibt den Basis-Pfad zurück |
| `SetupCloneMocks()` | Einfaches Setup der Clone- und Branch-Mocks ohne Verzeichniserstellung |
| `DeleteDirectoryIfExists(string)` | Löscht das Test-Verzeichnis inkl. aller Attribute (ReadOnly etc.) |
| `CreatePluginSelectionService(params IKiPlugin[])` | Erstellt einen `PluginSelectionService` mit gemocktem `IPluginManager` |
