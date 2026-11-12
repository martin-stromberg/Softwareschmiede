# Umsetzungsplan: Mehrinstanzfähigkeit der SCM-Ereignisüberwachung (GitHub Issue #350)

## Übersicht

Die Eingangsprüfung neu eingelesener SCM-Kommentar-Beobachtungen wird um eine instanzübergreifende Duplikaterkennung erweitert: Beim Lesen werden die Reaktionen des aktiven Ereignis-Kontos (Viewer-Identität) je Kommentar über das bestehende GraphQL-Fragment mitgeladen, in `ScmEventObservation` gespeichert und in `ScmEventValidationService.GetRejection` mit einem neuen Ablehnungsgrund ausgewertet. Betroffen sind der Vertrags-Record, das GitHub-Plugin, der Validierungsdienst, die Dispatch-Gegenprüfung im Orchestrator sowie die Test-Fixtures (`FakeGitHubEventApi`-basierte `Node`-Helfer, `ScmE2EApiClient`/`ScmE2EScenario`) und der konsolidierte SCM-E2E-Test. Keine UI-Änderung, keine Schema-Migration — die neue Eigenschaft reist auf dem JSON-Payload `ScmIncomingEvent.ObservationJson` und ist abwärtskompatibel optional.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine eingehende `ScmEventObservation` der Kommentar-Arten `IssueComment`, `ReviewComment` und `PullRequestReview` auf einer Issue-/Pull-Request-Referenz — jede neu eingelesene Textversion eines Kommentars. |
| Auslösende Ereignisse | Poll-Zyklus des `ScmEventMonitoringService` (`RunOnceAsync`): Lesen einer Quelle über `IScmEventSource.ReadEventsAsync` — im Modus `Benutzerkonto` über repository-weite Enumeration, im Modus `SeparatesKonto` über die Notifications-Inbox; die Kommentarprüfung ist in beiden Modi identisch. |
| Erwartete Wirkung | Ein Kommentar, auf den das aktive Ereignis-Konto bereits reagiert hat, erzeugt kein Inbox-Ereignis, keine Aufgabenanlage über `ScmEventTaskResolver` und keine Zustellung; die Ablehnung bleibt als `ScmContentSnapshot.Diagnostic` nachvollziehbar. Checkpoint/Watermark und die bestehende lokale Deduplikation laufen unverändert weiter. |
| Umgang mit fehlenden Objekten | Reaktionsinformation wird remote je Beobachtung mitgelesen — kein lokales Objekt neu zu erzeugen. Fehlt `reactionGroups` in einer sonst erfolgreichen Provider-Antwort (strukturell unvollständig), gilt der übliche Fehlerpfad (`ScmProviderException` → Checkpoint-Backoff, Wiederholung) — kein stillschweigendes Ignorieren und kein stillschweigendes Abschalten des Schutzes. Ein `ScmIncomingEvent` aus Altbestand ohne Reaktionsdaten wird mit `ViewerReactions = null` deserialisiert und verhält sich wie bisher. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | `Description`-Beobachtungen (Issue-/PR-Text) sind ausgenommen | Auf Referenztexte wird keine Empfangsreaktion gesetzt (`ScmEventOrchestrator.DispatchCoreAsync` versieht nur Nicht-`Description`-Beobachtungen mit `eyes`); dort gibt es kein vergleichbares Remote-Kriterium. | bestätigt (2026-11-12) |
| 2 | Kein lückenloser Schutz gegen echte Gleichzeitigkeit (Rest-Zeitfenster) | Lesen beide Instanzen denselben Kommentar, bevor irgendeine Instanz reagiert hat, verarbeiten beide. Die Anforderung deckt nur „die andere Instanz hat bereits reagiert" ab; die Dispatch-Gegenprüfung wird nicht remote erneuert. | bestätigt (2026-11-12) |
| 3 | Reaktionen fremder Konten unterdrücken keine Verarbeitung | Maßgeblich ist ausschließlich `viewerHasReacted` des eigenen Ereignis-Kontos. | bestätigt (2026-11-12) |
| 4 | Instanzen unter unterschiedlichen Kontomodi/-identitäten erkennen einander nicht | Reagiert Instanz A unter `SeparatesKonto` (Bot), sieht Instanz B unter `Benutzerkonto` die Bot-Reaktion nicht als eigene. Funktioniert nur, wenn beide Installationen dasselbe Ereignis-Konto verwenden. | bestätigt (2026-11-12) |
| 5 | Folgeversionen (Edits) eines bereits reagierten Kommentars werden ebenfalls verworfen | Die Reaktion hängt am Kommentar, nicht an der Textversion. Ein Edit eines Kommentars, auf das bereits `eyes` gesetzt wurde — auch durch dieselbe Instanz für die Vorgängerversion — erzeugt kein neues Ereignis. Konsequenz der wörtlichen Anforderungslogik; betrifft auch den Ein-Instanz-Fall. | bestätigt (2026-11-12) |
| 6 | Jede Reaktion des eigenen Kontos zählt als „reagiert", nicht nur die Empfangsreaktion `eyes` | Eine manuell gesetzte Reaktion (z. B. 👍) unter dem Ereignis-Konto unterdrückt den Kommentar ebenfalls. Empfohlene Auslegung der Anforderung („bzw. allgemein eine vorhandene Reaktion des aktiven Kontos"); die Alternative „nur `eyes`" wäre mit denselben Daten umsetzbar. | bestätigt (2026-11-12) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ablehnungsort | `ScmEventValidationService.GetRejection` mit neuer Konstante `BereitsBeantwortetRejection`, **nicht** Verwerfen in `GitHubPlugin.AddObservation` | Der Store-Pfad persistiert den Grund als `ScmContentSnapshot.Diagnostic` — damit bleibt eine sichtbare Spur (beantwortet offene Frage 4 der Anforderung mit „sichtbare Spur"). Zudem nutzt die Trigger-Suche für `ScmEventTaskResolver` (`ScmEventMonitoringService.cs` Z. 187) dieselbe Methode: reagierte Kommentare lösen automatisch auch keine Aufgabenanlage aus. Service-Ebene ist direkt unit-testbar. |
| Datenträger der Reaktionsinfo | `IReadOnlyList<string>? ViewerReactions` als optionaler Positionsparameter am `ScmEventObservation`-Record + berechnete Eigenschaft `ViewerHasReacted` | Die Inhaltsliste (GraphQL-`ReactionContent`-Werte wie `EYES`) hält die Entscheidung „nur `eyes` vs. jede Reaktion" (offene Frage 1) ohne erneutes Plugin-Mapping implementierbar und liefert Diagnose-Details. Optionaler Parameter sichert Abwärtskompatibilität des gespeicherten `ObservationJson` (fehlende JSON-Eigenschaft → `null`). |
| Reaktionsdaten remote | `reactionGroups { content viewerHasReacted }` im gemeinsamen `TextFields`-Fragment | Einmalige Erweiterung deckt `node(id:)`-Abfragen (`IssueComment`, `PullRequestReviewComment`), die `reviews`-Connection (`PullRequestReview`) und `Description` ab; alle Typen sind `Reactable`. `viewerHasReacted` bindet serverseitig an die Identität des abfragenden Tokens — `ClientFor(repository.Konto)` liefert damit automatisch das richtige Konto je Modus. Kein zusätzlicher Request. Gegenüber `reactions{nodes{user}}` + `IsViewer`-Abgleich einfacher und direkt kontoscharf. |
| Strenge beim Parsen | Fehlendes oder nicht-arrayförmiges `reactionGroups` in einer ansonsten erfolgreichen Antwort → `Permanent("Unvollständige GitHub-Antwort.")` (Checkpoint-Backoff) | Entspricht dem bestehenden Stil (`Required`, `RequiredDate`, `RequiredArray`) und verhindert, dass der Schutz bei einer strukturell abweichenden Antwort still deaktiviert ist. Ein Provider-*Fehler* geht ohnehin in denselben Backoff-Pfad. |
| Position der neuen Regel in `GetRejection` | Nach der Trigger-Prüfung, vor den Identitätsregeln (Owner/Bearbeiter/Marker); nur für `ContentKind is IssueComment or ReviewComment or PullRequestReview` | Nur triggerqualifizierte Kommentare erhalten die spezifische Diagnose; Kommentare ohne Trigger behalten „Kein Trigger"/`KeineErwaehnungRejection` — die präzisere Begründung für die Masse der Fälle. `Description` bleibt ausgenommen (Scope-Ausschluss 1), auch wenn das Fragment Reaktionsdaten mitliefert. |
| Dispatch-Gegenprüfung (`DispatchCoreAsync`, ~Z. 137–141) | Expliziter Zweig: `revalidation == BereitsBeantwortetRejection` → `WaitAsync` mit eigener Meldung, **nicht** zur Toleranzliste hinzufügen | Die Revalidierung prüft nur die gespeicherte Beobachtung; eine Beobachtung mit Kontoreaktion erreicht den Inbox-Pfad produktiv nie (Eingangsablehnung). Falls ein solcher Datensatz dennoch existiert (Altbestand, manuelle Eingriffe, Testdaten), ist Nicht-Zustellen die sichere Richtung — eine Toleranz würde eine remote bereits beantwortete Version erneut zustellen. `WaitAsync` lässt den Anwender den Fall sichtbar klären. Keine Remote-Erneuerung (Scope-Ausschluss 2). |
| E2E-Fixture-Modell | `ScmE2EText` erhält `ViewerReactions` (`List<string>`); `ScmE2EApiClient.Node()` emittiert daraus `reactionGroups` mit `viewerHasReacted: true` | Das Szenario deklariert explizit „dieser Kommentar wurde vom lesenden Konto bereits reagiert" — so ist der Mehrinstanz-Fall (Instanz A hat reagiert, Instanz B liest) in einer einzelnen Instanz darstellbar. Vom Host gepostete Antworten werden bewusst **nicht** in `reactionGroups` zurückgespiegelt (deterministische Fixture, kein versteckter Zustand). |
| `IScmEventSource`-Signatur | Unverändert | Das Kriterium reitet auf `ScmEventObservation`; keine Interface-Änderung nötig. |

## Programmabläufe

### Poll-Lesen mit Reaktionsdaten (beide Kontomodi)

1. `ScmEventMonitoringService.RunOnceAsync` erstellt je Referenz × `ContentKind` einen `ScmEventReadRequest` und ruft `IScmEventSource.ReadEventsAsync`.
2. `GitHubPlugin.ReadEventsAsync` sendet die bestehenden Abfragen mit dem erweiterten `TextFields`-Fragment (inkl. `reactionGroups { content viewerHasReacted }`) über `ClientFor(repository.Konto)` — im `Benutzerkonto` gegen `_eventApiClient`, im `SeparatesKonto` gegen `_agentEventApiClient`; `viewerHasReacted` bezieht sich damit automatisch auf das aktive Ereignis-Konto.
3. `GitHubPlugin.AddObservation` wertet je Knoten `reactionGroups` aus: Pflicht-Array (sonst `Permanent`), `content` aller Gruppen mit `viewerHasReacted == true` wird gesammelt und als `ViewerReactions` an den `ScmEventObservation`-Konstruktor übergeben. Watermark- und Eigenantwort-Filter bleiben unverändert vorher wirksam.
4. `ScmEventStore.AcceptBatchAsync` ruft `GetRejection`: enthält die Beobachtung eine Kontoreaktion und ist `ContentKind` eine Kommentar-Art, liefert die Prüfung `BereitsBeantwortetRejection`; `snapshot.Diagnostic` wird gesetzt, `IdentityResolved` bleibt `true`, es entsteht kein `ScmIncomingEvent`. Checkpoint-Watermark, `Failures`-Reset und `Diagnostic`-Aggregation laufen unverändert.
5. Bereits bekannte Versionen bleiben unverändert durch die Snapshot-Kurzschlusslogik (`BodyHash`-Gleichstand → `continue`) übersprungen — die lokale Deduplikation bleibt wirksam.

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `GitHubPlugin` (`ReadEventsAsync`, `AddObservation`, `ClientFor`), `ScmEventObservation`, `ScmEventStore`, `ScmEventValidationService`, `ScmContentSnapshot`, `ScmObservationCheckpoint`.

### Aufgabenauflösung bei unzugeordneter Referenz

1. Für Referenzen ohne lokale Aufgabe (`unassigned`) sucht `ScmEventMonitoringService.RunOnceAsync` (Z. 186–190) die erste Beobachtung mit `GetRejection(x) is null`.
2. Eine Beobachtung mit Kontoreaktion liefert `BereitsBeantwortetRejection` und wird dadurch nicht als Trigger für `ScmEventTaskResolver.ResolveAsync` verwendet — es entsteht keine neue Aufgabe aus einem bereits remote beantworteten Kommentar.
3. `AcceptBatchAsync` wird mit `aufgabeId == null` aufgerufen; der Kommentar erhält `UnassignedDiagnostic` als Snapshot-Diagnose (Batch-Sicht), kein Inbox-Eintrag.

Beteiligte Klassen/Komponenten: `ScmEventMonitoringService`, `ScmEventValidationService`, `ScmEventTaskResolver`, `ScmEventStore`.

### Zustellungs-Gegenprüfung (defensiv)

1. `ScmEventOrchestrator.DispatchCoreAsync` setzt zunächst wie bisher die `receipt`-/`eyes`-Aktion für Nicht-`Description`-Beobachtungen (`ScmEventOutbox.EnsureAsync`/`ExecuteAsync`).
2. In der Revalidierung mit aktualisiertem Repository wird `GetRejection` erneut aufgerufen.
3. Neuer Zweig vor der bestehenden Toleranzprüfung: `revalidation == ScmEventValidationService.BereitsBeantwortetRejection` → `WaitAsync` mit Meldung sinngemäß „Auf diese Textversion wurde bereits mit dem Ereignis-Konto reagiert; die Verarbeitung erfolgt vermutlich durch eine zweite Installation." — Status `Wartend`, keine CLI-Aktivierung, kein Prompt. Produktiv nicht erreichbar (Eingangsablehnung), sichert Altbestand/Eingriffe ab.

Beteiligte Klassen/Komponenten: `ScmEventOrchestrator`, `ScmEventValidationService`, `ScmEventOutbox`.

### Mehrinstanz-Gesamtablauf (Narrativ)

1. Instanz A (Produktiv) pollt, nimmt den Trigger-Kommentar auf, stellt an die CLI zu und setzt remote die `eyes`-Empfangsreaktion (`receipt`).
2. Instanz B (Vorabversion) pollt dieselbe Referenz danach: `node(id:)`-Abfrage liefert `viewerHasReacted: true` (Instanz B verwendet dasselbe Ereignis-Konto).
3. Instanz B lehnt die Beobachtung mit `BereitsBeantwortetRejection` ab: Snapshot-Diagnose, kein Inbox-Eintrag, keine Aufgabenanlage, keine Zustellung.
4. Umgekehrt symmetrisch, wenn B zuerst verarbeitet.

Beteiligte Klassen/Komponenten: wie „Poll-Lesen mit Reaktionsdaten" plus `ScmEventOrchestrator`/`ScmEventOutbox` (Schreibpfad der Erkennungsreaktion, unverändert).

## Neue Klassen

Keine — alle Änderungen sind Erweiterungen bestehender Typen.

## Änderungen an bestehenden Klassen

### `ScmEventObservation` (Value Object / positionaler Record)

Datei: `src/Softwareschmiede.Plugin.Contracts/Domain/ValueObjects/ScmEvents.cs` (Z. 30–49)

- **Neue Eigenschaften:** `ViewerReactions` (`IReadOnlyList<string>?`) — neuer optionaler Positionsparameter am Ende der Parameterliste (`= null`); Inhalte der vom lesenden Konto gesetzten Reaktionen (GraphQL-`ReactionContent`-Werte, z. B. `EYES`). `ViewerHasReacted` (`bool`, berechnet) — `ViewerReactions is { Count: > 0 }`.
- **Abwärtskompatibilität:** Optionaler Parameter ⇒ gespeicherte `ObservationJson`-Werte ohne das Feld deserialisieren zu `ViewerReactions = null`; `EventKey`/`ContentKey`/`BodyHash`/`Revision` bleiben unverändert berechnet und enthalten keine Reaktionsdaten (Versionsidentität bleibt textbasiert).
- **Keine Schema-Migration:** `ScmIncomingEvent.ObservationJson` ist JSON-Text, die neue Eigenschaft ist additiv.

### `GitHubPlugin` (partial, Ereignispfad)

Datei: `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.Events.cs`

- **Geänderte Member:** `TextFields` (private const, Z. 13) — Feldliste um `reactionGroups { content viewerHasReacted }` erweitern; wirkt in allen drei Verwendungen (`Description`-Abfrage Z. 66, `reviews`-Connection Z. 78–79, `node(id:)` Z. 102).
- **Geänderte Methoden:** `AddObservation` (Z. 232–258) — nach den bestehenden Filtern `reactionGroups` als Pflicht-Array auslesen (fehlend/nicht-arrayförmig → `Permanent("Unvollständige GitHub-Antwort.")`); `content` der Gruppen mit `viewerHasReacted == true` sammeln; bei keiner passenden Gruppe `null`; Ergebnis als letztes Argument an den `ScmEventObservation`-Konstruktor übergeben.

### `ScmEventValidationService` (Service)

Datei: `src/Softwareschmiede/Application/Services/ScmEventValidationService.cs`

- **Neue Konstante:** `BereitsBeantwortetRejection = "Bereits mit dem Konto reagiert"`.
- **Geänderte Methoden:** `GetRejection` — nach der Trigger-Prüfung (nach Rückgabe von `"Kein Trigger"`/`KeineErwaehnungRejection`) neue Regel: `observation.ContentKind is ScmContentKind.IssueComment or ScmContentKind.ReviewComment or ScmContentKind.PullRequestReview && observation.ViewerReactions is { Count: > 0 }` → Rückgabe `BereitsBeantwortetRejection`. Signatur bleibt unverändert; alle drei Aufrufstellen (`AcceptBatchAsync`, Trigger-Suche `RunOnceAsync`, `DispatchCoreAsync`) profitieren ohne Anpassung.

### `ScmEventOrchestrator` (Service)

Datei: `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs`

- **Geänderte Methoden:** `DispatchCoreAsync` (Revalidierung Z. 137–141) — explizite Behandlung des neuen Ablehnungsgrunds vor der bestehenden Toleranzprüfung: `revalidation == ScmEventValidationService.BereitsBeantwortetRejection` → `WaitAsync` mit Meldung sinngemäß „Auf diese Textversion wurde bereits mit dem Ereignis-Konto reagiert; die Verarbeitung erfolgt vermutlich durch eine zweite Installation." Kein Eintrag in die Toleranzliste.

### `ScmE2EText` (E2E-Fixture-Datenmodell)

Datei: `src/Softwareschmiede.App/Services/Testing/ScmE2ETestConfiguration.cs`

- **Neue Eigenschaften:** `ViewerReactions` (`List<string>`, Default `[]`) — deklariert im Szenario `scm-scenario.json` die vom lesenden Konto gesetzten Reaktionsinhalte.

### `ScmE2EApiClient` (E2E-Fixture-Transport)

Datei: `src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs`

- **Geänderte Methoden:** `Node(ScmE2EText, string?)` (Z. 154–165) — immer ein `reactionGroups`-Array emittieren (strenge Plugin-Auswertung); pro Eintrag in `text.ViewerReactions` ein Objekt `{ content = <wert>, viewerHasReacted = true }`, sonst leeres Array.

### `ScmE2EFixture` (E2E-Testfixture)

Datei: `src/Softwareschmiede.Tests/E2E/ScmE2EFixture.cs`

- **Geänderte Methoden:** `AddText(int number, long id, string body, string kind = "IssueComment", IEnumerable<string>? viewerReactions = null)` — optionaler Parameter setzt `ScmE2EText.ViewerReactions`; bestehende Aufrufe bleiben unverändert kompilierfähig.

## Datenbankmigrationen

Keine. Die neue Beobachtungseigenschaft reist ausschließlich auf `ScmIncomingEvent.ObservationJson` (JSON-Payload, additiv und optional). `ScmContentSnapshot.Diagnostic` ist bereits eine Freitext-Spalte und nimmt den neuen Ablehnungsgrund ohne Schemaänderung auf. Altbestand: `ObservationJson` ohne `ViewerReactions` deserialisiert zu `null` — Verhalten unverändert (Regel greift nur bei nicht-leerer Liste).

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `reactionGroups` in `AddObservation` | Muss als JSON-Array vorhanden sein (TextFields-Verbraucher sind alle `Reactable`; Abwesenheit = strukturell unvollständige Antwort) | `ScmProviderException` (`Permanent`, „Unvollständige GitHub-Antwort.") → Quell-Checkpoint-Backoff statt stillem Weiterlesen ohne Schutz |
| `ViewerReactions` auf `ScmEventObservation` | Nur bei `ContentKind is IssueComment or ReviewComment or PullRequestReview` und nicht-leerer Liste Ablehnung | `BereitsBeantwortetRejection` → Snapshot-Diagnose, kein Inbox-Eintrag |

Keine neuen benutzerseitigen Eingaben — keine Eingabevalidierung im UI/Einstellungsbereich.

## Konfigurationsänderungen

Keine — das Verhalten gilt ohne Schalter (Korrektur der Eingangsprüfung gemäß Anforderung).

## Seiteneffekte und Risiken

- **Edits reagierter Kommentare (auch eigene Instanz):** Sobald die `eyes`-Empfangsreaktion gesetzt ist, werden Folgeversionen desselben Kommentars ebenfalls verworfen — ein nachträglich angefügter Trigger-Text in einem bereits quittierten Kommentar erzeugt kein zweites Ereignis mehr (Scope-Ausschluss 5).
- **Snapshot-Diagnose-Überschreibung:** Wird eine neue Textversion eines Kommentars verworfen, auf dessen Vorgängerversion ein Inbox-Ereignis existiert, setzt der (einzige) `ScmContentSnapshot` pro `ContentKey` die Diagnose auf `BereitsBeantwortetRejection`. Akzeptierte Altversionen bleiben dagegen durch den `BodyHash`-Kurzschluss unberührt.
- **Kein Monitoring-Alarm:** `checkpoint.Diagnostic` aggregiert weiterhin nur `Ambiguous-`/`UnassignedDiagnostic`; der neue Grund landet nur auf dem Snapshot — sichtbare Spur ohne Alarm.
- **Reaktionsentfernung:** Eine nachträglich entfernte Reaktion holt eine bereits verworfene Version nicht zurück (Snapshot-Kurzschluss bei gleichem `BodyHash`); eine neue Textversion wird dagegen erneut regulär geprüft.
- **Description-Beobachtungen** tragen künftig ebenfalls `ViewerReactions` (gleiches Fragment) — ohne Wirkung, da die Regel auf die drei Kommentar-Arten beschränkt bleibt.
- **Alle bestehenden `new ScmEventObservation(...)`-Aufrufe** (1 Produktivstelle, ~10 Teststellen) bleiben durch den optionalen Endparameter unverändert kompilierfähig.
- **Test-Fixtures:** Strenge `reactionGroups`-Auswertung macht das Feld in allen Fake-GraphQL-Knoten erforderlich — abgedeckt durch die zwei zentralen Helfer (`GitHubEventSourceTests.Node`, `ScmE2EApiClient.Node`); keine ad-hoc-Knotenliterale bekannt.
- **Fixture-Identität im `SeparatesKonto`-E2E:** `viewerHasReacted` wird für den lesenden Client ausgegeben — im Bot-Modus modelliert `ViewerReactions` korrekt die Bot-Reaktion; `Remote()`-Antworten werden nicht in `reactionGroups` zurückgespiegelt (bewusste Vereinfachung, siehe Designentscheidungen).

## Umsetzungsreihenfolge

1. **`ScmEventObservation` erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: Optionaler Positionsparameter `ViewerReactions` + berechnete `ViewerHasReacted` in `ScmEvents.cs`. Alle bestehenden Konstruktor-Aufrufe bleiben kompilierfähig.

2. **GitHub-Plugin: Reaktionsdaten lesen**
   - Voraussetzungen: Schritt 1 (Record-Eigenschaft).
   - Beschreibung: `TextFields` um `reactionGroups { content viewerHasReacted }` erweitern; `AddObservation` wertet streng aus und befüllt `ViewerReactions`.

3. **Validierungsregel `BereitsBeantwortetRejection`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Konstante + neue Regel in `GetRejection` (nach Trigger-Prüfung, nur Kommentar-Arten). Wirkt sofort in `AcceptBatchAsync` und in der Trigger-Suche von `RunOnceAsync`.

4. **Dispatch-Gegenprüfung im Orchestrator**
   - Voraussetzungen: Schritt 3 (Konstante).
   - Beschreibung: Expliziter `BereitsBeantwortetRejection`-Zweig → `WaitAsync` mit eigener Meldung in `DispatchCoreAsync`.

5. **Unit-Test-Helfer und Plugin-Mapping-Tests**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: `GitHubEventSourceTests.Node()` um `reactionGroups` erweitern (immer emittieren, optionaler Reaktionsparameter); `ScmEventValidationServiceTests`-Helfer `Observe`/`ObserveSeparate` um optionalen `viewerReactions`-Parameter; neue Unit-Tests (Mapping, Ablehnung, Reihenfolge, Description-Ausnahme, SeparatesKonto, Legacy-`ObservationJson`).

6. **Integrationstests Monitoring/Store**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: Neue Fälle in `ScmEventMonitoringServiceTests_RepoWide`/`_TaskAssignment` (reagierter Kommentar auf zugeordneter und auf unzugeordneter Referenz) sowie defensiver Dispatch-Test in `ScmEventOrchestratorTests` (direkter `ScmIncomingEvent`-Insert mit `ViewerReactions` im JSON).

7. **E2E-Fixture-Erweiterung**
   - Voraussetzungen: Schritte 1–2 (Plugin erwartet `reactionGroups` im Knoten).
   - Beschreibung: `ScmE2EText.ViewerReactions`, `ScmE2EApiClient.Node()`-Emission, `ScmE2EFixture.AddText`-Parameter.

8. **E2E-Phasen im konsolidierten Test**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: Neue Phase in `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` vor `BotModeAsync` (reagierter Kommentar auf zugeordneter Referenz + reagierter Trigger-Kommentar auf unzugeordneter Referenz + Positivkontrolle) sowie eine Ergänzung in `BotModeAsync` (reagierter Mention-Kommentar).

9. **Dokumentation**
   - Voraussetzungen: Schritte 1–8.
   - Beschreibung: `Docs/help/plugins/github-ereignisse/` (insbesondere `business-rules.md`, `ablauf-technisch.md`): instanzübergreifende Duplikaterkennung über die Kontoreaktion dokumentieren, inkl. Rest-Zeitfenster und Voraussetzung „gleiches Ereignis-Konto".

10. **Verifikation**
    - Voraussetzungen: Schritte 1–9.
    - Beschreibung: `dotnet build Softwareschmiede.slnx -c Debug`; betroffene Suiten (`Category!=OsInterface` + `Category=OsInterface` für beide Testprojekte mit `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1`); `dotnet format Softwareschmiede.slnx --verify-no-changes`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Observe`/`ObserveSeparate` (erweitert) | `ScmEventValidationServiceTests` | Optionaler `viewerReactions`-Parameter setzt den neuen Record-Parameter |
| `ViewerReaction_RejectsCommentKinds` (o. ä.) | `ScmEventValidationServiceTests` | Trigger-Kommentar mit `ViewerReactions = ["EYES"]` → `BereitsBeantwortetRejection` (Benutzerkonto; mindestens `IssueComment`, Varianten für `ReviewComment`/`PullRequestReview`) |
| `SeparatesKonto_ViewerReaction_Rejects` | `ScmEventValidationServiceTests` | `@scm-bot`-Mention-Kommentar mit Reaktion → `BereitsBeantwortetRejection` |
| `ViewerReaction_CheckRunsAfterTrigger` | `ScmEventValidationServiceTests` | Kommentar mit Reaktion, aber ohne Trigger → weiterhin `"Kein Trigger"`/`KeineErwaehnungRejection` (dokumentiert die Reihenfolge) |
| `ViewerReaction_DescriptionNotRejected` | `ScmEventValidationServiceTests` | `Description`-Beobachtung mit `ViewerReactions` und gültigem Trigger → `null` (Scope-Ausschluss 1) |
| `Node(...)` (erweitert) | `GitHubEventSourceTests` | Neuer optionaler Reaktionsparameter; `reactionGroups` wird immer emittiert |
| `ViewerReactions_MapFromReactionGroups` | `GitHubEventSourceTests` | `node(id:)`-Antwort mit `viewerHasReacted: true` auf einer Gruppe → `ViewerReactions` enthält deren `content` |
| `ViewerReactions_FalseOrEmpty_Ignored` | `GitHubEventSourceTests` | Gruppen mit `viewerHasReacted: false` bzw. leeres Array → `ViewerReactions` leer/`null`, Kommentar normal verarbeitbar |
| `MissingReactionGroups_ThrowsPermanent` | `GitHubEventSourceTests` | Knoten ohne `reactionGroups` → `ScmProviderException` mit `FailureKind.Permanent` (strenge Auswertung; Fehlerpfad statt stillem Schutzverlust) |
| `ReactionGroups_RequestedInQuery` | `GitHubEventSourceTests` | `FakeGitHubEventApi.Requests` enthält `reactionGroups` im GraphQL-Body (Lesepfad fordert das Feld an) |
| `Monitor_ReactedComment_StoresDiagnosticWithoutInbox` | `ScmEventMonitoringServiceTests_RepoWide` | Aufgabenverknüpfte Referenz: reagierter Kommentar → kein `ScmIncomingEvent`, `ScmContentSnapshot.Diagnostic == BereitsBeantwortetRejection`, `IdentityResolved`, Checkpoint-Watermark fortgeschrieben |
| `Monitor_ReactedCommentOnUnassignedReference_CreatesNoTask` | `ScmEventMonitoringServiceTests_TaskAssignment` (oder `_RepoWide`) | Reagierter Trigger-Kommentar auf unzugeordneter Referenz → `ScmEventTaskResolver` greift nicht: keine neue `Aufgabe`, kein Inbox-Eintrag |
| `Monitor_ReactedComment_NextPoll_StaysQuiet` | `ScmEventMonitoringServiceTests_RepoWide` | Zweiter `RunOnceAsync` erzeugt weiterhin kein Ereignis (Snapshot-Dedup mit Diagnose) |
| `ObservationJson_WithoutViewerReactions_Deserializes` | `ScmEventPersistenceTests` | `ScmIncomingEvent` mit Alt-JSON (ohne `ViewerReactions`-Eigenschaft) → `ScmEventStore.Observation` liefert `ViewerReactions == null`; `GetRejection` unverändert |
| `Dispatch_ReactedStoredObservation_Waits` | `ScmEventOrchestratorTests` | Direkt eingefügter `ScmIncomingEvent` mit `ViewerReactions` im `ObservationJson` → `DispatchAsync` → Status `Wartend` mit dedizierter Meldung, kein CLI-Start, kein Prompt (defensive Abdeckung des neuen Revalidierungszweigs) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GitHubEventSourceTests` — Hilfsmethode `Node(...)` (Z. 549–560) | Muss `reactionGroups` emittieren, damit die strenge Plugin-Auswertung bestehende Fake-Antworten weiter parst; `Review(...)` erbt automatisch |
| `ScmEventValidationServiceTests` — `Observe`/`ObserveSeparate` (Z. 144–158) | Optionaler `viewerReactions`-Parameter für die neuen Fälle; bestehende Aufrufe bleiben kompilierfähig |
| `ScmDispatchFixture.AddAsync` (Z. 184) / alle `new ScmEventObservation(...)`-Aufrufe in `ScmEventMonitoringServiceTests_*`, `ScmEventDispatchTests`, `ScmEventPersistenceTests`, `ScmPromptAppendixTests` | Bleiben dank optionalem Endparameter unverändert kompilierfähig — nur bei Bedarf an neue Parameter anpassen, kein Zwang |
| `ScmEventPersistenceTests.MigrationAndReplayPreserveOldTasksAndObserveVersions` | Regressionsbeleg: migrierte Daten + `ObservationJson` ohne `ViewerReactions` müssen weiter deserialisierbar sein (kein Codeeingriff nötig, aber zu beobachten) |

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung verändert keinen UI-Ablauf, aber das systemweite Verhalten vom Remote-Lesen bis zur Inbox-/Dispatch-Entscheidung; der Nachweis, dass die echte Anwendung (Poll → Plugin-Parsing → Store → Orchestrator) einen remote reagierten Kommentar verwirft, ist über Unit-/Integrationstests allein nicht erreichbar. Die Erweiterung erfolgt im konsolidierten SCM-E2E-Test `ScmEventsE2ETests.ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` (echte WPF-App, produktiver GitHub-Parser, Fixture-Transport) — keine neue Testmethode (Konsolidierungsregel), sondern neue Phasen in derselben laufenden Instanz. Dieser Lauf gilt zugleich als reale End-to-End-Abnahme des Features.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Benutzerkonto: reagierter Trigger-Kommentar auf aufgabenverknüpfter Referenz → kein `ScmIncomingEvent`, Snapshot-Diagnose `BereitsBeantwortetRejection`; zusätzlich reagierter Trigger-Kommentar auf unzugeordneter Referenz → keine Aufgabenanlage; Positivkontrolle: neuer Kommentar ohne Reaktion → Inbox-Ereignis | `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (neue Phase vor `BotModeAsync`) | „Kommentar mit Kontoreaktion erzeugt kein Inbox-Ereignis, keine Aufgabenanlage, keine Zustellung" + „unberührte Kommentare laufen unverändert" | Nur die echte App instanziiert Monitoring, Plugin-Parsing und Store gemeinsam; der Effekt tritt über Prozess- und Schichtgrenzen hinweg auf |
| Pflicht | SeparatesKonto (Bot-Phase): reagierter Mention-Kommentar mit Notification → kein Inbox-Ereignis, Snapshot-Diagnose | `src/Softwareschmiede.Tests/E2E/E2E_ScmEvents.cs` (Ergänzung in `BotModeAsync`) | Gleiche Prüfung unter der zweiten Konto-Identität (`viewerHasReacted` bindet an Bot-Client) | Deckt den `ClientFor`-Routingspfad ab, den Unit-Tests nur gemockt nachstellen |

Bestehende E2E-Tests, die angepasst werden müssen:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ScmE2EFixture.AddText` (Aufrufe im konsolidierten Test) | Signatur erhält optionalen `viewerReactions`-Parameter — bestehende Aufrufe bleiben unverändert kompilierfähig |
| `ScmE2EApiClient.Node()` | Muss `reactionGroups` emittieren (strenge Plugin-Auswertung) — Fixture-Produktivcode, kein Test im engeren Sinn |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path Mehrinstanz (Benutzerkonto) | Periodischer Poll (`RunOnceAsync`) | Instanz A hat einen Trigger-Kommentar bereits angenommen, zugestellt und mit `eyes` quittiert; Instanz B (gleiches Ereignis-Konto, eigene lokale DB) hat weder Snapshot noch Event für den Kommentar | B liest die Quelle: `node(id:)` liefert `viewerHasReacted` → `ViewerReactions` auf der Beobachtung → `GetRejection` → `AcceptBatchAsync` | In B: kein `ScmIncomingEvent`, keine Aufgabenanlage, kein Prompt, keine Zustellung; `ScmContentSnapshot.Diagnostic = "Bereits mit dem Konto reagiert"`; Checkpoint-Watermark läuft normal fort | Integrationstest `Monitor_ReactedComment_StoresDiagnosticWithoutInbox` + E2E-Phase (Nachweis über `OpenDatabase`) |
| 2 | Unberührter Kommentar im selben Zyklus | Poll | wie 1, zusätzlich ein zweiter, neuer Kommentar ohne Kontoreaktion auf derselben/einer anderen Referenz | Lesen → Validierung (`ViewerReactions` leer) → `AcceptBatchAsync` | Neues `ScmIncomingEvent` im üblichen Fluss (Inbox/Wartend), Snapshot ohne Ablehnungsdiagnose | Integrationstest-Positivkontrolle + E2E-Phase |
| 3 | Reagierter Kommentar auf unzugeordneter Referenz | Poll | Referenz ohne lokale Aufgabe im enumerierten Repository; Trigger-Kommentar trägt Kontoreaktion | Trigger-Suche (`GetRejection`) verwirft die Beobachtung → `ScmEventTaskResolver.ResolveAsync` wird nicht aufgerufen → `AcceptBatchAsync` mit `aufgabeId == null` | Keine neue `Aufgabe`, kein Inbox-Eintrag; Snapshot trägt `UnassignedDiagnostic` | Integrationstest `Monitor_ReactedCommentOnUnassignedReference_CreatesNoTask` + E2E-Phase |
| 4 | SeparatesKonto | Notifications-Inbox liefert Mention → Quell-Lesen | Bot-Modus konfiguriert; Mention-Kommentar trägt Reaktion des Bot-Kontos | Bot-Client liest Knoten → `viewerHasReacted` bezieht sich auf Bot → Ablehnung | Kein `ScmIncomingEvent`/keine Aufgabe; Snapshot-Diagnose | Integrationstest `SeparatesKonto_ViewerReaction_Rejects` (Validierung) + E2E-Ergänzung in `BotModeAsync` |
| 5 | Provider-Fehler / unvollständige Antwort | Poll | `reactionGroups` fehlt strukturell oder der GraphQL-/REST-Aufruf schlägt fehl | `Permanent`/typisierte `ScmProviderException` → `ScmCheckpointSupport.RecordFailureAsync` | Quell-Checkpoint mit `ErrorCode`/`NextAttemptUtc`; der Kommentar wird nicht stillschweigend verworfen und nach Backoff erneut gelesen | `MissingReactionGroups_ThrowsPermanent` + bestehende Fehlerpfad-Tests (`FailedSecondPage_...`, `Monitor_EnumerationFailure_...`) |
| 6 | Rest-Zeitfenster Gleichzeitigkeit | Dispatch in beiden Instanzen nahezu gleichzeitig | Beide Instanzen haben den Kommentar gelesen, bevor eine reagiert hat; das Ereignis liegt in beiden Inboxen | Jede Instanz dispatcht eigenständig (keine Remote-Erneuerung in der Gegenprüfung) | Mögliche Doppelverarbeitung — **bewusster Scope-Ausschluss 2**, kein Testanspruch; defensive Absicherung nur über gespeicherte Beobachtung | Scope-Ausschluss (dokumentiert); kein Test |
| 7 | Altbestand-Ereignis | Deserialisierung in Dispatch-/Prüfpfaden | `ScmIncomingEvent` mit `ObservationJson` ohne `ViewerReactions`-Eigenschaft (vor der Umstellung geschrieben) | `ScmEventStore.Observation` → `ViewerReactions = null` → `GetRejection` ohne neuen Grund | Ereignis verhält sich exakt wie vor der Umstellung | `ObservationJson_WithoutViewerReactions_Deserializes` + `MigrationAndReplayPreserveOldTasksAndObserveVersions` |
| 8 | Edit eines bereits reagierten Kommentars | Poll | Kommentar wurde angenommen und mit `eyes` quittiert; danach wurde der Text editiert (neuer `BodyHash`) | Neue Textversion gelesen → `ViewerReactions` nicht leer → Ablehnung | Kein neues Ereignis für die Edit-Version (Scope-Ausschluss 5); Snapshot-Diagnose aktualisiert | Unit-/Integrationstest (Ablehnung unabhängig von Versionsstand; `EventKey`-Semantik unverändert) |
| 9 | Fremde Konto-Reaktion | Poll | Kommentar trägt Reaktionen ausschließlich anderer Konten | `viewerHasReacted` ist `false` für alle Gruppen → `ViewerReactions` leer | Normale Verarbeitung (Inbox wie bisher) | `ViewerReactions_FalseOrEmpty_Ignored` + `ViewerReaction_RejectsCommentKinds` (Negativseite) |

## Offene Punkte

Alle drei Punkte wurden dem Anwender vorgelegt und am 2026-11-12 mit „wie vorgeschlagen" bestätigt — die jeweils empfohlenen Vorschläge gelten als Festlegung (siehe Scope-Ausschlüsse, Status `bestätigt`).

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Zählt als „reagiert" ausschließlich die Empfangsreaktion `eyes` oder jede beliebige Reaktion des Ereignis-Kontos (eine manuell gesetzte 👍 würde den Kommentar dann ebenfalls unterdrücken)? | **Jede Reaktion des Kontos zählt** (Umsetzung über `ViewerReactions is { Count: > 0 }`): deckt auch manuell quittierte Kommentare ab, entspricht der Anforderungsalternative „allgemein eine vorhandene Reaktion" und ist einfacher; die gespeicherten `ViewerReactions`-Inhalte erlauben eine spätere Einschränkung auf `EYES` ohne erneutes Plugin-Mapping. Als Scope-Ausschluss 6 zur Bestätigung vorgelegt. |
| 2 | Gilt die Erkennung, wenn beide Instanzen unter unterschiedlichen Kontomodi/-identitäten antworten (`viewerHasReacted` sieht nur die eigene Identität)? | Als bewusste Einschränkung akzeptieren (Scope-Ausschluss 4): Voraussetzung „beide Installationen verwenden dasselbe Ereignis-Konto" in `Docs/help/plugins/github-ereignisse/` dokumentieren; eine kontoübergreifende Erkennung würde zusätzliche `reactions{nodes{user}}`-Auswertung und Identitätslogik erfordern — nicht von der Anforderung gefordert. |
| 3 | Soll die Zustellungs-Gegenprüfung in `DispatchCoreAsync` die Reaktionslage remote erneuern, um das Rest-Zeitfenster zu schließen? | **Nein**: Das Rest-Zeitfenster ist als Scope-Ausschluss 2 deklariert; ein Remote-Refresh je Dispatch würde eine neue Leseanforderung, neue Fehlerpfade und neue Wartebedingungen erzeugen. Stattdessen defensiver `WaitAsync`-Zweig auf dem gespeicherten Stand (ohne zusätzliche Remote-Calls). |

Die vierte Frage der Anforderung („sichtbare Spur oder still verwerfen?") ist durch die Designentscheidung zum Ablehnungsort beantwortet: Die Ablehnung über `GetRejection` hinterlässt eine sichtbare Spur als `ScmContentSnapshot.Diagnostic` analog „Kein Trigger" — kein stilles Verwerfen.
