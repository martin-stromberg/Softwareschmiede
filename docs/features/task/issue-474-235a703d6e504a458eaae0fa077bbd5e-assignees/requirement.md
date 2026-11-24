# Übersetzte Anforderung: Assignee-Auslöser für SCM-Ereignisse (Issue #474)

## Fachliche Zusammenfassung

Im Überwachungsmodus `ScmAccountMode.SeparateAccount` wird die Notifications-Inbox des separaten Kontos um einen zweiten Auslöser erweitert: Neben `reason = mention` führt auch `reason = assign` (das separate Konto wurde als Assignee zugeordnet) zur Ereignisverarbeitung — für Issues und Pull Requests. Bei einer Zuweisungs-Notification gilt die Beschreibung der Referenz (`ScmContentKind.Description`) als auslösender Text, ohne dass sie eine Erwähnung oder ein Keyword enthalten muss; ist sie leer, wird keine Aufgabe angelegt. Die Zuweisung wird nur akzeptiert, wenn sie vom Repository-Owner, vom separaten Konto selbst oder von einem vertrauenswürdigen Automatisierungskonto (z. B. github-actions) vorgenommen wurde. Jede Zuweisung — auch eine erneute Zuweisung derselben Referenz — erzeugt eine neue Ereignisverarbeitung; eine bereits bestehende Aufgabe wird wiederverwendet und erhält den Prompt zugestellt. Der zugestellte Prompt enthält erkennbar die Information, dass er durch eine Assignee-Zuweisung ausgelöst wurde.

## Fachlicher Scope

- **Verarbeitungseinheit:** Eine Zuweisungs-Notification (`reason = assign`) der Notifications-Inbox des separaten Kontos auf einer SCM-Referenz (`ScmReferenceKind.Issue` oder `ScmReferenceKind.PullRequest`) eines registrierten Repositories, samt der daraus abgeleiteten Referenzbeschreibung als Auftragsinhalt. Zum Scope gehört die Prüfung des Zuweisenden (Akteur der Zuordnung), die Beschreibungs-basierte Auftragsableitung und die Zustellung des Prompts an die zugeordnete Aufgabe; nicht dazu gehört die laufende Textbeobachtung (Kommentare/Reviews) der Referenz — diese folgt den bestehenden Regeln.

- **Auslösende Ereignisse:**
  - Eintrag mit `Reason = "assign"` und `Kind = Issue` in der per `IScmNotificationSource.EnumerateNotificationsAsync` gelesenen Inbox (GitHub: `subject.type = "Issue"`, `reason = "assign"`).
  - Eintrag mit `Reason = "assign"` und `Kind = PullRequest` — der Assignee-Auslöser gilt gleichermaßen für Pull Requests.
  - Erneute Zuweisung einer bereits verknüpften Referenz (neuer bzw. aktualisierter `assign`-Thread) löst eine erneute Ereignisverarbeitung aus; die vorhandene Aufgabe wird weiterbearbeitet.

- **Erwartete Wirkung:**
  - Existiert noch keine lokale Aufgabe zur Referenz, wird — wie beim Erwähnungs-Auslöser über `ScmEventTaskResolver` — genau eine Aufgabe aus den Referenzdaten angelegt (Issue-Pfad `WorkItemService.CreateFromIssueEventAsync`, PR-Pfad `WorkItemService.CreateFromPullRequestAsync`); die Referenzbeschreibung ist der Auftragsinhalt.
  - Existiert bereits eine Aufgabe, wird sie wiederverwendet und das neue Ereignis wird ihr als Prompt zugestellt (keine Doppelanlage).
  - Der an die CLI-Sitzung zugestellte Prompt enthält die Information, dass das Ereignis durch eine Assignee-Zuweisung entstanden ist.
  - Verworfene Zuweisungen (Zuweisender nicht berechtigt, leere Beschreibung, nicht registriertes Repository) erzeugen keine Aufgabe und keinen Prompt; sie bleiben über die bestehenden Diagnosewege (Snapshot-/Checkpoint-Diagnose, `InboxDiscardDiagnosticPrefix` für Fremd-Repositories) nachvollziehbar.

- **Umgang mit fehlenden Objekten:**
  - Aufgabe zur Referenz fehlt → über `ScmEventTaskResolver` neu erzeugen (bestehende Mechanik, unverändert).
  - Aufgabe zur Referenz existiert → wiederverwenden; das Zuweisungsereignis wird als neuer Prompt an diese Aufgabe zugestellt.
  - Referenzbeschreibung leer oder nur Leerraum → keine Aufgabe anlegen, Ereignis verwerfen (neu).
  - Zuweisender nicht bestimmbar oder nicht im erlaubten Kreis (Repository-Owner, separates Konto = `ViewerLogin`, `ScmEventValidationService.TrustedAutomationLogins` wie `github-actions[bot]`/`app/github-actions`) → verwerfen/ablehnen (neu).
  - Repository der Notification nicht registriert → verwerfen mit bestehender `Inbox:`-Zählerdiagnose (unverändert).

- **Scope-Ausschlüsse:**
  - Der Assignee-Auslöser gilt ausschließlich im Modus `ScmAccountMode.SeparateAccount` (Notifications-Inbox). Im Modus `Benutzerkonto` bleibt das konfigurierte Trigger-Keyword der einzige Auslöser; es wird keine Assignee-Auswertung eingeführt.
  - Das Entfernen der Zuweisung (Unassign) löst kein Ereignis aus und bricht keine Aufgabe ab.
  - Weitere Notification-Reasons (`subscribed`, `review_requested`, `ci_activity` u. a.) bleiben unverändert ohne Wirkung.
  - Keine neue Konfiguration und keine UI-Änderung: Der Auslöser folgt dem bestehenden Schalter `scm.monitoring.accountmode` und ist nicht separat abschaltbar.
  - Der auslösende Text muss weder das Trigger-Keyword noch eine `@`-Erwähnung enthalten; inhaltliche Trigger-Anforderungen entfallen nur für diesen Auslöser, nicht für die übrigen Inhalte derselben Referenz.

## Betroffene Klassen und Komponenten

- **Datenmodell / Verträge (`src/Softwareschmiede.Plugin.Contracts`):**
  - `ScmNotification` (`Domain/ValueObjects/ScmEvents.cs`): neue Reason-Konstante (z. B. `AssignReason = "assign"` analog `MentionReason`); ggf. Erweiterung um Zuweisungs-Metadaten (Zeitpunkt der Zuweisung), damit jede Zuweisung eine eigene Ereignisidentität erhält.
  - `IScmNotificationSource` oder neue optionale Fähigkeit (z. B. `IScmAssignmentSource`): Ermittlung des Akteurs der jüngsten Zuweisung auf den Viewer, da die Notifications-API den Zuweisenden nicht liefert (Annahme — siehe Implementierungsansatz).
  - `ScmEventObservation`: ggf. neue Komponente für die Auslöseherkunft (Assignee vs. Textauslöser), damit Validierung, Dedup und Prompt-Zusammensetzung die Herkunft unterscheiden können.

- **Logik / Services (`src/Softwareschmiede`):**
  - `ScmEventMonitoringService.EnumerateNotificationsAsync`: Reason-Filter um `assign` erweitern; die Reason muss bis zur abgeleiteten `ResolvedReference` getragen werden (z. B. neues Feld auf `ResolvedReference`), weil die Zuweisungs-Referenz anders validiert und dedupliziert wird als die Erwähnungs-Referenz.
  - `ScmEventMonitoringService.RunOnceAsync`: assign-abgeleitete Referenzen in den bestehenden Read-/Resolve-/Accept-Block einspeisen; Triggersuche für assign-Referenzen auf `ScmContentKind.Description` ohne Erwähnungs-/Keyword-Pflicht ausrichten.
  - `ScmEventValidationService`: assign-spezifische Ablehnungsregeln — leere Beschreibung, Zuweisender außerhalb des erlaubten Kreises (Owner / `ViewerLogin` / `TrustedAutomationLogins`); neue Rejection-Konstanten nach dem Muster von `NoMentionRejection`.
  - `ScmEventTaskResolver`: sicherstellen, dass bei leerem Auftragsinhalt keine Aufgabe angelegt wird und bei bestehender Aufgabe die Wiederverwendung greift (bestehende `IssueSourceKey`-/`PullRequestTaskIdsAsync`-Dedup-Mechanik).
  - `ScmEventStore.AcceptBatchAsync`: Dedup über `ScmIncomingEvent.EventKey`/`ScmContentSnapshot` darf eine erneute Zuweisung bei unveränderter Beschreibung nicht verschlucken — die Beobachtungsidentität des Zuweisungsereignisses muss zuweisungsspezifisch sein.
  - `ScmEventOrchestrator` / `ScmEventExchangeService` / `ScmExchangeRequest` bzw. `ScmPromptAppendix`-naher Erweiterungspunkt: Aufnahme des festen Assignee-Herkunftshinweises in den zugestellten Prompt.

- **Plugin (`plugins/Softwareschmiede.Plugin.GitHub`):**
  - `GitHubPlugin.EnumerateNotificationsAsync`: `assign`-Threads kommen bereits im Payload an (kein Subject-Filter nötig); neu ist die Ermittlung des Zuweisenden — voraussichtlich über die Issue-/PR-Timeline oder Events des Providers (z. B. REST `issues/{n}/events`/`timeline` oder GraphQL `AssignedEvent`), unter dem Agent-Client (`ClientFor(ScmAccountMode.SeparateAccount)`).

- **Test-Infrastruktur:**
  - `ScmDispatchFixture` (`AddNotification`, Mock von `IScmNotificationSource`), `ScmE2ETestConfiguration`/`ScmE2ENotification`, `ScmE2EApiClient` (`GET /notifications`-Fixture) und `ScmE2EFixture` im E2E-Pfad.

- **Tests:**
  - `ScmEventMonitoringServiceTests_Notifications` (assign → Aufgabe, assign auf PR, erneute Zuweisung → erneute Zustellung an dieselbe Aufgabe, leere Beschreibung → keine Aufgabe, fremder Zuweisender → Verwerfung, Owner-/Bot-/Automations-Zuweisender → akzeptiert).
  - `ScmEventValidationServiceTests` (neue assign-Ablehnungsfälle).
  - `GitHubNotificationSourceTests` bzw. neue Tests der Assigner-Abfrage.
  - Konsolidierter SCM-E2E (`E2E_ScmEvents.cs`): assign-Phase in den bestehenden `SeparateAccount`-Lauf integrieren.

## Implementierungsansatz

- **Reason-Erweiterung der Inbox-Auswertung:** In `ScmEventMonitoringService.EnumerateNotificationsAsync` den bisherigen Filter `x.Reason == ScmNotification.MentionReason` um `assign` erweitern und die Reason je abgeleiteter Referenz mitführen (`ResolvedReference`-Erweiterung oder separate Auflistung), damit nachgelagerte Verarbeitung Erwähnungs- und Zuweisungsauslöser unterscheiden kann.
- **Auslösender Text = Beschreibung:** Für assign-getriggerte Referenzen wird die `Description`-Beobachtung der Referenz zum Auftragsinhalt, unabhängig von ihrem Inhalt. Dafür ist ein assign-bewusster Validierungspfad nötig (`ScmEventValidationService` um eine Überladung/Variante von `GetRejection` oder einen eigenen Prüfschritt): Die Erwähnungs-/Keyword-Prüfung entfällt, dafür gelten die neuen Ablehnungsregeln „leere Beschreibung" und „Zuweisender nicht berechtigt".
- **Zuweisender-Ermittlung (Annahme):** Die Notifications-API liefert den Akteur der Zuweisung nicht im Thread-Payload. Es wird eine neue Plugin-Fähigkeit benötigt, die die jüngste Zuweisung des Viewers auf der Referenz mit Akteur-Login liefert (z. B. eigene Interface-Methode auf `IScmNotificationSource`-Ebene oder `IScmEventSource`-Ebene; GitHub-seitig Timeline-/Events-Endpunkt). Die Akzeptanzregel spiegelt die bestehende Akteursprüfung: Zuweisender = Repository-Owner (`OwnerId`/`Owner`) oder `ViewerLogin` (das separate Konto, z. B. Selbst-/Automations-Zuweisung) oder Eintrag aus `TrustedAutomationLogins` (`github-actions[bot]`, `app/github-actions`). Kann der Zuweisende nicht ermittelt werden, wird abgelehnt.
- **Erneute Zuweisung = neues Ereignis:** Damit die `ScmIncomingEvent`-/`ScmContentSnapshot`-Dedup (Schlüssel `ContentKey`/`Revision`/`BodyHash` der Textversion) eine wiederholte Zuweisung bei unverändertem Beschreibungstext nicht verschluckt, muss die Beobachtung des Zuweisungsereignisses eine zuweisungsspezifische Identität erhalten — z. B. indem Zeitpunkt oder Identität der Zuweisung (Notification `UpdatedAt`/`ThreadId` bzw. Timeline-Event) in `RemoteNodeId`/Revision des Assignee-Ereignisses einfließt. Die bestehende Aufgabenauflösung (`ScmEventTaskResolver`: `IssueSourceKey`/`PullRequestTaskIdsAsync`) ordnet das neue Ereignis dann der bereits existierenden Aufgabe zu, und der Prompt geht an diese Aufgabe.
- **Assignee-Herkunft im Prompt:** Der zugestellte Prompt erhält einen festen Herkunftshinweis (z. B. vorangestellte Zeile), dass das Ereignis durch eine Assignee-Zuweisung ausgelöst wurde. Offene Platzierung: als Teil des gespeicherten Ereignistextes (`ObservationJson`) oder erst bei der Prompt-Zusammensetzung in `PrepareAsync`/`ScmPromptAppendix`-Mechanik — letztere ist konfigurierbar und deckt die geforderte, nicht konfigurierbare Information nicht zwingend ab; die Entscheidung obliegt der Planung.
- **Wiederverwendung bestehender Mechanik:** Repository-Allowlist (`RepositoryNodeId`-Match), Inbox-Checkpoint/Watermark-Commit, Fremd-Verwerfungsdiagnose (`InboxDiscardDiagnosticPrefix`), Aufgabenauflösung und Dispatch (`ScmEventOrchestrator.DispatchDueEventsAsync`) bleiben unverändert; der assign-Pfad speist sich in dieselbe Pipeline ein.

## Konfiguration

Keine neue Konfiguration erforderlich. Der Auslöser folgt dem bestehenden Überwachungsmodus `scm.monitoring.accountmode` (`AppSettingService.ScmAccountModeKey`) und ist nur aktiv, wenn `SeparateAccount` konfiguriert ist; die bestehenden Einstellungen (Trigger-Keyword, Prompt-Anhänge) bleiben unverändert und greifen für den Assignee-Auslöser nicht bzw. nur wie für andere Ereignisse.

## Offene Fragen

1. **Zuweisender-Ermittlung:** Die Notifications-Inbox enthält keinen Akteur. Ist die Beschaffung des Zuweisenden über die Issue-/PR-Timeline (REST `issues/{n}/events`/`timeline` oder GraphQL `AssignedEvent` mit `assignee` = Viewer) die vorgesehene Quelle? Die konkrete API- und Filterwahl (jüngstes `assigned`-Event auf den Viewer) ist in der Planung zu entscheiden.
2. **Leere Beschreibung bei Pull Requests:** Der Kommentar nennt nur die „Issue-Beschreibung". Übertragen auf den PR-Auslöser wird angenommen, dass auch eine leere PR-Beschreibung keine Aufgabe erzeugt — als Annahme vermerkt.
3. **Mehrfach-Zuweisungshistorie:** Bei „zugewiesen → entfernt → erneut zugewiesen" gilt die jüngste Zuweisung auf den Viewer als maßgeblich (Annahme); Zuweisungen an Dritte sind für die Inbox ohnehin nicht sichtbar, da `assign` nur für das eigene Konto feuert.
4. **Herkunftshinweis:** Soll der Assignee-Hinweis nur im zugestellten Prompt stehen (Interpretation der Anforderung) oder auch im gespeicherten Ereignistext/der Ereignisanzeige sichtbar sein? Wird als „im Prompt" umgesetzt, sofern nicht anders entschieden.
5. **Diagnose-Tiefe verworfener Zuweisungen:** Ablehnungen (fremder Zuweisender, leere Beschreibung) werden analog bestehender `GetRejection`-Diagnosen am Snapshot/Checkpoint sichtbar gemacht (Annahme); eine Kommentar-Antwort auf die Referenz wie bei anderen Ablehnungen ist nicht vorgesehen, da es keinen Kommentar-Anker gibt — falls gewünscht, bitte präzisieren.
