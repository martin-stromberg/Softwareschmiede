# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Verifikation der Befunde aus Iteration 1

Alle 6 in `review-code.1.md` gemeldeten Befunde wurden unabhängig geprüft und sind korrekt behoben:

1. **RepositoryAssignDialog.xaml** — `IsSourceBranchManualInput`-Eigenschaft in `RepositoryAssignViewModel` ergänzt; `ComboBox`/`TextBox` für den Basis-Branch schalten jetzt exklusiv über `Visibility`-Bindings um, analog zum Arbeitsverzeichnis-Muster. Korrekt behoben.
2. **ProjectDetailViewModel.SaveSourceBranchAsync** — `ValidateSourceBranchInput()` wurde ergänzt und wird vor dem Persistieren aufgerufen (Zeile 599, 635–646); ein Negativ-Testfall (`ProjectDetailVM_SaveSourceBranch_ShouldFail_WhenBranchDoesNotExist`) wurde ergänzt. Korrekt behoben.
3. **ProjektService.AddRepositoryAsync** — Parameterreihenfolge korrigiert, `CancellationToken ct = default` ist jetzt der letzte Parameter (Zeile 131–137); Aufrufstellen (`ProjectDetailViewModel`, `ProjectListViewModel`) sind konsistent aktualisiert. Korrekt behoben.
4. **GitBranchNotFoundException.cs** — Klasse wurde von `Domain.Enums` nach `Domain.Exceptions` (neuer Ordner/Namespace) verschoben; die alte Datei in `Domain.Enums` existiert nicht mehr. Korrekt behoben.
5. **EntwicklungsprozessService.SetupBranchAsync** — `GetDefaultBranchAsync` wird jetzt einmalig ermittelt und über `defaultBranch ??= ...` in beiden Vergleichsstellen wiederverwendet (Zeile 455, 459, 476). Korrekt behoben.
6. **GitOrchestrationService.ResolveDefaultSourceBranchNameAsync** — `LogWarning` wurde analog zu `ResolveSelectedPluginPrefixAsync` ergänzt, wenn mehrere aktive Repositories eine eindeutige Basis-Branch-Auflösung verhindern (Zeile 387–393). Korrekt behoben.

## Befunde

### ProjectDetailView.xaml

- **Korrektheit / Inkonsistenz** — In der neuen "Basis-Branch-Kachel" (Bearbeitungs-Modus, ca. Zeilen 206–221) werden eine `ComboBox` (`BasisBranchBearbeitenAuswahlComboBox`) und eine `TextBox` (`BasisBranchBearbeitenEingabe`) **gleichzeitig und dauerhaft sichtbar** dargestellt, beide `TwoWay` an dieselbe Eigenschaft `SelectedRepositorySourceBranchName` gebunden — ohne jegliches `Visibility`-Toggle zwischen beiden Controls. Dies ist exakt das in Iteration 1 für `RepositoryAssignDialog.xaml` gemeldete und dort korrekt behobene Muster (Befund 1), das hier jedoch unverändert fortbesteht: `ProjectDetailViewModel` besitzt keine zu `IsSourceBranchManualInput`/`IsWorkingDirectoryManualInput` analoge Eigenschaft für den Bearbeitungsmodus des Basis-Branches (siehe `ProjectDetailViewModel.cs`, Eigenschaften `IsEditingSourceBranch`, `AvailableSourceBranchesForEdit`, `SourceBranchInputError`, `IsLoadingSourceBranchesForEdit` — keine `IsManualInput`-Variante). Wenn z. B. das SCM-Plugin keine Branches liefern kann (Exception in `EditSourceBranchAsync` wird verschluckt, `AvailableSourceBranchesForEdit` bleibt leer) oder generell bei jedem Öffnen des Edit-Modus, sieht der Nutzer ComboBox und TextBox übereinandergestapelt für denselben Wert.

  Empfehlung: Analog zur bereits in `RepositoryAssignDialog.xaml`/`RepositoryAssignViewModel` umgesetzten Lösung eine Eigenschaft (z. B. `IsEditingSourceBranchManualInput`, gesetzt wenn `AvailableSourceBranchesForEdit.Count == 0` nach dem Laden) ergänzen und `ComboBox`/`TextBox` in `ProjectDetailView.xaml` darüber exklusiv per `Visibility`-Binding umschalten.

### RepositoryAssignViewModel.cs (RepositoryAssignViewModel) / ProjectDetailViewModel.cs (ProjectDetailViewModel)

- **Duplizierter Code** — Durch die Behebung von Befund 2 aus Iteration 1 enthalten beide ViewModels nun eine praktisch identische private Methode `ValidateSourceBranchInput()`:
  - `RepositoryAssignViewModel.cs`, Zeilen 368–379 (prüft gegen `DefaultSourceBranchName`/`AvailableSourceBranches`/`SourceBranchInputError`)
  - `ProjectDetailViewModel.cs`, Zeilen 635–646 (prüft gegen `SelectedRepositorySourceBranchName`/`AvailableSourceBranchesForEdit`/`SourceBranchInputError`)

  Beide Implementierungen sind bis auf die verwendeten Property-/Feld-Namen identisch (gleiche Bedingung, gleiche Fehlermeldung `"Branch '{0}' existiert nicht im Repository."`, gleicher `StringComparer.OrdinalIgnoreCase`-Vergleich). Für das analoge Arbeitsverzeichnis-Validierungsmuster existiert bereits ein gemeinsamer Helfer (`WorkingDirectoryInputValidator` in `DirectoryStructureLoadHelper.cs`), der genau diese Art von Duplizierung vermeidet — für die Basis-Branch-Validierung wurde kein entsprechender gemeinsamer Helfer eingeführt.

  Empfehlung: Die Validierungslogik in eine gemeinsame statische Hilfsmethode extrahieren (z. B. `SourceBranchInputValidator.Validate(string? branchName, IReadOnlyCollection<string> availableBranches, out string? error)`, analog zu `WorkingDirectoryInputValidator.TryNormalize`) und von beiden ViewModels aufrufen lassen, statt die Logik zweimal zu pflegen.

### E2E_RepositoryManagementTests.cs / E2E_WorkingDirectory.cs

- **Duplizierter Code** — Die private Hilfsmethode `WaitForFirstRepositoryItem(AutomationElement dialog)` ist wortwörtlich identisch in beiden Testklassen dupliziert (`E2E_RepositoryManagementTests.cs`, Zeilen 121–139 und `E2E_WorkingDirectory.cs`, Zeilen 289–307), ebenso `OpenRepositoryAssignDialog(AutomationElement mainWindow)` (`E2E_RepositoryManagementTests.cs`, Zeilen 114–119 und `E2E_WorkingDirectory.cs`, Zeilen 282–287). Da `E2E_RepositoryManagementTests.cs` in diesem Branch neu hinzugefügt wurde, hätte die bereits in `E2E_WorkingDirectory.cs` vorhandene Implementierung in die gemeinsame Basisklasse `WpfTestBase` (dort liegen bereits andere gemeinsame Hilfsmethoden wie `WaitForElement`, `WaitForWindow`) verschoben und wiederverwendet werden können, statt sie erneut zu kopieren.

  Empfehlung: Beide Methoden nach `WpfTestBase` als `protected static`/`protected` Hilfsmethode verschieben und aus beiden Testklassen entfernen, um die Duplizierung aufzulösen.

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
- `src/Softwareschmiede/Domain/Exceptions/GitBranchNotFoundException.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20260803163323_AddDefaultSourceBranchNameToGitRepository.cs`
- `src/Softwareschmiede/Migrations/20260803163323_AddDefaultSourceBranchNameToGitRepository.Designer.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
