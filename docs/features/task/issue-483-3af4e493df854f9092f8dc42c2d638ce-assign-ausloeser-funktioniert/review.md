# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Code-Änderungen

- [x] `GitHubPlugin.AddObservation` (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`, Z. 384–389) — Viewer-Marker-Vorfilter auf `ScmContentKind.IssueComment or ReviewComment or PullRequestReview` beschränkt; `Description` liefert stets eine Beobachtung; Kommentar zur Filterabsicht (Plugin kennt den Auslöser nicht, hostseitige Validierung entscheidet) ergänzt
- [x] `GitHubPlugin.IsViewer` (Z. 655) — unverändert, weiterhin für Kommentar-Filter, `GetLatestViewerAssignmentAsync` (Z. 320) und `ReconcileResponseAsync` wirksam
- [x] `ScmEventValidationService.GetAssignRejection` (`src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`, Z. 98–116) — Marker-`else if`-Glied entfernt; Akzeptanzkette = nicht-leerer Body + bestimmbarer Zuweisender + Zuweisender-Kreis; Texttrigger-Fallback (Z. 112–115) unverändert
- [x] `<summary>`-Kommentar von `GetAssignRejection` (Z. 86–97) — präzisiert: Zuweisender-Kreis trägt die Legitimation, Marker-Abwehr wirkt nur noch über Fallback/Texttrigger-Pfad
- [x] `ScmEventValidationService.GetRejection` inkl. Marker-Regel (Z. 81–82), `HasFreeStandingTrigger`, `SameIdentity`, `IsTrustedAutomationLogin`, alle Ablehnungskonstanten — unverändert
- [x] `ScmEventStore` (`src/Softwareschmiede/Application/Services/ScmEventStore.cs`) — XML-Kommentar von `EmptyDescriptionBatchDiagnostic` (Z. 25–30) und Kommentar vor der Checkpoint-Diagnose (Z. 140–145) auf verbleibende Leer-Batch-Ursachen (remote nicht lesbar / Post-Watermark) präzisiert; Konstante und Logik unverändert
- [x] `ScmEventMonitoringService` (`src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`, Z. 224–226) — Kommentar an der Assign-Dekoration ergänzt, dass der Plugin-Vorfilter Beschreibungen nicht mehr entfernt; keine Code-Änderung (plangemäß)

### Tests

- [x] `GetRejection_AssignOwnResponseMarker_Rejected` → umgewandelt in `GetRejection_AssignOwnResponseMarker_Accepted` (`ScmEventValidationServiceTests`, Z. 291–296): Marker-Body + Owner-Zuweisender → `Assert.Null`
- [x] `GetRejection_AssignForeignAssignerWithMarker_StaysRejected` (`ScmEventValidationServiceTests`, Z. 298–303): Marker-Body + fremder Zuweisender → `AssignerNotAllowedRejection`
- [x] `OwnMarkedDescription_IsDelivered` (`GitHubEventSourceTests`, Z. 193–207): Viewer-autorierte Marker-`Description` wird als Beobachtung geliefert
- [x] `Monitor_SeparatesAccount_AssignViewerAuthoredMarkerDescription_CreatesTaskAndDispatches` (`ScmEventMonitoringServiceTests_Assignments`, Z. 74–110): assign-Notification + Marker-Beschreibung (Autor `BOT`/`scm-bot`) + Zuweisender `owner` ⇒ Aufgabe + `ScmIncomingEvent` (`Trigger.Reason == "assign"`, `Zugestellt`) + Prompt mit `AssigneePromptPrefix`-Hinweis
- [x] Doc-Kommentar von `Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded` (Z. 401–404) — Marker-Filterung als Leer-Batch-Ursache entfernt

### Hilfedokumentation

- [x] `docs/help/plugins/github-ereignisse/ablauf-technisch.md` (Z. 23) — `GetAssignRejection`-Akzeptanzkette ohne „kein eigener Antwortmarker"; Leer-Batch-Erklärung auf remote-nicht-lesbar/Post-Watermark präzisiert
- [x] `docs/help/plugins/github-ereignisse/business-rules.md` — Marker-Abwehr als reine Texttrigger-Regel präzisiert (Z. 22) und Assign-Akzeptanz marker-tragender Beschreibungen bei berechtigtem Zuweisenden ergänzt (Z. 52)
- [x] `docs/help/plugins/github-ereignisse/ereignisablaeufe.md` (Ablauf B+, Schritt 3, Z. 115–118) — Marker aus der Akzeptanzkette entfernt, Hinweis auf Fallback-Wirkung ergänzt
- [x] `docs/help/plugins/github-ereignisse/troubleshooting.md` (Z. 80) — Hinweis ergänzt, dass ein eigener Antwortmarker im Assign-Pfad kein Ablehnungsgrund ist

### Verifikation

- [x] Build: `dotnet build` für `Softwareschmiede.Tests` und `Softwareschmiede.IntegrationTests` — je 0 Warnungen / 0 Fehler (Review-Lauf)
- [x] Gezielte Testläufe im Review: 17/17 Unit-Tests (`GetRejection_Assign*`, `OwnMarked*`) und 2/2 Integrationstests (`AssignViewerAuthoredMarkerDescription`, `AssignEmptyDescriptionBatch`) bestanden — mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`
- [x] Vollständige Lanes laut Tasks-Datei zuvor ausgeführt: Unit 2880/2880 (+3 übersprungen), Integration 437/437, `dotnet format --verify-no-changes` sauber

## Hinweise

- Der alte Testname `GetRejection_AssignOwnResponseMarker_Rejected` existiert nicht mehr — umbenannt, wie geplant.
- `OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted` (Kommentar-Filter bleibt bestehen) und `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` (Regression: keine Re-Auslösung der Aufbereitung) bleiben als bestehende Tests wirksam — im 17er-Filterlauf mitgeprüft bzw. unverändert vorhanden.
- Die Tasks-Datei (`docs/features/task-issue-483-…-tasks.md`) ist bereits vollständig auf `Erledigt` mit Testnachweisen; die Prüfergebnisse bestätigen alle 13 Einträge — keine Änderung erforderlich.
