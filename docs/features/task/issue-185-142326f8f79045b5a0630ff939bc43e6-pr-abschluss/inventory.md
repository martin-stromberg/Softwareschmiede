# Bestandsaufnahme - PR-Abschluss

## Kurzfazit

Die Anwendung kann Pull Requests bereits aus der Aufgaben-Detailansicht heraus erstellen. Der aktuelle Fluss endet aber nach `gh pr create`: Es wird ein Protokolleintrag geschrieben, die PR-URL wird geoeffnet, und der zurueckgegebene `PullRequest` ist nur ein nicht persistiertes Value-Object mit Nummer, Titel, URL und Branch.

Fuer die Anforderung fehlen damit insbesondere:

- persistierte Pull-Request-Referenzen je Aufgabe,
- Statusmodell fuer Pull Requests und GitHub Actions,
- ein Inhaltsbereich `PR` in der Aufgaben-Detailansicht,
- GitHub-API-Operationen zum Lesen von PR-/Workflow-Status und zum Auto-Abschluss,
- Hintergrundueberwachung nach Erstellung und nach Merge,
- Tests fuer Persistenz, ViewModel/UI, GitHub-CLI-Aufrufe und Ueberwachungslogik.

## Detaildokumente

- [UI und Task-Detailansicht](inventory/ui-task-detail.md)
- [Persistenz und Domain-Modell](inventory/domain-persistence.md)
- [GitHub-Plugin und Plugin-Vertraege](inventory/github-plugin-contracts.md)
- [Monitoring, DI und Tests](inventory/monitoring-tests.md)

## Relevante Einstiegspunkte

| Bereich | Dateien | Bedeutung |
|--------|---------|-----------|
| PR-Erstellung | `src/Softwareschmiede/Application/Services/GitOrchestrationService.cs`, `src/Softwareschmiede/App/ViewModels/TaskDetailViewModel.cs`, `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs` | Erstellt und oeffnet PRs, persistiert sie aber nicht. |
| Aufgaben-UI | `src/Softwareschmiede.App/Views/TaskDetailView.xaml`, `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | Enthalten die aktuellen Inhaltsbereiche `Info`, `CLI`, `Diff`, `Dateien` und die Ribbon-Action `PR erstellen`. |
| Plugin-Vertrag | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs`, `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PullRequest.cs` | Muss fuer Statusabfragen und Abschluss-/Merge-Operationen erweitert werden. |
| Datenmodell | `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`, `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`, `src/Softwareschmiede/Migrations/` | Aufgaben haben keine PR-Navigation und keine PR-/Action-Tabellen. |
| Plugin-Einstellungen | `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, `src/Softwareschmiede/Application/Services/PluginSettingsService.cs`, `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/PluginSettingFieldType.cs` | Boolean- und Enum-Einstellungen sind vorhanden und koennen fuer Auto-Abschluss genutzt werden. |

## Wichtige Befunde

1. `GitOrchestrationService.PullRequestErstellenAsync` pusht den Branch, ruft `CreatePullRequestAsync` auf und protokolliert den PR, speichert ihn aber nicht an der Aufgabe.
2. `TaskDetailViewModel.PullRequestErstellenAsync` ruft den Service auf, laedt die Aufgabe neu und oeffnet die PR-URL. Es gibt kein Collection-Property fuer PRs oder Workflow-Runs.
3. `TaskDetailViewModel.DetailAnsicht` kennt `Info`, `Cli`, `Diff`, `Dateibrowser`. Die Anforderung nennt `Info`, `CLI`, `Dateien`; technisch existiert zusaetzlich `Diff`.
4. `PullRequest` im Plugin-Contract ist ein schmales Record ohne Provider, Repository, Status, Merge-Status, Head/Base-SHA oder Aktualisierungszeit.
5. Das GitHub-Plugin nutzt `gh` und `git` ueber `ICliRunner`; Status- und Merge-Funktionen koennen konsistent ebenfalls ueber `gh api`/`gh pr` implementiert und getestet werden.
6. Der GitHub-Token wird als `GH_TOKEN` gesetzt und im Fehlerfall sanitisiert. Zusaetzliche Berechtigungen fuer Approval, Merge, Auto-Merge oder Bypass muessen sichtbar in den Einstellungen und Fehlermeldungen beruecksichtigt werden.
7. Es gibt keine generische Hintergrundservice-Registrierung fuer PR-Monitoring. Der WPF-Host nutzt `Microsoft.Extensions.Hosting`; ein Singleton/Hosted-Service oder ein timerbasierter Singleton passt zur bestehenden Architektur.

## Offene Risiken fuer die Planung

- Die fachliche Bedeutung von "bestaetigt" ist in `requirement.md` offen: Approval, Auto-Merge-Aktivierung oder direktes Merge.
- GitHub-Bypass ist nicht mit normalem PR-Review gleichzusetzen. Der Plan muss eine explizite Strategie festlegen und fehlende Berechtigungen als sichtbaren Zustand behandeln.
- Die Auswahl relevanter Checks/Actions ist ungeklärt. Technisch sind required checks aus Branch Protection sauberer als "alle Runs", aber aufwendiger.
- Post-Merge-Actions muessen ueber Merge-Commit oder Zielbranch-Run-Zuordnung modelliert werden, sonst besteht Zuordnungsrisiko bei parallelen Merges.

