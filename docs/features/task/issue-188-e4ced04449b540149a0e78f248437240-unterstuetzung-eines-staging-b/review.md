# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Datenmodell

- [x] `GitRepository.DefaultSourceBranchName` (`string?`) — Eigenschaft in Domain Model vorhanden
- [x] Migration `AddDefaultSourceBranchNameToGitRepository` — Datenbank-Spalte hinzugefügt (TEXT, nullable, max 255)

### Interfaces & Service-Contracts

- [x] `IGitPlugin.CreateBranchAsync()` — Parameter `sourceBranchName` hinzugefügt
- [x] `IGitPlugin.CreatePullRequestAsync()` — Parameter `baseBranch` hinzugefügt
- [x] `IGitPlugin.GetRemoteBranchesAsync()` — vorhanden (für Validierung)
- [x] `IGitPlugin.CheckoutRemoteBranchAsync()` — vorhanden (für Basis-Branch-Checkout)

### Plugin-Implementierungen

- [x] `GitPluginBase.CreateBranchAsync()` — Implementierung mit `sourceBranchName`-Unterstützung (`git checkout -b <branchName> origin/<sourceBranchName>`)
- [x] `GitHubPlugin.CreatePullRequestAsync()` — Implementierung mit `--base` Flag für `baseBranch`

### Application Services

- [x] `EntwicklungsprozessService.ProzessStartenAsync()` — Ruft `ValidateBaseBranchExistsAsync()` vor Klon auf
- [x] `EntwicklungsprozessService.ValidateBaseBranchExistsAsync()` — Private Methode wirft `GitBranchNotFoundException` wenn Branch nicht existiert
- [x] `EntwicklungsprozessService.SetupBranchAsync()` — Erweitert um Basis-Branch-Logik:
  - Ruft `CheckoutRemoteBranchAsync()` auf, wenn `DefaultSourceBranchName` nicht null und nicht Standard-Branch
  - Ruft `CreateBranchAsync()` mit `sourceBranchName = DefaultSourceBranchName` auf
- [x] `GitOrchestrationService.PullRequestErstellenAsync()` — Erweitert um Basis-Branch-Handling:
  - Ruft `ResolveDefaultSourceBranchNameAsync()` auf
  - Übergibt `baseBranch` an `CreatePullRequestAsync()`
- [x] `GitOrchestrationService.ResolveDefaultSourceBranchNameAsync()` — Private Methode ermittelt `DefaultSourceBranchName` aus `GitRepository`
- [x] `ProjektService.UpdateRepositorySourceBranchAsync()` — Methode zum Speichern geänderter Basis-Branch-Konfiguration
- [x] Exception: `GitBranchNotFoundException` — Wird bei Validierungsfehler geworfen

### ViewModels

- [x] `RepositoryAssignViewModel.DefaultSourceBranchName` (`string?`) — Property vorhanden
- [x] `RepositoryAssignViewModel.AvailableSourceBranches` (`ObservableCollection<string>`) — Property vorhanden
- [x] `RepositoryAssignViewModel.IsLoadingSourceBranches` (`bool`) — Property vorhanden
- [x] `RepositoryAssignViewModel.SourceBranchInputError` (`string?`) — Property vorhanden
- [x] `ProjectDetailViewModel.SelectedRepositorySourceBranchName` (`string?`) — Property vorhanden
- [x] `ProjectDetailViewModel.IsEditingSourceBranch` (`bool`) — Property vorhanden
- [x] `ProjectDetailViewModel.AvailableSourceBranchesForEdit` (`ObservableCollection<string>`) — Property vorhanden
- [x] `ProjectDetailViewModel.SourceBranchInputError` (`string?`) — Property vorhanden
- [x] `ProjectDetailViewModel.EditSourceBranchCommand` — ICommand vorhanden
- [x] `ProjectDetailViewModel.SaveSourceBranchCommand` — ICommand vorhanden
- [x] `ProjectDetailViewModel.CancelSourceBranchEditCommand` — ICommand vorhanden
- [x] `ProjectDetailViewModel.EditSourceBranchAsync()` — Private Methode vorhanden
- [x] `ProjectDetailViewModel.SaveSourceBranchAsync()` — Private Methode vorhanden (ruft `ProjektService.UpdateRepositorySourceBranchAsync()` auf)
- [x] `ProjectDetailViewModel.CancelSourceBranchEdit()` — Private Methode vorhanden

### UI-Views

- [x] `RepositoryAssignDialog.xaml` — Basis-Branch-Auswahl-UI hinzugefügt:
  - ComboBox für verfügbare Branches
  - TextBox für freie Eingabe
  - Ladeindikator (`IsLoadingSourceBranches`)
  - Validierungsfehler-Anzeige (`SourceBranchInputError`)
- [x] `ProjectDetailView.xaml` — Basis-Branch-Anzeige und -Bearbeitung-UI hinzugefügt:
  - Anzeige des aktuellen `SelectedRepositorySourceBranchName` (oder Fallback-Text "Standard")
  - "Bearbeiten"-Button (`EditSourceBranchCommand`)
  - Edit-Modus mit ComboBox/TextBox und Speichern/Abbrechen-Buttons

### Tests

#### Service-Tests
- [x] `EntwicklungsprozessServiceTests_BasisBranch.ProzessStartenAsync_ShouldThrow_WhenBaseBranchDoesNotExist` — Validierung vor Klon
- [x] `EntwicklungsprozessServiceTests_BasisBranch.ProzessStartenAsync_ShouldSucceed_WhenBaseBranchExists` — Validierung erfolgreich
- [x] `EntwicklungsprozessServiceTests_BasisBranch.ProzessStartenAsync_ShouldSucceed_WhenNoBranchConfigured` — Fallback-Szenario
- [x] `EntwicklungsprozessServiceTests_BasisBranch.SetupBranchAsync_ShouldCreateBranchFromBaseBranch_WhenConfigured` — Feature-Branch vom Basis-Branch
- [x] `EntwicklungsprozessServiceTests_BasisBranch.SetupBranchAsync_ShouldCreateBranchFromHead_WhenNotConfigured` — Feature-Branch vom HEAD ohne Konfiguration
- [x] `GitOrchestrationServiceTests` — Tests für PR-Erstellung mit Basis-Branch (Pull-Request-Erstellung übergibt `baseBranch`)

#### ViewModel-Tests
- [x] `RepositoryAssignViewModelTests_BasisBranch` — Tests für Branch-Laden, Validierung, Dialog-Confirm
- [x] `ProjectDetailViewModelTests_BasisBranch` — Tests für Basis-Branch-Bearbeitung

#### E2E-Tests
- [x] `E2E_RepositoryManagementTests.BasisBranchVerwaltung()` — Übergeordneter E2E-Test
- [x] `E2E_RepositoryManagementTests.RepositoryZuweisen_MitBasisBranchAuswahl_SpeichertUndZeigtBasisBranch_E2E()` — Repository-Zuordnung mit Basis-Branch-Auswahl
- [x] `E2E_RepositoryManagementTests.BasisBranchBearbeiten_InProjektdetailansicht_SpeichertNeuenBasisBranch_E2E()` — Basis-Branch-Bearbeitung in Projektdetailansicht

## Offene Aufgaben

Keine — alle Planelemente sind vollständig umgesetzt.

## Hinweise

- **Lazy-Validierung:** Basis-Branch wird erst beim Aufgabenstart validiert, nicht beim Speichern. Das ermöglicht Szenarien, in denen ein Branch später erstellt wird.
- **Abwärtskompatibilität:** Bestehende Repositories ohne `DefaultSourceBranchName` (null) verwenden den Remote-Standard-Branch (Plugin-Fallback).
- **PR-Ziel-Branch:** Wenn `DefaultSourceBranchName = null`, wird `baseBranch = null` an das Plugin übergeben, was zu dessen Standard-Fallback (Remote-Standard) führt.
- **Feature-Branch-Erstellung:** Der neue Task-Branch wird entweder:
  - Vom konfigurierten Basis-Branch abgezweigt (wenn `DefaultSourceBranchName` gesetzt und != Standard-Branch)
  - Vom aktuellen HEAD abgezweigt (wenn nicht konfiguriert oder == Standard-Branch)
  - In einem Sonderfall (nicht im Plan, aber existierend): wenn `basisBranchName` der Standard-Branch ist, wird dieser Branch statt eines neuen ausgecheckt
