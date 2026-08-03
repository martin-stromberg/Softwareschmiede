# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### RepositoryAssignDialog.xaml

- **Korrektheit / Inkonsistenz** — Im Abschnitt "Basis-Branch-Auswahl" (Zeilen 129–166) werden eine `ComboBox` (`AvailableSourceBranches`) und eine `TextBox` (`WorkingDirectoryInputText`-Analogon: direkt `DefaultSourceBranchName`) **gleichzeitig und dauerhaft sichtbar** dargestellt, beide `TwoWay` an dieselbe Eigenschaft `DefaultSourceBranchName` gebunden. Anders als im unmittelbar darüberliegenden, strukturell identischen Abschnitt "Arbeitsverzeichnis-Auswahl" (Zeilen 90–127), der `ComboBox` und `TextBox` über `Visibility="{Binding IsWorkingDirectoryManualInput, ...}"` exklusiv umschaltet, fehlt für die Basis-Branch-Eingabe eine entsprechende Umschalt-Logik komplett — weder in der XAML (kein `Visibility`-Binding auf einer der beiden Controls) noch im ViewModel (`RepositoryAssignViewModel` hat keine zu `IsWorkingDirectoryManualInput` analoge Eigenschaft für den Basis-Branch, siehe `RepositoryAssignViewModel.cs`). Der Nutzer sieht dadurch immer beide Eingabeelemente übereinander für denselben Wert, unabhängig davon, ob Branches erfolgreich geladen wurden (z. B. bei GitHub-Repositories, wo die Liste tatsächlich gefüllt ist).

  Empfehlung: Entweder die `TextBox` analog zum Arbeitsverzeichnis-Muster über eine neue Eigenschaft (z. B. `IsSourceBranchManualInput`) exklusiv mit der `ComboBox` umschalten, oder falls beide Eingabewege dauerhaft nebeneinander gewünscht sind, dies bewusst im Layout kennzeichnen (z. B. "oder manuell eingeben") statt zwei optisch gleichwertige, aber ununterscheidbare Controls übereinander zu stapeln.

### ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Fehlerbehandlung / Testabdeckung** — `SaveSourceBranchAsync` (Zeilen 594–623) persistiert `SelectedRepositorySourceBranchName` ungeprüft über `_projektService.UpdateRepositorySourceBranchAsync`, ohne vorher zu validieren, ob der eingegebene Branch-Name in `AvailableSourceBranchesForEdit` enthalten ist. Das analoge `RepositoryAssignViewModel` validiert denselben Anwendungsfall dagegen explizit über `ValidateSourceBranchInput()` und blockiert die Bestätigung bei unbekanntem Branch (`SourceBranchInputError` wird gesetzt, `CanConfirm()` liefert `false`). In `ProjectDetailViewModel` existiert keine entsprechende Validierungsmethode; ein Tippfehler im Bearbeitungsfeld wird kommentarlos gespeichert und fällt erst beim nächsten Aufgabenstart als `GitBranchNotFoundException` auf (siehe `EntwicklungsprozessService.ValidateBaseBranchExistsAsync`). Die neue Testklasse `ProjectDetailViewModelTests_BasisBranch.cs` deckt entsprechend auch keinen Fall mit ungültigem Branch-Namen ab (im Gegensatz zu `RepositoryAssignViewModelTests_BasisBranch.SourceBranchValidation_ShouldFail_WhenBranchDoesNotExist`).

  Empfehlung: Vor dem Aufruf von `UpdateRepositorySourceBranchAsync` prüfen, ob `SelectedRepositorySourceBranchName` (falls nicht leer) in `AvailableSourceBranchesForEdit` enthalten ist, andernfalls `SourceBranchInputError` setzen und das Speichern abbrechen — analog zu `RepositoryAssignViewModel.ValidateSourceBranchInput()`. Ergänzend einen Testfall für den Negativpfad hinzufügen.

### ProjektService.cs (ProjektService)

- **Namenskonventionen / Einheitlichkeit** — Die erweiterte Überladung `AddRepositoryAsync(Guid projektId, string pluginTyp, string repositoryUrl, string repositoryName, CancellationToken ct = default, string? defaultSourceBranchName = null)` (Zeilen 131–137) platziert `CancellationToken ct` **vor** dem neuen optionalen Parameter `defaultSourceBranchName` statt danach. Das widerspricht der in der gesamten übrigen Codebasis konsequent eingehaltenen Konvention "CancellationToken ist immer der letzte Parameter" (vgl. jede andere Methode in `ProjektService`, `EntwicklungsprozessService`, `GitOrchestrationService` etc.). Sichtbare Folge: Aufrufer müssen entweder auf positionale Aufrufe mit zufällig passender Reihenfolge vertrauen oder explizit benannte Argumente verwenden, z. B. `ProjectDetailViewModel.RepositoryZuweisenAsync` (`..., ct, vm.DefaultSourceBranchName)`, oder `ProjectListViewModel.cs`, wo ein bestehender rein positionaler Aufruf im Zuge dieser Änderung auf `ct: ct` umgestellt werden musste.

  Empfehlung: Parameterreihenfolge auf `AddRepositoryAsync(Guid projektId, string pluginTyp, string repositoryUrl, string repositoryName, string? defaultSourceBranchName = null, CancellationToken ct = default)` ändern und alle Aufrufstellen entsprechend anpassen.

### GitBranchNotFoundException.cs

- **Namenskonventionen / Einheitlichkeit** — Die neue Exception-Klasse liegt im Namespace `Softwareschmiede.Domain.Enums` (Datei `src/Softwareschmiede/Domain/Enums/GitBranchNotFoundException.cs`), obwohl sie kein Enum ist, sondern eine `InvalidOperationException`-Ableitung. Der Namespace-Ordner `Domain.Enums` enthält sonst ausschließlich Enum-Typen; die Platzierung dort erschwert das Auffinden und widerspricht der sonst im Projekt sauber getrennten Ordnerstruktur (z. B. `Domain.Entities`, `Domain.Interfaces`, `Domain.Abstractions`).

  Empfehlung: Klasse in einen passenden Namespace/Ordner verschieben, z. B. `Domain.Exceptions` (ggf. neu anlegen) oder `Domain.Entities`, konsistent mit dem Namespace anderer Domain-Exceptions im Projekt.

### EntwicklungsprozessService.cs (EntwicklungsprozessService)

- **Effizienz / Duplizierter Code** — In `SetupBranchAsync` (Zeilen 445–492) wird `gitPlugin.GetDefaultBranchAsync(repositoryUrl, ct)` potenziell zweimal in derselben Ausführung aufgerufen: einmal zu Beginn, um `basisBranchName` gegen den Standard-Branch zu vergleichen (Zeile 457), und – falls dieser Vergleich Gleichheit ergibt (`nutzeExistierendenBranch = false`) und zusätzlich `defaultSourceBranchName` konfiguriert ist – ein zweites Mal im `else`-Zweig (Zeile 474), um denselben Standard-Branch erneut gegen `defaultSourceBranchName` zu vergleichen. Dieser Pfad ist real erreichbar (z. B. wenn ein Nutzer explizit `basisBranchName` gleich dem Remote-Standard-Branch übergibt, während am Repository zusätzlich ein `DefaultSourceBranchName` konfiguriert ist) und verursacht einen unnötigen doppelten Remote-/CLI-Aufruf (`git ls-remote` je nach Plugin).

  Empfehlung: Das Ergebnis von `GetDefaultBranchAsync` einmalig ermitteln (lazy/gecached innerhalb der Methode) und für beide Vergleiche wiederverwenden, statt es im `else`-Zweig erneut abzufragen.

### GitOrchestrationService.cs (GitOrchestrationService)

- **Fehlerbehandlung / Konsistenz** — `ResolveDefaultSourceBranchNameAsync` (Zeilen 371–388) liefert bei mehreren aktiven Repositories im Projekt (Mehrdeutigkeit) still `null` zurück, ohne dies zu loggen. Die strukturell identische Nachbarmethode `ResolveSelectedPluginPrefixAsync` (Zeilen 338–368) behandelt denselben Mehrdeutigkeitsfall dagegen explizit mit `_logger.LogWarning(...)`. Dadurch ist ein stillschweigend ignorierter, konfigurierter Basis-Branch bei mehreren Repositories im Log nicht nachvollziehbar, obwohl das Nachbarmuster in derselben Klasse zeigt, dass dieser Fall als loggenswert eingestuft wird.

  Empfehlung: In `ResolveDefaultSourceBranchNameAsync` analog zu `ResolveSelectedPluginPrefixAsync` einen `LogWarning`-Aufruf ergänzen, wenn `aktiveRepositories.Count > 1` und dadurch kein Basis-Branch aufgelöst werden kann.

## Geprüfte Dateien

- `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs`
- `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`
- `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectListViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/RepositoryAssignViewModel.cs`
- `src/Softwareschmiede.App/Views/ArbeitsverzeichnisBearbeitenDialog.xaml`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Views/RepositoryAssignDialog.xaml`
- `src/Softwareschmiede.IntegrationTests/Services/EntwicklungsprozessServiceTests.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/ProjectDetailViewModelTests_BasisBranch.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/RepositoryAssignViewModelTests_BasisBranch.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests_BasisBranch.cs`
- `src/Softwareschmiede.Tests/Application/Services/EntwicklungsprozessServiceTests_WorkingDirectoryValidation.cs`
- `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
- `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_RepositoryManagementTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_WorkingDirectory.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubPluginTests.cs`
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`
- `src/Softwareschmiede/Application/Services/ProjektService.cs`
- `src/Softwareschmiede/Domain/Entities/GitRepository.cs`
- `src/Softwareschmiede/Domain/Enums/GitBranchNotFoundException.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20260803163323_AddDefaultSourceBranchNameToGitRepository.cs`
- `src/Softwareschmiede/Migrations/20260803163323_AddDefaultSourceBranchNameToGitRepository.Designer.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
