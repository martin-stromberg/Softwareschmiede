# Abnahmeprüfung – Entwicklungsschritt 4

**Schritt:** Durchgängiger automatischer Start und Zustellung der neuen Aufgaben inklusive Wartend-Verhalten ohne Standard-CLI
**Branch:** `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u-schritt-4-auto-start-zustellung`
**Diff-Basis:** `task/issue-283-2b92467ce14e49ca92e372b84bb1e97f-github-projekte-vollstaendig-u...HEAD` (9 Dateien, +336/−10)

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

### Prüfung der Akzeptanzkriterien

**Kriterium 1 – Angelegte/wiederverwendete Aufgabe wird nach Sicherheits-/Owner-Prüfungen automatisch gestartet und an Standard-CLI zugestellt — nachweisbar E2E: erfüllt.**

- Die Behauptung „kein neuer Produktionscode nötig" ist verifiziert und plausibel: `ScmEventMonitoringService.RunOnceAsync` (`ScmEventMonitoringService.cs:129–138`) setzt für unverknüpfte Referenzen mit qualifizierter Beobachtung `aufgabeId` aus `ScmEventTaskResolver.ResolveAsync` und übergibt sie an `AcceptBatchAsync`; die Dispatch-Schleife (Z. 148–165) ruft für **jedes** nicht verarbeitete, fällige Ereignis `ScmEventOrchestrator.DispatchAsync` → `DispatchCoreAsync` auf — herkunftsunabhängig derselbe Pfad. Diff-Basis bestätigt: kein `src/Softwareschmiede/`-Produktionscode im Diff.
- `DispatchCoreAsync` (`ScmEventOrchestrator.cs:72–226`) enthält alle geforderten Elemente pro Zustellversuch: erneute Repository-/`CanWrite`-Prüfung (Z. 126–129), Owner-Revalidierung über `ScmEventValidationService.GetRejection` (Z. 130–131), Blocked-Prüfung (Z. 122–123, `BlockReason` Z. 387–389), `Neu → Gestartet` via `AufgabeService.SetStatusAsync` (Z. 132), Auflösungskette Aufgabe → Projekt-Default → globaler Default (`ResolveDevelopmentAutomationPluginWithProjectScopeAsync`, Z. 141; Implementierung `PluginSelectionService.cs:101–124`), Klon/Branch/CLI-Start über `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync`/`CliNeustartenAsync` (Z. 147–151), deduplizierte Zustellung über `DeliveryAttemptId`/`CliRunId` → `Zugestellt` (Z. 177–196).
- Integrationsnachweis des vollautomatischen Falls (CLI-Default konfiguriert → Anlage, Start und `Zugestellt` im selben Poll): `ScmEventMonitoringServiceTests_AutoStartDelivery.Monitor_CreatedTaskWithDefaultCli_StartsAndDelivers` (`ScmEventMonitoringServiceTests_AutoStartDelivery.cs:21–53`) — assertiert `Gestartet`/`Aktiv`, `LokalerKlonPfad` unter Root, `task/issue-9-…`-Branch, `KiPluginPrefix`, `Zugestellt` mit `DeliveryAttemptId`/`CliRunId`, genau eine Sitzung/ein Prompt.
- E2E-Nachweis der vollen Kette „Ereignis ohne Aufgabe → Aufgabe → gestartet → zugestellt → Verarbeitung via Antwortkanal": E8-Abschnitt (`E2E_ScmEvents.cs:317–338`) — angelegte Aufgabe wartet ohne CLI, Queue-Auswahl (`ScmEventOpen` + Plugin-Auswahl) startet echten Klon (`.git` unter `TestRoot`), Branch `task/issue-20-…`, `KiPluginPrefix = Softwareschmiede.KiSimulator`, Ereignis erreicht `Verarbeitet` (erfordert korrelierte Annahme + Abschluss über den echten Datei-Antwortkanal), exakt +1 Prompt mit Originaltext.
- Einordnung: Die rein automatische Variante (Default-CLI → Start+Zustellung im selben Poll) ist auf Integrationsebene nachgewiesen; E2E läuft die angelegte Aufgabe über den Queue-Auswahl-Einstieg in denselben `DispatchCoreAsync`-Pfad — konsistent zur dokumentierten Entscheidung (E2E-Fixture hat keinen globalen CLI-Default) und zur Konsolidierungsregel.

**Kriterium 2 – Ohne Standard-CLI: wartend mit bedienbarer Aufforderung; nach CLI-Auswahl/-Konfiguration + „Erneut prüfen" wird zugestellt: erfüllt.**

- Wartegrund exakt assertiert: `"Keine Standard-CLI konfiguriert. Ereignis öffnen und CLI auswählen."` (`ScmEventMonitoringServiceTests_AutoStartDelivery.cs:81`; Produktion Z. 143) — eindeutige, bedienbare Aufforderung; `WaitWithCommentAsync` postet zusätzlich den GitHub-Wartehinweis (receipt + waiting comment im Test assertiert, Z. 85–86).
- „Erneut prüfen"-Pfad: `Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers` (Z. 59–114) — Folgelauf ohne CLI erzeugt weder zweite Aufgabe noch verlorenes Ereignis (Z. 89–96); nach `SaveDefaultPluginPrefixAsync` + `ScmEventOrchestrator.RetryAsync(eventId)` → `Zugestellt`, `LokalerKlonPfad` gesetzt, genau eine Sitzung/ein Prompt.
- Queue-Auswahl-Pfad: `Monitor_CreatedTaskWithoutCli_QueueSelectionDelivers` (Z. 119–158) — `OpenFromQueueAsync` mit Plugin-Auswahl → `Zugestellt`, Aufgabe `Gestartet` mit Klonpfad; E2E-deckungsgleich durch E8 (`StartSelected` → `ChoosePlugin`).

**Kriterium 3 – Blockierte Aufgaben werden auch im neuen Pfad nicht gestartet; Ereignis bleibt mit Grund wartend: erfüllt.**

- `Monitor_ReusedBlockedTask_StaysWaitingWithoutStart` (Theory, Z. 162–188): `Beendet`, `Archiviert`, `Neu`+`PausiertBisUtc` — wiederverwendete Aufgabe bleibt im Status, `LokalerKlonPfad` bleibt `null`, Ereignis `Wartend` mit Grund `beendet/archiviert` bzw. `pausiert`, keine Sitzung/kein Prompt.
- Produktion: `BlockReason` (Z. 387–389) greift in `DispatchCoreAsync` (Z. 122–123) und in `OpenFromQueueAsync` (Z. 52) identisch für angelegte wie verknüpfte Aufgaben; E2E-Bestand E4 (`E2E_ScmEvents.cs:110–132`) unverändert.

**Kriterium 4 – Bestandsschutz inkl. konsolidierter SCM-E2E: erfüllt.**

- Kein Bestandstest geändert außer der konsolidierten E2E-Methode und dem Fixture-Refactoring: `WaitEventAsync` delegiert auf `WaitEventCoreAsync` (`ScmE2EFixture.cs:128–146`), Semantik unverändert (gleiche 60-s-Frist, gleiche Fehlermeldung mit Label); `SelectEvent` delegiert auf `SelectEventByTitle` (`E2E_ScmEvents.cs:516–535`). Alle übrigen Testdateien diff-frei.
- SCM-E2E-Konsolidierung eingehalten: E8 ist ein Abschnitt **innerhalb** von `ScmEvents_QueueReceptionDispatchAndReplies_UseRealPipeline` (Z. 317–338) — kein neuer Testmethode-/App-Start (der zweite App-Start der Methode bleibt E5, Z. 52). Trigger-/Owner-/Token-Regeln und deduplizierte Zustellung bleiben durch die unveränderten E1–E7-Bestandsabschnitte und `ScmEventMonitoringServiceTests_*`-Bestandsdateien abgesichert.
- Die gemeldeten Zahlen (1986/1987 Unit, 156/156 Integration, SCM-E2E 2× grün) liegen — wie in den Schritten 2/3 — nicht als auswertbares Artefakt (TRX/Bericht) im Repository vor und wurden nicht eigenständig nachvollzogen; sie sind intern konsistent (Schritt 3 meldete 150/156 → +6 neue Fälle = 156).

**Kriterium 5 – Entscheidung zum Anlagezeitpunkt bei fehlender Standard-CLI dokumentiert und testabgedeckt: erfüllt.**

- Dokumentiert: `docs/help/plugins/github-ereignisse/business-rules.md` — neuer Abschnitt „Anlage bei fehlender Standard-CLI" (Anlage erfolgt **auch** ohne auflösbare Standard-CLI, Begründung: sofortige Sichtbarkeit, `SourceKey`-Dedup gegen Duplikate, kein Ereignisverlust); zusätzlich `README.md`, `ablauf-technisch.md`, `beschreibung.md`, `changes.log` konsistent. Designentscheidung war bereits im (nach Umsetzung entfernten) `plan.md` festgelegt (Commit `1378431`).
- Testabgedeckt: `Monitor_CreatedTaskWithoutCli_WaitsThenConfiguresAndDelivers` assertiert die angelegte Aufgabe trotz fehlender CLI (`Assert.Single(db.Aufgaben)`, Z. 75) inkl. Duplikatfreiheit im Folgelauf; `Monitor_CreatedTaskWithoutCli_QueueSelectionDelivers` deckt denselben Anlagepfad ab.
- Produktion: `ScmEventTaskResolver` (`ScmEventTaskResolver.cs:46–74`) enthält keinerlei CLI-Abhängigkeit — Anlage erfolgt in der Aufnahmephase vor jeder CLI-Auflösung; Entscheidung entspricht dem Ist-Zustand und ist dokumentarisch konsistent.

### Weitere Feststellungen

- **Status-Setzung vor CLI-Auflösung:** `DispatchCoreAsync` setzt `Neu → Gestartet` (Z. 132) bereits vor der CLI-Auflösung; der Test assertiert daher `Gestartet` mit `LokalerKlonPfad == null` für den wartenden Fall (Z. 76–77). Fachlich konsistent mit der Doku („neue, nicht blockierte Aufgaben werden pro Zustellversuch gestartet"; `business-rules.md`, `ablauf-technisch.md`) — kein Verstoß.
- **Temporäre Plan-Artefakte entfernt:** `plan.md`, `requirement.md`, `inventory/**`, `todo.md` wurden im Plan-Commit `1378431` angelegt und im Feat-Commit `d195557` wieder entfernt; die verbindlichen Entscheidungen sind in `business-rules.md`/`tasks.md` überführt — kein Informationsverlust.
- **Testdatei kompiliert konsistent** gegen die vorhandenen Verträge: `ScmEventObservation`-/`ScmEventBatch`-Signaturen, `PluginDefaultSettingsService.SaveDefaultPluginPrefixAsync`, `ScmEventOrchestrator.RetryAsync`/`OpenFromQueueAsync`-Delegat-Signatur `(Aufgabe, IScmPromptSessionAdapter?, CancellationToken) → Task<string?>` und `ScmResponseKind.Reaction/Comment` stimmen mit dem gelesenen Produktionscode überein.
- **Testnachweis-Einschränkung:** Testläufe wurden laut Prüfauftrag nicht eigenständig ausgeführt; die Aussage beruht auf Code-Volllektüre der geänderten Dateien und der gemeldeten Zahlen — identische Einschränkung wie bei den Abnahmen der Schritte 2 und 3.
