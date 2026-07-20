# Abnahmekriterien-Nachweis

Erstellt am: 2026-07-20

Dieser Nachweis ordnet die elf Akzeptanzkriterien aus `requirement.md` den vorhandenen und ergaenzten automatisierten Tests sowie UI-/Provider-Verifikationen zu.

| Nr. | Kriterium | Nachweis |
|---:|---|---|
| 1 | Aufgabe ohne Issue kann aus der Detailansicht den Issue-Dialog oeffnen. | `TaskDetailViewModelTests.IssueAnlegenCommand_CanExecute_WhenProviderSupportsCreateAndNoReferenceExists` prueft `CanCreateIssue` und Command-Verfuegbarkeit bei fehlender Referenz. |
| 2 | Beim Oeffnen ist die Anforderungsbeschreibung bearbeitbar vorausgefuellt. | `IssueCreateDialogViewModelTests.Initialize_ShouldUseTaskTitleAndOriginalRequirement` prueft Initialtitel und Body aus der Aufgabe. |
| 3 | Beschreibung kann geaendert und ohne Template gesendet werden. | `IssueCreateDialogViewModelTests.Submit_ShouldCreateIssueAndClose_WhenProviderSucceeds` prueft den No-Template-Submit; `Submit_ShouldShowErrorAndKeepInputs_WhenProviderReturnsFailure` weist nach, dass Eingaben editierbar erhalten bleiben. |
| 4 | Provider-Templates werden auswaehlbar angezeigt, wenn geliefert. | `IssueCreateDialogViewModelTests.LoadTemplatesAsync_ShouldKeepSubmitEnabled_WhenNoTemplatesAvailable` sowie GitHub-Provider-Tests `GetIssueTemplatesAsync_ShouldReturnTemplates_WhenRepositoryContainsTemplates` und `GetIssueTemplatesAsync_ShouldIgnoreYamlIssueFormsAndConfig`. |
| 5 | Template-Auswahl erzeugt Template-Inhalt, Trennlinie und `Originalanforderung:`. | `IssueCreateDialogViewModelTests.SelectedTemplate_ShouldComposeBodyWithOriginalRequirement`. |
| 6 | Zusammengesetzter Inhalt bleibt vor dem Absenden editierbar. | `IssueCreateDialogViewModelTests.SelectedTemplate_ShouldRecomposeBodyAndKeepOriginalRequirement_WhenTemplateChanges` und die Submit-Fehler-Tests weisen nach, dass Body-Werte nicht verworfen werden. |
| 7 | KI-Ausfuellhilfe nutzt Template und Originalanforderung; Ergebnis bleibt editierbar. | `IssueCreateDialogViewModelTests.KiAusfuellen_ShouldReplaceBodyWithGeneratedText`, `KiAusfuellen_ShouldKeepBodyAndShowError_WhenGeneratorFails` und `KiAusfuellen_ShouldKeepBodyAndResetGenerating_WhenCancelled`. |
| 8 | Erfolgreiches Absenden erstellt ein Provider-Issue und ordnet die Referenz lokal zu. | `IssueCreateDialogViewModelTests.Submit_ShouldCreateIssueAndClose_WhenProviderSucceeds`, `TaskDetailViewModelTests.IssueAnlegenAsync_ShouldPersistCreatedIssueAndReloadTask`, `GitHubPluginTests.CreateIssueAsync_ShouldReturnIssue_WhenCliSucceeds`, `BitbucketPluginTests.CreateIssueAsync_ShouldPostJiraAdfPayload`. |
| 9 | Nach erfolgreicher Zuordnung ist eine weitere Anlage nicht verfuegbar. | `TaskDetailViewModelTests.IssueAnlegenAsync_ShouldPersistCreatedIssueAndReloadTask` und `IssueAnlegenCommand_CannotExecute_WhenIssueReferenceExists`. |
| 10 | Bei Abbruch oder Fehler wird kein Issue lokal zugeordnet. | `TaskDetailViewModelTests.IssueAnlegenAsync_ShouldNotPersistReference_WhenDialogIsCancelled`, `IssueAnlegenAsync_ShouldNotOverwriteReference_WhenIssueWasAssignedAfterDialog`, `IssueAnlegenAsync_ShouldShowExternalIssueUrl_WhenLocalPersistenceFails`; Dialogtests fuer Providerfehler und Cancellation. |
| 11 | Issue-Anlage ohne Template funktioniert trotz fehlender oder leerer Templates. | `IssueCreateDialogViewModelTests.LoadTemplatesAsync_ShouldKeepSubmitEnabled_WhenNoTemplatesAvailable`, `LoadTemplatesAsync_ShouldShowErrorAndKeepSubmitEnabled_WhenProviderFails`, `GitHubPluginTests.GetIssueTemplatesAsync_ShouldReturnEmpty_WhenTemplateDirectoryDoesNotExist`. |

Ergaenzende Randfallabdeckung:

- Cancellation: `GitPluginBaseTests.CreateBranchAsync_ShouldPropagateCancellation`, `GitHubPluginTests.CreateIssueAsync_ShouldPropagateCancellation_WhenCliIsCancelled`, `BitbucketPluginTests.CreateIssueAsync_ShouldPropagateCancellation_WhenCurlIsCancelled`, Dialogtests fuer Template-Laden, KI und Submit.
- Providerfehler und Validierung: GitHub-/Bitbucket-Tests fuer fehlende Pflichtfelder, fehlende Jira-Konfiguration und echte Providerfehler; Dialogtests fuer Failed-Ergebnisse und Exceptions.
- Doppelte Aktionen: `IssueCreateDialogViewModelTests.Submit_ShouldIgnoreSecondExecution_WhileProviderCallIsRunning` und `TaskDetailViewModelTests.IssueAnlegenAsync_ShouldIgnoreSecondExecution_WhileCreateDialogIsOpen`.
