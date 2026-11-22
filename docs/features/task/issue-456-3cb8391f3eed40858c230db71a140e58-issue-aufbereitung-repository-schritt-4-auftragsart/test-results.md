# Testergebnisse Schritt 4 — Auftragsart (Issue #451)

## Build

- `dotnet build` Softwareschmiede + App + beide Testprojekte: 0 Warnungen, 0 Fehler (voller Build vor jedem Testlauf, kein `--no-build`).
- `dotnet format Softwareschmiede.slnx --verify-no-changes`: sauber.

## Integrationstests (fokussiert, 73/73 grün)

`--filter "FullyQualifiedName~IssuePreparation|FullyQualifiedName~ScmTaskCompletionMonitoringServiceTests|FullyQualifiedName~ScmEventTaskResolver"`

- `ScmEventMonitoringServiceTests_IssuePreparation`: Aufbereitungsaufgabe wird mit `Kind=IssuePreparation` angelegt (echter Service-Pfad).
- `ScmEventDispatchTests_IssuePreparation`: nach erfolgreicher Prompt-Zustellung steht die Aufgabe auf `Kind=Instruction`, `Purpose` bleibt `IssueAufbereitung`; abgebrochene/verbleibende Pfade unverändert.

## Unit-Tests (148/148 grün)

`--filter "FullyQualifiedName~WorkItemServiceTests|FullyQualifiedName~WorkItemsListsViewTests|FullyQualifiedName~WorkItemsListsModeServiceTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~DashboardViewModelTests"`

- `WorkItemServiceTests`: `Kind`-Assertion je Anlagepfad (Manual/PR/Alert) + neue Theory `CreateFromIssueEventAsync` (purpose=null → `Instruction`, purpose=`IssueAufbereitung` → `IssuePreparation`).
- `WorkItemsListsViewTests`: neuer Test `Configure_ShouldApplyGroupingAndSorting_WhenModeIsKind` (Gruppendeskriptor `KindGroupName`, Primärsortierung `KindSortOrder`).

## Hinweis

OS-Interface-/E2E-Lane nicht Teil dieses Laufs; UI-Badge/Modus-Auswahl über Unit-Tests der Deskriptoren abgedeckt (kein FlaUI-E2E für das Badge — Badge-Sichtbarkeit folgt trivial aus `KindLabel`-Binding).
