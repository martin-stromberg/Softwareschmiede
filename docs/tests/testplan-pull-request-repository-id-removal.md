# Testplan – Pull-Request-Repository-ID entfernen

## Ziel
Die in `docs/tests/testluecken-pull-request-repository-id-removal.md` beschriebenen Restlücken für den PR-Workflow schließen.

## Abgedeckte Testbereiche

1. **Service-Layer**
   - `GitOrchestrationServiceTests`
   - Fehlende Guards für `PullRequestErstellenAsync`
   - Default-Titel/-Body
   - `ResolveRepositoryIdAsync` bei fehlendem Projekt bzw. fehlenden aktiven Repositories
   - `ExtractRepositoryIdFromUrl` für SSH- und Fehlerformate

2. **UI-Code-Behind**
   - `AufgabeDetailFolgePromptTests` um PR-bezogene Initialisierung und Handler-Logik erweitern
   - Erfolgs- und Fehlerpfade von `PullRequestErstellenAsync`
   - Titel-Validierung
   - Schließen/Zurücksetzen des Formularzustands nach Erfolg

3. **Markup-Contract**
   - `AufgabeDetail`-Markup auf fehlendes Repository-ID-Feld prüfen
   - Sichtbarkeit und Bindings des PR-Formulars absichern

## Konkrete Arbeitspakete

### AP-01: GitOrchestrationService vollständig absichern
- Neue Tests in `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
- Fälle:
  - `PullRequestErstellenAsync_ShouldThrowInvalidOperationException_WhenTaskIsMissing()`
  - `PullRequestErstellenAsync_ShouldThrowInvalidOperationException_WhenBranchNameIsMissing()`
  - `PullRequestErstellenAsync_ShouldUseTaskTitleAsDefault_WhenTitleIsNull()`
  - `PullRequestErstellenAsync_ShouldUseGeneratedBodyAsDefault_WhenBodyIsNull()`
  - `ResolveRepositoryIdAsync_ShouldThrowInvalidOperationException_WhenProjectIsMissing()`
  - `ResolveRepositoryIdAsync_ShouldThrowInvalidOperationException_WhenProjectHasNoActiveRepositories()`
  - `ExtractRepositoryIdFromUrl_ShouldSupportSshUrlAndThrowForInvalidFormats()`

### AP-02: AufgabeDetail-PR-Flow absichern
- Neue oder erweiterte Tests in `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/`
- Fälle:
  - `OnInitializedAsync_ShouldPrepopulatePullRequestTitleFromTaskTitle()`
  - `PullRequestErstellenAsync_ShouldShowValidationError_WhenTitleIsBlank()`
  - `PullRequestErstellenAsync_ShouldCloseFormAndRefreshAfterSuccess()`
  - `PullRequestErstellenAsync_ShouldSurfaceError_WhenServiceThrows()`

### AP-03: Markup-Regression verhindern
- Neuer Markup-Contract-Test für `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor`
- Absichern:
  - kein Repository-ID-Feld im PR-Formular
  - PR-Formular nur über die erwartete Sichtbarkeitslogik erreichbar

## Validierungskriterien

- Die oben genannten Tests existieren oder sind explizit durch andere Tests abgedeckt.
- Der PR-Workflow bleibt ohne Repository-ID-Feld funktionsfähig.
- Die Service-Guards und Default-Fallbacks sind gegen Regressionen abgesichert.
