# Plan-Prüfung (2. Runde): Screenshot-Nachweis — Bilder aus CLI-Läufen hostseitig hochladen

**Status: Plan lückenhaft**

Geprüft wurde der aktualisierte [plan.md](plan.md) gegen [requirement.md](requirement.md), [inventory.md](inventory.md) inkl. `inventory/{austauschprotokoll,github-plugin,orchestrierung-und-prompt,tests-und-doku}.md`, den vorherigen Befund [plan-check.1.md](plan-check.1.md) sowie den tatsächlichen Codebestand auf diesem Branch. Der Plan wurde nicht geändert; dieses Dokument ist ausschließlich Befund.

Beide in Runde 1 gemeldeten Muss-Lücken sind geschlossen, und alle fünf kleineren Anmerkungen wurden im Plan nachgezogen. Die Revision ist substanziell: Die Fehlersemantik ist jetzt korrekt begründet und der dritte Interface-Implementierer ist benannt, lokalisiert und mit festgelegtem Stub-Verhalten versehen. Eine Residualvariante der Lücke-2-Mechanik bleibt jedoch bestehen und erfordert eine letzte Plankorrektur: **nicht typisierte Ausnahmen im Upload-Teilschritt — insbesondere das Parsen der Antwort des undokumentierten Endpoints — landen über dieselbe Outbox-Klausel ebenfalls dauerhaft auf `Uncertain`.**

## Prüfung der gemeldeten Lücken

### Lücke 1 — `ScmE2EApiClient` (build-brechend): **geschlossen**

Der Plan erfasst den dritten `IGitHubEventApiClient`-Implementierer jetzt an vier Stellen konsistent:

- Artefaktliste (Plan Z. 68) und Änderungsabschnitt (Z. 115, 117) nennen `src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs` explizit — verifiziert: `ScmE2EApiClient.cs` Z. 9–10 implementiert `IGitHubEventApiClient`, wird zweifach in `ScmE2EPluginManager.cs` Z. 28 instanziiert (Benutzer- und Bot-Identität). Eine Repo-weite Suche bestätigt exakt drei Implementierer (`GitHubHttpApiClient.cs` Z. 11, `FakeGitHubEventApi.cs` Z. 9, `ScmE2EApiClient.cs` Z. 10); es gibt keine weiteren — weder `Mock<IGitHubEventApiClient>` noch weitere Test-Stubs.
- Das Stub-Verhalten ist festgelegt (Plan Z. 123): Aufruf in `configuration.CallsPath` protokollieren wie `SendAsync` (verifiziert Z. 16–24, Append in Z. 21) + deterministische Fixture-`url`. Die Begründung gegen `ScmProviderException(Permanent)` ist sachlich stimmig (Fixture-Philosophie, Diagnostizierbarkeit, ruhender Pfad — die E2E-Suite legt keine Screenshots ab). Die Fixture-Antwort `{"url": "https://github.com/user-attachments/assets/e2e-fixture"}` passiert die geplante URL-Validierung (https, `github.com`).
- Umsetzungsreihenfolge Schritt 3 (Z. 185) verlangt alle drei Implementierer im selben Schritt — der Hinweis „sonst bricht der `Softwareschmiede.App`-Build" ist korrekt (`ScmE2EApiClient` liegt im App-Projekt).
- Die Tabelle „Betroffene bestehende Tests" (Z. 226) führt den Implementierer erneut — kein Durchrutsch-Risiko.

### Lücke 2 — `Uncertain`-Semantik: **geschlossen** (mit einer Residualvariante, s. neue Lücke unten)

Der Plan legt die Fehlersemantik jetzt einheitlich und korrekt fest:

- Upload-Teilschritt **nie** `outcomeUncertain`: Übersicht (Z. 5), OF2-Tabelle (Z. 38), Fehlerpfade (Z. 61), `GitHubHttpApiClient.UploadAsync` (Z. 121, explizit als „Überschreibung der `SendAsync`-Mutationsheuristik Z. 56–59" — verifiziert: `SendAsync` setzt `outcomeUncertain` für Nicht-GET/nicht-graphql, `GitHubHttpApiClient.cs` Z. 56–59), `ComposeBodyAsync`/`SendUploadApiAsync` (Z. 134: „`outcomeUncertain: false` in allen Fällen (auch ≥ 500 und Transport)"), Tests (Z. 212, 219, 244).
- Kommentar-POST bleibt `outcomeUncertain` (Doppelpost-Schutz) — korrekt, denn nur hier kann das Remote-Artefakt ungewiss sein; der Marker-Reconcile löst den Erfolgsfall auf (`ReconcileResponseAsync`, EventResponses Z. 26–35, Containment Z. 32).
- `Failed` → echtes Re-Execute ist verifiziert: `ScmEventOutbox.ExecuteAsync` Z. 37–48 führt `Sending`/`Uncertain` nur ab, bei `Failed` (uncertain=false) geht es über Reconcile → `Sending` → erneutes `ExecuteResponseAsync` (Z. 49–55). Der Nachholweg für ein `Verarbeitet`-Ereignis mit fälliger Nicht-`Succeeded`-Aktion existiert real: Monitoring-Selektion `ScmEventMonitoringService.cs` Z. 235–239 → `DispatchCoreAsync` → erweiterte Hostaktionen-Schleife (Orchestrator Z. 102–103, Filter `waiting || attachments` vor dem `Verarbeitet`-Early-Return Z. 104) bzw. `RetryAsync` (Z. 268–278, setzt `NextAttemptUtc` zurück, funktioniert auf `Verarbeitet`, da nur `Abgebrochen` abgefangen wird). Präzedenztest `ScmEventDispatchTests_HostRetry.CompletedEvent_RetriesHostResponseWithoutNewDispatch` (Z. 72–99) belegt genau diesen Pfad.
- Verwaiste Assets durch Re-Upload sind hingenommen und dokumentiert (Z. 170) — konsistent, da der Endpoint keinen Idempotenzschlüssel hat; die Begründung „der Preis der Zuverlässigkeit" ist nachvollziehbar.
- Das Restrisiko `Uncertain` beim Kommentar-POST ist jetzt ehrlich benannt (Z. 171: geerbte Bestandssemantik, beobachtbar über `Error`/`ErrorCode`, identisch zu `waiting:`/`ai:`-Aktionen).

Die Kernbegründung der Entscheidung (Z. 38) ist zudem am Bestand verifiziert: `ResendAsync` ist bei `Verarbeitet` tatsächlich unzulässig (Z. 308: nur `ZustellungUngewiss`/`VerarbeitungUngeklärt`), und `RetryAsync` setzt nur `NextAttemptUtc`, nicht den Status.

### Kleinere Anmerkungen aus Runde 1: **alle adressiert**

1. **Nicht-Wurf-Garantie** `CollectAttachmentsAsync`: jetzt explizit und vollständig spezifiziert (Z. 90–91), inkl. einzig propagierter `OperationCanceledException`. Die Begründung ist verifiziert korrekt: eine entweichende `IOException`/`UnauthorizedAccessException`/`JsonException` würde über den Catch in `DispatchCoreAsync` Z. 107–113 den soeben gesetzten `Verarbeitet`-Status nach `VerarbeitungUngeklärt` kippen.
2. **`AttemptId = null`**: jetzt als zwingende Regel dokumentiert (Z. 107) — verifiziert: `EnsureAsync` setzt `AttemptId` nur bei `purpose.StartsWith("ai:")` (`ScmEventOutbox.cs` Z. 23); ein befüllter `AttemptId` läge in der Mengenprüfung (Orchestrator Z. 428) und verhinderte `Verarbeitet`. Der Plan warnt ausdrücklich vor dem Befüllen „zur Vollständigkeit".
3. **Verifikations-Lane**: jetzt `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` + `--filter "Category!=OsInterface"` auch für `src/Softwareschmiede.IntegrationTests/` (Z. 197) — verifiziert nötig: `StartPs1IntegrationTests.cs` Z. 10 trägt `[Trait("Category", "OsInterface")]`.
4. **Abbruch-Race**: jetzt als geerbtes Fenster dokumentiert mit Milderung durch den neuen `IsCancelledAsync`-Prüfpunkt (Z. 103, 172).
5. **Detailkorrektheit**: Host-Vergleich jetzt `OrdinalIgnoreCase` (Z. 119); Tippfehler „Lokalfad" korrigiert (Z. 77 „Lokalpfad").

## Neu geprüfte Revisionsteile (stichprobenfrei verifiziert)

- **Einschub-Stelle:** `CollectAttachmentsAsync`+`EnsureAsync`/`ExecuteAsync` nach `item.Status = "Verarbeitet"` (Z. 433–434) liegt im `messages.Accepted`- und `Complete`-Erfolgszweig — nie bei `Diagnostic`, offenen Aktionsresultaten oder `noRemoteAction`-Verletzung, wohl bei begründetem `noRemoteAction` (Bedingung Z. 430). `complete` ist dort gebunden, `attempt`/`exchange`/`protokoll`/`responder` im Scope.
- **`ScmExchangeComplete.Attachments`** als letzter optionaler Positional-Parameter: quellkompatibel — alle 8 Konstruktionsstellen verifiziert (4× `ScmEventExchangeServiceTests`, 3× `ScmEventDispatchTests_AgentActions`, 1× `ScmTaskCompletionMonitoringServiceTests`); `UnmappedMemberHandling.Disallow` (Z. 91) macht die Schema-Erweiterung zwingend, Feld-Validierung in `ReadAsync` Z. 257–263 fängt Strukturfehler als `Diagnostic` über den bestehenden Catch (Z. 267–270) — konsistent.
- **`PrepareAsync`** Z. 186–187: `screenshots/` mit identischem `CreatePrivateDirectory(…, allowedSids)`-Profil — nötig, damit der eingeschränkte CLI-Prozess dort schreiben darf; Alt-Versuche ohne Verzeichnis → leere Collection.
- **Contracts:** `ScmResponseRequest`/`ScmEventRepository` (ScmEvents.cs Z. 58–61, 15–17) — optionale Schlussparameter, `JsonSerializerDefaults.Web` ohne `UnmappedMemberHandling` (ScmEventStore Z. 23) → alte Datensätze deserialisieren mit `null`. `ActionId`-Regex in `ValidateResponse` (Z. 112) akzeptiert die GUID-`D`-Form der deterministischen `ActionId(item.Id, "attachments")`.
- **Testplan:** `fixture.Responses` ist `List<ScmResponseRequest>` (ScmDispatchFixture Z. 45, 76–77) — die Dispatch-Tests assertieren den `Attachments`-tragenden Request direkt; Dateiablagen ins `ExchangePath` sind etabliertes Muster (`ScmEventDispatchTests_AgentActions` Z. 126–131); `Identity(api)`-Setup (`GitHubEventSourceTests` Z. 633–637) existiert wie beschrieben; `HatGueltigeDateisignatur`-Konvention (`BenachrichtigungsEinstellungenService` Z. 210) existiert.

## Neue Muss-Lücke

### Lücke 3 — Residual der Uncertain-Mechanik: nicht typisierte Ausnahmen im Upload-Teilschritt verlieren den Nachweis ebenfalls still

Die Lücke-2-Korrektur deckt alle **typisierten** Fehlerpfade ab (`ScmProviderException` mit `outcomeUncertain: false`). Die Outbox markiert eine Aktion aber auch dann `Uncertain`, wenn `ExecuteResponseAsync` eine **andere** Ausnahme wirft, solange der Aktionsstatus bereits `Sending` ist (`ScmEventOutbox.cs` Z. 67: `… || (action.Status == "Sending" && failure is null) ? "Uncertain" : "Failed"`). Der Status wird unmittelbar vor dem Execute auf `Sending` gesetzt und persistiert (Z. 51–53) — **jede nicht-`ScmProviderException` zwischen Marker-Reconcile und Kommentar-POST landet daher auf `Uncertain`, wird fortan nur noch abgeglichen und nie wieder ausgeführt.** Da der Kommentar nie gepostet wurde, findet `ReconcileResponseAsync` nie einen Marker: identischer Verlustmechanismus wie Lücke 2 — ohne Eskalationsweg, da das Ereignis bereits `Verarbeitet` ist und `ResendAsync` dort unzulässig ist.

Der Plan führt neue, potenziell ungetypt werfende Codepfade genau in dieses Fenster ein:

- **Antwort-Parsing des Uploads:** `JsonDocument.Parse`/`GetProperty("url")` auf den Body einer 2xx-Antwort wirft `JsonException`/`KeyNotFoundException`/`InvalidOperationException`. Die geplante URL-Validierung („sonst `Permanent`", Z. 54, 134, 159) greift erst **nach** erfolgreichem Parsen — ein nicht-JSON- oder umstrukturierter Body entgeht ihr. Ausgerechnet für den explizit als undokumentiert/reverse-engineered markierten Endpoint ist eine veränderte Antwortform der wahrscheinlichste Drift-Fehlerfall — und der Plan verspricht für genau diesen Fall fälschlich „`Permanent`-/`Temporary`-Fehlerbild … Nachweis schlägt dann sauber fehl statt still zu unterbleiben" (Z. 169). Tatsächlich entstünde `Uncertain` ohne Re-Execute.
- **Konventionsfalle:** Das naheliegende Implementierungsmuster `EventApiAsync` (Events.cs Z. 304–306) wandelt unleserliche Schreib-Antworten in `Temporary` **mit `outcomeUncertain: true`** um. Ein Implementierer, der `SendUploadApiAsync`/die `url`-Extraktion „analog" baut — wie der Plan es für die Fehlermatrix vorsieht —, würde die gerade geschlossene Lücke über diesen Pfad wieder einbauen.
- **Restpfade:** Weitere nicht typisierte Ausnahmen in `ComposeBodyAsync`/`UploadAsync`/`SendUploadApiAsync` (z. B. `InvalidOperationException` aus `Required`-Hilfen, unerwartete `Uri`-/`Content`-Fehler) treffen dieselbe Klausel. Datei-IO ist bereits abgedeckt (Plan: „Lesefehler → Übersprungen-Zeile"), die Abdeckung endet aber dort.

Erforderliche Planergänzung (eine Klausel genügt): **Der Upload-Teilschritt darf keine nicht typisierten Ausnahmen propagieren** — jeder Fehlerausgang zwischen Reconcile und Kommentar-POST (Transport, HTTP-Status, Antwort-Parsing/Feldextraktion, sonstige Laufzeitfehler) muss in eine `ScmProviderException` mit `outcomeUncertain: false` (i. d. R. `Temporary`, bei ungültiger `url` `Permanent`) oder einen `Skipped`-Hinweis gewandelt werden; explizit benennen, dass die `EventApiAsync`-Mutation-Parse-Konvention (`outcomeUncertain: true`) hier **nicht** gilt. Damit hält die im Plan postulierte Invariante „nur der Kommentar-POST erzeugt `Uncertain`" auch für nicht-HTTP-Fehlerbilder.

## Kleinere Anmerkungen (kein Blocker)

1. **Fixture-Dateiname:** `configuration.CallsPath` zeigt auf `scm-calls.jsonl` (`ScmE2ETestConfiguration.cs` Z. 13), nicht `calls.jsonl` (Plan Z. 123, zweimal) — Property-Referenz stimmt, nur der Dateiname im Klammerzusatz ist falsch.
2. **`id` optional lesen:** `NumericRepositoryId` aus dem `repos`-Metadatenfeld `id` muss **optional** extrahiert werden (`String`-Hilfe, nicht `Required`) — sonst brächen alle Repos-Mocks ohne `id`-Feld (`Identity` Z. 636, `ScmE2EApiClient` Z. 46–47) und nicht nur neue Tests. Die geplante Nachlade-Fallback-Logik impliziert das bereits; ein Halbsatz verhindert eine `Required`-Implementierung mit Folge-Testbrüchen. Zahl → invariante Zeichenfolge beachten (`GetInt64`/`GetRawText`, nicht `GetString`).
3. **Crash-Residual `Sending` (geerbt):** Überlebt ein Prozessabsturz die Persistierung von `Status = "Sending"` (Outbox Z. 51–53) mitten im Upload, bleibt die Aktion reconcile-only — für den Nachweis ohne je geposteten Kommentar final verloren. Identische Bestandssemantik wie bei `ai:`/`waiting:`-Aktionen, nur mit anderem Ergebnis (dort kann der Reconcile das Remote-Artefakt finden). Edge-Fall (Sekundenfenster + Prozesstod); ein Satz im Risikoabschnitt würde ihn neben dem Kommentar-POST-Residual ehrlich machen.
4. **Reconcile vor Upload ist korrekt verortet:** `ComposeBodyAsync` läuft erst im Endpoint-Switch nach `ReconcileResponseAsync` (Z. 50) → kein Upload bei `AlreadyExisted`. Nur der Vollständigkeit halber: Die Reihenfolge „erst Reconcile, dann lesen+hochladen, dann POST" ist im Plan implizit richtig und sollte bei der Umsetzung nicht umgedreht werden.

## Bewertung je Prüfaspekt

| Aspekt | Befund |
|--------|--------|
| Lücke 1 (ScmE2EApiClient) | **Geschlossen** — benannt, lokalisiert, Stub-Verhalten festgelegt |
| Lücke 2 (Uncertain-Semantik, typisierte Fehler) | **Geschlossen** — konsistent, am Outbox-/Monitoring-Bestand verifiziert |
| Residual Uncertain (untypisierte Ausnahmen) | **Lücke 3** — Parse-/Restpfade erzeugen weiterhin `Uncertain` ohne Re-Execute; Plan-Risikotext beschreibt den Fall falsch |
| Kleinere Anmerkungen Runde 1 | Alle fünf adressiert |
| Muss-Anforderungen / Scope / Sicherheit / Idempotenz / Tests | Unverändert vollständig abgedeckt und verifiziert (s. plan-check.1.md) |

## Fazit

**Plan lückenhaft — eine verbleibende, kleine Korrektur.** Beide Runde-1-Muss-Lücken sind sauber geschlossen und sämtliche Nebenbefunde adressiert; die Architektur bleibt unverändert tragfähig. Vor der Umsetzung fehlt nur noch die explizite Festlegung, dass der Upload-Teilschritt **keinerlei nicht typisierte Ausnahmen** propagiert — insbesondere nicht das Antwort-Parsing des undokumentierten Endpoints —, weil die Outbox solche Ausnahmen im Status `Sending` zu `Uncertain` (reconcile-only) statt `Failed` (Re-Execute) markiert und der Nachweis damit über denselben Mechanismus wie in Lücke 2 still verloren ginge. Alle anderen Aspekte sind umsetzungsreif.
