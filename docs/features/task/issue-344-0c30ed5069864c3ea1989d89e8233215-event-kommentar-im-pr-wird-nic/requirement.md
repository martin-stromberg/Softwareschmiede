# Übersetzte Anforderung: SCM-Ereignis an aktive Aufgabe desselben Kontexts zustellen statt Doppelaufgabe

**Quelle:** Issue #344 (`martin-stromberg/Softwareschmiede`) / Aufgaben-ID `0c30ed50-6986-4c3e-a198-9d89e8233215`
**Branch:** `task/issue-344-0c30ed5069864c3ea1989d89e8233215-event-kommentar-im-pr-wird-nic`

## Fachliche Zusammenfassung

Die Aufgabenauflösung eingehender SCM-Ereignisse (`ScmEventTaskResolver`) ordnet ein Pull-Request-Ereignis einer bestehenden Aufgabe nur zu, wenn bereits eine persistierte `PullRequestReferenz` für genau diesen Pull Request existiert. Ein von der laufenden CLI-Sitzung einer Aufgabe selbst angelegter Pull Request ist aber erst nach deren Abschlussmeldung (`pullRequestNumber` in `complete.json` → `LinkReportedPullRequestAsync` → `PullRequestReferenzService.SaveReportedAsync`) oder bei hostseitiger PR-Anlage (`GitOrchestrationService.PullRequestErstellenAsync` → `SaveCreatedAsync`) verknüpft. Solange die Aufgabe noch läuft (im Szenario: die CLI wartet auf die PR-Checks), fehlt diese Verknüpfung — ein eingehender PR-Kommentar wird daher als unzugeordnet gewertet und `CreateFromPullRequestAsync` legt eine zweite, kontextlose Aufgabe (Rolle `ReviewSource`) an.

Die Anforderung erweitert die Auflösung um eine Kontextprüfung: Bevor aus einem SCM-Ereignis eine neue Aufgabe angelegt wird, muss geprüft werden, ob bereits eine **aktive Aufgabe** für denselben Aufgabenkontext existiert — den betroffenen Pull Request, das ursprüngliche Issue oder allgemein denselben Origin-Bezug. Trifft genau eine aktive Aufgabe zu, ist das Ereignis dieser Aufgabe zuzuordnen (und damit der laufenden Sitzung über die bestehende Dispatch-Pipeline zuzustellen); nur bei fehlendem Treffer wird eine neue Aufgabe angelegt, bei Mehrdeutigkeit bleibt die bestehende `Ambiguous`-Diagnose.

## Betroffene Klassen und Komponenten

### Bestehende, voraussichtlich zu ändernde Artefakte

- `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` — Kern der Änderung. `ResolvePullRequestAsync` (Zeilen 76–106) prüft heute ausschließlich `PullRequestTaskIdsAsync` (Provider + normalisierte `RepositoryId` + `PullRequestNumber`) und legt bei `taskIds.Count == 0` sofort via `CreateFromPullRequestAsync` an. Hier ist vor der Anlage die aktive-Aufgabe-Suche für denselben Kontext einzuschieben. Analog ist `ResolveIssueAsync` (Zeilen 52–74) auf dieselbe Erweiterung zu prüfen.
- `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` — Aufbau der `linked`-Liste (Zeilen 46–59): Referenzen werden nur über `IssueReferenz.IssueNummer` und `task.PullRequests` einer Aufgabe zugeordnet. Optionaler zweiter Ansatzpunkt: Ein enumerierter Pull Request, dessen Head-Branch dem `BranchName` einer aktiven Aufgabe entspricht, könnte bereits hier — statt erst im Resolver — der Aufgabe zugeordnet werden (Implementierungsalternative, siehe unten).
- `src/Softwareschmiede/Application/Services/AufgabeService.cs` — `CreateFromIssueEventAsync` (Zeile 264) und `CreateFromPullRequestAsync` (Zeile 331) bleiben die Anlagepfade, werden aber erst nach der neuen Kontextprüfung erreicht; ggf. sind die deduplizierenden Vorab-Lookups (Zeilen 276–285, 364–371) um die neuen Kontextkriterien zu erweitern (Annahme).
- `src/Softwareschmiede/Domain/Entities/Aufgabe.cs` — kein Schema-Eingriff zwingend: die benötigten Signale existieren bereits (`IssueReferenz`, `PullRequests`, `BranchName`, `Status`, `AusfuehrungsStatus`, `PausiertBisUtc`). Neue Persistenz ist aus der Anforderung nicht ableitbar (Annahme).

### Verfügbare Korrelationssignale (Bestand, keine neuen Artefakte)

- `PullRequest.BranchName` (Head-Branch aus `IScmEventSource.ReadPullRequestAsync`) ↔ `Aufgabe.BranchName`: Von der Anwendung erzeugte Task-Branches folgen dem Muster `task/issue-{IssueNummer}-{AufgabeId:N}-{TitelSlug}` (`EntwicklungsprozessService.ErstelleTaskBranchName`, Zeilen 833–841) — der Branchname enthält damit sogar die Aufgaben-GUID und die Issue-Nummer; ein Gleichheitsvergleich auf `BranchName` ist ein sehr starkes Kontextsignal.
- PR-Body ↔ ursprüngliches Issue: `PullRequestBodyBuilder` ergänzt automatisch `Closes #{IssueNummer}` (Zeilen 14–29, Erkennungsregex Zeile 51); ein im Body referenziertes/geschlossenes Issue kann über `IssueReferenz.IssueNummer` einer aktiven Aufgabe zugeordnet werden.
- `PullRequestReferenz.SourceBranch` — falls Referenzen anderer Rollen existieren, ebenfalls als Branch-Signal nutzbar.
- `ScmIncomingEvent`-/`ScmContentSnapshot`-Bezüge der Ursprungsaufgabe — die ursprüngliche Aufgabe besitzt bereits Inbox-Ereignisse desselben Repositories (schwächeres Signal, primär über Issue-/PR-Bezug abzudecken).

### Definition „aktive Aufgabe"

- Kandidat: `Aufgabe.Status` nicht `Beendet` und nicht `Archiviert` — spiegelt `ScmEventOrchestrator.BlockReason` (Zeilen 469–471), das genau diese beiden Status als Startsperre behandelt. `Neu`, `Gestartet`, `Wartend` gelten demnach als aktiv; pausierte Aufgaben (`PausiertBisUtc`) wären danach ebenfalls Empfänger — das Ereignis wartet dann regulär („Aufgabe pausiert; Start gesperrt."), statt eine Doppelaufgabe zu erzeugen. Ob die Definition zusätzlich `AusfuehrungsStatus == Aktiv` oder eine lebende `IScmPromptSessionAdapter`-Sitzung (`KiAusfuehrungsService.GetScmPromptSession`) verlangt, ist zu klären (Offene Fragen).

### Tests

- `src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_TaskAssignment.cs` — neuer Fall: qualifizierter Kommentar auf einem Pull Request, dessen Head-Branch/Issue-Bezug einer laufenden Aufgabe ohne `PullRequestReferenz` entspricht → Ereignis wird dieser Aufgabe zugeordnet, **keine** zweite Aufgabe entsteht (Gegenstück zu `Monitor_ExistingTask_IsReusedWithoutSecondCreation`, Zeile 167). Weitere Fälle: Branch-Match, Closing-Directive-Match, kein Match → Anlage wie bisher, Mehrfach-Match → `AmbiguousAssignmentDiagnostic`, Match auf beendete Aufgabe (je nach Klärung).
- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` — Fixture ggf. um einen „laufende Aufgabe mit Branch, ohne PR-Referenz"-Zustand erweitern (Annahme).
- Ggf. neue Unit-Tests nahe `ScmEventTaskResolver` (aktuell überwiegend indirekt über Integrationstests abgedeckt — Annahme, Teststruktur im Planungsschritt festlegen).

## Implementierungsansatz

- **Erweiterungspunkt:** `ScmEventTaskResolver.ResolvePullRequestAsync` — nach dem bestehenden `PullRequestReferenzen`-Lookup und **vor** `CreateFromPullRequestAsync` eine Kandidatensuche über die o. g. Signale durchführen:
  1. Aktive Aufgaben der kanonisch aufgelösten Repositories (`repositories`-Parameter bzw. `GitRepositoryId`), deren `BranchName` dem PR-Head-Branch entspricht;
  2. aktive Aufgaben, deren `IssueReferenz.IssueNummer` dem im PR-Body referenzierten/geschlossenen Issue entspricht (`ReadPullRequestAsync` liefert `Body` bereits);
  3. bestehender `PullRequestReferenzen`-Treffer (unverändert, stärkstes Signal).
- **Ergebnissemantik unverändert:** Genau ein Kandidat → `ScmTaskResolution.Assigned(taskId)`; mehrere unterschiedliche Kandidaten → `Ambiguous` (bestehende `AmbiguousAssignmentDiagnostic`/`"Mehrdeutige Zuordnung zu Aufgaben"`); kein Kandidat → bisherige Anlage. Die Zustellung an die laufende Sitzung erfordert **keine** Änderung am Dispatch: Ist die CLI beschäftigt, wartet das Inbox-Ereignis über `WaitForInputReadyAsync`/„CLI beschäftigt; wartet auf Eingabebereitschaft." (`ScmEventOrchestrator`, Zeilen 174–189) und wird bei Eingabebereitschaft über den `ScmEventExchangeProtocol`-Antwortkanal zugestellt; die Sequentialisierung läuft über `ScmTaskCoordinator` und die Sequenzordnung der `ScmIncomingEvents`.
- **Alternative/Ergänzung (abzuwägen):** Frühzeitige Selbstheilung der Verknüpfung — sobald das Monitoring einen Pull Request entdeckt, dessen Head-Branch dem `BranchName` einer aktiven Aufgabe entspricht, die `PullRequestReferenz` sofort persistieren (analog `SaveReportedAsync`, Rolle `CreatedByTask`). Danach greifen die bestehenden Pfade (`linked`-Zuordnung im Monitoring und Referenz-Lookup im Resolver) ohne weitere Resolver-Änderung. Zu klären: Rollen-/Statussemantik einer so implizit verknüpften Referenz und deren Effekt auf `ScmTaskCompletionMonitoringService` (die Abschlussüberwachung wertet `task.PullRequests` mit Vorrang vor dem Issue aus — eine früh verknüpfte Referenz beeinflusst `DetermineClosureAsync`, Zeilen 149–184).
- **Symmetrie:** Für `ScmReferenceKind.Issue` existiert bereits eine referenzbasierte Wiederverwendung (`IssueReferenz.IssueNummer`, statusagnostisch). Ob dort ebenfalls eine „nur aktive Aufgabe"-Einschränkung bzw. ein zusätzliches Kontextsignal nötig ist, ist zu klären — die Anforderung nennt den PR-Fall, spricht aber von „allgemein für denselben Aufgabenkontext/Origin-Bezug".
- **Anlagerennen:** Der bestehende Unique-Index-/Retry-Pfad in `CreateFromPullRequestAsync` (AufgabeService, Zeilen 414–431) und `ResolvePullRequestAsync` (Zeilen 99–105) bleibt die Sicherheit gegen parallele Anlage; die neue Kontextsuche ist ein zusätzlicher Vor-Filter, keine Garantie — Race-Handling bleibt erhalten.

## Konfiguration

- Aus der Anforderung ist **kein** Konfigurationsbedarf ableitbar; die Kontextprüfung soll als fester Bestandteil der Aufgabenauflösung gelten. Eine optionale Deaktivierbarkeit (z. B. in den bestehenden SCM-Ereignis-Einstellungen, vgl. `GetScmMonitoringEnabledAsync`/`GetScmTaskCompletionEnabledAsync`) ist nicht gefordert — falls gewünscht, zu klären.

## Offene Fragen

1. **Definition „aktiv":** Gilt `Status ∉ {Beendet, Archiviert}` als ausreichend, oder muss die Aufgabe zusätzlich eine lebende CLI-Sitzung/`AusfuehrungsStatus.Aktiv` besitzen? Und wie ist mit pausierten Aufgaben (`PausiertBisUtc`) umzugehen — Ereignis zuordnen und warten lassen oder als „nicht aktiv" werten?
2. **Beendete Aufgabe als einziger Treffer:** Verweist der Kontext (z. B. PR-Branch) nur auf eine bereits `Beendet`/`Archiviert` Aufgabe — wird dann wie bisher eine neue Aufgabe angelegt, oder soll das Ereignis der beendeten Aufgabe zugeordnet werden (wo es mangels Startsperre nie zugestellt würde), oder soll die beendete Aufgabe reaktiviert/fortgesetzt werden?
3. **Konkurrierende Signale:** Zeigen verschiedene Signale auf verschiedene Aufgaben (z. B. Branch-Match auf Aufgabe A, `Closes #N`-Match auf Aufgabe B) — gilt das als `Ambiguous`, oder gibt es eine feste Signal-Priorität (PR-Referenz > Branch > Issue-Bezug)?
4. **Issue-Ereignisse symmetrisch?** Soll auch ein Issue-Kommentar, dessen Issue zu einem Pull Request einer aktiven Aufgabe gehört (umgekehrte Richtung), dieser zugeordnet werden — oder bleibt die Issue-Auflösung bei `IssueReferenz` unverändert?
5. **Branch-Match auf Fremd-Branches:** Reicht `Aufgabe.BranchName == pullRequest.BranchName` als hinreichendes Kontextsignal auch dann, wenn der Branch nicht nach dem App-Muster (`task/...`) benannt ist (z. B. manuell erstellte Branches gleichen Namens), oder soll der Match auf app-generierte Branch-Namen bzw. auf `SourceRepositoryId`-Gleichheit eingeschränkt werden (Forks vs. Same-Repo-PRs)?
6. **Selbstheilende Verknüpfung:** Soll ein per Kontext zugeordnetes Ereignis die `PullRequestReferenz` gleich persistent nachziehen (damit Folgeereignisse und die Abschlussüberwachung `DetermineClosureAsync` die Referenz sehen), oder bleibt die Zuordnung rein ereignisbezogen bis die Aufgabe den PR selbst meldet?
7. **Kontextlose Fremd-PRs unverändert?** Bestätigung, dass PRs ohne jede Beziehung zu einer aktiven Aufgabe weiterhin wie bisher eine `ReviewSource`-Aufgabe erzeugen — die Anforderung betrifft nur den Doppelaufgaben-Fall, nicht das bewusste Anlegen aus freien PR-Ereignissen.
