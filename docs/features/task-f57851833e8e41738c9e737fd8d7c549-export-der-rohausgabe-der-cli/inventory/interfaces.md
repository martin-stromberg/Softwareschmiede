## `IDialogService`
Datei: `src/Softwareschmiede.App/Services/IDialogService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `BestaetigenDialog` | `string nachricht, string titel` | `bool` | Bestätigungsdialog (Ja/Nein). |
| `RepositoryZuweisenDialog` | `RepositoryAssignViewModel viewModel` | `bool` | Repository-Zuweisung in Dialogform. |
| `ArbeitsverzeichnisBearbeitenDialog` | `ArbeitsverzeichnisBearbeitenViewModel viewModel` | `bool` | Bearbeiten des Arbeitsverzeichnisses. |
| `ShowPluginSelectionDialogAsync` | `IEnumerable<string> availablePlugins, string? currentSelection, CancellationToken ct = default` | `Task<PluginSelectionResult>` | Auswahl eines KI-Plugins. |
| `ShowIssueSelectionDialogAsync` | `IssueSelectionDialogViewModel viewModel, CancellationToken ct = default` | `Task<Issue?>` | Auswahl eines vorhandenen Issues. |
| `ShowIssueCreateDialogAsync` | `IssueCreateDialogViewModel viewModel, CancellationToken ct = default` | `Task<IssueCreateDialogResult?>` | Anlage eines neuen Issues via Dialog. |
| `ShowOpenTodosDialogAsync` | `OpenTodosDialogViewModel viewModel, CancellationToken ct = default` | `Task` | Anzeige offener To-Dos (read-only). |
| `ShowSolutionSelectionDialogAsync` | `IReadOnlyList<string> solutionPfade, CancellationToken ct = default` | `Task<string?>` | Auswahl eines Solution-/Entry-Points. |
| `ShowAutonomAufgabeInitialisierungsDialogAsync` | `AutonomAufgabeInitialisierungsDialogViewModel viewModel, CancellationToken ct = default` | `Task<AutonomAufgabeKonfiguration?>` | Initialisierungsdialog für autonome Aufgaben. |

Hinweis zum Anforderungsbezug:
- Es existiert aktuell keine Methode für einen Save-File-Dialog (z. B. Zielpfad für `*.raw`).

---

## `ITerminalOutputSink`
Datei: `src/Softwareschmiede/Infrastructure/Terminal/ITerminalOutputSink.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `OnOutputChunk` | `ReadOnlySpan<byte> bytes` | `void` | Nimmt rohe Terminal-Output-Chunks entgegen. |
| `Complete` | – | `void` | Schließt die Ausgabe idempotent und flusht Restdaten. |
| `CompleteAsync` | `TimeSpan timeout, CancellationToken ct = default` | `Task` | Schließt asynchron mit begrenzter Wartezeit auf Persistenz-Drain. |

Querverweis:
- `CliOutputProtokollWriter` implementiert dieses Interface und persistiert die Zeilen in `ProtokollService`.
