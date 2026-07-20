# Testresultate

Status: Keine Fehler

## Build

Kommando:

```text
dotnet build Softwareschmiede.slnx --no-restore --nologo
```

Ergebnis: erfolgreich, 0 Warnungen, 0 Fehler. Dauer: ca. 1,5 Sekunden.

## Fokussierte Tests

Kommando Unit-/Provider-/KI-/ViewModel-Tests:

```text
dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --no-build --nologo --filter "FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~IssueSelectionDialogViewModelTests|FullyQualifiedName~TaskDetailViewModelTests|FullyQualifiedName~AufgabeServiceTests|FullyQualifiedName~GitPluginBaseTests|FullyQualifiedName~GitHubPluginTests|FullyQualifiedName~BitbucketPluginTests|FullyQualifiedName~LocalDirectoryPluginTests|FullyQualifiedName~ClaudeCliPluginTests|FullyQualifiedName~CodexPluginTests|FullyQualifiedName~GitHubCopilotPluginTests|FullyQualifiedName~KiSimulatorPluginTests|FullyQualifiedName~CliKiPluginBaseTests" --logger "console;verbosity=minimal"
```

Ergebnis: 311 Tests bestanden, 0 fehlgeschlagen, 0 übersprungen.

Kommando `AufgabeService`-Integrationstests:

```text
dotnet test src/Softwareschmiede.IntegrationTests/Softwareschmiede.IntegrationTests.csproj --no-build --nologo --filter "FullyQualifiedName~AufgabeServiceTests" --logger "console;verbosity=minimal"
```

Ergebnis: 15 Tests bestanden, 0 fehlgeschlagen, 0 übersprungen.

## Breite Tests

Kommando:

```text
dotnet test Softwareschmiede.slnx --no-build --nologo --logger "console;verbosity=minimal"
```

Ergebnis: erfolgreich mit Exit-Code 0. Dauer: ca. 4 Minuten 12 Sekunden. Die Testadapter-Ausgabe enthielt in dieser Umgebung keine abschließende Testanzahl.
