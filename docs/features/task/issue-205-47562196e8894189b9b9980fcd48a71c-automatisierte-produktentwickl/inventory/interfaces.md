# Interfaces

## `IDialogService`

Datei: `src/Softwareschmiede.App/Services/IDialogService.cs`

Dialog-Service für UI-Integration nach dem MVVM-Muster. Abstrahiert Fenster/Dialog-Öffnungen.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `BestaetigenDialog` | `nachricht: string, titel: string` | `bool` | Zeigt Bestätigungsdialog |
| `RepositoryZuweisenDialog` | `viewModel: RepositoryAssignViewModel` | `bool` | Dialog zur Repository-Zuweisung |
| `ArbeitsverzeichnisBearbeitenDialog` | `viewModel: ArbeitsverzeichnisBearbeitenViewModel` | `bool` | Dialog zur Bearbeitung des Arbeitsverzeichnisses |
| `ShowPluginSelectionDialogAsync` | `availablePlugins: IEnumerable<string>, currentSelection: string?` | `Task<PluginSelectionResult>` | Zeigt Plugin-Auswahl-Dialog |
| `ShowIssueSelectionDialogAsync` | `viewModel: IssueSelectionDialogViewModel` | `Task<Issue?>` | Zeigt Issue-Auswahl-Dialog |
| `ShowIssueCreateDialogAsync` | `viewModel: IssueCreateDialogViewModel` | `Task<IssueCreateDialogResult?>` | Zeigt Issue-Anlage-Dialog |
| `ShowOpenTodosDialogAsync` | `viewModel: OpenTodosDialogViewModel` | `Task` | Zeigt offene To-Dos read-only |
| `ShowSolutionSelectionDialogAsync` | `solutionPfade: IReadOnlyList<string>` | `Task<string?>` | Zeigt Solution-Auswahl-Dialog |
| `ShowAutonomAufgabeInitialisierungsDialogAsync` | `viewModel: AutonomAufgabeInitialisierungsDialogViewModel` | `Task<AutonomAufgabeKonfiguration?>` | Zeigt Initialisierungsdialog für Autonome Aufgabe |
| `ShowAutonomAufgabeDetailAsync` | `viewModel: AutonomAufgabeDetailViewModel` | `Task` | **Zeigt Detail-Ansicht einer Autonomen Aufgabe** — aktuell als separater Dialog via `ShowDialog()` |

**Zu beachten:** `ShowAutonomAufgabeDetailAsync()` öffnet aktuell ein eigenständiges Fenster (`AutonomAufgabeDetailDialog`). Nach der Integration soll diese Methode weiterhin existieren (für Fallback-Szenarien) oder deprecated werden, und die Anzeigelogik nach `TaskDetailViewModel` verschoben werden.
