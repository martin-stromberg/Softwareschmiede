# Review Schritt 4 — Auftragsart (Issue #451)

## Umfang

- `WorkItemKind`-Enum (Domain): `Unspecified`, `Manual`, `Instruction`, `PullRequest`, `Alert`, `IssuePreparation`.
- `WorkItem.Kind` → EF-Spalte `Aufgaben.Auftragsart` (string, non-null, Default `Unspecified`), Migration `20261122125707_AddWorkItemKind` mit Bestandsdaten-Klassifikation in verbindlicher Reihenfolge (IssuePreparation → zugestellte Aufbereitung ⇒ Instruction → Alert → PullRequest → Instruction via ScmIncomingEvents → Rest Manual).
- Kind-Zuweisung in allen fünf Anlagepfaden (`CreateAsync`, `CreateFromIssueAsync`, `CreateFromIssueEventAsync`, `CreateFromPullRequestAsync`, `CreateFromAlertAsync`); Ereignispfad unterscheidet per `purpose == ScmIssuePreparationService.Purpose`.
- Typwechsel Issue-Aufbereitung → Anweisung in `DispatchPreparationAsync` nach erfolgreicher `WritePromptAsync` + Abbruchprüfung; unbedingt, damit vor der Typisierung persistierte Aufbereitungen (`Unspecified`) ebenfalls wechseln. `Purpose` bleibt `IssueAufbereitung` (Routing-Isolation unverändert).
- UI: `WorkItemListMode.Kind` + Sortier-/Gruppierdeskriptoren (`KindSortOrder` ordnet Unspecified ans Ende), deutsche Gruppen-/Labelnamen in `WorkItemListLayout.KindGroupName`, `Kind`/`KindGroupName`/`KindLabel` auf `ActiveWorkItemPanelItem`, „Art"-Radiobutton in `MainWindow.xaml` + `DashboardView.xaml`, Typ-Badge in `ActiveTasksListControl.xaml` (sichtbar nur bei gesetztem `KindLabel`; AutomationId/Tooltip vorhanden).
- Doku: neuer Abschnitt „Auftragsart der Aufgabe" in `business-rules.md`, Release-Notes DE+EN.

## Prüfungen

- Keine Änderung am Ereignis-Routing: `Kind` fließt in keinen Resolver-/Isolationspfad ein; `Purpose` bleibt alleinige Routing-Marke.
- Migration geprüft gegen Snapshot: Spaltennamen `Auftragsart`, `Zweck`, `AufgabeId` korrekt; `AlertReferenzen`/`PullRequestReferenzen`/`ScmIncomingEvents` existieren mit `AufgabeId`.
- Bestandsdaten-Entscheidung dokumentiert: bereits zugestellte Aufbereitungen (Ereignis-Status `Zugestellt`/`InBearbeitung`/`Verarbeitet` mit `Zweck='IssueAufbereitung'`) werden `Instruction`; abgebrochene/unklare Verläufe bleiben konservativ `IssuePreparation`.
- UI-Klassifikation `CreateFromIssueAsync` = `Manual` ist konsistent: Ursprung ist der Benutzerklick (kein Ereignis); `CreateFromPullRequestAsync` bleibt `PullRequest` für beide Aufrufer (Ereignis + UI), weil die Aufgabenart die PR-Natur beschreibt.
- Self-Hosting-Regel: App-Projekt gebaut (0 Warnungen/0 Fehler), kein `Softwareschmiede.App.exe`-Prozess angefasst; kein Screenshot (laufende Instanz gehört dem Anwender).
