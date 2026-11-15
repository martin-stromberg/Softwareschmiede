# Logik — Bestandsaufnahme

## `ScmEventValidationService`
Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

Zentrale, ausschließlich statische Validierung. **Primäre Änderungsstelle der Anforderung.**

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `DefaultTriggerKeyword` (Konstante) | `public const` | Standard-Trigger `$Softwareschmiede` (Z. 11) |
| `KeineErwaehnungRejection` (Konstante) | `public const` | Ablehnungsgrund im Modus `SeparatesKonto` (Z. 15) |
| `BereitsReagiertRejection` (Konstante) | `public const` | Ablehnungsgrund bei Kontoreaktion (Z. 19) |
| `QuoteChars` (Konstante) | `private const` | Zitatzeichen für die Frei-stehend-Regel (Z. 21) |
| `NormalizeTriggerKeyword(string?)` | `public static` | Fallback auf `DefaultTriggerKeyword`, trimmt (Z. 26–27) |
| `GetRejection(ScmEventObservation, string?)` | `public static` | Vollständige Eingangsprüfung; `null` = angenommen (Z. 33–61) |
| `HasFreeStandingTrigger(string, string)` | `private static` | Trigger nur frei stehend, ordinal-ignore-case (Z. 64–76) |
| `SameIdentity(string?, string?, string?, string?)` | `private static` | Identitätsvergleich: IDs ordinal vor Logins ignore-case (Z. 78–82) |

**Prüfreihenfolge in `GetRejection` (Z. 33–61):**
1. Trigger je Kontomodus — `SeparatesKonto`: frei stehende `@{ViewerLogin}`-Erwähnung → sonst `KeineErwaehnungRejection`; `Benutzerkonto`: frei stehendes Keyword → sonst `"Kein Trigger"` (Z. 39–42).
2. `BereitsReagiertRejection` bei Kommentar-Arten mit `ViewerHasReacted` (Z. 46–48).
3. Nur `Benutzerkonto`: Viewer = Owner → sonst `"Angemeldeter Benutzer ist nicht Repository-Owner"` (Z. 49–50).
4. Akteur bestimmen: `Editor*` wenn `LastEditedAt`, sonst `Author*`; beide leer → `"Bearbeiter nicht bestimmbar"` (Z. 51–55).
5. **Akteursprüfung (Z. 56–57):** `SameIdentity(actorId, actorLogin, repository.OwnerId, repository.Owner)` → sonst `"Textversion stammt nicht vom Repository-Owner"`. Genau hier wird ein `github-actions`-Autor heute verworfen; es gibt keine zweite akzeptierte Identität und keinen Provider-/Host-Bezug in der Prüfung.
6. Eigener Antwortmarker `<!-- softwareschmiede:` → `"Eigene Antwort"` (Z. 58–59).

Abonnierte Events: keine. Publizierte Events: keine.

## `ScmEventStore` — Aufrufer 1 (Aufnahme-Diagnose)
Datei: `src/Softwareschmiede/Application/Services/ScmEventStore.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AcceptBatchAsync(Guid?, string, ScmEventBatch, bool, string?, CancellationToken)` | `public` | Atomare Inboxaufnahme; ruft `GetRejection` je Beobachtung auf (Z. 62), persistiert Ablehnung als `ScmContentSnapshot.Diagnostic` und legt bei Annahme ein `ScmIncomingEvent` an (Z. 59–87). `IdentityResolved` ist bei `"Bearbeiter nicht bestimmbar"` `false` (Z. 63) — solche Texte werden ohne `since`-Grenze erneut gelesen |
| `Observation(ScmIncomingEvent)` | `public static` | Deserialisiert `ObservationJson` (Z. 24–25) |
| `ActionId(Guid, string)` | `public static` | Deterministische Aktions-ID (Z. 27–28) |
| `RetryAt(...)` | `public static` | Backoff-Berechnung (Z. 30–31) |
| `ClaimTaskAsync` / `RenewTaskAsync` / `ReleaseTaskAsync` | `public` | Instanzübergreifende Aufgabenleases (Z. 101–114) |

Relevante Konstanten: `AmbiguousAssignmentDiagnostic`, `UnassignedDiagnostic`, `InboxDiscardDiagnosticPrefix`, `JsonOptions` (Z. 15–22).

## `ScmEventMonitoringService` — Aufrufer 2 (Trigger-Suche)
Datei: `src/Softwareschmiede/Application/Services/ScmEventMonitoringService.cs`

| Methode / Stelle | Kurzbeschreibung |
|------------------|------------------|
| Poll-Zyklus (Z. 158–204) | Gruppiert aufgelöste Referenzen, liest je `ScmContentKind` via `IScmEventSource.ReadEventsAsync`; bei unzugeordneter Quelle sucht `GetRejection`-freie Beobachtung als Trigger für `ScmEventTaskResolver.ResolveAsync` (Z. 186–193), danach `AcceptBatchAsync` (Z. 194–195) |
| `since`-Wiederanlauf (Z. 179–182) | Quellen mit `IdentityResolved == false` oder Zuordnungsdiagnosen werden vollständig neu gelesen |
| `SeparatesKonto`-Inbox (Z. 130–141) | Notifications-Inbox (`IScmNotificationSource.EnumerateNotificationsAsync`) ersetzt die repository-weite Enumeration |

## `ScmEventOrchestrator` — Aufrufer 3 (Revalidierung beim Dispatch)
Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DispatchAsync(Guid, string?, CancellationToken)` | `public` | Einstieg; läuft unter `ScmTaskCoordinator`-Lease (Z. 23–28) |
| `OpenFromQueueAsync(...)` | `public` | Queue-Auswahl unter derselben Lease (Z. 31–72) |
| `DispatchCoreAsync(...)` | `private` | Revalidiert das gespeicherte Ereignis mit dem **frisch** vom Provider geladenen `ScmEventRepository` (`GetEventRepositoryAsync`, Z. 130–139): `BereitsReagiertRejection` → Wartegrund „…zweite Installation" (Z. 143–144); jede andere Ablehnung außer „Kein Trigger"/`KeineErwaehnungRejection` → Wartegrund `"Angemeldeter Benutzer ist nicht mehr der bestätigte Repository-Owner."` (Z. 145–146). Die Owner-Prüfung wirkt hier also ein zweites Mal — ein `github-actions`-Ereignis müsste beide Prüfungen passieren |
| `ActivateForOpenAsync`, `RetryAsync`, weitere Mutations-/Hilfsmethoden | `public`/`private` | Statusübergänge und Wiederanläufe (ab Z. 258) |

## `ScmEventTaskResolver`
Datei: `src/Softwareschmiede/Application/Services/ScmEventTaskResolver.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ResolveAsync(ScmEventReference, IScmEventSource, ScmEventObservation, IReadOnlyList<GitRepository>, CancellationToken)` | `public` | Wiederverwendung oder Anlage der Aufgabe zur Referenz der qualifizierten Trigger-Beobachtung (Z. 46–50); identitätsagnostisch — entscheidet nichts über die Autorschaft |
| `ResolveIssueAsync` / `ResolvePullRequestAsync` | `private` | Anlage über `CreateFromIssueEventAsync`/`CreateFromPullRequestAsync` mit `ComposeBody` aus Referenztext + Trigger-Text (Z. 52–109) |

## `GitHubPlugin` (Ereignispfad)
Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs` — Deklaration: `GitHubPlugin.cs` Z. 19, `PluginPrefix = "Softwareschmiede.GitHub"` (`GitHubPlugin.cs` Z. 53)

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `TextFields` (Konstante) | `private const` | GraphQL-Fragment `id body createdAt updatedAt lastEditedAt url author { login ... on Node { id } } editor { login ... on Node { id } } reactionGroups { content viewerHasReacted }` (Z. 13) — **kein `__typename`**, kein Actor-Typ |
| `GetEventRepositoryAsync` | `public` | Lädt Owner/Viewer-Identität und `CanWrite` (Z. 29–47) |
| `ReadEventsAsync` | `public` | Liest Description/Reviews/Kommentare; GraphQL + REST-Pagination (Z. 50–111) |
| `EnumerateReferencesAsync` | `public` | Offene + seit `sinceUtc` aktive Referenzen (Z. 114–132) |
| `ReadIssueAsync` / `ReadPullRequestAsync` | `public` | Anlagedaten für `ScmEventTaskResolver` (Z. 135–167) |
| `EnumerateNotificationsAsync` | `public` | Inbox des separaten Kontos (`IScmNotificationSource`, Z. 170–199) |
| `AddObservation` | `private static` | Übernimmt `author.id`/`author.login` und `editor.*` unverändert in die Beobachtung (Z. 232–264); filtert eigene markierte Viewer-Texte (Z. 246) und verlangt `reactionGroups` (Z. 247–251) |
| `IsViewer` | `private static` | ID- vor Login-Vergleich gegen `ViewerId`/`ViewerLogin` (Z. 443–445) |

**Feststellung:** Für `github-actions[bot]`-Autoren liefert GitHub in `author.login` den Bot-Login und in `author.id` dessen Node-ID — beides landet bereits unverändert in `AuthorId`/`AuthorLogin`. Eine Typunterscheidung (`Bot`/`App` vs. `User`) ist derzeit weder abgefragt noch im Beobachtungsmodell ablegbar.

## `BitbucketPlugin` / `LocalDirectoryPlugin`
Dateien: `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs` (Z. 12), `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs` (Z. 40)

Implementieren `IScmEventSource` **nicht** — sie erzeugen keine `ScmEventObservation`s und durchlaufen `GetRejection` nie. „Provider ohne `github-actions`-Konzept" ist damit faktisch kein Pfad, der geschützt werden müsste.

## `AppEinstellungService` (Konfigurationsbezug)
Datei: `src/Softwareschmiede/Application/Services/AppEinstellungService.cs`

- `ScmTriggerKeywordKey` = `"scm.trigger.keyword"` (Z. 46); `GetScmTriggerKeywordAsync` normalisiert über `ScmEventValidationService.NormalizeTriggerKeyword` (Z. ~356–364). Präzedenzfall „fest verdrahteter Standard + optionale Einstellung", den die Anforderung für vertrauenswürdige Konten diskutiert — aktuell existiert keine solche Einstellung.

## Dokumentation (Logikbeschreibung)
- `docs/help/plugins/github-ereignisse/business-rules.md` — Abschnitt „Trigger und Repository-Owner" (Z. 9–11) und „Kontomodus und Notifications-Inbox" (Z. 29–33) beschreiben den Owner-Vertrag; `troubleshooting.md` (Z. 63–65) nennt `github-actions[bot]`-Autorenschaft heute als Fehlerursache samt `REVIEW_ISSUE_TOKEN`-Workaround; `ablauf-technisch.md` (Z. 38, 40) beschreibt `GetRejection` im Aufnahme- und Reaktionspfad.
