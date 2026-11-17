# Abnahmeprüfung – Entwicklungsschritt 5

**Geprüfter Stand:** Branch `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-5-issue-erkennung`, HEAD `3cf087c5` (Diff-Basis `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`). Prüfung durch vollständiges Lesen des Diffs und aller betroffenen Bestandspfade; die Testergebnisse in `test-results.md` wurden nicht eigenständig nachgefahren (siehe Hinweise).

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Prüfnachweise je Kriterium

**Kriterium 1 – genau ein Aufbereitungsvorgang:** `ScmIssueAufbereitungsService.ErkennenUndEinreihenAsync` (`src/Softwareschmiede/Application/Services/ScmIssueAufbereitungsService.cs`, Z. 62-144) legt pro Erkennung genau eine `Zweck`-markierte Aufgabe (`AufgabeService.CreateFromIssueEventAsync`, `src/Softwareschmiede/Application/Services/AufgabeService.cs` Z. 282-350, mit `Zweck`+`KiPluginPrefix`, `GitRepositoryId` und `IssueReferenz` im eigenen SourceKey-Namensraum `…/IssueAufbereitung/{n}`) plus ein internes `ScmIncomingEvent` (`Zweck`, `AufgabeId`, `EventKey = {ContentKey}/IssueAufbereitung`, `NextAttemptUtc = long.MaxValue`, SystemMeldung im Protokoll) an. Belegt durch `Monitor_NewIssueAfterBaseline_CreatesPreparationTaskAndInternalEvent`.

**(a) Baseline wasserdicht:** „Beobachtungsbeginn" ist der persistierte Checkpoint `issueaufbereitung/{provider}/{host}/{repoId}` (eigener `ScmObservationCheckpoint`-Namensraum, einmalig gesetzt, nie fortgeschrieben — `ScmIssueAufbereitungsService.cs` Z. 88-99). Er wird beim ersten Issue-Description-Batch unter Freischaltung gesetzt; der Vergleich läuft gegen `observation.CreatedAt`, das beim GitHub-Plugin die `createdAt`-Zeit des Issue-Knotens trägt (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs` Z. 235) — also der Issue-Erstellzeitpunkt, nicht der Änderungszeitpunkt. Ein Alt-Issue mit später Description-Beobachtung (Edit) scheidet doppelt aus: `CreatedAt <= baselineUtc` (Z. 104) und bestehender `ScmContentSnapshot` zum ContentKey (Z. 107). Auch ein vor der Baseline angelegtes, erst später entdecktes Issue ist abgedeckt (`Monitor_IssueCreatedBeforeBaseline_CreatesNothing`). Semantik ist in `docs/help/plugins/github-ereignisse/business-rules.md` dokumentiert.

**(b) Geparkter Auftrag:** `NextAttemptUtc = long.MaxValue` entzieht das Ereignis der `DispatchDueEventsAsync`-Auswahl (`ScmEventOrchestrator.cs` Z. 40-47) dauerhaft; der Guard in `DispatchCoreAsync` (Z. 133-138) fängt explizite Dispatches/Retries ab, parkt erneut und meldet ehrlich `Wartend` („Die Ausführung der Issue-Aufbereitung ist noch nicht verfügbar.") ohne Prompt/CLI/Hostaktion — belegt durch `Monitor_InternalEvent_ExplicitDispatch_WaitsWithoutDelivery`. Das passt zur Anforderung: Schritt 5 soll den Auftrag nur einreihen; der Projektplan sieht für Schritt 6 den eigenen Ausführungspfad vor, der das Ereignis dann zweckbezogen aufnimmt.

**(c) Resolver-Ausschluss:** `a.Zweck == null` steht in `ResolveIssueAsync` (`ScmEventTaskResolver.cs` Z. 65) UND `FindActiveContextTaskIdsAsync` (Z. 135); zusätzlich filtert der `linked`-Aufbau der verknüpften Beobachtungsquellen (`ScmEventMonitoringService.cs` Z. 50-51) — ohne diesen Filter würden spätere Trigger-Ereignisse demselben Issue der Aufbereitungs-Aufgabe zugeordnet. Alle übrigen `IssueNummer`-Konsumenten wurden geprüft: `ScmTaskCompletionMonitoringService` (s. u.), `EntwicklungsprozessService`/`GitOrchestrationService`/`PullRequestBodyBuilder` wirken nur auf eine bereits in Ausführung befindliche Aufgabe, nicht auf die Ereigniszuordnung. `PullRequestTaskIdsAsync` ist per Referenzart immun.

**(d) Schleifensicherheit:** dreistufig — (1) das Plugin verwirft viewer-autorisierte Beobachtungen mit `<!-- softwareschmiede:`-Marker bereits an der Quelle (`GitHubPlugin.Events.cs` Z. 246); `IssueBodyUpdate` schreibt genau diesen Marker (`ScmEvents.cs` Z. 76), (2) selbst ohne Marker existiert nach der Erstbeobachtung ein `ScmContentSnapshot` zum ContentKey → Skip (Z. 107), (3) `EventKey`-Unique-Index dedupliziert zusätzlich. Belegt durch `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop`.

**(e) Aufgaben-Maschinerie:** `GitRepositoryId` gesetzt (Z. 78 / `AufgabeService` Z. 311). `ScmTaskCompletionMonitoringService.CheckTaskAsync` bricht bei nicht abgeschlossenen Ereignissen der Aufgabe ab (`ScmTaskCompletionMonitoringService.cs` Z. 84-86) — das geparkte interne Ereignis verhindert einen vorzeitigen Abschluss; nach Verarbeitung in Schritt 6 wäre ein Abschluss bei geschlossenem Issue sachgerecht. Die Aufgabe erscheint als reguläre Aufgabe (Status `Neu`) in der Aufgaben-UI — gewollt als Ausführungsträger für Schritt 6.

**(f) EventKey-Dedup repo-übergreifend:** `AufbereitungsEventKey = {ContentKey}/IssueAufbereitung`; `ContentKey` enthält `RepositoryId` (node_id) → gleiche Issue-Nummer in verschiedenen Repositories erzeugt verschiedene Keys. Erneute Beobachtung → identischer Key → AnyAsync-Vorabprüfung (Z. 110) + Unique-Index mit `DbUpdateException`-Rennbehandlung (Z. 131-137).

**(g) EF-Migration `20261117231445_AddIssueAufbereitung`:** Up fügt `Zweck` (TEXT, 100, nullable) auf `Aufgaben` und `ScmIncomingEvents` sowie `IssueAufbereitungAktiv` (INTEGER, NOT NULL, DEFAULT 0) auf `GitRepositories` hinzu; Down entfernt alle drei spiegelbildlich; ModelSnapshot aktualisiert. Keine Datenverluste (additive Spalten, bestehende Zeilen erhalten NULL/false).

**Kriterien 2/3/5:** Abgedeckt durch `Monitor_FirstObservation_CreatesBaselineOnly`, `…_GlobalSwitchDisabled_CreatesNothing` (auch keine Baseline als Nebeneffekt), `…_RepositoryFlagDisabled_CreatesNothing` (Baseline unverbraucht), `…_RepositoryWithoutEventCapablePlugin_CreatesNothing`, `…_RepeatedPolls_CreateNoSecondPreparation`, `…_TriggerOnPreparedIssue_ResolvesToCanonicalTask` (kein Ambiguous, kanonische `…/Issue/9`-Aufgabe, Aufbereitungsaufgabe unberührt). Das interne Ereignis wird als Entity direkt angelegt — `GetRejection` läuft niemals darauf; der externe Pfad `AcceptBatchAsync` ist unverändert.

**Kriterium 6:** Migration s. o.; Integrations-Testsuite `ScmEventMonitoringServiceTests_IssueAufbereitung` (10 Tests: Baseline, Happy Path, Dedup, drei Ausschlussfälle, Altbestand, Schleife, Isolation, Parkverhalten) plus `AppEinstellungServiceTests_IssueAufbereitung` (7 Tests: Defaults/Roundtrips der drei persistierten Schlüssel `scm.issueaufbereitung.enabled` opt-in `false`, `scm.issueaufbereitung.plugin` leer→`null`/getrimmt, `scm.issueaufbereitung.comment.enabled` Default `true`).

### Einschränkungen / Randnotizen (keine Abweichungen)

- **Testläufe nicht eigenständig wiederholt:** Die in `test-results.md` dokumentierten Lanes (Integration 346/346, regulär 2669/2672 mit 3 Umgebungs-Skips, Format-Check sauber) wurden hier nicht nachgefahren; die Verifikation erfolgte durch Code-Lektüre der Implementierung und aller 17 Tests.
- **Baseline-Definition konsequent:** Ein Repository ohne jedes Issue legt die Baseline mit dem allerersten Issue-Description-Batch fest — dieses Issue gilt damit als Altbestand (`CreatedAt` vor dem eben gesetzten Beobachtungsbeginn). Deckt sich mit „Erstbeobachtung = Baseline ohne Auslösung" und der dokumentierten Semantik.
- **`SeparatesKonto`-Modus:** Die Erkennung ist bewusst auf den Enumerationskanal beschränkt (`!inboxMode`, `ScmEventMonitoringService.cs` Z. 201) und in der Hilfe dokumentiert — die Anforderung benennt ausdrücklich die repository-weite Enumeration.
- **Robustheitsasymmetrie (minimal):** `ErkennenUndEinreihenAsync` prüft — anders als `ScmEventTaskResolver` — nicht `issue.Nummer == reference.Number`; ein theoretisch falsches `ReadIssueAsync`-Ergebnis würde eine `IssueReferenz` mit abweichender `IssueNummer` unter korrektem SourceKey anlegen.
- **`DbUpdateException`-Catch** (Z. 131-137) behandelt jede Update-Exception als Anlagerennen — ein anders gelagerter Constraint-Verstoß würde still in den nächsten Zyklus wiederholt (Backoff greift nicht, kein Warning).
- **Zwischenzustand Queue-UI:** „Öffnen" eines geparkten internen Ereignisses führt durch den Plugin-Auswahldialog und setzt die Aufgabe auf `Gestartet`, parkt dann aber erneut — kosmetisch, bis Schritt 6 den Aufbereitungsauftrag selbst konsumiert; ebenso ist ein manueller Start der `Neu`-Aufgabe nicht gesperrt (im Review als gewollt dokumentiert).
- **`issue.IstGeschlossen` wird nicht geprüft:** Ein zwischen Anlage und Beobachtung bereits geschlossenes neues Issue erhält trotzdem einen Aufbereitungsvorgang — fachlich vertretbar (es wurde erstellt), bei Bedarf in Schritt 6 einzuordnen.
- Baseline-Anlagerennen über Instanzen würde als `DbUpdateException` in den bestehenden SourceKey-Backoff laufen und sich selbst heilen.
