# Code-Review: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Schritt 5)

**Status: Keine Befunde** — selbst gegengeprüfter Diff (kein Subagenten-Start möglich; dokumentierte
Abweichung), bewusst konservativ bewertet. Iteration 1/3.

## Geprüfte Dateien (Diff des Feature-Commits)

- `src/Softwareschmiede/Application/Services/ScmIssueAufbereitungsService.cs` (neu)
- `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs` (Z. 46, 48-51, 198-207)
- `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs` (Z. 65, 135)
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` (Z. 131-138)
- `src/Softwareschmiede/Application/Services/AufgabeService.cs` (Z. 270-284, 312-313)
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` (Z. 45-52, 211-241)
- `src/Softwareschmiede/Domain/Entities/{Aufgabe,GitRepository,ScmIncomingEvent}.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20261117231445_AddIssueAufbereitung{,.Designer}.cs`, `SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede.App/{App.xaml.cs,ViewModels/ScmEventQueueViewModel.cs}`
- `src/Softwareschmiede.Tests/Helpers/ScmDispatchFixture.cs` (DI-Registrierung)
- Tests: `ScmEventMonitoringServiceTests_IssueAufbereitung.cs` (10), `AppEinstellungServiceTests_IssueAufbereitung.cs` (7)

## Korrektheit der zentralen Punkte

- **Baseline:** Dedizierter Checkpoint `issueaufbereitung/{provider}/{host}/{repoId}` wird nur bei
  fehlendem Eintrag gesetzt und nie fortgeschrieben — „Beobachtungsbeginn" ist persistiert und
  stabil. `WatermarkUtc ?? 0` ist defensiv (eigener Code setzt den Wert immer); konservativ ok.
- **Erstbeobachtung vor `AcceptBatchAsync`:** Der Hook liegt korrekt im `try`-Block vor der
  Inbox-Aufnahme (Snapshot existiert dort noch nicht); Fehler laufen in den bestehenden
  SourceKey-Backoff, Wiederholung ist über SourceKey-/EventKey-Unique-Index idempotent —
  ein Abbruch zwischen Aufgaben- und Ereignisanlage heilt sich über die EventKey-Prüfung.
- **Trigger-Bypass ohne Validierungslücke:** Das interne Ereignis wird als Entity direkt angelegt —
  `GetRejection` wird nie aufgerufen. Externe Beobachtungen des selben Batches laufen unverändert
  durch `AcceptBatchAsync` → keine Wirkung auf fremde Pfade.
- **Isolation:** Drei Punkte wie analysiert — `ResolveIssueAsync` (Z. 65), `FindActiveContextTaskIdsAsync`
  (Z. 135) und der `linked`-Aufbau (Monitoring Z. 50) filtern `Zweck == null`; der eigene
  SourceKey-Namensraum `…/IssueAufbereitung/{n}` verhindert Kollisionen mit `…/Issue/{n}` am
  Unique-Index von `IssueReferenz.SourceKey`. `PullRequestTaskIdsAsync` ist immun (keine PR-Referenz).
- **Parken:** `NextAttemptUtc = long.MaxValue` entzieht das Ereignis der `DispatchDueEventsAsync`-Auswahl
  (Z. 42) dauerhaft; der Orchestrator-Guard (vor Plugin-/Beobachtungsauflösung, Z. 131-138) fängt
  explizite Dispatches/`RetryAsync` ab, parkt erneut und meldet ehrlich „Wartend" — keine
  Hostaktionen, kein Prompt, kein CLI-Start.
- **Konfiguration:** Globaler Schalter wird einmal pro Zyklus gelesen (opt-in `false`);
  Plugin-Schlüssel und Kommentar-Schalter werden bei der Einreihung gelesen (Letzterer sichtbar im
  `Reason`-Text; fachliche Auswertung Schritt 6). Getter-Defaults und Trim-Verhalten durch
  Unit-Tests belegt.
- **Migration:** Drei Spalten wie geplant (`Zweck` TEXT 100 nullable auf Aufgaben+ScmIncomingEvents,
  `IssueAufbereitungAktiv` INTEGER NOT NULL DEFAULT 0); Down-Migration sauber; Snapshot aktualisiert.
- **Konventionen:** CS1591-deutsche XML-Docs auf allen neuen öffentlichen Membern;
  `dotnet format --verify-no-changes` sauber; Scoped-DI in App + Fixture identisch;
  `ErstellungsDatum`/`ReceivedUtc`/`Zeitstempel` über `TimeProvider`/`DateTimeOffset.UtcNow`
  konsistent zum Bestandscode.

## Bewertete Kanten (dokumentierte Restrisiken, keine Befunde)

- **Provider-seitiger Remote-Node-Wechsel** derselben Beschreibung würde einen neuen ContentKey und
  damit eine fälschliche „Erstbeobachtung" erzeugen — bei GitHub sind Node-IDs unveränderlich;
  zusätzlich begrenzt der `CreatedAt`-Vergleich gegen die Baseline das Risiko auf echt neue Issues.
- **Manueller Start der Aufbereitungs-Aufgabe** ist nicht gesperrt (Status `Neu`) — gewollt für
  Schritt 6, der die Aufgabe als Ausführungsträger übernimmt; ein vorzeitiger manueller Start wäre
  ein regulärer Entwicklungsfluss, kein Datenfehler.
- **Inbox-Modus (`SeparatesKonto` + `IScmNotificationSource`):** ohne Enumeration keine Erkennung —
  die Anforderung benennt ausdrücklich den Enumerationskanal (Annahme A3).
- **`ScmTaskCompletionMonitoringService`:** Aufbereitungs-Aufgaben wären theoretisch Kandidaten,
  aber `CheckTaskAsync` bricht bei nicht abgeschlossenen Ereignissen ab — das geparkte interne
  Ereignis verhindert einen vorzeitigen Abschluss; nach Verarbeitung (Schritt 6) ist ein Abschluss
  bei geschlossenem Issue sachgerecht.
- **Alias-Auswahl bei mehreren lokalen Repositories:** deterministisch (RepositoryUrl, dann Id) —
  deckt sich mit `ScmEventTaskResolver.ChooseRepository`.

## Befunde

Keine.
