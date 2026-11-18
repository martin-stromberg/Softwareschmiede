# Umsetzungsplan: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (Schritt 5, #412 Teil 1)

Umgesetzt wird Projektplan-Schritt 5 (Entscheidungen 3+4 inkl. Isolationsabsatz): Erkennung neu angelegter Issues am repository-weiten Enumerationskanal, Baseline ohne Retro-Scan, dedizierte markierte Aufbereitungs-Aufgabe + internes, trigger-freies `ScmIncomingEvent`, Isolation von der kanonischen Issue-Ereignisauflösung, persistierte Konfigurationsschlüssel (UI folgt in Schritt 6).

## Umsetzungsidee

### 1. Persistenz: Entity-Erweiterungen + EF-Migration `AddIssueAufbereitung`

- `GitRepository.IssueAufbereitungAktiv` (`bool`, Default `false`, `HasDefaultValue(false)`): Repository-Freischaltung der Aufbereitungs-Überwachung.
- `Aufgabe.Zweck` (`string?`, `HasMaxLength(100)`, `null` = reguläre Arbeitsaufgabe): Zweck-Markierung; einziger Wert vorerst `"IssueAufbereitung"`.
- `ScmIncomingEvent.Zweck` (`string?`, `HasMaxLength(100)`): Zweck-Kennzeichnung des internen Ereignisses, gleicher Token.
- Konstante `ScmIssueAufbereitungsService.Zweck = "IssueAufbereitung"` als einzige Quelle des Tokens (Aufgabe + Ereignis).
- Migration per `dotnet ef migrations add AddIssueAufbereitung -p src/Softwareschmiede` (Design-Time-Factory vorhanden).

### 2. Einstellungsschlüssel (`AppEinstellungService`)

- `ScmIssueAufbereitungEnabledKey = "scm.issueaufbereitung.enabled"` + `GetScmIssueAufbereitungEnabledAsync` (Default `false` — opt-in).
- `ScmIssueAufbereitungPluginKey = "scm.issueaufbereitung.plugin"` + `GetScmIssueAufbereitungPluginPrefixAsync` (getrimmt, leer → `null` = Fallback auf globales Standard-Plugin durch die Schritt-1-Auflösung beim späteren Dispatch).
- `ScmIssueAufbereitungKommentarEnabledKey = "scm.issueaufbereitung.comment.enabled"` + `GetScmIssueAufbereitungKommentarEnabledAsync` (Default **`true`** — *Annahme A1*: bei opt-in-Funktion ist der dokumentierende Kommentar Teil des erwarteten Verhaltens; Schritt 6 kann den Default bei der UI-Umsetzung noch einmal bewerten).
- Kein UI (Schritt 6); die Schlüssel werden bereits jetzt von Erkennung/Einreihung gelesen.

### 3. Neuer Service `ScmIssueAufbereitungsService` (`Application/Services`)

Scoped-Service (DI: `AddScoped` in `App.xaml.cs` + `ScmDispatchFixture`) mit:

```csharp
public const string Zweck = "IssueAufbereitung";

// Baseline-Checkpoint je kanonischem Ereignis-Repository (eigener Checkpoint-Namensraum).
internal static string BaselineKey(ScmEventRepository repository)
    => string.Join('/', new[] { "issueaufbereitung", repository.ProviderPrefix.ToLowerInvariant(),
        repository.Host.ToLowerInvariant(), repository.RepositoryId }.Select(Uri.EscapeDataString));

// Eigener SourceKey-Namensraum (Isolationsentscheidung): {provider}/{host}/{repoId}/IssueAufbereitung/{number}
internal static string AufbereitungsSourceKey(ScmEventReference reference) { /* wie IssueSourceKey, Segment "IssueAufbereitung" */ }

/// <summary>Wertet einen gelesenen Issue-Description-Batch auf „Issue erstellt" aus und legt
/// ggf. Aufbereitungs-Aufgabe + internes Ereignis an. Muss vor AcceptBatchAsync laufen
/// (Erstbeobachtung = noch kein ScmContentSnapshot zum ContentKey).</summary>
public async Task ErkennenUndEinreihenAsync(IScmEventSource source, ScmEventBatch batch,
    IReadOnlyList<GitRepository> repositories, CancellationToken ct)
```

Ablauf `ErkennenUndEinreihenAsync`:

1. `flagged = repositories.Where(x => x.IssueAufbereitungAktiv)` — leer → return (nicht freigeschaltetes Repository).
2. Baseline: `ScmObservationCheckpoints.FindAsync([BaselineKey(repo)])`; fehlt → Checkpoint mit `WatermarkUtc = now` anlegen, speichern, **return** (Erstbeobachtung der Quelle = Baseline ohne Auslösung; alle Beobachtungen haben zwangsläufig `CreatedAt <= now`).
3. Je Observation: `CreatedAt.ToUnixTimeMilliseconds() <= baseline.WatermarkUtc` → skip (Altbestand); `ScmContentSnapshots.AnyAsync(ContentKey)` → skip (keine Erstbeobachtung — Schleifenschutz gegen spätere/selbst verursachte Beschreibungsänderungen); `ScmIncomingEvents.AnyAsync(EventKey)` → skip (Dedup), wobei `EventKey = observation.ContentKey + "/" + Zweck` — deterministisch je Issue, dedupliziert über den Unique-Index unabhängig von Revision/BodyHash.
4. `issue = await source.ReadIssueAsync(reference)` (Anlagedaten wie der Trigger-Pfad); `null` → Protokoll/Log + skip.
5. `pluginPrefix = await einstellungen.GetScmIssueAufbereitungPluginPrefixAsync(ct)`; `kommentar = await einstellungen.GetScmIssueAufbereitungKommentarEnabledAsync(ct)` — beide Schlüssel werden bei der Einreihung gelesen; `kommentar` fließt in den `Reason`-Text des Ereignisses ein (sichtbare Konfigurationswirkung, fachliche Auswertung in Schritt 6).
6. `task = await aufgaben.CreateFromIssueEventAsync(repository.ProjektId, issue, repository.Id, AufbereitungsSourceKey(reference), ct, zweck: Zweck, kiPluginPrefix: pluginPrefix)` — `repository` = erstes freigeschaltetes (`OrderBy(RepositoryUrl).ThenBy(Id)` wie `ChooseRepository`); die Methode dedupliziert über den SourceKey-Unique-Index inkl. Anlagerennen.
7. Internes `ScmIncomingEvent`: `AufgabeId = task.Id`, `EventKey` (s. o.), `ObservationJson` = serialisierte Trigger-Beobachtung (trägt Referenz + Issue-Body für Schritt 6), `Zweck = Zweck`, `Status = "Aufgenommen"`, `NextAttemptUtc = long.MaxValue` (eingereiht, aber nicht zustellbar — *Annahme A2*: ein Dispatch über den generischen Pfad würde den rohen Issue-Body als Prompt zustellen; die Aufbereitungs-Ausführung folgt in Schritt 6), `Reason = "Interner Aufbereitungsauftrag für Issue #{n}."` (+ Kommentar-Hinweis). `Protokolleintrag` (SystemMeldung) an der Aufgabe. `SaveChanges` mit `DbUpdateException`-Toleranz (EventKey-Rennen → bereits eingereiht). `notifier.Notify()`.

### 4. Andockpunkt im Monitoring (`ScmEventMonitoringService.RunOnceAsync`)

- `var aufbereitungAktiv = await einstellungen.GetScmIssueAufbereitungEnabledAsync(ct)` einmal pro Zyklus (Z. ~45, neben `triggerKeyword` u. a.).
- `linked`-Aufbau (Z. 47): Query um `x.Zweck == null` erweitern — markierte Aufgaben tragen ihre Issue-Nummer **nicht** als verknüpfte Quelle ein (dritter Isolationspunkt; sonst würde die Aufbereitungs-Aufgabe spätere Trigger-Ereignisse auf sich ziehen).
- Im `kind`-Loop (Z. 167-204), innerhalb des `try`, **nach** `ReadEventsAsync` und **vor** `AcceptBatchAsync` (Snapshots dürfen noch nicht existieren):

```csharp
if (!inboxMode && aufbereitungAktiv && reference.Kind == ScmReferenceKind.Issue
    && kind == ScmContentKind.Description && batch.Observations.Count > 0
    && localRepositories.TryGetValue(reference.Repository, out var aufbereitungRepos))
    await batchScope.ServiceProvider.GetRequiredService<ScmIssueAufbereitungsService>()
        .ErkennenUndEinreihenAsync(source, reference, batch, aufbereitungRepos, ct);
```

- Fehler laufen über den bestehenden `catch → RecordFailureAsync(sourceKey)`-Backoff; die Inbox-Aufnahme bleibt transaktional unverändert (erneuter Versuch dedupliziert über SourceKey/EventKey — idempotent).
- `inboxMode`-Ausschluss: Die Anforderung benennt die repository-weite Enumeration als Kanal; im SeparatesKonto-Modus existiert sie nicht (*Annahme A3*).

### 5. Resolver-Isolation (`ScmEventTaskResolver`)

- `ResolveIssueAsync` Z. 63-66: `&& a.Zweck == null` — markierte Aufgaben liefern weder Treffer noch `Ambiguous`-Beitrag; ohne reguläre Aufgabe wird die kanonische Stamm-Aufgabe angelegt (SourceKey-Kollision entfällt durch eigenen Namensraum).
- `FindActiveContextTaskIdsAsync` Z. 132-139: `&& a.Zweck == null` — Aufbereitungs-Aufgabe liefert keinen PR-Kontext.
- `PullRequestTaskIdsAsync`: unverändert (Aufbereitungs-Aufgaben besitzen keine `PullRequestReferenz`).

### 6. `AufgabeService.CreateFromIssueEventAsync` — optionale Parameter

Signatur um `string? zweck = null, string? kiPluginPrefix = null` erweitern; beide in den `new Aufgabe`-Initializer (`Zweck`, `KiPluginPrefix`). Rückgabepfad bei vorhandener/paralleler Anlage unverändert (Markierung wurde bei deren Anlage bereits persistiert).

### 7. Orchestrator-Guard (`ScmEventOrchestrator.DispatchCoreAsync`)

Nach `Abgebrochen`-Prüfung (Z. ~130): bei `item.Zweck == ScmIssueAufbereitungsService.Zweck` → `item.NextAttemptUtc = long.MaxValue` + `WaitAsync(item, "Die Ausführung der Issue-Aufbereitung ist noch nicht verfügbar.", ct)` + return. Verhindert unbeabsichtigte Zustellung des rohen Issue-Bodys über `RetryAsync`/manuelle Prüfung, bis Schritt 6 den Ausführungspfad schafft. Kein Remote-Kommentar (`WaitAsync`, nicht `WaitWithCommentAsync`).

### 8. Ereignisübersicht (`ScmEventQueueViewModel`/`ScmEventQueueItem.From`)

`Kind`-Anzeige: `item.Zweck == ScmIssueAufbereitungsService.Zweck ? "Issue-Aufbereitung" : <bisherige ContentKind-Mapping>` — interner Auftrag in der Übersicht erkennbar; alle übrigen Mechaniken (Status, Reason, Abbrechen) unverändert.

## Akzeptanzszenarien

1. **Happy Path (Integration):** Freigeschaltetes aktives Repository, Baseline existiert → Mock-Enumeration meldet neue Issue-Referenz, `ReadEventsAsync` liefert `Description`-Beobachtung mit `CreatedAt` nach Baseline → `Monitor.RunOnceAsync` erzeugt genau eine Aufgabe mit `Zweck == "IssueAufbereitung"`, `IssueReferenz.SourceKey` `…/IssueAufbereitung/{n}` + genau ein Ereignis mit `Zweck`, `AufgabeId`, deduplizierendem `EventKey`, `NextAttemptUtc = long.MaxValue`. **Nachweis:** Integrationstest 2.
2. **Baseline ohne Retro-Scan:** Erste Beobachtung (Repository oder Funktion neu) legt nur den Baseline-Checkpoint an; bestehende Issues erzeugen nichts; ein spät entdecktes Issue mit `CreatedAt` vor Baseline ebenfalls nichts. **Nachweis:** Integrationstests 1, 6.
3. **Dedup:** Wiederholte Zyklen über demselben Issue → weiterhin genau ein Vorgang. **Nachweis:** Integrationstest 3.
4. **Negative Schalter:** Globaler Schalter aus / Repository-Flag aus / Plugin ohne `IScmEventSource` → nichts (auch keine Baseline-Nebeneffekte, soweit die Erkennung nicht erreicht wird). **Nachweis:** Integrationstests 4, 5.
5. **Schleifenschutz:** Beschreibungsänderung nach Anlage (neue Revision/Body, inkl. selbst geschriebener `<!-- softwareschmiede:`-Texte) → kein zweiter Auftrag (Snapshot existiert). **Nachweis:** Integrationstest 7.
6. **Isolation:** Aufbereitungs-Aufgabe existiert → Trigger-Kommentar auf demselben Issue wird regulär aufgelöst: keine Zuordnung zur Aufbereitungs-Aufgabe, keine `Ambiguous`-Sperre, kanonische Aufgabe mit `…/Issue/{n}`-SourceKey anlegbar. **Nachweis:** Integrationstest 8.
7. **Geparkter interner Auftrag:** `DispatchDueEventsAsync` übersieht das geparkte Ereignis; expliziter Dispatch endet ehrlich wartend ohne CLI-Start/Prompt (`Zweck`-Guard). **Nachweis:** Integrationstest 9.
8. **Externe Trigger unverändert:** Bestehende Monitoring-/Dispatch-Tests bleiben grün (Trigger-Pfad unberührt; `GetRejection` wird bei der Einreihung nicht aufgerufen).

## Offene Punkte

Keine — Entscheidungen aus Projektplan verbindlich; Annahmen A1–A3 dokumentiert. Nutzerentscheidungen sind nicht einholbar (Anwender-Vorgabe); die gewählten Optionen sind die engste anforderungskonforme Lesart.

## Testbedarf

- **Integration (`src/Softwareschmiede.IntegrationTests/Services/ScmEventMonitoringServiceTests_IssueAufbereitung.cs`, `ScmDispatchFixture`):** Baseline-Only, Happy Path, Dedup über Folgezyklen, globaler Schalter aus, Repository-Flag aus, Altbestand-CreatedAt, Beschreibungsänderung (Schleife), Isolation (Trigger auf demselben Issue → kanonische Aufgabe statt Ambiguous/Aufbereitung), geparkter interner Auftrag (kein Dispatch/kein CLI, Guard-Reason).
- **Unit (`src/Softwareschmiede.Tests`):** `AppEinstellungService`-Getter (Defaults + Roundtrip der drei neuen Schlüssel); ggf. `ScmEventTaskResolver`-Filter über InMemory-DB.
- **E2E:** Keine — kein neuer Benutzerfluss (keine Einstellungs-UI in diesem Schritt; Funktion ist ohne UI nicht bedienbar).
- **Reale End-to-End-Abnahme:** Nicht durchführbar im Benutzerfluss-Sinn — kein UI-Pfad, kein echtes SCM; dokumentiert in `test-results.md` (wie Schritt 4: Mock-/Integrationsebene als Nachweis).
