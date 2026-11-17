# Bestandsaufnahme: „Issue erstellt"-Erkennung und interner Aufbereitungsauftrag (Schritt 5, #412 Teil 1)

**Stand:** Branch `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung-schritt-5-issue-erkennung`, Basis `task/issue-410-c69c8e7f6eb14c4cbe407bdbdc9e1cd6-ausfuehrungsbegrenzung`. Ergänzend zu `docs/projects/.../inventory.md` (Lücken L6, L7, L9, L10) mit den konkreten Befunden auf dem aktuellen Stand (Schritte 1–4 bereits gemergt).

## 1. Beobachtungspfad (`ScmEventMonitoringService.RunOnceAsync`, Z. 30-234)

- Z. 47-49: Lädt Aufgaben mit aktivem `GitRepository` (inkl. `IssueReferenz`/`PullRequests`) und alle aktiven Repositories (`db.GitRepositories.Where(Aktiv)`).
- Z. 53-60: Baut `linked` — Aufgaben mit `IssueReferenz.IssueNummer > 0` werden als verknüpfte Issue-Quellen eingetragen. **Eine Aufbereitungs-Aufgabe mit IssueReferenz würde hier ihre Issue-Nummer als eigenen verknüpften Kanal eintragen und Trigger-Ereignisse auf sich ziehen — muss per Zweck-Markierung ausgenommen werden (dritter Isolationspunkt neben den beiden Resolver-Abfragen).**
- Z. 76-78: `aliasGroups` = lokale Repositories gruppiert nach `RepositoryUrl`; Auflösung via `source.GetEventRepositoryAsync` → kanonisches `ScmEventRepository`; `localRepositories` (Z. 99-102) mappt `ScmEventRepository → List<GitRepository>` — Träger der lokalen Repository-Flag.
- Z. 130-142: Im Modus `SeparatesKonto` mit `IScmNotificationSource` ersetzt die Inbox die repository-weite Enumeration — **kein Enumerationskanal, daher dort keine Issue-Erkennung** (Anforderung benennt ausdrücklich die repository-weite Enumeration).
- Z. 143-157: Sonst `ScmCheckpointSupport.EnumerateRepositoryAsync` — enumeriert Referenzen repository-weit mit eigenem Checkpoint `enumeration/{provider}/{host}/{repoId}` (Watermark wird je Zyklus auf `now` fortgeschrieben, Z. 105 — kann daher selbst nicht als „Beobachtungsbeginn" dienen).
- Z. 159-204: Pro Referenz-Gruppe und Content-Kind (`Issue → Description + IssueComment`) wird `source.ReadEventsAsync(request)` gelesen und `ScmEventStore.AcceptBatchAsync` aufgerufen — **der Ansatzpunkt der Erkennung**: Beim `Description`-Batch einer Issue-Referenz ist der Erstbeobachtungs-/Baseline-Zustand prüfbar, bevor `AcceptBatchAsync` den `ScmContentSnapshot` anlegt (Reihenfolge: Erkennung vor Aufnahme).
- Z. 233: Zyklus endet mit `ScmEventOrchestrator.DispatchDueEventsAsync`.

## 2. Aufnahme und Dedup (`ScmEventStore`, `ScmIncomingEvent`, Checkpoints/Snapshots)

- `AcceptBatchAsync` (Z. 50-129): Dedup über `ScmContentSnapshot` (ContentKey + BodyHash) und Unique-Index `ScmIncomingEvent.EventKey`; `rejection` aus `ScmEventValidationService.GetRejection` verhindert Ereignisanlage; Checkpoint `sourceKey` bekommt `WatermarkUtc = batch.Request.WatermarkUtc`.
- `ScmEventObservation` (`ScmEvents.cs` Z. 30-52): `CreatedAt` (Remote-Erstellzeitpunkt), `ObservedAt`, `ContentKey` (`{provider}/{host}/{repoId}/{Kind}/{Number}/{ContentKind}/{RemoteNodeId}`), `EventKey` (`ContentKey + Revision + BodyHash`). **Ein neu angelegtes Issue materialisiert sich als neue `Description`-Beobachtung mit neuem ContentKey.**
- `ScmObservationCheckpoint` (PK `SourceKey`): frei wählbarer Schlüssel-Namensraum — ein dedizierter Baseline-Checkpoint pro Ereignis-Repository (`issueaufbereitung/{provider}/{host}/{repoId}`) kann den „Beobachtungsbeginn" dauerhaft festhalten (einmalig setzen, nie fortschreiben).
- `ScmIncomingEvent`: `AufgabeId` Pflicht; `Status` Default `"Aufgenommen"`; `NextAttemptUtc == long.MaxValue` wird von der Auswahl in `DispatchDueEventsAsync` (Orchestrator Z. 42-44: `NextAttemptUtc == null || <= now`) nie selektiert — geparkt, aber in der Ereignisübersicht sichtbar (Filter „Wartend" = `Status != Verarbeitet/Abgebrochen`).
- Selbst geschriebene Remote-Texte: `GetRejection` (ValidationService Z. 65) lehnt Bodies mit `<!-- softwareschmiede:` ab („Eigene Antwort") — für die Aufbereitungs-Erkennung greift zusätzlich die Snapshot-Regel (keine Erstbeobachtung → kein Auslöser).

## 3. Aufgabenauflösung und -anlage (`ScmEventTaskResolver`, `AufgabeService`, `IssueReferenz`)

- `ResolveIssueAsync` (Z. 52-74): Z. 63-68 lädt Aufgaben-IDs mit `GitRepositoryId ∈ repositories && IssueReferenz.IssueNummer == reference.Number` — **Auflösung über IssueNummer, nicht SourceKey**; >1 Treffer → `Ambiguous`, 1 → `Assigned`, 0 → Anlage via `CreateFromIssueEventAsync` mit kanonischem `IssueSourceKey` (Z. 172-176: `{provider}/{host}/{repoId}/Issue/{number}`). **Zweck-Filter nötig.**
- `FindActiveContextTaskIdsAsync` (Z. 125-144): PR-Kontextabgleich über Branch-Name bzw. Closing-Issues in aktiven Aufgaben — **Zweck-Filter nötig** (Aufbereitungs-Aufgabe trägt IssueNummer und dürfte sonst PR-Kontext liefern).
- `PullRequestTaskIdsAsync` (Z. 111-115): auf `PullRequestReferenzen` — Aufbereitungs-Aufgaben besitzen keine, kein Filter nötig.
- `CreateFromIssueEventAsync` (`AufgabeService` Z. 273-337): legt `Aufgabe` (Status Neu, AusfuehrungsStatus NichtGestartet, Titel/Body aus `Issue`-VO) + `IssueReferenz` mit `SourceKey` an; Dedup über Unique-Index inkl. Anlagerennen-Behandlung (`DbUpdateException` → bestehende Aufgabe zurück). **Erweiterbar um `zweck`/`kiPluginPrefix`.**
- `IssueReferenz.SourceKey` Unique-Index (`DbContext` Z. 267): Ein zweiter `SourceKey`-Namensraum `…/IssueAufbereitung/{number}` kollidiert nicht mit dem kanonischen `…/Issue/{number}` und sichert die Einmaligkeit des Aufbereitungsvorgangs je Issue.
- `ScmTaskCompletionMonitoringService` (Z. 52-63, 84-86): Aufbereitungs-Aufgaben wären prinzipiell Kandidaten (IssueReferenz + Ereignis), aber `CheckTaskAsync` bricht bei jedem nicht abgeschlossenen Ereignis ab — das geparkte interne Ereignis verhindert einen vorzeitigen Abschluss; nach dessen Verarbeitung (Schritt 6) ist ein Abschluss bei geschlossenem Issue sachgerecht. **Keine Änderung nötig.**

## 4. Dispatch-Pfad (`ScmEventOrchestrator`)

- `DispatchDueEventsAsync` (Z. 32-64): Selektion `Status != "Verarbeitet" && (NextAttemptUtc null || <= now)` oder fällige Outbox-Aktionen → ein Ereignis mit `NextAttemptUtc = long.MaxValue` und ohne Aktionen bleibt eingereiht, aber nie fällig.
- `DispatchCoreAsync` (Z. 118-344): Trigger-freie interne Ereignisse müssten ohne Gegenmaßnahme den generischen Zustellpfad durchlaufen (Re-Validierung lässt „Kein Trigger" durch, Z. 199-200 → CLI-Start mit rohem Issue-Body als Prompt). **Zweck-Guard nötig:** markierte interne Ereignisse warten ehrlich (`WaitAsync`, `NextAttemptUtc = long.MaxValue`), bis Schritt 6 den Aufbereitungs-Ausführungspfad implementiert — verhindert auch unbeabsichtigte Zustellung über `RetryAsync`/„Erneut prüfen" (Z. 356-366 setzt `NextAttemptUtc = null`).
- `WaitAsync` (Z. 615-620): Status `"Wartend"` + `Reason` — ehrliches Wartemodell ohne Remote-Kommentar (`WaitWithCommentAsync` würde einen Kommentar posten — für interne Ereignisse ungeeignet).

## 5. Konfiguration (`AppEinstellungService`, `GitRepository`)

- `AppEinstellungService` (Z. 15-118 Schlüssel-Konstanten, Z. 133-189 generische Getter/Setter, typisierte Getter wie `GetScmMonitoringEnabledAsync` Z. 199-200 mit `?? true`-Default): Platz für `scm.issueaufbereitung.enabled` (Default `false`), `scm.issueaufbereitung.plugin` (PluginPrefix, leer = Fallback auf globales Standard-Plugin — Lücke L7) und `scm.issueaufbereitung.comment.enabled` (Kommentar-Schalter).
- `GitRepository` (Entity, 38 Z.): neues bool-Flag für die Repository-Freischaltung; Migration via `dotnet ef migrations add` (Design-Time-Factory vorhanden, `dotnet ef` 10.0.10 installiert; Migrations-Konvention `yyyyMMddHHmmss_Add*.cs` + Designer + ModelSnapshot).
- `PluginSelectionService.ResolveEnabledDevelopmentAutomationPluginWithProjectScopeAsync` (aktivierungsbewusste Kette aus Schritt 1) bleibt die Auflösung beim späteren Dispatch; `Aufgabe.KiPluginPrefix` = Funktions-Plugin-Zuordnung.

## 6. Ereignisübersicht (`ScmEventQueueViewModel`)

- Listet alle `ScmIncomingEvents` (Filter „Wartend"/„Verarbeitet"/„Abgebrochen", Z. 170-173); `ScmEventQueueItem.From` (Z. 393-406) projiziert Kind/Status/Reason — ein `Zweck`-Feld am Ereignis ermöglicht eine eigene Kennzeichnung („Issue-Aufbereitung") in der Kind-Spalte ohne weiteren UI-Aufwand.

## 7. Test-Infrastruktur

- `ScmDispatchFixture` (Tests-Projekt, Namespace `Softwareschmiede.TestSupport`): echte SQLite-DB + echte Services; `Git`-Mock `As<IScmEventSource>()` (GetEventRepositoryAsync → `fixture.Repository`, ReadEventsAsync/EnumerateReferencesAsync/ReadIssueAsync skriptierbar), `fixture.Monitor` (Singleton `ScmEventMonitoringService`), `fixture.SetSettingAsync`, `fixture.DispatchAsync`, `fixture.ConfigureIssue` — Deckt den kompletten Beobachtungs→Erkennungs→Einreihungs-Pfad ohne echten SCM-Call ab.
- `ScmEventMonitoringServiceTests_RepoWide` (IntegrationTests): Muster für Enumerations-/Baseline-/Dedup-Tests (`AddRepositoryAsync`, `AddTaskWithIssueAsync`, `ConfigureEnumeration`, `ConfigureObservations`); Assertions über `db.Aufgaben`, `db.ScmIncomingEvents`, `db.ScmContentSnapshots`, `db.ScmObservationCheckpoints`, `fixture.Prompts`/`Sessions`/`Responses`.
- `GitRepository` benötigt als Pflichtfelder nur `ProjektId`/`RepositoryUrl`/`PluginTyp`/`RepositoryName`; neue bool-Spalte Default `false` — bestehende Test-Fixtures bleiben kompatibel.
- Reguläre Lane: `dotnet test src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj --filter "Category!=OsInterface"`; Integration-Projekt separat; `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` in der Sandbox; `dotnet test` nie im Hintergrund.

## 8. Lücken/Konsequenzen für Schritt 5

| Befund | Konsequenz |
|--------|-----------|
| Kein „Issue erstellt"-Signal; `Description`-Beobachtung ohne Trigger wird verworfen (L6) | Erkennung im Monitoring vor `AcceptBatchAsync`: Erstbeobachtung (kein `ScmContentSnapshot` zum ContentKey) + `CreatedAt` nach Baseline → Auslöser |
| Kein „Beobachtungsbeginn" persistiert (Enumerations-Watermark wird fortgeschrieben) | Dedizierter Baseline-Checkpoint `issueaufbereitung/{provider}/{host}/{repoId}` in `ScmObservationCheckpoints` — einmalig setzen = Erstbeobachtung ohne Auslösung |
| `AufgabeId` ist Pflichtfeld (L10) | Dedizierte Aufbereitungs-Aufgabe mit `IssueReferenz` (eigener SourceKey-Namensraum) + internem `ScmIncomingEvent` (deduplizierender EventKey, geparkt via `NextAttemptUtc = long.MaxValue`, Zweck-markiert) |
| Issue-Ereignisauflösung nutzt `IssueNummer` (nicht SourceKey) | Zweck-Markierung `Aufgabe.Zweck` + Filter in `ResolveIssueAsync`, `FindActiveContextTaskIdsAsync` **und** im `linked`-Aufbau des Monitoring-Dienstes (Z. 53-60) |
| Generischer Zustellpfad würde internes Ereignis als Prompt zustellen | Zweck-Guard in `DispatchCoreAsync`: ehrlich wartend (`Wartend` + Reason), `NextAttemptUtc` erneut geparkt |
| Fehlende Funktions-Plugin-Einstellung (L7) | `scm.issueaufbereitung.plugin` als Prefix-Schlüssel; `Aufgabe.KiPluginPrefix` bei Einreihung setzen; Fallback regelt Schritt-1-Auflösung beim späteren Dispatch |
| Fehlende Funktions-/Kommentar-/Repository-Schalter | `scm.issueaufbereitung.enabled`, `scm.issueaufbereitung.comment.enabled` (AppEinstellung-Keys + typsierte Getter), `GitRepository.IssueAufbereitungAktiv` (EF-Migration) |
| SCM-Ereignisse nur GitHub (L9) | Erkennung erbt die Einschränkung unverändert (nur Plugins mit `IScmEventSource` enumerieren) |
