# Übersetzte Anforderung: Ausführungsbegrenzung — Platz bei wartender CLI am Eingabeprompt freigeben

**Quelle:** GitHub-Issue #431 / Aufgaben-ID `1102930a-65ae-4b2c-b1c6-afda71922ba5` (`issue.md`)
**Branch:** `task/issue-431-1102930a65ae4b2cb1c6afda71922ba5-ausfuehrungsbegrenzung-platz-b`

> **Namenshinweis:** Der Issue-Text verwendet noch die deutschen Bezeichner aus der Zeit vor der Code-Sprachfinalisierung. Gemeint sind die aktuellen englischen Namen: `ScmEventExecutionLimitService.GetBelegungAsync` → `GetOccupancyAsync`, `CliAusfuehrungsBelegung` → `CliExecutionOccupancy`, `Aufgabe` → `WorkItem`, `PausiertBisUtc` → `WorkItem.PausedUntilUtc`, `Aufgabe.LaufStatus`/`AufgabeLaufStatus.WartetAufEingabe` → `WorkItem.RunStatus`/`WorkItemRunStatus.WaitingForInput`, `CliRuntimeStatus.WartetAufEingabe` → `CliRuntimeStatus.WaitingForInput`, `KiAusfuehrungsService` → `AiExecutionService`, `AppEinstellung(Service)` → `AppSetting(Service)`, `KiPluginPrefix` → `AiPluginPrefix`.

## Fachliche Zusammenfassung

Die Belegungszählung der SCM-Ausführungsbegrenzung (`ScmEventExecutionLimitService.GetOccupancyAsync`) erhält neben der Pausenfreigabe einen zweiten Freigabezustand: Eine Aufgabe belegt weder im globalen noch im Plugin-Zähler einen Platz, wenn ihre gebundene SCM-Promptsitzung einen **verifizierten** Eingabeprompt zeigt (`IScmPromptSessionAdapter.IsAlive && ReadyStatus == ScmPromptReadyStatus.Ready`) **und** kein unverarbeitetes `ScmIncomingEvent` der Aufgabe existiert (`Status ∉ {"Verarbeitet", "Abgebrochen"}`). Beim Übergang in diesen Wartezustand wird die ereignisgesteuerte Startprüfung (`ScmStartCheckTrigger`) angefordert, damit das nächste startbare Ereignis nicht erst auf den Poll wartet. Kehrt eine freigegebene Aufgabe zur Arbeit zurück, zählt sie wieder als belegt — das Limit begrenzt wie bisher ausschließlich Starts, nie laufende Prozesse; die dadurch mögliche zeitweise Überschreitung ist toleriert und zu dokumentieren.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein Belegungsplatz der SCM-Ausführungsbegrenzung — konkret die Zählbewertung einer einzelnen laufenden CLI-Ausführung (lebendes Prozess-Handle einer Aufgabe) im globalen und im Plugin-Zähler, ausgewertet bei jeder Start-Zulassungsprüfung.
- **Auslösende Ereignisse:**
  - **Bewertungszeitpunkt:** Jede Startzulassung (`CheckAndReserveStartSlotAsync` / `GetStartBlockReasonAsync`) liest die Belegung live neu — die erweiterte Freigaberegel wirkt an genau dieser Stelle, ohne eigenen Cache oder Zeitpunkt.
  - **Freigabesignal:** Übergang der Aufgabe in den Wartezustand (verifizierter Prompt `Ready` und kein offenes Ereignis) → Anforderung der Startprüfung über `ScmStartCheckTrigger.RequestStartCheck()` — zusätzlich zu den Bestandsauslösern (sinkende Laufzählung via `IRunningAutomationStatusSource.RunningCountChanged`, Pausen-Setz-/Aufhebepfade `WorkItemService.SetPauseAsync`/`ScmEventStore`/`AiPluginLimitService`, `ScmEventOrchestrator.CancelAsync`).
  - **Sicherheitsnetz:** Der Poll-Zyklus des `ScmEventMonitoringService` bleibt als langsamere Wiederholung bestehen.
- **Erwartete Wirkung:**
  - Die Ereignis-Queue verhungert nicht mehr: Im Zielszenario (Plugin-Limit `plugins.maxparallel.Softwareschmiede.Devin = 1`) gibt eine fertige, am `❭`-Prompt wartende Aufgabe ihren Platz frei und das nächste wartende Ereignis startet zeitnah — ohne manuelles Beenden, Pausieren oder PR-Merge.
  - **Konservativ weiter zählend:** keine SCM-Promptsitzung, `ReadyStatus` `Unknown`/`TrustPrompt`/`Busy`/`Exited`, Prozess-Handles ohne auflösbare Aufgabenzeile → weiterhin voll belegt.
  - **Pending-Guard:** Solange ein `ScmIncomingEvent` der Aufgabe nicht in einem Endstatus steht (z. B. `Aufgenommen`, `Wartend`, `Verzögert`, `Zugestellt`, `InBearbeitung`, `ZustellungUngewiss`, `VerarbeitungUngeklärt`), bleibt der Platz belegt — auch bei sichtbarem `Ready`. Damit gibt ein frisch gestarteter Lauf im Zustellfenster (Kanalprobe / Promptversand, `MarkInputStarted` → `Busy`) den Platz nicht vorzeitig frei, und die Queue startet nicht kaskadenartig parallel.
  - **Rückkehr zur Arbeit:** Trifft eine Eingabe/ein Folgeprompt ein (`MarkInputStarted` → `Busy`, neues offenes Ereignis) oder verliert die Session den `Ready`-Nachweis, zählt die Aufgabe bei der nächsten Bewertung wieder als belegt.
- **Umgang mit fehlenden Objekten:**
  - Kein `IScmPromptSessionAdapter` für die Aufgabe registriert (klassischer Start ohne SCM-Startvertrag, Sitzung ohne Adapter) → konservativ belegt, kein Fehler.
  - Keine `ScmIncomingEvent`-Zeilen zur Aufgabe → kein Pending → freigabefähig, sofern `Ready` verifiziert.
  - `ScmStartCheckTrigger` nicht registriert (manuelle DI-Konstruktionen in Tests) → optionaler Parameter wie bei den bestehenden Pausenpfaden, kein Fehler.
- **Scope-Ausschlüsse:**
  - **Kein** Freigabekriterium über `WorkItemRunStatus.WaitingForInput`/`CliRuntimeStatus.WaitingForInput` — das ist nur eine I/O-Ruhe-Heuristik (~4 s ohne Terminal-I/O) und tritt auch mitten in einem arbeitenden Turn auf; als Freigabekriterium würde es das Limit faktisch aufheben. Es kommt höchstens als Auslösesignal für die Startprüfung in Betracht, niemals als Zählkriterium. *(Status: bestätigt — explizite Issue-Vorgabe.)*
  - Kein Beenden, Drosseln oder anderweitiges Eingreifen in den am Prompt wartenden CLI-Prozess — der Prozess läuft bewusst weiter; der fachliche Abschluss bleibt Aufgabe von `ScmTaskCompletionMonitoringService` (nach PR-Merge/Issue-Schluss + Wartezeit) oder des Anwenders. *(Status: bestätigt — Issue.)*
  - Keine Änderung an Limit-Konfiguration (`scm.execution.maxparallel`, `plugins.maxparallel.<prefix>`), Priorisierung (`ScmDispatchPriority`), Reservierungssemantik (`ScmStartSlotReservationPool`), `BlockReason`-Sperren oder der Unbegrenztheit reiner Hostaktionen/Reconcile-Versuche.
  - Keine UI-/Anzeigeänderung — die „Wartet"-Anzeige der Kachel (`WorkItemRunStatus.WaitingForInput`) bleibt unverändert rein informativ.
  - Keine neue Konfiguration und keine Migration.

## Betroffene Klassen und Komponenten

- `ScmEventExecutionLimitService` (`src/Softwareschmiede/Application/Services/ScmEventExecutionLimitService.cs`): `GetOccupancyAsync` (Z. 189–214) — die Aufgaben-Projektion um Session- und Pending-Prüfung erweitern; XML-Doku von `CliExecutionOccupancy`/`GetOccupancyAsync` um die neue Semantik ergänzen. `CheckAndReserveStartSlotAsync`, `GetStartBlockReasonAsync`, `EffectiveOccupancyAsync` und `GetStartBlockReason` bleiben strukturell unverändert (sie profitieren automatisch von der geänderten Belegungszahl).
- `IScmPromptSessionAdapter` / `ScmPromptSessionAdapter` (`src/Softwareschmiede/Application/Services/ScmPromptSessionAdapter.cs`): `IsAlive` + `ReadyStatus` als Freigabesignal; bei Wahl eines dedizierten Auslösers ggf. ein neues öffentliches Übergangssignal (`ReadyStatus` → `Ready`, heute nur interner `_changed`-TCS in `SetReady`, Z. 375–382).
- `ICliExecutionLimitSource` (`src/Softwareschmiede/Application/Services/ICliExecutionLimitSource.cs`) / `AiExecutionService.GetScmPromptSession` (`AiExecutionService.cs` Z. 480) — liefert die Session je Aufgabe; bereits vorhanden und unverändert nutzbar.
- `CliProcessManager` (`src/Softwareschmiede/Application/Services/CliProcessManager.cs`): `OnRuntimeStatusChanged` (Z. 318–324) — möglicher Ankoppelpunkt für `RequestStartCheck()` beim Übergang in `CliRuntimeStatus.WaitingForInput`; bei dieser Variante optionaler `ScmStartCheckTrigger`-Konstruktorparameter (Muster wie bei `WorkItemService`, `ScmEventStore`, `ScmEventOrchestrator`, `AiPluginLimitService`).
- `ScmStartCheckTrigger` / `ScmStartCheckService` (`src/Softwareschmiede/Application/Services/ScmStartCheckService.cs`) — bestehender Mechanismus, unverändert wiederverwendet; die Doku-Kommentare (Z. 9–15, 26–35) sind um den neuen Auslöser zu ergänzen.
- `SoftwareschmiedeDbContext.ScmIncomingEvents` — Pending-Abfrage `x.WorkItemId == id && x.Status != "Verarbeitet" && x.Status != "Abgebrochen"` (Bestandsmuster: `ScmTaskCompletionMonitoringService` Z. 84–85/133–135, `ScmEventOrchestrator` Z. 182).
- Dokumentation (Liste aus dem Issue, plus Fundstelle): `README.md` (Z. ~30, ~187), `docs/help/einstellungen/beschreibung.md` (Z. 75), `docs/help/plugins/github-ereignisse/beschreibung.md` (Z. 11), `docs/help/plugins/github-ereignisse/ablauf-technisch.md` (Z. ~34), `docs/help/aufgaben/ablauf-technisch.md` (Z. ~521). **Zusätzlich gefunden:** `docs/help/plugins/github-ereignisse/business-rules.md` (Z. ~103–107) beschreibt „Pausierte Aufgaben belegen keinen Platz" ebenfalls ausführlich — Annahme: vom Issue-Abgleich („alle Stellen") mit umfasst.
- Tests: `src/Softwareschmiede.Tests/Application/Services/ScmEventExecutionLimitServiceTests.cs` (Unit-Tests mit `ICliExecutionLimitSource`-Mock + Adapter-Mock je `ReadyStatus`/Pending-Kombination), `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests_Limit.cs` und `ScmEventDispatchTests_Startcheck.cs` (`ScmDispatchFixture`, Simulator mit `SCM_READY_V1`-Prompt), ggf. `CliProcessManagerTests_RunStatus` für den Triggerpunkt.

## Implementierungsansatz

- **Belegungsregel:** In `GetOccupancyAsync` wird pro laufender Aufgaben-ID neben `PausedUntilUtc` zusätzlich die SCM-Session (`cli.GetScmPromptSession`) und der Pending-Stand bewertet. Die Freigabe greift analog dem Pausenfilter **vor** der globalen Zählung und der Plugin-Attribution: Eine Aufgabe mit `session?.IsAlive == true && session.ReadyStatus == ScmPromptReadyStatus.Ready` und ohne offenes `ScmIncomingEvent` entfällt aus beiden Zählern; Handles ohne Aufgabenzeile zählen weiterhin voll. Die Pending-Abfrage ist über die gesamte ID-Menge zu bündeln (eine Query), kein N+1-Zugriff je Aufgabe.
- **Auslöser der Startprüfung (vom Issue offen gelassen, zwei Varianten):**
  - **(a)** `CliProcessManager.OnRuntimeStatusChanged` fordert bei `CliRuntimeStatus.WaitingForInput` eine Startprüfung an — geringer Aufwand (Subscription besteht bereits); feuert auch mitten im Turn, was harmlos ist, weil die Belegungsauswertung die strengen Kriterien selbst erneut prüft.
  - **(b)** Dediziertes Session-Signal bei `ReadyStatus`-Übergang nach `Ready` — präziser, erfordert aber eine neue öffentliche Ereignisquelle am Adapter und deren Verdrahtung (z. B. über `AiExecutionService` → `ScmStartCheckTrigger`).
- **Randfall Trigger-Deckung:** Das kombinierte Freigabekriterium kann auch erst durch den Ereignis-Endstatus wahr werden — Session steht bereits auf `Ready`, dann wird das letzte offene Ereignis `Verarbeitet`. Ob hierfür ein zusätzlicher `RequestStartCheck()` am Abschluss der Ereignisverarbeitung nötig ist, ist in der Planung zu entscheiden; der Poll deckt den Fall andernfalls als Sicherheitsnetz ab.
- **Reservierungsfenster bleibt geschützt:** `ScmStartSlotReservationPool` und die atomare `CheckAndReserveStartSlotAsync` bleiben unverändert; ein gerade startender Lauf hält seinen Platz über die Reservierung, danach über den Pending-Guard (das auslösende Ereignis ist bis zum Abschluss unverarbeitet) und über `MarkInputStarted` → `Busy` — ein kaskadierender Parallelstart ist damit mehrfach abgesichert.

## Konfiguration

Keine neue Konfiguration. Das Verhalten gilt implizit unter den bestehenden Limits (`scm.execution.maxparallel`, `plugins.maxparallel.<prefix>`) und dem Laufzeit-Schalter der Ereignisüberwachung (`GetScmMonitoringEnabledAsync`); die dokumentierte Verhaltensschärfe (Freigabe am verifizierten Prompt, tolerierte Überschreitung bei Rückkehr zur Arbeit) ist in den genannten Doku-Dateien zu beschreiben.

## Offene Fragen

1. **Auslöse-Signal der Startprüfung:** Variante (a) `CliRuntimeStatus.WaitingForInput`-Übergang in `CliProcessManager` (einfach, aber heuristisch) vs. Variante (b) dediziertes `ReadyStatus → Ready`-Signal am `IScmPromptSessionAdapter` (präzise, mehr Verdrahtung) — ggf. kombiniert mit einem Trigger am Ereignis-Endstatus (Randfall „Session bereits Ready, letztes Ereignis wird `Verarbeitet`"). Intern zu entscheiden; der Issue lässt beide Wege ausdrücklich zu.
