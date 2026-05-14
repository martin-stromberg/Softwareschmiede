# Testplan – Git-Plugin Capabilities + Copy-Flow UI-Aktionssteuerung + Merge nach Source

## Eingabe / Grundlage
- Quelle: `docs/tests/testluecken-lokales-verzeichnis-plugin-kopie-aktionsmatrix.md`
- Fachliche Basis:
  - `docs/requirements/lokales-verzeichnis-plugin-kopie-aktionsmatrix-requirements-analysis.md`
  - `docs/improvements/lokales-verzeichnis-plugin-kopie-aktionsmatrix-architecture-review.md`
- Technische Basis:
  - `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
  - `src/Softwareschmiede/Components/Pages/Aufgaben/AufgabeDetail.razor(.cs)`

## Ziel
Vervollständigung der Testabdeckung für:
1. Capability-Vertrag des Git-Plugins
2. UI-Aktionsmatrix im Copy-Flow
3. Merge nach Source inkl. Fehlerpfade

## Umsetzungsstand

- ✅ P0.1 umgesetzt
- ✅ P0.2 umgesetzt (Logik-/Handler-Ebene, ohne bUnit-Rendering)
- ✅ P0.3 umgesetzt
- ✅ P1.4 umgesetzt
- ✅ P1.5 umgesetzt
- ⏳ Offen: bUnit-Renderingtests der Action-Bar, direkte Tests der Interface-Default-Methoden in `IGitPlugin`

## Priorität P0

### 1) Capability-Vertrag LocalDirectoryPlugin absichern
- Testklasse: `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests.cs`
- Tests:
  - `GetGitActionCapabilitiesAsync_ShouldReturnInSourceFlags_WhenWorkspaceModeIsInSourceDirectory`
  - `GetGitActionCapabilitiesAsync_ShouldFallbackToSeparateFlags_WhenWorkspaceModeIsMissing`
  - `GetGitActionCapabilitiesAsync_ShouldFallbackToSeparateFlags_WhenWorkspaceModeIsInvalid`
  - `GetGitActionCapabilitiesAsync_ShouldReturnLocalDirectoryRepositoryKind_Always`

### 2) UI-Aktionsmatrix deterministisch testen
- Testklasse: `src/Softwareschmiede.Tests/Components/Pages/Aufgaben/AufgabeDetailGitActionVisibilityTests.cs`
- Tests:
  - `EvaluateGitActionVisibility_ShouldHidePushPullPrAndMerge_WhenLocalCopyAndCanMergeFalse`
  - `EvaluateGitActionVisibility_ShouldHidePushPullPrAndShowMerge_WhenLocalCopyAndCanMergeTrue`
  - `EvaluateGitActionVisibility_ShouldUseFlags_WhenLocalDirectoryButNotCopy`
  - `EvaluateGitActionVisibility_ShouldUseFlags_WhenRemoteRepository`
  - `LadeGitActionCapabilitiesAsync_ShouldApplyFallbackCapabilities_WhenServiceThrows`
  - `LadeGitActionCapabilitiesAsync_ShouldClosePushPullAndPrForms_WhenVisibilityTurnsFalse`

### 3) MergeToSource direkt testen
- Klassen:
  - Unit: `src/Softwareschmiede.Tests/Infrastructure/Plugins/LocalDirectoryPluginTests.cs`
  - Integration: `src/Softwareschmiede.IntegrationTests/Infrastructure/Plugins/LocalDirectoryPluginIntegrationTests.cs`
- Tests:
  - `MergeToSourceAsync_ShouldCopyChangedFilesFromWorkspaceToSource`
  - `MergeToSourceAsync_ShouldDeleteSourceFilesMissingInWorkspace`
  - `MergeToSourceAsync_ShouldThrowInvalidOperationException_WhenWorkspaceEqualsSource`
  - `MergeToSourceAsync_ShouldThrowInvalidOperationException_WhenSourceDirectoryMissing`
  - `MergeToSourceAsync_ShouldSynchronizeWorkspaceToSource_EndToEnd`

## Priorität P1

### 4) GitOrchestrationService-Guards ergänzen
- Testklasse: `src/Softwareschmiede.Tests/Application/Services/GitOrchestrationServiceTests.cs`
- Tests:
  - `MergeToSourceAsync_ShouldThrowInvalidOperationException_WhenLocalPathMissing`
  - `GetGitActionCapabilitiesAsync_ShouldThrowInvalidOperationException_WhenTaskNotFound`
  - `GetGitActionCapabilitiesAsync_ShouldForwardNullLocalPath_ToPlugin_WhenPathMissing`

### 5) Plugin-Default-Verträge absichern
- Testklasse: `src/Softwareschmiede.Tests/Domain/Abstractions/GitPluginBaseTests.cs`
- Tests:
  - `GetGitActionCapabilitiesAsync_ShouldReturnRemoteDefaults_ByDefault`
  - `MergeToSourceAsync_ShouldThrowNotSupportedException_ByDefault`

## DoD
- P0-Tests implementiert und grün
- Regressionsfrei in bestehenden Testprojekten
- Testläufe:
  - `dotnet test .\src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --nologo`
  - `dotnet test .\src\Softwareschmiede.IntegrationTests\Softwareschmiede.IntegrationTests.csproj --nologo`
