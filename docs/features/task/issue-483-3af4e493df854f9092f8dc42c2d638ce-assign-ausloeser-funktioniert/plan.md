# Umsetzungsplan: Assign-Auslöser akzeptiert aufbereitete Referenzbeschreibungen (Marker-Texte des Überwachungskontos)

## Übersicht

Eine von der Issue-Aufbereitung unter dem Überwachungskonto neu geschriebene, marker-tragende Referenzbeschreibung (`<!-- softwareschmiede:action:<id> -->`, geschrieben via `ScmResponseKind.IssueBodyUpdate`) wird heute von zwei „Eigene Ausgabe"-Schutzebenen verworfen, sodass eine berechtigte Zuweisung keine Ausführung auslöst. Der Plan nimmt zwei gezielte Änderungen vor: (1) Der plugin-seitige Viewer-Marker-Vorfilter in `GitHubPlugin.AddObservation` wird auf die Kommentar-Arten beschränkt, sodass Beschreibungen stets als Beobachtung ausgeliefert werden; (2) die Marker-Ablehnung in `ScmEventValidationService.GetAssignRejection` entfällt — der Rekursionsschutz ist im Assign-Pfad durch die Zuweisender-Validierung abgedeckt. Betroffen sind ausschließlich Filter-/Validierungslogik und deren Kommentare/Dokumentation — keine Vertrags-, Konfigurations-, Datenbank- oder UI-Änderungen.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Der Description-`ScmEventBatch` einer SCM-Referenz nach `assign`-Notification (`ScmNotification.AssignReason`) — konkret die `ScmEventObservation` der Referenzbeschreibung nach der Assign-Dekoration (`ScmEventTrigger(Reason = "assign")`). Gilt gleichermaßen für `ScmReferenceKind.Issue` und `ScmReferenceKind.PullRequest` (gemeinsamer `issues/{n}/events`-Endpunkt; die Aufweichung wird bewusst nicht auf Issues eingeschränkt). |
| Auslösende Ereignisse | Zuweisung einer überwachten Referenz an das Überwachungskonto durch einen berechtigten Zuweisenden, erkannt als Inbox-Notification `reason = assign` (nur `ScmAccountMode.SeparateAccount`): `ScmEventMonitoringService.RunOnceAsync` → `ReadEventsAsync(ScmContentKind.Description)` → Dekoration via `IScmAssignmentSource.GetLatestViewerAssignmentAsync` → `ScmEventStore.AcceptBatchAsync` → Dispatch durch `ScmEventOrchestrator`. |
| Erwartete Wirkung | Die assign-ausgelöste Beobachtung einer marker-tragenden, vom Überwachungskonto stammenden Beschreibung erzeugt bei berechtigtem Zuweisenden ein `ScmIncomingEvent` (inkl. Aufgabenbindung über reguläre/Purpose-Auswahl bzw. `ScmEventTaskResolver`) und wird über `ScmEventOrchestrator.DispatchCoreAsync` mit `AssigneePromptPrefix` zugestellt — sichtbar als ausgeführte Aufgabe statt `Inbox: Keine verwertbare Referenzbeschreibung`. |
| Umgang mit fehlenden Objekten | Kein wirksames Zuweisungsereignis / Zuweisender nicht bestimmbar → unverändert `AssignerUnknownRejection`. Zuweisender außerhalb {Owner, Viewer, `TrustedAutomationLogins`} → unverändert `AssignerNotAllowedRejection` (Texttrigger-Fallback bleibt wirksam, kann eine Marker-Beschreibung aber weiterhin nicht legitimieren). Leere Beschreibung → unverändert `AssignEmptyDescriptionRejection`; gänzlich leerer Batch → unverändert `EmptyDescriptionBatchDiagnostic`. Marker-Beschreibung ohne Assign-Kontext → unverändert „Eigene Antwort" (Texttrigger-Pfad, `GetRejection` Z. 81). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Der „Eigene Antwort"-Schutz bleibt für den regulären Texttrigger (Erwähnung/Keyword, Nicht-Assign-Reads) vollständig wirksam — eigene marker-tragende Texte lösen ohne Assign-Kontext weiterhin kein Ereignis aus. | Die Anforderung grenzt die Aufweichung ausdrücklich auf assign-ausgelöste Description-Reads ein; die Rekursionsabwehr darf außerhalb des Assign-Pfads nicht geöffnet werden. | bestätigt (2026-11-27, Issue-Kommentar 7228823021) |
| 2 | Keine Mindest-Inhaltsbedingung für assign-akzeptierte Beschreibungen: Eine Beschreibung, die ausschließlich den Marker ohne lesbaren Auftragstext trägt, wird bei berechtigter Zuweisung trotzdem zugestellt (nur `IsNullOrWhiteSpace` bleibt Ablehnungsgrund). | Die bewusste Zuweisung durch ein berechtigtes Konto trägt die Legitimation; eine inhaltliche Mindestlänge wäre willkürlich und würde legitime Zuweisungen mit knappen Texten aushebeln. | bestätigt (2026-11-27, Issue-Kommentar 7228823021) |
| 3 | Keine Änderung am berechtigten Zuweisender-Kreis, an `AlreadyReactedRejection`, an der Mention-/Keyword-Triggerlogik oder am ContentKey-/Deduplizierungsmodell (`{nodeId}/assign-{eventNodeId}`). | Außerhalb der Fehlerursache; jede Ausweitung erzeugt neues Risiko ohne Issue-Bezug. | bestätigt (2026-11-27, Issue-Kommentar 7228823021) |
| 4 | Kein rückwirkender „Fix" historischer Checkpoints/Snapshots mit der alten Diagnose (`Inbox: Keine verwertbare Referenzbeschreibung` aufgrund der Marker-Filterung). | Eine erneute Zuweisung erzeugt über die Zuweisungs-Ereignisidentität im `ContentKey` ohnehin eine neue Beobachtung; historische Diagnosen bleiben fachlich korrekte Protokolle des damaligen Verhaltens. | bestätigt (2026-11-27, Issue-Kommentar 7228823021) |
| 5 | Keine neue Konfiguration, keine UI-Änderung, keine EF-Migration, keine Änderung an `ScmEventReadRequest` (kein Kontext-Flag im Vertrag). | Reine Filter-/Validierungskorrektur; ein Vertrags-Flag wäre die aufwändigere Variante ohne Mehrwert gegenüber der gewählten Lösung. | bestätigt (2026-11-27, Issue-Kommentar 7228823021) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Plugin-Vorfilter in `GitHubPlugin.AddObservation` (Z. 384) | **Variante (a):** Marker-Vorfilter auf die Kommentar-Arten `IssueComment`, `ReviewComment`, `PullRequestReview` beschränken; `ScmContentKind.Description` wird stets als Beobachtung ausgeliefert. | Kleinste Änderung im Einklang mit dem Abwehrziel: Das Plugin kann zum Filterzeitpunkt keinen Assign-Kontext sehen (`ScmEventReadRequest` trägt keinen Auslöser). Gegenüber (b) Kontext-Flag im Vertrag: keine Vertragsänderung nötig. Gegenüber (c) komplette Entfernung: Der Kommentar-Vorfilter bleibt als frühe Rekursionsbremse erhalten — Kommentare des eigenen Kontos mit Marker sind stets eigene Outputs und erzeugen auch hostseitig nie etwas anderes als „Eigene Antwort"/„Kein Trigger". Für Beschreibungen dagegen ist die hostseitige „Eigene Antwort"-Regel (`GetRejection` Z. 81) in allen Nicht-Assign-Pfaden weiterhin wirksam, sodass kein Schutz verloren geht. |
| Marker-Ablehnung in `ScmEventValidationService.GetAssignRejection` (Z. 106–107) | Marker-Prüfung als Ablehnungskriterium entfernen; Akzeptanzkette = nicht-leerer Body + bestimmbarer Zuweisender + Zuweisender-Kreis. | Das Abwehrziel der Marker-Regel (Rekursion über eigene Outputs) ist im Assign-Pfad durch die Zuweisender-Validierung abgedeckt: Nur {Owner, Viewer, `TrustedAutomationLogins`} legitimieren. Der Texttrigger-Fallback (Z. 113) bleibt unverändert — dort greift die Marker-Regel weiter, sodass eine Marker-Beschreibung keinen fremden Zuweisender legitimieren kann. Die Zustellungs-Gegenprüfung im Orchestrator profitiert automatisch, da der `Trigger` in `ObservationJson` persistiert ist und erneut in `GetAssignRejection` läuft. |
| Geltung für Pull-Request-Beschreibungen | Bewusst für beide Referenzarten (keine Einschränkung auf Issues). | Gleicher Code-Pfad (`GetLatestViewerAssignmentAsync` läuft über den gemeinsamen `issues/{n}/events`-Endpunkt); eine Einschränkung auf Issues müsste künstlich eingebaut werden und widerspräche dem bestehenden Verhalten (`Monitor_SeparatesAccount_AssignOnPullRequest_CreatesTask`). |

## Programmabläufe

### Assign-Auslöser mit marker-tragender Beschreibung (Happy Path)

1. `ScmEventMonitoringService.RunOnceAsync` enumeriert die Inbox des separaten Kontos und findet eine Notification mit `Reason == ScmNotification.AssignReason` für eine überwachte Referenz.
2. Der Description-Read `IScmEventSource.ReadEventsAsync(request mit ContentKind = Description)` ruft `GitHubPlugin.ReadEventsAsync`; `AddObservation` wendet den Viewer-Marker-Vorfilter **nicht** mehr auf `ScmContentKind.Description` an — die vom Überwachungskonto geschriebene Beschreibung samt `<!-- softwareschmiede:action:<id> -->` wird als `ScmEventObservation` im Batch ausgeliefert.
3. Die Assign-Dekoration (`ScmEventMonitoringService` Z. 217–236) fragt `IScmAssignmentSource.GetLatestViewerAssignmentAsync` ab und erweitert jede Beobachtung: `RemoteNodeId += "/assign-{eventNodeId}"`, `Trigger = new ScmEventTrigger("assign", assignerId, assignerLogin, eventNodeId, occurredAt)`.
4. Aufgabenauswahl (reguläre vor zweck-markierter Aufgabe, ggf. `ScmEventTaskResolver.ResolveAsync` mit der ersten nicht abgelehnten Beobachtung).
5. `ScmEventStore.AcceptBatchAsync` prüft je Beobachtung `ScmEventValidationService.GetRejection` → `GetAssignRejection`: Body nicht leer, Zuweisender bestimmbar, Zuweisender im Kreis {Owner, Viewer, `TrustedAutomationLogins`} → `null` (akzeptiert); **kein** Marker-Abbruch mehr. Es entsteht ein `ScmIncomingEvent` mit der persistierten Beobachtung.
6. `ScmEventOrchestrator.DispatchCoreAsync` wiederholt `GetRejection` auf der persistierten Beobachtung (`Trigger` aus `ObservationJson` läuft erneut durch `GetAssignRejection` — solange die Zuweisung wirksam bleibt, wird akzeptiert) und stellt den Prompt `AssigneePromptPrefix + "\n\n" + Body` via `PromptBody` zu.

Beteiligte Klassen/Komponenten: `GitHubPlugin` (`ReadEventsAsync`, `AddObservation`, `IsViewer`), `ScmEventMonitoringService`, `IScmAssignmentSource`, `ScmEventValidationService`, `ScmEventStore`, `ScmEventTaskResolver`, `ScmEventOrchestrator`.

### Marker-tragende Beschreibung ohne Assign-Kontext (Rekursionsschutz bleibt)

1. Eine marker-tragende Beschreibung wird über einen mention-/keyword-ausgelösten oder enumerierten Read (ohne `assign`-Notification in der Referenzgruppe) gelesen — mit dem beschränkten Vorfilter nun als Beobachtung im Batch statt gar nicht.
2. `GetRejection` mit `Trigger = null` (kein `Trigger.Reason == "assign"`): Ohne frei stehenden Trigger greift zuerst `NoMentionRejection`/`"Kein Trigger"`; mit Trigger im Body greifen Akteur-Regeln und die Marker-Regel Z. 81 → `"Eigene Antwort"`.
3. Ergebnis: abgelehnter Snapshot mit Diagnose (keine Checkpoint-Sichtbarkeit, kein `ScmIncomingEvent`) — der Rekursionsschutz bleibt hostseitig vollständig wirksam.

Beteiligte Klassen/Komponenten: `GitHubPlugin.AddObservation`, `ScmEventValidationService.GetRejection`, `ScmEventStore.AcceptBatchAsync`.

### Assign mit unberechtigtem Zuweisenden und Marker-Beschreibung (Fallback)

1. Wie Happy Path bis Schritt 5; `GetAssignRejection` ermittelt `AssignerNotAllowedRejection`.
2. Der Texttrigger-Fallback ruft `GetRejection(observation with { Trigger = null })` auf: Enthält die Beschreibung eine gültige frei stehende Erwähnung, legitimiert der Texttrigger unabhängig vom Zuweisender — außer die Marker-Regel (Z. 81) oder Akteur-Regeln lehnen ab. Eine Marker-Beschreibung bleibt im Fallback stets abgelehnt → `AssignerNotAllowedRejection` steht.

Beteiligte Klassen/Komponenten: `ScmEventValidationService`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `GitHubPlugin` (partielle Klasse, Ereignisteil — `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`)

- **Geänderte Methoden:** `AddObservation` (Z. 370–402) — der Viewer-Marker-Vorfilter (Z. 384: `IsViewer(repo, authorId, authorLogin) && body.Contains("<!-- softwareschmiede:", OrdinalIgnoreCase) ⇒ return`) wird auf die Kommentar-Arten beschränkt: Er greift nur noch bei `request.ContentKind is ScmContentKind.IssueComment or ScmContentKind.ReviewComment or ScmContentKind.PullRequestReview`. `ScmContentKind.Description` liefert stets eine Beobachtung. Kommentar zur Filterabsicht um den Assign-Kontext ergänzen (Beschreibungen werden hostseitig validiert, weil das Plugin den Auslöser nicht kennt).
- **Unverändert:** `IsViewer` (Z. 650–652) bleibt für Kommentare, `GetLatestViewerAssignmentAsync` und `ReconcileResponseAsync` wirksam.

### `ScmEventValidationService` (`src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`)

- **Geänderte Methoden:** `GetAssignRejection` (Z. 94–114) — das `else if`-Glied Z. 106–107 (`body.Contains("<!-- softwareschmiede:") ⇒ "Eigene Antwort"`) entfällt als Ablehnungskriterium; die Kette prüft nur noch leeren Body (`AssignEmptyDescriptionRejection`), unbestimmbaren Zuweisenden (`AssignerUnknownRejection`) und den Zuweisender-Kreis (`AssignerNotAllowedRejection`). Der Texttrigger-Fallback (Z. 113) bleibt unverändert — die dortige Marker-Regel ist damit die alleinige Marker-Abwehr und deckt den Fall ab, dass eine Marker-Beschreibung einen fremden Zuweisender legitimieren könnte.
- **Geänderte Dokumentation:** Der `<summary>`-Kommentar von `GetAssignRejection` (Z. 86–93, „die eigene-Antwort-Markierung gilt weiter") wird präzisiert: Im Assign-Pfad legitimiert der Zuweisender-Kreis statt der Marker-Abwehr; die Marker-Regel wirkt nur noch über den Texttrigger-Fallback bzw. den regulären Texttrigger-Pfad.
- **Unverändert:** `GetRejection` (Z. 51–84) inkl. Marker-Regel Z. 81–82, `HasFreeStandingTrigger`, `SameIdentity`, `IsTrustedAutomationLogin`, alle Ablehnungskonstanten.

### `ScmEventStore` (`src/Softwareschmiede/Application/Services/ScmEventStore.cs`)

- **Geänderte Dokumentation:** XML-Kommentar von `EmptyDescriptionBatchDiagnostic` (Z. 25–31) und der erläuternde Kommentar vor der Checkpoint-Diagnose (Z. 141–145) nennen plugin-seitig herausgefilterte Marker-Texte als Ursache — dies entfällt für Beschreibungen; die Kommentare werden auf die verbleibenden Ursachen präzisiert (Beschreibung remote nicht lesbar/Post-Watermark ausgeschlossen). Diagnosetext und Verhalten bleiben unverändert.
- **Unverändert:** `AcceptBatchAsync`-Logik, `EmptyDescriptionBatchDiagnostic`-Konstante, alle übrigen Methoden.

### `ScmEventMonitoringService` / `ScmEventOrchestrator`

- **Keine Code-Änderung.** Beide profitieren automatisch: Die Assign-Dekoration (Z. 217–236) erhält künftig nicht-leere Batches; die Zustellungs-Gegenprüfung (`DispatchCoreAsync` Z. 219–232) läuft über denselben `GetAssignRejection`-Pfad. Zur Klarstellung kann der Kommentar an der Assign-Dekoration (Z. 219–223) um einen Satz ergänzt werden, dass der Plugin-Vorfilter Beschreibungen nicht mehr entfernt.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ScmEventObservation.Body` (Assign-Pfad) | `IsNullOrWhiteSpace` → `AssignEmptyDescriptionRejection` (unverändert); Marker-Vorhandensein ist **kein** Ablehnungsgrund mehr | Leere/Whitespace-Beschreibung bleibt abgelehnt; Marker-Text allein gilt als nicht-leer und wird bei berechtigtem Zuweisenden zugestellt (siehe Scope-Ausschluss 2) |
| `ScmEventTrigger.ActorId`/`ActorLogin` (Assign-Pfad) | Bestimmbar + ∈ {Owner, Viewer, `TrustedAutomationLogins`} (unverändert) | `AssignerUnknownRejection` / `AssignerNotAllowedRejection`; Texttrigger-Fallback unverändert |
| `ScmEventObservation.Body` (Texttrigger-Pfad) | Marker `<!-- softwareschmiede:` → `"Eigene Antwort"` (unverändert, `GetRejection` Z. 81) | Eigene Outputs ohne Assign-Kontext bleiben abgewiesen |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Abgelehnte Snapshots statt leerer Batches:** Marker-tragende Beschreibungen in Nicht-Assign-Pfaden (mention-/keyword-ausgelöste und enumerierte Description-Reads sowie Description-Reads assign-loser Referenzen) erzeugen künftig Beobachtungen und damit abgelehnte `ScmContentSnapshot`-Einträge (Diagnose „Eigene Antwort"/„Kein Trigger"/`NoMentionRejection`), statt plugin-seitig gar nicht zu existieren. Keine Checkpoint-Sichtbarkeit, kein Ereignis — aber mehr Snapshot-Zeilen. Bewusst hingenommen (Anforderung).
- **Issue-Aufbereitungs-Erkennung (`ScmIssuePreparationService.DetectAndEnqueueAsync`):** Läuft auf Description-Batches im Enumerationspfad (UserAccount) und in `ScmIssuePreparationMonitoringService`. Eine vom Viewer geschriebene Marker-Beschreibung kommt jetzt als Beobachtung an. Schleifenschutz bleibt gewahrt: `SkipEvaluationAsync` (bereits ausgewertete Referenz), `CreatedAt <= baselineUtc` (Altbestand; `IssueBodyUpdate` ändert `updatedAt`, nicht `createdAt`) und der `issueaufbereitung-issue/…`-Endzustand verhindern eine Re-Auslösung; der bestehende Test `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` deckt genau diesen Fall.
- **Kommentar-Arten unverändert gefiltert:** Viewer-autorierte Marker-Kommentare werden weiterhin plugin-seitig verworfen — bewusst, da sie in keinem Pfad legitimierbar sind (kein Assign-Kontext für Kommentare existiert).
- **`GitHubEventSourceTests`:** `OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted` bleibt gültig (Kommentar-Filter besteht fort). Ein Description-Gegenstück existiert nicht und ist neu zu schreiben.
- **`GetRejection_AssignOwnResponseMarker_Rejected`:** Erwartung kehrt sich um — wird zum Akzeptanz-Test.

## Umsetzungsreihenfolge

1. **Plugin-Vorfilter auf Kommentar-Arten beschränken**
   - Voraussetzungen: Keine.
   - Beschreibung: In `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs` `AddObservation` den Marker-Vorfilter (Z. 384) auf `request.ContentKind is ScmContentKind.IssueComment or ScmContentKind.ReviewComment or ScmContentKind.PullRequestReview` begrenzen; Kommentar zur Filterabsicht aktualisieren.

2. **Marker-Ablehnung aus `GetAssignRejection` entfernen**
   - Voraussetzungen: Keine (unabhängig von Schritt 1, aber erst gemeinsam wirksam).
   - Beschreibung: In `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs` das `else if`-Glied Z. 106–107 streichen und den `<summary>`-Kommentar (Z. 86–93) auf die neue Akzeptanzkette präzisieren.

3. **Einheits- und Plugin-Tests anpassen/neu schreiben**
   - Voraussetzungen: Schritte 1–2 (Tests verifizieren das neue Verhalten).
   - Beschreibung: `GetRejection_AssignOwnResponseMarker_Rejected` in `ScmEventValidationServiceTests` in den Akzeptanzfall umwandeln (umbenennen, `Assert.Null`); ggf. Ergänzungsfall „fremder Zuweisender + Marker-Body ohne Mention → `AssignerNotAllowedRejection`". Neuen Plugin-Test in `GitHubEventSourceTests`: viewer-autorierte Marker-`Description` wird als Beobachtung geliefert; Kommentar-Filter bleibt (bestehender Test deckt das bereits).

4. **Integrationstest für den Assign-Marker-Pfad**
   - Voraussetzungen: Schritte 1–3; `ScmDispatchFixture` (`assignmentSource: true`, `ConfigureDescriptionObservations(body, authorId, authorLogin)`, `ConfigureAssignmentSource`, `BotRepository`) vorhanden.
   - Beschreibung: Neuer Test in `ScmEventMonitoringServiceTests_Assignments`: assign-Notification + `ConfigureDescriptionObservations` mit Marker-Body und Viewer-Autor (`"BOT"`/`"scm-bot"`) + berechtigter Zuweisender ⇒ Aufgabe + `ScmIncomingEvent` (`Trigger.Reason == "assign"`) + Prompt mit `AssigneePromptPrefix`. Doc-Kommentar von `Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded` anpassen (Marker-Filterung ist keine Leer-Batch-Ursache mehr).

5. **Kommentare in `ScmEventStore` präzisieren**
   - Voraussetzungen: Schritt 1 (ändert die Leer-Batch-Ursachen).
   - Beschreibung: XML-Kommentare zu `EmptyDescriptionBatchDiagnostic` (Z. 25–31) und vor der Checkpoint-Diagnose (Z. 141–145) auf die verbleibenden Ursachen umformulieren; ggf. Kommentar an der Assign-Dekoration in `ScmEventMonitoringService` ergänzen.

6. **Hilfedokumentation aktualisieren**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `docs/help/plugins/github-ereignisse/ablauf-technisch.md` (Assign-Akzeptanzbedingungen ohne „kein eigener Antwortmarker"; Leer-Batch-Erklärung ohne Marker-Filterung als Ursache), `business-rules.md` (Marker-Abwehr als Texttrigger-Regel präzisieren, Assign-Akzeptanz ergänzen), `ereignisablaeufe.md` (Ablauf B+ Schritt 3, ~Z. 115–117: „kein eigener Antwortmarker" aus der `GetAssignRejection`-Akzeptanzkette entfernen — Datei ist selbstdeklariert normativ mit der Implementierung abgeglichen), `troubleshooting.md` (Assign-Ablehnungsgründe unverändert korrekt — ggf. Hinweis, dass Marker-Beschreibungen bei Zuweisung akzeptiert werden).

7. **Build und reguläre Testlanes verifizieren**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: Vollständiger Build, dann `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1 dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"` und dasselbe für `src/Softwareschmiede.IntegrationTests`; `dotnet format Softwareschmiede.slnx --verify-no-changes`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `OwnMarkedDescription_IsDelivered` (Arbeitstitel) | `GitHubEventSourceTests` | `ReadEventsAsync(Description)` mit Viewer-autoriertem, marker-tragendem Body liefert die Beobachtung (Vorfilter greift nicht mehr auf `Description`); ergänzt `OwnMarkedComment_IsExcluded_ButForeignMarkerIsNotTrusted`, das weiterhin den Kommentar-Filter belegt |
| `GetRejection_AssignOwnResponseMarker_Accepted` (Umwandlung des bestehenden `…_Rejected`) | `ScmEventValidationServiceTests` | Marker-Body + berechtigter Zuweisender (Owner) im Assign-Pfad → `null` |
| `GetRejection_AssignForeignAssignerWithMarker_StaysRejected` (optionaler Ergänzungsfall ohne Mention) | `ScmEventValidationServiceTests` | Marker-Body + fremder Zuweisender → `AssignerNotAllowedRejection` (Fallback kann Marker-Body nicht legitimieren) |
| `Monitor_SeparatesAccount_AssignViewerAuthoredMarkerDescription_CreatesTaskAndDispatches` (Arbeitstitel) | `ScmEventMonitoringServiceTests_Assignments` | End-to-end: assign-Notification + Viewer-autorierte (`BOT`/`scm-bot`) Marker-Beschreibung + `ScmAssignment` von `owner` ⇒ eine Aufgabe, ein `ScmIncomingEvent` mit `Trigger.Reason == "assign"`, Dispatch mit `AssigneePromptPrefix` im Prompt |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GetRejection_AssignOwnResponseMarker_Rejected` (`ScmEventValidationServiceTests`, Z. 293–296) | Erwartung kehrt sich um: Marker im Assign-Pfad ist kein Ablehnungsgrund mehr — Umwandlung in Akzeptanz-Test (siehe neue Tests) |
| `Monitor_SeparatesAccount_AssignEmptyDescriptionBatch_Discarded` (`ScmEventMonitoringServiceTests_Assignments`, Z. 368–391) | Verhalten bleibt korrekt (echt leerer Batch); nur der XML-Kommentar nennt die Marker-Filterung als Ursache und wird aktualisiert |
| `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` (`ScmEventMonitoringServiceTests_IssuePreparation`, Z. 235–266) | Keine Anpassung nötig — wird als Regressionstest mitgeführt (Marker-Beschreibung im Enumerationspfad löst weiterhin keinen zweiten Aufbereitungsauftrag aus) |

### E2E-Tests (primärer Funktionsnachweis)

Keine FlaUI-E2E-Tests erforderlich. Begründung: Die Anforderung betrifft keinen über UI oder Nutzeraktion erreichbaren Ablauf — der Auslöser ist eine externe GitHub-Notification, die Wirkung ein Inbox-Ereignis + CLI-Zustellung in Hintergrunddiensten. Der vollständige Nutzer-/Systemablauf (Notification → Description-Read → Validierung → Aufgabe → Dispatch-Prompt) wird stattdessen über den Integrationstest in `ScmEventMonitoringServiceTests_Assignments` nachgewiesen, der mit `ScmDispatchFixture` echte Services gegen echte SQLite und den realen `ScmEventOrchestrator`-Dispatch (Prompt-Erfassung inkl. `AssigneePromptPrefix`) fährt — das ist die bestehende, für diesen Pfad kanonische Nachweisebene aller Assign-Integrationstests. Die plugin-seitige Filterkorrektur wird durch den `GitHubEventSourceTests`-Test gegen den echten `GitHubPlugin`-Code mit Fake-Transport nachgewiesen; beide zusammen decken die komplette geänderte Kette ab.

Anzupassende bestehende E2E-Tests: Keine.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: berechtigte Zuweisung auf aufbereitetes Issue | GitHub-Zuweisung eines Issues an das Überwachungskonto durch den Repository-Owner | `SeparateAccount`-Modus, Inbox-Notification `reason = assign` vorhanden, Beschreibung ist vom Überwachungskonto neu geschrieben und trägt `<!-- softwareschmiede:action:<id> -->`; noch keine lokale Aufgabe zur Referenz | Notification → `ReadEventsAsync(Description)` liefert Marker-Beschreibung → Assign-Dekoration (`Trigger`, `assign-{eventNodeId}` im `RemoteNodeId`) → `GetAssignRejection` akzeptiert → `ScmEventTaskResolver` legt Aufgabe an → `AcceptBatchAsync` reiht `ScmIncomingEvent` ein → `DispatchCoreAsync` besteht die Gegenprüfung und schreibt den Prompt | Aufgabe in der Übersicht, Ereignis `Zugestellt`, zugestellter Prompt beginnt mit `AssigneePromptPrefix`; keine `Inbox: Keine verwertbare Referenzbeschreibung`-Diagnose | Integrationstest `…_AssignViewerAuthoredMarkerDescription_CreatesTaskAndDispatches`; Plugin-Test `OwnMarkedDescription_IsDelivered` |
| 2 | Zuweisung entzogen vor Verarbeitung | assign-Notification, danach `unassigned` auf GitHub | Wie Szenario 1; `GetLatestViewerAssignmentAsync` liefert `null` | Dekoration mit `assign-unbestimmt`; `GetAssignRejection` → `AssignerUnknownRejection` | Kein Ereignis; Snapshot-Diagnose `Zuweisender nicht bestimmbar`, `Inbox:`-Diagnose am Checkpoint | Bestehender Test `Monitor_SeparatesAccount_AssignUndeterminable_Rejected` |
| 3 | Unberechtigter Zuweisender mit Marker-Beschreibung | assign-Notification durch fremdes Konto | Wie Szenario 1; `ScmAssignment.Assigner` = Fremdlogin | `GetAssignRejection` → `AssignerNotAllowedRejection`; Fallback lehnt Marker-Body ebenfalls ab | Kein Ereignis; `Inbox: Zuweisung nicht durch berechtigtes Konto` | Neuer Unit-Test `GetRejection_AssignForeignAssignerWithMarker_StaysRejected`; bestehender `…_AssignByForeignLogin_Rejected` |
| 4 | Marker-Beschreibung ohne Assign-Kontext | Enumerierter/mention-ausgelöster Description-Read | Beschreibung trägt Marker, Referenzgruppe ohne `assign`-Notification | Beobachtung geht ohne `Trigger` in `GetRejection` → `NoMentionRejection`/`„Kein Trigger"` bzw. `"Eigene Antwort"` | Kein Ereignis; abgelehnter Snapshot (keine Inbox-Sichtbarkeit); kein Rekursions-Loop | `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop`; `SeparatesAccount_OwnResponseMarkerStillRejected` |
| 5 | Tatsächlich leerer Batch / leere Beschreibung | assign-Notification, Beschreibung remote nicht lesbar bzw. leer | Beschreibung leer (Whitespace) oder Description-Read ohne Beobachtung | Leerer Body → `AssignEmptyDescriptionRejection` je Beobachtung; leerer Batch → `EmptyDescriptionBatchDiagnostic` am Checkpoint | `Inbox: Referenzbeschreibung ist leer` bzw. `Inbox: Keine verwertbare Referenzbeschreibung` | Bestehende Tests `…_AssignEmptyDescription_CreatesNoTask`, `…_AssignEmptyDescriptionBatch_Discarded` |

## Offene Punkte

Keine — die vier Fragen der Anforderung wurden als Designentscheidungen bzw. Scope-Festlegungen verbindlich in den Plan übernommen: Plugin-Variante (a), Geltung für Issues und PRs, keine Mindest-Inhaltsbedingung (Scope-Ausschluss 2, Status `bestätigt` in Schritt 5a am 2026-11-27), Anpassung der Diagnose-Kommentare (Schritt 5). Alle fünf Scope-Ausschlüsse tragen den Status `bestätigt`.
