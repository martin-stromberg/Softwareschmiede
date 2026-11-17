# Plan-Prüfung: Screenshot-Nachweis — Bilder aus CLI-Läufen hostseitig hochladen

**Status: Plan lückenhaft**

Geprüft wurde [plan.md](plan.md) gegen [requirement.md](requirement.md), [inventory.md](inventory.md) und die Detaildokumente `inventory/{austauschprotokoll,github-plugin,orchestrierung-und-prompt,tests-und-doku}.md` sowie gegen den tatsächlichen Codebestand auf diesem Branch. Der Plan wurde nicht geändert; dieses Dokument ist ausschließlich Befund.

Der Plan ist im Kern technisch fundiert: Alle geprüften Zeilenreferenzen stimmen, die neun offenen Fragen sind mit nachvollziehbaren, am Bestand verifizierten Standardentscheidungen beantwortet, und die Architektur (Orchestrator sammelt, dedizierte Outbox-Aktion, Plugin lädt hoch, eigener Nachweis-Kommentar) passt sauber in die vorhandene Outbox-/Marker-Semantik. Zwei Befunde erfordern jedoch eine Plankorrektur vor der Umsetzung: ein übergangener, build-brechender Artefakt und eine Fehlersemantik, die den Nachweis im häufigsten transienten Fehlerfall still verlieren würde.

## Verifizierte Stärken

### Abdeckung der Muss-Anforderungen

- **Ablage-Konvention (beide Wege):** `screenshots/` wird in `PrepareAsync` nach `ScmEventExchangeProtocol.cs` Z. 186–187 mit identischem `CreatePrivateDirectory`-Profil erzeugt; das `attachments`-Feld erweitert `ScmExchangeComplete` (Z. 52–54) als letzten optionalen Positional-Parameter — quellkompatibel, verifiziert an allen 8 Konstruktionsstellen (`ScmEventExchangeServiceTests`, `ScmEventDispatchTests_AgentActions`, `ScmTaskCompletionMonitoringServiceTests`). `UnmappedMemberHandling.Disallow` (Z. 91) macht die Schema-Erweiterung zwingend — der Plan berücksichtigt sie.
- **Upload-Endpoint und `repository_id`:** `GET /repos/{o}/{r}` liefert die numerische `id` heute tatsächlich mit, sie wird nur nicht gelesen (`GitHubPlugin.Events.cs` Z. 43 nutzt `node_id`); die Plan-Erweiterung `NumericRepositoryId` ist minimal und korrekt lokalisiert. `uploads.github.com` ist mit `BuildRequestUri` (Z. 63–72) tatsächlich nicht abbildbar — ein neuer `UploadAsync`-Pfad ist nötig, kein Workaround möglich.
- **Einbettung:** Der dedizierte Nachweis-Kommentar ist eine vertretbare Auslegung der Anforderung: Ein hostseitiger „Abschlussbericht" existiert faktisch nicht (`complete.Report` wird nie versendet, Orchestrator Z. 426–436); Kanalaktions-Bodies sind zum `complete.json`-Zeitpunkt bereits remote und würden den `RequestJson`-Integritätsvergleich (Z. 409–415) auslösen — Ausschluss #7 ist korrekt begründet.
- **Auslösendes Ereignis:** Die Platzierung im akzeptierten Complete-Erfolgszweig (Z. 430–435) ist verifiziert korrekt: `CollectAttachmentsAsync` läuft nur, wenn `item.Status == "InBearbeitung"` und die Mengenprüfung erfolgreich war — nie bei `Diagnostic`, `VerarbeitungUngeklärt` oder offenen Aktionsresultaten.
- **Token:** `ClientFor(repository.Konto)` (Events.cs Z. 334–341) deckt beide Kontomodi ab; kein neuer Authentifizierungspfad.
- **Keine Ablagen → kein Verhalten:** Leere Collection → keine Outbox-Aktion → kein Kommentar, keine Textänderung — deckt die Anforderung exakt ab.

### Scope-Ausschlüsse

Alle sieben Ausschlüsse sind im Issue bzw. Bestand begründet und konsistent: kein Agenten-Upload, kein Repo-Commit, kein Browser-Flow, kein `PATCH /pulls/{n}` (kein Editierpfad existiert — verifiziert), keine unscharfe Plausibilitätsheuristik, kein unverifizierter GHE-Endpoint (ehrlicher `Permanent`-Fehler statt geratenem Pfad), keine Einbettung in bereits versendete Kanalaktions-Bodies.

### Sicherheit des lokalen Dateizugriffs

- Containment-Regel (`GetFullPath` + Versuchsverzeichnis-Präfix, plattformangepasster Vergleich) + `EnsureSafePath` (Z. 391–396, Reparse-Point-Verbot je Pfadglied) auf jedem Kandidaten — verhindert `../`-, Absolute- und Symlink-Ausbrüche. `VerifyPrivateDirectory` auf `screenshots/` erbt das Rechteprofil; Alt-Versuche ohne Verzeichnis → leere Collection (kein Kippen eines akzeptierten Abschlusses).
- Dateivalidierung folgt der `BenachrichtigungsEinstellungenService`-Konvention: Erweiterungs-Whitelist (png/jpg/jpeg/gif/webp), Magic-Bytes, 10 MiB, ≤ 16 Dateien; SVG bewusst ausgeschlossen. Deklarierte Pfade können faktisch nur Bilddateien im Versuchsverzeichnis adressieren — Kanaldateien (`request.json`, `actions/*.json`, `results/*.json`) fallen an der Erweiterungs-Whitelist.
- `name`-Parameter/Alt-Text sanitisiert (`[A-Za-z0-9._-]`, ≤ 100), Caption ≤ 1000 mit Steuerzeichen-Entfernung — kein Markdown-/Query-Injection-Vektor.

### Upload-/Token-Sicherheit

- Bearer geht ausschließlich an den abgeleiteten Origin `uploads.github.com` und nur wenn `request.Host == "github.com"`; andere Hosts → `Permanent` **vor** Netzaktivität. `Authorization` wird vom `HttpClient` bei Cross-Host-Redirects nicht mitgesendet; Token nie in URL/Log/Fehlertext — konsistent mit der `ValidateHost`-Dokumentation (Z. 74–76) und den `GitHubHttpApiClientTests`-Konventionen (Z. 182–203).
- `byte[]`-Payload ≤ 10 MiB; Plugin-eigenes Limit (`MaxUploadBytes`), da das Plugin korrekt nur `Softwareschmiede.Plugin.Contracts` referenziert (csproj verifiziert) — die Begründung stimmt.

### Idempotenz

Vier Anker, alle am Bestand verifiziert: (a) `ScmEventStore.ActionId(item.Id, "attachments")` ist deterministisch (SHA-256, `ScmEventStore.cs` Z. 28–29) und `EnsureAsync` dedupliziert (`ScmEventOutbox.cs` Z. 17–18); (b) Marker-Reconcile (`ReconcileResponseAsync`, EventResponses Z. 26–35) vor jedem POST verhindert Doppelkommentare; (c) der persistierte `RequestJson`-Snapshot friert die Dateimenge ein — dokumentierte Grenze für nachträglich abgelegte Dateien; (d) `Purpose = "attachments"` erhält `AttemptId = null` (`EnsureAsync` Z. 23) und ist damit korrekt **aus** der Complete-Mengenprüfung (Orchestrator Z. 428) herausgenommen — wäre `AttemptId` gesetzt, würde das Ereignis nie `Verarbeitet` erreichen. Der Retry-Pfad für ein `Verarbeitet`-Ereignis mit fälliger Nicht-`Succeeded`-Aktion existiert real: `ScmEventMonitoringService` Z. 235–239 selektiert genau diesen Fall, Präzedenz `ScmEventDispatchTests_HostRetry.CompletedEvent_RetriesHostResponseWithoutNewDispatch`. Der Exchange-Poll schließt `Verarbeitet` zwar aus (`ScmEventExchangeService.cs` Z. 27), der Monitoring-Pfad übernimmt — der Plan trifft hier die richtige Annahme.

### Einschaltpunkt im Orchestrator

Verifiziert: Der Code-Einschub nach `item.Status = "Verarbeitet"` (Z. 433–434) liegt vor `LinkReportedPullRequestAsync` (Z. 440–441), die Timeout-Prüfung (Z. 449–458) betrifft `Verarbeitet` nicht, und `db.SaveChangesAsync` (Z. 461) persistiert Endstatus + Protokoll. `outbox.ExecuteAsync` fängt `ScmProviderException` intern ab (Outbox Z. 64–73) — kein Propagationspfad, der den Abschluss kippen könnte.

### Plugin-/Contracts-Kompatibilität

- `ScmResponseRequest.Attachments` und `ScmEventRepository.NumericRepositoryId` als letzte optionale Positional-Parameter: Quell- und JSON-kompatibel (`ScmEventStore.JsonOptions` = `JsonSerializerDefaults.Web` ohne `UnmappedMemberHandling`, `ScmEventStore.cs` Z. 23 — alte Datensätze → `null`, der Plugin-Fallback auf frische `GET /repos`-Auflösung ist vorgesehen). `IScmEventResponder` bleibt unverändert — verifiziert (`IScmEventResponder.cs` Z. 6–13).
- BitBucket-Abgrenzung stimmt: `plugin is not IScmEventResponder` fängt in `DispatchCoreAsync` Z. 89 bereits ab; BitBucket-Ereignisse erreichen nie einen zugestellten Versuch.

### Testplan und reale Abnahme

- Der Testplan ist konkret und an reale Muster gebunden: temp-Git-Repo-Tests (`ScmEventExchangeServiceTests`), Queue-Fake (`FakeGitHubEventApi`), `ScmDispatchFixture` mit direktem Schreiben von `accepted.json`/`complete.json` ins `ExchangePath` (Präzedenz `ScmEventDispatchTests_AgentActions` Z. 129–137), HostRetry-Muster, `Identity(api)`-Setup (GitHubEventSourceTests Z. 633–637 — der `repos`-Mock muss für die neuen Tests das `id`-Feld ergänzen, was der Plan implizit vorsieht).
- Keine neuen FlaUI-E2E-Tests ist die richtige Entscheidung (keine UI-Änderung; CLAUDE.md-Konsolidierungsregel respektiert). Die Pflicht zur manuellen realen GitHub-Abnahme (undokumentierter Endpoint) ist korrekt erkannt und in vier Szenarien (a–d) operationalisiert.

## Festgestellte Lücken

### Lücke 1 — Übergangener Interface-Implementierer `ScmE2EApiClient` (build-brechend)

`IGitHubEventApiClient` hat **drei** Implementierer, der Plan nennt zwei (`GitHubHttpApiClient`, `FakeGitHubEventApi`). Der dritte ist `src/Softwareschmiede.App/Services/Testing/ScmE2EApiClient.cs` Z. 9–10 — der Fixture-Transport der FlaUI-E2E-Pipeline, instanziiert von `ScmE2EPluginManager.cs` Z. 28. Sobald `UploadAsync` zum Interface hinzukommt, kompiliert `Softwareschmiede.App` nicht mehr. Erforderliche Planergänzung: `ScmE2EApiClient` in die Artefaktliste aufnehmen und ein Stub-Verhalten festlegen (z. B. Aufruf in `calls.jsonl` protokollieren und eine Fixture-`url` zurückgeben oder `ScmProviderException(Permanent)` werfen — die E2E-Suite erzeugt keine Screenshots, beides ist sicher).

### Lücke 2 — `Uncertain`-Semantik: Der Nachweis geht im häufigsten Fehlerfall still verloren

Der Plan markiert den Upload-POST explizit als `outcomeUncertain` (Transport-/Timeout-Fehler Z. 117; ≥ 500 Z. 129) und behauptet gleichzeitig, „`RetryAsync` bzw. die erweiterte Hostaktionen-Schleife führt die Aktion erneut aus" und „bereits hochgeladene Dateien werden beim Retry erneut hochgeladen". Das widerspricht der realen Outbox-Semantik (`ScmEventOutbox.cs` Z. 37–48): Eine Aktion im Status `Sending`/`Uncertain` wird **nur noch abgeglichen, niemals erneut ausgeführt** (`found is null && uncertain` → erneut `Uncertain` mit Backoff). `RetryAsync` (Orchestrator Z. 268–278) setzt nur `NextAttemptUtc` zurück, nicht den Status — auch manueller Retry ändert daran nichts.

Konsequenz: Ein einziger `outcomeUncertain`-Fehler beim Upload (genau der für einen binären POST über das Netz wahrscheinlichste transiente Fall) lässt die `attachments`-Aktion dauerhaft auf `Uncertain` stehen. Da der Kommentar-POST nie erfolgte, findet das Marker-Reconcile nie etwas — der Screenshot-Nachweis wird **nie** geliefert, und zwar ohne Eskalationsweg: Das Ereignis steht bereits auf `Verarbeitet`, `ResendAsync` ist dort nicht zulässig (Z. 308), und anders als bei `ai:`-Aktionen wartet kein Agent auf ein `results/*.json`, das den Fehler zurückspiegelt. Für ein Feature, dessen Kernversprechen zuverlässige Lieferung ist („Vollzug statt Befolgung"), ist das ein funktionaler Verlust im Regelfall, den der Plan weder benennt noch auflöst — er suggeriert sogar das Gegenteil.

Erforderliche Entscheidung im Plan (zwei sachlich vertretbare Optionen):
- **Upload-Teilschritt nicht `uncertain` markieren** (Fehler → `Failed` + echtem Re-Execute): Das einzig ungewisse Remote-Ergebnis ist der Kommentar-POST; ein verwaistes Asset nach erneutem Upload ist bereits als hingenommen dokumentiert — die Logik „Orphans sind akzeptabel" des Plans spricht sogar dafür.
- Oder den Verlust **ehrlich dokumentieren** und einen Beobachtungsweg benennen (z. B. Protokolleintrag/UI-Sichtbarkeit der dauerhaft `Uncertain`-Aktion), falls die strenge Reconcile-only-Semantik bewusst beibehalten wird.

Der Kommentar-POST selbst muss in beiden Varianten `outcomeUncertain` bleiben (Doppelpost-Schutz ist korrekt).

## Kleinere Anmerkungen (kein Blocker)

1. **`CollectAttachmentsAsync` „wirft nicht":** Der Plan verlangt zu Recht, dass Integritätsfehler in `Skipped` statt in eine Ausnahme münden — Restpfade (z. B. `Directory.GetFiles`-I/O-Fehler) würden über den äußeren Catch (Orchestrator Z. 108–113) den soeben akzeptierten Abschluss doch noch in `VerarbeitungUngeklärt` kippen. Die Implementierung muss die Nicht-Wurf-Garantie tatsächlich vollständig einhalten; ein Satz dazu im Plan würde die Falle dokumentieren.
2. **`AttemptId = null`-Abhängigkeit unkommentiert:** Die Korrektheit des Ausschlusses aus der Complete-Mengenprüfung hängt implizit am `purpose.StartsWith("ai:")`-Zweig in `EnsureAsync` Z. 23 — ein Hinweis im Plan verhindert, dass ein Implementierer den `AttemptId` „zur Vollständigkeit" befüllt und damit `Verarbeitet` unmöglich macht.
3. **Verifikations-Lane:** `dotnet test src/Softwareschmiede.IntegrationTests/` ungefiltert enthält `StartPs1IntegrationTests` (`Category=OsInterface`, umgebungssensitiv); in dieser Sandbox ist ein Filter `Category!=OsInterface` bzw. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` analog zur regulären Lane ratsam.
4. **Vererbte Abbruch-Race (Bestandsmuster, nicht neu):** `EnsureAsync` persistiert via eigenem `SaveChangesAsync` den getrackten `item`-Zwischenstand (`Verarbeitet`) und kann einen zwischen den `IsCancelledAsync`-Prüfpunkten eintreffenden `Abgebrochen`-Schreibvorgang überschreiben — identisches Fenster existiert heute bei `ai:`/`waiting`-Aktionen; keine neue Schwäche, aber der neu eingefügte Block erweitert die Strecke geringfügig.
5. **Detailkorrektheit:** `request.Host == "github.com"` sollte als `OrdinalIgnoreCase`-Vergleich spezifiziert werden; Tippfehler „Lokalfad" (Plan Z. 76) — trivial.

## Bewertung je Prüfaspekt

| Aspekt | Befund |
|--------|--------|
| Muss-Anforderungen | Vollständig abgedeckt; alle Entscheidungen begründet |
| Scope-Ausschlüsse | Vollständig und korrekt begründet |
| Lokaler Dateizugriff | Sicher: Containment + Reparse-Verbot + ACL-Profil + Typ-/Größen-Whitelist |
| Upload-/Token-Sicherheit | Sicher: fester Origin, Host-Gate vor Netzwerk, kein Token in URL/Logs |
| Idempotenz | Vier verifizierte Anker; Snapshot-/Dedup-Semantik korrekt |
| Einschaltpunkt Orchestrator | Verifiziert korrekt platziert; `AttemptId`-Ausschluss implizit richtig |
| Plugin-/Contracts-Kompatibilität | **Lücke 1** — `ScmE2EApiClient` nicht erfasst (Build-Bruch) |
| Fehler-/Retry-Semantik | **Lücke 2** — `Uncertain` verliert den Nachweis still, Plan beschreibt es falsch |
| Testplan | Konkret, an reale Fixtures gebunden; reale E2E-Abnahme korrekt als manuell markiert |

## Fazit

**Plan lückenhaft.** Die Architektur und die fachlichen Entscheidungen sind belastbar und weitgehend punktgenau am Bestand verifiziert. Vor der Umsetzung sind zwei Punkte im Plan nachzuziehen: (1) `ScmE2EApiClient` als dritten `IGitHubEventApiClient`-Implementierer in die Artefakt-/Änderungsliste aufnehmen inkl. festgelegtem Stub-Verhalten, und (2) die Fehlersemantik des Upload-Teilschritts so festlegen, dass ein `outcomeUncertain`-Fehler den Nachweis-Kommentar nicht dauerhaft unzustellbar macht — oder den Verlust explizit als akzeptiertes Verhalten samt Beobachtbarkeit dokumentieren. Beide Korrekturen sind klein und ändern nichts an der Grundarchitektur.
