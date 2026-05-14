# Testlücken – Git-Plugin Capabilities + Copy-Flow UI-Aktionssteuerung + Merge nach Source

## Status

- ✅ Abgeschlossen: 6 Lücken
- ⏳ Offen: 2 Lücken

## P1 (hoch)

- [ ] **UI-Aktionssteuerung nicht als UI-Flow getestet (nur statische Policy-Methode)**
  - Klasse/Datei: `Softwareschmiede.Components.Pages.Aufgaben.AufgabeDetail` (`AufgabeDetail.razor`, `AufgabeDetail.razor.cs`)
  - Betroffene Logik:
    - Rendering der Action-Bar über `_gitActionVisibility.ShowPushPullToggle`, `ShowPullRequest`, `ShowMerge`
    - Rendering der Push/Pull-Karte über `ShowPush`, `ShowPull`
  - Lücke: Es gibt Tests für `EvaluateGitActionVisibility(...)`, aber kein bUnit-Renderingtest, der die tatsächliche Sichtbarkeit der Buttons/Formulare im UI verifiziert.

- [x] **`LadeGitActionCapabilitiesAsync()` Fehler-/Fallbackpfade getestet**
  - Klasse/Datei: `AufgabeDetail.razor.cs`
  - Methode: `LadeGitActionCapabilitiesAsync()`
  - Abgedeckt durch:
    - `LadeGitActionCapabilitiesAsync_ShouldApplyFallbackCapabilities_WhenPluginReturnsNull`
    - `LadeGitActionCapabilitiesAsync_ShouldApplyFallbackCapabilities_WhenPluginThrows`
    - `LadeGitActionCapabilitiesAsync_ShouldClosePushPullAndPrForms_WhenActionsAreHidden`

- [x] **`MergeToSourceAsync()` UI-Handler getestet**
  - Klasse/Datei: `AufgabeDetail.razor.cs`
  - Methode: `MergeToSourceAsync()`
  - Abgedeckt durch:
    - `MergeToSourceAsync_ShouldSetSuccessMessage_WhenServiceSucceeds`
    - `MergeToSourceAsync_ShouldSetErrorMessage_WhenServiceThrows`

- [x] **Service-Guardrails für Merge/Capabilities ergänzt**
  - Klasse/Datei: `Softwareschmiede.Application.Services.GitOrchestrationService`
  - Methoden:
    - `MergeToSourceAsync(Guid, CancellationToken)`
    - `GetGitActionCapabilitiesAsync(Guid, CancellationToken)`
  - Abgedeckt durch:
    - `MergeToSourceAsync_ShouldThrowInvalidOperationException_WhenLocalPathIsMissing`
    - `GetGitActionCapabilitiesAsync_ShouldThrowInvalidOperationException_WhenTaskIsMissing`
    - `GetGitActionCapabilitiesAsync_ShouldForwardNullLocalPath_WhenTaskHasNoLocalPath`

## P2 (mittel)

- [x] **Capabilities-Matrix im LocalDirectory-Plugin ergänzt**
  - Klasse/Datei: `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs`
  - Methode: `GetGitActionCapabilitiesAsync(string? localPath = null, CancellationToken ct = default)`
  - Abgedeckt durch:
    - `GetGitActionCapabilitiesAsync_ShouldReturnInSourceFlags_WhenWorkspaceModeIsInSourceDirectory`
    - `GetGitActionCapabilitiesAsync_ShouldFallbackToSeparateFlags_WhenWorkspaceModeIsMissing`
    - `GetGitActionCapabilitiesAsync_ShouldFallbackToSeparateFlags_WhenWorkspaceModeIsInvalid`

- [x] **Direkter Merge-Entry-Point separat getestet**
  - Klasse/Datei: `LocalDirectoryPlugin.cs`
  - Methode: `MergeToSourceAsync(string localPath, CancellationToken ct = default)`
  - Abgedeckt durch:
    - Unit: `MergeToSourceAsync_ShouldSynchronizeFilesAndDeleteGitDeletedEntries_InSeparateMode`
    - Integration: `MergeToSourceAsync_ShouldSynchronizeWorkspaceToSource_EndToEnd`

## P3 (niedrig)

- [x] **Default-Capability-Vertrag im Contracts-Layer getestet**
  - Klasse/Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/GitPluginBase.cs`
  - Methoden:
    - `GetGitActionCapabilitiesAsync(...)` (Default: `RemoteGit`, Push/Pull/PR true, Merge false)
    - `MergeToSourceAsync(...)` (Default: `NotSupportedException`)
  - Abgedeckt durch:
    - `GetGitActionCapabilitiesAsync_ShouldReturnRemoteDefaults_ByDefault`
    - `MergeToSourceAsync_ShouldThrowNotSupportedException_ByDefault`

- [ ] **Interface-Default-Implementierungen ungetestet**
  - Klasse/Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`
  - Methoden:
    - `GetGitActionCapabilitiesAsync(...)`
    - `MergeToSourceAsync(...)`
    - `GetRepositoryLinkFields()`
  - Offen, da aktuell die produktive Pfadabdeckung über `GitPluginBase` erfolgt und die Interface-Default-Methoden im Projekt nicht direkt genutzt werden.
