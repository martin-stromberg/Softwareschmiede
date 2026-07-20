# Code-Review

Status: Befunde vorhanden

## Befunde

1. **Hoch - KI-Ausfuellhilfe ist im Produktivcode nie verfuegbar.**  
   `IssueCreateDialogViewModel` zeigt KI-Provider nur an, wenn ein Development-Automation-Plugin `IIssueTemplateTextGenerator` implementiert ([IssueCreateDialogViewModel.cs](../../../../src/Softwareschmiede.App/ViewModels/IssueCreateDialogViewModel.cs):202). `CanUseAi` bleibt zusaetzlich false, solange `FindSelectedTextGenerator()` keinen solchen Provider findet ([IssueCreateDialogViewModel.cs](../../../../src/Softwareschmiede.App/ViewModels/IssueCreateDialogViewModel.cs):146). Eine Suche nach `IIssueTemplateTextGenerator`/`FillIssueTemplateAsync` findet aber keine Implementierung in `plugins/` oder `src/` ausser dem Test-Fake. Damit ist die in FA-4 und Akzeptanzkriterium 7 geforderte KI-Aktion fuer reale Installationen nicht nutzbar; die Tests decken nur einen kuenstlichen Fake-Provider ab.

2. **Mittel - GitHub-Issue-Forms und `config.yml` werden als rohe Body-Templates angeboten.**  
   `GitHubPlugin.IsSupportedTemplateFile` akzeptiert neben Markdown auch `.yml` und `.yaml` ([GitHubPlugin.cs](../../../../plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs):503), und `GetIssueTemplatesAsync` uebernimmt den geladenen Dateiinhalt unveraendert als `IssueTemplate.Body` ([GitHubPlugin.cs](../../../../plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs):444). In GitHub-Repositories sind YAML-Dateien unter `.github/ISSUE_TEMPLATE` haeufig Issue-Forms oder `config.yml`; diese Dateien sind Schema-/Chooser-Konfigurationen, kein editierbarer Issue-Body. Bei Auswahl wird also YAML-Konfiguration plus `Originalanforderung:` als Issue-Beschreibung erstellt, statt die Provider-Template-Semantik korrekt abzubilden oder nicht unterstuetzte Formate auszublenden. Es fehlt ein Test, der `config.yml`/Issue-Forms ausschliesst oder korrekt mappt.

## Verifikation

- `dotnet build Softwareschmiede.slnx --no-restore` erfolgreich, 0 Warnungen, 0 Fehler.
- Fokussierter Testlauf erfolgreich: `dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --no-build --filter "FullyQualifiedName~IssueCreateDialogViewModelTests|FullyQualifiedName~TaskDetailViewModelTests|FullyQualifiedName~GitHubPluginTests|FullyQualifiedName~BitbucketPluginTests|FullyQualifiedName~GitPluginBaseTests"` mit 181 bestandenen Tests.
- Voller `dotnet test Softwareschmiede.slnx`-Lauf nach 124 Sekunden ohne verwertbare Ausgabe durch Timeout beendet.
