# Bestandsaufnahme – KI-Funktion in Issue-Anlage mit Devin & Copilot

## Zugehörige Artefakte

- `docs/features/task/issue-223-be8010a312d2422aa69484fb71ac85ad-ki-funktion-in-issue-anlage-mi/requirement.md`

## Betroffene Code-Bereiche

### Dialog und ViewModel

- `src/Softwareschmiede.App/Views/IssueCreateDialog.xaml`
  - Zeigt die KI-Plugin-Auswahl (`VerfuegbareKiPlugins`) in einem `ComboBox` an.
- `src/Softwareschmiede.App/ViewModels/IssueCreateDialogViewModel.cs`
  - Befüllt `VerfuegbareKiPlugins` aus `_pluginManager.GetDevelopmentAutomationPlugins()`
    gefiltert auf Plugins, die `IIssueTemplateTextGenerator` implementieren.
  - Ruft bei „Ausfüllen" `FindSelectedTextGenerator()` auf und startet `FillIssueTemplateAsync`.

### Plugin-Verträge

- `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IIssueTemplateTextGenerator.cs`
  - Interface mit einer einzigen Methode `FillIssueTemplateAsync`.
- `src/Softwareschmiede.Plugin.Contracts/Domain/Abstractions/CliKiPluginBase.cs`
  - Stellt `BuildIssueTemplateFillPrompt` und `RunOneShotTextGenerationAsync` bereit.

### Vorhandene KI-Plugins

- `plugins/Softwareschmiede.Plugin.Codex/CodexPlugin.cs`
  - Implementiert bereits `IIssueTemplateTextGenerator`.
- `plugins/Softwareschmiede.Plugin.ClaudeCli/ClaudeCliPlugin.cs`
  - Implementiert bereits `IIssueTemplateTextGenerator`.
- `plugins/Softwareschmiede.Plugin.Devin/DevinPlugin.cs`
  - Implementiert `CliKiPluginBase`, aber **nicht** `IIssueTemplateTextGenerator`.
- `plugins/Softwareschmiede.Plugin.GitHubCopilot/GitHubCopilotPlugin.cs`
  - Implementiert `CliKiPluginBase`, aber **nicht** `IIssueTemplateTextGenerator`.

### Plugin-Laden und Auswahl

- `src/Softwareschmiede/Infrastructure/Plugins/PluginManager.cs`
  - Lädt Plugins aus dem `plugins`-Ordner.
  - `GetDevelopmentAutomationPlugins()` liefert alle `IKiPlugin`s mit `PluginType.DevelopmentAutomation`.
- `src/Softwareschmiede/Application/Services/PluginSelectionService.cs` (Referenz)
  - Wird für den Default-Plugin-Vorschlag genutzt, aber nicht direkt für den Issue-Dialog.

### Tests

- `src/Softwareschmiede.Tests/App/ViewModels/IssueCreateDialogViewModelTests.cs`
  - Enthält bereits Tests für die KI-Ausfüllhilfe mit Fake-Plugins.
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/DevinPluginTests.cs`
  - Enthält Unit-Tests für `DevinPlugin`.
- `src/Softwareschmiede.Tests/Infrastructure/Plugins/GitHubCopilotPluginTests.cs`
  - Enthält Unit-Tests für `GitHubCopilotPlugin`.
- `src/Softwareschmiede.Tests/E2E/Views/Dialogs/IssueCreateDialogView.cs`
  - FlaUI-Page-Object für den Issue-Create-Dialog.
- `src/Softwareschmiede.Tests/E2E/E2E_PluginAuswahlUndWechsel.cs` (Referenz)
  - Zeigt das Muster für konsolidierte E2E-Tests.

## Beobachtungen

- `IssueCreateDialogViewModel.Initialize()` filtert die Plugin-Liste auf:
  `p is IIssueTemplateTextGenerator`.
- `DevinPlugin` und `GitHubCopilotPlugin` sind bereits vom richtigen Typ (`IKiPlugin`,
  `PluginType.DevelopmentAutomation`) und werden vom `PluginManager` geladen.
- Sie tauchen nur dann in der Auswahl auf, wenn sie `IIssueTemplateTextGenerator`
  implementieren und `FillIssueTemplateAsync` bereitstellen.
- `CliKiPluginBase` bietet mit `RunOneShotTextGenerationAsync` und `BuildIssueTemplateFillPrompt`
  die nötigen Werkzeuge für eine One-Shot-Ausfüllung.

## Offene Punkte

Keine.
