# Projektplanung: SCM-Ereignisverarbeitung – Ausführungsbegrenzung, Plugin-Fallback, Issue-Aufbereitung

**Quelle:** GitHub-Issues **#410** (Ausführungsbegrenzung), **#411** (Deaktivierte KI-Plugins bei SCM-Ereignisausführung), **#412** (Issue-Aufbereitung) — gemeinsame Umsetzung in einem finalen PR gegen `staging`.
**Grundlagen:** `requirement.md` (Fachlichkeit, offene Fragen mit verbindlichen Vorschlägen) und `inventory.md` (Ist-Stand mit Belegen, Lücken L1–L10) im selben Verzeichnis.
**Verbindliche Annahmen:** Alle 15 „Vorschlag"-Antworten in `requirement.md` → „Offene Fragen" gelten als verbindliche Annahmen und sind in den Entscheidungen/Schritten unten eingearbeitet. Keine Abweichung (siehe Abschnitt „Offene Punkte").

## Übersicht

**Projekttitel:** Ausführungsbegrenzung der SCM-Ereignisverarbeitung, robuster Plugin-Fallback und KI-gestützte Issue-Aufbereitung

**Gesamtziel:** Die heute unbegrenzte Sofort-Ausführung eingehender SCM-Ereignisse wird um eine in den Einstellungen konfigurierbare Parallelitätsbegrenzung (global und/oder je KI-Plugin) mit definierter Priorisierung der Warteschlange ergänzt (#410). Die Plugin-Auflösung für SCM-getriggerte Ausführungen wird robust gegen deaktivierte oder nicht mehr verfügbare Plugin-Zuordnungen: Greift die Zuordnung nicht, fällt die Ausführung auf das globale Standard-Plugin zurück; ist auch dieses nicht ausführbar, startet sie nicht und bleibt mit ehrlichem Wartegrund eingereiht (#411). Ergänzend wird eine optional aktivierbare Aufbereitung neu angelegter Issues eingeführt: Titel und Beschreibung werden mit einem fest hinterlegten Analyse-Prompt an ein konfigurierbares KI-Plugin übergeben; die erzeugte, verbesserte Anforderungsbeschreibung ersetzt die ursprüngliche Issue-Beschreibung (Titel unverändert), optional dokumentiert ein separat schaltbarer Kommentar die Analyseergebnisse; Fehler werden protokolliert, ohne das Issue zu verändern (#412).

**Betroffene Bereiche:**

- **SCM-Ereignisverarbeitung:** `ScmEventMonitoringService` (Beobachtung, Dispatch-Schleife `RunOnceAsync` Z. 229-248), `ScmEventOrchestrator` (Dispatch-Kern, Wartegründe), `ScmEventStore` (Inbox, `NextAttemptUtc`, Leases), `ScmIncomingEvent` (Status-/Warteschlangenmodell), `ScmDelayDirective` (`#verzögert`), `ScmTaskCoordinator`/`ScmTaskLeases` (Serialisierung pro Aufgabe).
- **CLI-Ausführung/Zählung:** `KiAusfuehrungsService` (`_handles`, `GetRunningCount`, `GetLaufendeAufgabenIds`, `RunningCountChanged`), `CliProcessHandle` (kein `PluginPrefix` — Attribution über `Aufgabe.KiPluginPrefix` bzw. `ScmPromptSessionAdapter.PluginPrefix`), `AufgabeService`/`EntwicklungsprozessService` (Beendet-/Pausiert-Erkennung).
- **KI-Plugin-Verwaltung:** `PluginSelectionService`, `PluginDefaultSettingsService`, `PluginActivationService`, `PluginManager`, `IKiPlugin`/`IIssueTemplateTextGenerator`.
- **SCM-Plugin-Schnittstellen:** `IScmEventSource`, `IScmEventResponder`, `ScmResponseKind`/`ScmResponseRequest`, `ScmEventOutbox`/`ScmOutgoingAction` — faktisch nur das GitHub-Plugin implementiert die Ereignisfähigkeiten (Lücke L9: Aufbereitung und Ereignisverarbeitung bleiben daher GitHub-only).
- **Einstellungen & UI:** `AppEinstellungService`/`AppEinstellung`, `SettingsViewModel`, `SettingsView.xaml`, Plugin-Einstellbereich, `GitRepository`/`Projekt` (Repository-Konfiguration), Ereignisübersicht (`ScmEventQueueViewModel`).
- **Persistenz:** EF-Core-Migration(en) für neue Einstellungs-Felder/Entity-Erweiterungen.

## Grobe Vorgehensentscheidungen

### 1. Reihenfolge und Integration von #411/#410 im gemeinsamen Auflösungs- und Dispatch-Pfad

#411 und #410 berühren denselben Pfad (`ScmEventOrchestrator.DispatchCoreAsync` Plugin-Auflösung, `PluginSelectionService`). **#411 wird zuerst umgesetzt** (Schritt 1), damit #410 auf der endgültigen, aktivierungsbewussten Auflösung aufsetzt — Merge-Konflikte und doppelte Reibung entfallen, und die Pro-Plugin-Limitzählung kann sich am tatsächlich verwendeten Plugin orientieren.

**Strategie #411:** Die Prefix-Auflösung im SCM-Dispatch wird aktivierungsbewusst: Eine gespeicherte Zuordnung (Aufgaben-/Projekt-Standard) gilt nur, wenn das Plugin tatsächlich ausführbar ist (aktiviert, installiert, ladbar — „nicht verfügbar" umfasst jeden nicht ausführbaren Zustand, verbindlicher Vorschlag 7). Ist sie es nicht, wird die Zuordnung ignoriert und das globale Standard-Plugin gewählt; ist auch dieses nicht ausführbar, startet die Ausführung nicht — das Ereignis bleibt mit ehrlichem Wartegrund und Protokolleintrag eingereiht und kann nach Konfigurationskorrektur erneut starten (Vorschlag 8). Es wird niemals ein willkürliches Ersatz-Plugin gewählt; eine explizite Benutzerauswahl über die Ereignisübersicht bleibt wie bisher streng (kein stilles Umschwenken). Heute endet dieser Fall in einer harten Sperre (`NextAttemptUtc = long.MaxValue`, Lücke L4).

### 2. #410: Limit-Integration als „Startfreigabe" statt Queue-Umbau

Die Begrenzung wirkt **ausschließlich auf neue CLI-Starts** — laufende Ausführungen werden nie unterbrochen, und rein zustellungs-/abschlussbezogene Hostaktionen (Empfangsreaktionen, Outbox, Reconcile begonnener Versuche) bleiben unbegrenzt, weil sie keine neue Ausführung darstellen (Nicht-Ziel + Ablaufdefinition). Konkret heißt das:

- **Zählung:** Die zentrale Wahrheit „CLI läuft" ist `KiAusfuehrungsService` (`GetRunningCount`, `GetLaufendeAufgabenIds` — nur Handles mit `!Process.HasExited`). Da `CliProcessHandle` kein `PluginPrefix` trägt (Lücke L2), erfolgt die Pro-Plugin-Attribution über die Aufgabe des Handles (`Aufgabe.KiPluginPrefix`, verbindlicher Vorschlag 2); bei einer lebenden SCM-Promptsitzung kann das tatsächlich gestartete Plugin (`ScmPromptSessionAdapter.PluginPrefix`, relevant bei #411-Fallback) herangezogen werden.
- **Gleichzeitigkeit der Limits:** Globales und pluginbezogenes Limit können gleichzeitig aktiv sein; das restriktivere gewinnt — ein Start erfordert freien Slot in beiden (Vorschlag 1).
- **Zwei Eingriffspunkte:** (a) Die Auswahl im Monitoring-Zyklus ändert sich von „alle fälligen Ereignisse nacheinander" zu „priorisierte Auswahl nur so vieler startbarer Ereignisse, wie Slots frei sind". (b) Die eigentliche Freigabe sitzt unmittelbar vor dem CLI-Start im Orchestrator, sodass auch UI-ausgelöste Starts (`CheckStatusCommand`, `OpenFromQueueAsync`, `RetryAsync`) dieselbe Prüfung durchlaufen.
- **Priorisierung** (Vorschlag 3, verbindlich): 1. Ereignisse für pausierte oder beendete Aufgaben — verbindlich festgelegt als Fortsetzungsereignisse für Aufgaben mit angehaltenem oder beendetem SCM-/CLI-Lauf bei weiterhin aktiver Aufgabe (fachliche Semantik: „pausiert" = `Aufgabe.PausiertBisUtc` gesetzt; „beendet" = der letzte CLI-/SCM-Lauf der Aufgabe ist beendet — persistiert `Aufgabe.AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Beendet`, gesetzt von `AufgabeService.AktivenLaufBeendenAsync` beim Prozessende, bzw. kein laufendes Handle in `KiAusfuehrungsService` — während `Aufgabe.Status` aktiv bleibt, also `Neu`/`Gestartet`/`Wartend`), damit angefangene Arbeit im bestehenden Kontext abgeschlossen wird, bevor neue Aufgaben Slots belegen. **Explizit nicht** gemeint ist `AufgabeStatus.Beendet`/`Archiviert`: `BlockReason` sperrt diese Aufgaben permanent (`ScmEventOrchestrator.cs` Z. 531-533) — sie bleiben gesperrt; ein Reaktivierungsmechanismus ist nicht Teil dieses Vorhabens (Ereignisse werden solchen Aufgaben weiter zugeordnet, sind aber nie startbar und gehören folglich nicht in die startbare Klasse 1). Eine „neue Aufgabe" ist demgegenüber eine Aufgabe ohne bisherigen Lauf (`AusfuehrungsStatus.NichtGestartet`, `PausiertBisUtc == null`) und fällt in Klasse 3; 2. Ereignisse mit fälliger Verzögerungszeit, aufsteigend nach Zeitpunkt — das ist der vorhandene `ScmDelayDirective`/`NextAttemptUtc`-Mechanismus (Vorschlag 5, keine neue Verzögerungsinfrastruktur); 3. sonstige Ereignisse in Eingangsreihenfolge (`Sequence`). Startbar bleibt ein Ereignis nur, wenn die bestehenden Sperren (`BlockReason` der Aufgabe, `NextAttemptUtc`, ältere offene Ereignisse derselben Aufgabe) es zulassen — die Priorität ordnet nur die Reihenfolge unter den startbaren.
- **Kein freier Slot → ehrliches Warten:** Das Ereignis geht in den bestehenden Wartezustand (`Status = "Wartend"` mit Grund, konsistent zum Wartegrund-Konzept des Orchestrators); nichts wird verworfen (Vorschlag 4).
- **Auslöser der Startprüfung:** (1) Ereigniseingang — über den Poll-Zyklus, der neue Ereignisse ohnehin aufnimmt und zur Auswahl führt; (2) Beendet-/Pausiert-Erkennung — die fehlende ereignisgesteuerte Benachrichtigung (Lücke L3) wird an zentralen, bereits existierenden Signalen angedockt (`RunningCountChanged` des `IRunningAutomationStatusSource` sowie die Pausen-Setzpfade `SetPauseAsync`/`PauseForDelayAsync`/Rate-Limit-Pausen), kein neuer Polling-Mechanismus. Der 60-s-Poll bleibt als Fallback-Netz bestehen.
- **Parallelität:** `ScmTaskCoordinator` serialisiert weiterhin pro Aufgabe; die Begrenzung betrifft die Parallelität *zwischen* Aufgaben, die daraus entsteht.
- **Limits konfigurierbar:** ganzzahlig ≥ 1, leer = unbegrenzt/deaktiviert; globaler Wert im Automatisierungs-Abschnitt der Einstellungen, pluginbezogene Werte auf der jeweiligen Plugin-Konfigurationsfläche (Vorschlag 6) — im bestehenden `AppEinstellung`-Key-Value-Muster (`GetIntSettingAsync`/`SetSettingAsync`).

### 3. #412 — „Issue erstellt" wird am bestehenden Beobachtungskanal erkannt, nicht über einen eigenen Poller

**Entscheidung:** Andocken an den bestehenden Beobachtungskanal des `ScmEventMonitoringService` (Vorschlag 9 in dieser Richtung). Begründung aus `inventory.md`: Aktive Repositories werden bereits repository-weit enumeriert (`EnumerateReferencesAsync`), und jede Referenz wird vollständig gelesen — ein neu angelegtes Issue materialisiert sich dort als **neue `Description`-Beobachtung einer Issue-Referenz** (neuer `ContentKey`/Snapshot). Checkpoints, Watermarks, Fehler-Backoff und Dedup existieren für diesen Kanal schon; ein dedizierter Beobachtungsdienst würde dieselbe Infrastruktur duplizieren. Es gibt zwar keinen dedizierten „Issue erstellt"-Ereignistyp (Lücke L6), aber das Signal ist im Beobachtungsstrom bereits enthalten und muss nur ausgewertet werden.

- **Retro-Schutz/Baseline:** Der erstmalige Aufbau einer Quellen-Beobachtung löst nichts aus (alle bereits bestehenden Issues würden sonst nachträglich analysiert — explizites Nicht-Ziel). Danach gilt eine neu erscheinende Issue-`Description`-Beobachtung mit `CreatedAt` nach dem etablierten Beobachtungsstand als „Issue erstellt".
- **Schleifensicherung** (Vorschlag 13): Auslöser ist strikt die Erstbeobachtung eines neuen Issues; eine spätere Beschreibungsänderung (inkl. der von der Anwendung selbst geschriebenen, aufbereiteten Fassung) erzeugt keine neue Erstbeobachtung und damit keine Folgeanalyse.
- **Konfigurierbarkeit der überwachten Repositories** (Vorschlag 10): globaler Funktionsschalter plus Aktivierung je überwachtem Repository, an die bestehende `GitRepository`-Konfiguration angeknüpft.

### 4. #412 — Aufgabenlose Aufbereitung passt ins Ereignismodell über eine dedizierte Aufbereitungs-Aufgabe

`ScmIncomingEvent.AufgabeId` ist Pflichtfeld und die gesamte Verarbeitungsmaschinerie (Aufgabenlease, `ScmTaskCoordinator`, CLI-Sitzung pro Aufgabe, Status-/Retry-Modell, Ereignisübersicht, Protokoll) ist aufgabengebunden (Lücke L10). **Entscheidung:** Pro erkanntem neuen Issue wird ein eigener, dem Issue zugeordneter **Aufbereitungsvorgang als Aufgabe** angelegt (Verknüpfung über `IssueReferenz`, klar als Aufbereitungsvorgang kennzeichbar — Muster: `ScmEventTaskResolver`/`CreateFromIssueEventAsync`) und ein **intern erzeugtes, trigger-freies `ScmIncomingEvent`** für diese Aufgabe eingereiht (Aufnahme ohne Keyword-Validierung; der Trigger bleibt für externe Ereignisse unverändert). Damit gilt automatisch:

- die Analyse-Ausführung läuft über denselben Ausführungspfad wie SCM-Ereignisse und **unterliegt der #410-Begrenzung** (Vorschlag 12);
- Wartezustände, Retry, Protokollierung und Ereignis-Sichtbarkeit kommen aus dem Bestand;
- die `AufgabeId`-Pflicht bleibt unverletzt.

**Isolation von der Issue-Ereignisauflösung (verbindlich):** Die Aufbereitungs-Aufgabe darf die kanonische Aufgabenzuordnung des Issues nicht beeinflussen. `ScmEventTaskResolver.ResolveIssueAsync` ordnet Trigger-Ereignisse Aufgaben über `IssueReferenz.IssueNummer` zu — **nicht** über `SourceKey` — und liefert bei zwei Treffern `Ambiguous` (`ScmEventTaskResolver.cs` Z. 63-68; für den Pull-Request-Kontext analog `FindActiveContextTaskIdsAsync` Z. 132-144). Ein abweichender `SourceKey` allein verhindert die Kollision daher nicht: Ohne Gegenmaßnahme würde die Aufbereitungs-Aufgabe als einzig gefundene Aufgabe des Issues spätere Trigger-Ereignisse auf sich ziehen bzw. nach Anlage einer regulären Arbeitsaufgabe zu `Ambiguous`-Sperren führen. Deshalb gilt zweifach: (a) Als Aufbereitungsvorgang markierte Aufgaben werden in beiden Resolver-Abfragen ausdrücklich ausgeschlossen (Filter auf der Zweck-Markierung); (b) ihr `IssueReferenz.SourceKey` liegt in einem eigenen, nicht-kollidierenden Namensraum (`{provider}/{host}/{repositoryId}/IssueAufbereitung/{number}` statt des kanonischen `…/Issue/{number}` aus `IssueSourceKey`, Z. 172-176) — so scheitert die spätere Anlage der kanonischen Stamm-Aufgabe zum selben Issue nicht am Unique-Index auf `SourceKey` (`SoftwareschmiededDbContext.cs` Z. 267), und derselbe Index sichert zugleich die Einmaligkeit des Aufbereitungsvorgangs je Issue.

**Bewusster Verzicht auf `IIssueTemplateTextGenerator`:** Die Einmal-Textgenerierung (`RunOneShotTextGenerationAsync`) startet einen Prozess direkt, außerhalb von `KiAusfuehrungsService._handles` — sie würde die #410-Begrenzung aushebeln und Status-/Wartemodell umgehen. Sie bleibt ein möglicher interner Baustein, ist aber **nicht** der Ausführungspfad der Aufbereitung.

### 5. #412 — Issue-Body-Update und Analyse-Kommentar laufen über die SCM-Plugin-Schnittstelle/Outbox

- **Beschreibungs-Update:** Es existiert keine Schnittstelle zum Ändern einer Issue-Beschreibung (Lücke L5). Der Antwortvertrag wird um eine Antwort-Art „Issue-Beschreibung aktualisieren" erweitert (neuer `ScmResponseKind`, `Execute` **und** `Reconcile` in `IScmEventResponder`), über das bestehende Outbox-Gerüst mit deterministischer Aktions-ID — dadurch erbt das Update Idempotenz, Reconcile und Backoff. Das GitHub-Plugin implementiert den Schreibzugriff (`PATCH issues/{n}`); Plugins ohne die Fähigkeit melden einen ehrlichen Fehler statt still zu ignorieren. Der Titel bleibt unverändert (Nicht-Ziel).
- **Analyse-Kommentar:** nutzt den vorhandenen `ScmResponseKind.Comment`-Outbox-Pfad; separat aktivier-/deaktivierbar; kein Kommentar bei deaktivierter Option.
- **Ergebnisrücklauf:** Die Analyse läuft als CLI-Ausführung der Aufbereitungs-Aufgabe; das Ergebnis (verbesserte Beschreibung, optionale Analyse-Dokumentation) kommt über den bestehenden Antwort-/Exchange-Kanal zurück und wird hostseitig in die Outbox-Aktionen überführt. Der Analyse-/Verbesserungs-Prompt ist **fest in der Anwendung hinterlegt** mit den im Issue genannten Qualitätskriterien (Vorschlag 14).
- **Funktions-Plugin:** eigene Einstellung „Standard-KI-Plugin für die Issue-Aufbereitung" mit Fallback auf das globale Standard-Plugin — analog der #411-Kette (Vorschlag 11), wobei die aktivierungsbewusste Auflösung aus Schritt 1 wiederverwendet wird (Lücke L7 wird dafür geschlossen).
- **Fehlerbehandlung:** Fehler während der Analyse oder der Rückschreibung werden im Anwendungs-Log und als Protokolleintrag am Verarbeitungsvorgang festgehalten; das Issue bleibt unverändert, und es entsteht kein Fehler-Kommentar am Issue (Vorschlag 15).

## Entwicklungsschritte

### Schritt 1: Plugin-Fallback für SCM-Ereignisausführungen — aktivierungsbewusste Plugin-Auflösung (#411)

Eine über ein SCM-Ereignis ausgelöste Ausführung wählt ihr KI-Plugin robust: Ist die gespeicherte Plugin-Zuordnung (projektbezogener bzw. aufgabenbezogener Standard) nicht mehr ausführbar — deaktiviert, nicht installiert oder nicht ladbar —, wird diese Zuordnung ignoriert und das global gesetzte Standard-Plugin verwendet. Ist auch das globale Standard-Plugin nicht ausführbar oder nicht konfiguriert, startet die Ausführung nicht: Das Ereignis verbleibt mit einem verständlichen Wartegrund in der Warteschlange (sichtbar in der Ereignisübersicht, protokolliert), ohne still verworfen oder dauerhaft gesperrt zu werden; nach Konfigurationskorrektur ist ein Start möglich. Eine explizite Plugin-Auswahl des Anwenders in der Ereignisübersicht bleibt unverändert streng — dort gibt es keinen stillen Fallback. Ersetzt wird das heutige Verhalten, bei dem ein deaktivierter Prefix eine `InvalidOperationException` auslöst und das Ereignis mit `NextAttemptUtc = long.MaxValue` hart sperrt.

**Akzeptanzkriterien:**

- [ ] Deaktiviertes/nicht mehr vorhandenes Projekt- bzw. Aufgaben-Standard-Plugin → die SCM-Ereignisausführung startet mit dem globalen Standard-Plugin.
- [ ] Globales Standard-Plugin ebenfalls nicht ausführbar/aktiv → kein Start; Ereignis mit ehrlichem Wartegrund („wartend") und Protokolleintrag, kein willkürliches Ersatz-Plugin, keine harte `long.MaxValue`-Sperre ohne Wiederaufnehmbarkeit nach Konfigurationsänderung.
- [ ] „Nicht verfügbar" deckt deaktiviert, nicht installiert und nicht ladbar ab; ein ausführbarer explizit gewählter Prefix verhält sich wie bisher (kein Fallback bei expliziter Benutzerwahl).
- [ ] Unit-Tests für die neue Auflösungskette (alle Fallback-Zweige, deaktiviert/nicht installiert/unset) sowie Dispatch-Tests über `ScmDispatchFixture` für Fallback-Start und verweigerten Start mit Wartegrund.

**Abhängigkeiten:** —

**Betroffene Bereiche:** `PluginSelectionService` (aktivierungsbewusste SCM-Auflösung), `ScmEventOrchestrator` (Prefix-Auflösung/Fehlerpfad), `PluginActivationService`/`PluginDefaultSettingsService` (Nutzung), Ereignis-Wartegrund/Protokoll, `Softwareschmiede.Tests` (Selection-/Orchestrator-Tests).

---

### Schritt 2: Ausführungsbegrenzung mit Priorisierung — Limits konfigurieren, Startprüfung bei Ereigniseingang (#410, Teil 1)

In den Einstellungen lassen sich ein globales Limit paralleler CLI-Ausführungen und je KI-Plugin ein eigenes Limit setzen (ganzzahlig ≥ 1, leer = unbegrenzt). Bei Eingang bzw. Fälligkeit von SCM-Ereignissen findet eine Startprüfung statt: Die Anwendung zählt die Aufgaben mit laufender CLI-Ausführung — global und je Plugin (Attribution über `Aufgabe.KiPluginPrefix` der laufenden Handles; bei lebender SCM-Sitzung das tatsächlich gestartete Plugin). Beide Limits gelten gleichzeitig, das restriktivere gewinnt. Ist ein Limit erreicht, bleibt das Ereignis mit ehrlichem Wartegrund eingereiht — nichts wird verworfen, bereits laufende Ausführungen werden nicht unterbrochen, und rein zustellungsbezogene Hostaktionen laufen unbegrenzt weiter. Ist ein Slot frei, wird das nächste Ereignis exakt in definierter Priorität gewählt: (1) Fortsetzungsereignisse für Aufgaben mit angehaltenem oder beendetem Lauf bei aktivem `Aufgabe.Status` — d. h. `PausiertBisUtc` gesetzt (pausiert) oder `AusfuehrungsStatus == AufgabeAusfuehrungsStatus.Beendet` bei `Status` `Neu`/`Gestartet`/`Wartend` (letzter Lauf beendet, Fortsetzung mit bestehendem Kontext); **nicht** `AufgabeStatus.Beendet`/`Archiviert` — diese bleiben über `BlockReason` permanent gesperrt, ein Reaktivierungsmechanismus ist nicht Bestandteil, (2) fällige Verzögerungs-Ereignisse aufsteigend nach Zielzeitpunkt (`NextAttemptUtc`, bestehender `#verzögert`-Mechanismus), (3) sonstige Ereignisse in Eingangsreihenfolge (`Sequence`). Die Auswahl der Dispatch-Schleife wird entsprechend von reiner `Sequence`-Reihenfolge auf die priorisierte, slotbegrenzte Auswahl umgestellt. Die Einstellungen-Oberfläche erhält den globalen Wert im Automatisierungs-Abschnitt und die Plugin-Limits auf der jeweiligen Plugin-Konfigurationsfläche.

**Akzeptanzkriterien:**

- [ ] Globales Limit und je-Plugin-Limit lassen sich in den Einstellungen setzen, werden persistiert (`AppEinstellung`-Muster) und beim Dispatch wirksam; leere/ungesetzte Werte bedeuten unbegrenzt.
- [ ] Bei erreichtem globalem oder pluginbezogenem Limit startet keine neue Ausführung; das Ereignis bleibt mit Wartegrund vorgemerkt und startet später, sobald ein Slot frei ist.
- [ ] Bei freiem Slot wird das nächste Ereignis exakt in der Prioritätenreihenfolge (pausierte/beendete Aufgaben → fällige Verzögerungs-Ereignisse zeitaufsteigend → Eingangsreihenfolge) ausgewählt.
- [ ] Semantik der Priorität 1 abgesichert: Ein Ereignis für eine Aufgabe mit beendetem letztem Lauf (`AusfuehrungsStatus.Beendet`, `Aufgabe.Status` nicht `Beendet`/`Archiviert`) oder gesetztem `PausiertBisUtc` zählt als Fortsetzungsereignis und wird vorrangig gestartet; ein Ereignis für eine Aufgabe mit `AufgabeStatus.Beendet`/`Archiviert` bleibt dagegen permanent über `BlockReason` gesperrt und wird nicht reaktiviert.
- [ ] Laufende CLI-Ausführungen werden nicht unterbrochen; Hostaktionen/Reconcile laufender Versuche sind vom Limit nicht betroffen; die Pro-Plugin-Zählung attributiert laufende Ausführungen korrekt.
- [ ] Unit-/Integrationstests für Zählung, Grenzfälle (beide Limits, gemischte Plugins) und Priorisierung (inkl. `NextAttemptUtc`-Fälligkeit); `SettingsViewModel`-Tests.
- [ ] E2E-Abdeckung (FlaUI, konsolidiert) des Einstellungs-Benutzerflusses für die Limits.

**Abhängigkeiten:** 1

**Betroffene Bereiche:** `ScmEventMonitoringService` (Dispatch-Auswahl), `ScmEventOrchestrator` (Startfreigabe/Wartegrund), `KiAusfuehrungsService`/`CliProcessHandle`-Attribution über `Aufgabe.KiPluginPrefix`, `AppEinstellungService` (neue Schlüssel), `SettingsViewModel`/`SettingsView.xaml` + Plugin-Einstellbereich, Ereignisübersicht (Wartegrund-Anzeige), Tests + E2E.

---

### Schritt 3: Ereignisgesteuerte Startprüfung bei Beendigung oder Pausierung (#410, Teil 2)

Zweiter, in der Anforderung explizit belegter Auslöser: Sobald eine über ein SCM-Ereignis ausgeführte Aufgabe als beendet oder pausiert erkannt wird, läuft dieselbe Startprüfung aus Schritt 2 sofort an — ein frei werdender Slot wird ohne Wartezeit auf den nächsten 60-Sekunden-Poll mit dem priorisiert nächsten Ereignis gefüllt. Die Erkennung dockt an zentralen, bereits vorhandenen Signalen an (Laufzählung/`RunningCountChanged` für beendete CLI-Ausführungen; die Pausen-Setzpfade für Pausierungen); es wird kein zusätzlicher Polling-Mechanismus eingeführt. Die Prüfung ist mehrfachauslösbar und bleibt serialisiert/idempotent (bestehende Aufgabensperren/Leases werden nicht umgangen); der Poll bleibt als langsamere Sicherheitsnetz-Wiederholung bestehen.

**Akzeptanzkriterien:**

- [ ] Nach Beendigung einer SCM-ausgeführten Aufgabe (manuell oder über `ScmTaskCompletionMonitoringService`) wird ein wartendes, startbares Ereignis unmittelbar — nicht erst beim nächsten Poll — geprüft und bei freiem Slot gestartet.
- [ ] Nach Pausierung einer solchen Aufgabe findet die Startprüfung ebenfalls statt (Priorisierung/Freigabe wird neu bewertet).
- [ ] Mehrfache/überlappende Auslöser führen zu keinem Doppelstart und nie zu einem Start über das Limit hinaus; Nicht-SCM-Ausführungen lösen die Prüfung zwar aus, verfälschen die Zählung aber nicht.
- [ ] Unit-/Integrationstests mit skriptbarer Zeit/Sessions (`ScmDispatchFixture`, Fake-`TimeProvider`): Trigger-Pfad, Slot-Freigabe, Idempotenz.

**Abhängigkeiten:** 2

**Betroffene Bereiche:** `KiAusfuehrungsService`/`IRunningAutomationStatusSource` (`RunningCountChanged`), `AufgabeService`/`EntwicklungsprozessService`/`ScmEventStore` (Beendet-/Pausiert-Setzpfade), Startprüfungs-Logik aus Schritt 2, Hintergrunddienst-/DI-Verdrahtung, Tests.

---

### Schritt 4: SCM-Plugin-Schnittstelle zum Aktualisieren einer Issue-Beschreibung (#412, Schnittstellen-Erweiterung)

Die Anwendung erhält die Fähigkeit, die Beschreibung eines Issues in einem überwachten Repository über den etablierten, abgeglichenen Antwortkanal zu ändern — als neue Antwort-Art im SCM-Vertrag (`ScmResponseKind`), ausführbar über `IScmEventResponder` inklusive Reconcile, und eingebettet in das Outbox-Gerüst mit deterministischer Aktions-ID, Wiederholung und Backoff. Das GitHub-Plugin implementiert den Schreibzugriff (`PATCH issues/{n}`) inklusive des bestehenden typisierten Fehlermodells (`ScmProviderException`: Authentication/Permission → Sperre, Temporary/RateLimit → Wiederholung). Der Issue-Titel ist von der Schnittstelle ausdrücklich nicht betroffen. SCM-Plugins ohne diese Fähigkeit liefern einen ehrlichen Fehler statt stiller Ignorierung.

**Akzeptanzkriterien:**

- [ ] `ScmResponseRequest`/`ScmResponseKind` erlauben ein Issue-Beschreibungs-Update; `IScmEventResponder` (`ExecuteResponseAsync` + `ReconcileResponseAsync`) unterstützt die Art abgleichsicher (kein Doppelschreiben, Erkennung bereits angewendeter Aktionen).
- [ ] GitHub-Plugin setzt die Beschreibung remote; Fehler werden als typisierte Providerfehler mit korrekter Wiederhol-/Sperrsemantik zurückgemeldet.
- [ ] Ausführung ausschließlich über die Outbox mit deterministischer Aktions-ID (konsistent zu `receipt`/`waiting`-Aktionen); Unit- und Integrationstests (`ScmDispatchFixture`/`ScmEventDispatchTests`-Muster) für Ausführung, Reconcile und Fehlerfälle.

**Abhängigkeiten:** —

**Betroffene Bereiche:** `Softwareschmiede.Plugin.Contracts` (`ScmEvents.cs`, `IScmEventResponder`), GitHub-Plugin (`EventResponses`), `ScmEventOutbox`/Reconcile-Pfad, Tests.

---

### Schritt 5: „Issue erstellt"-Erkennung und trigger-freier Aufbereitungsauftrag (#412, Teil 1)

Für Repositories mit aktivierter Issue-Aufbereitung erkennt die Anwendung neu angelegte Issues am bestehenden Beobachtungskanal: Der erstmalige Beobachtungsstand einer Quelle etabliert eine Baseline ohne Auslösung (keine nachträgliche Analyse des Altbestands); danach gilt eine neu erscheinende `Description`-Beobachtung einer Issue-Referenz mit Erstellungszeitpunkt nach Beobachtungsbeginn als „Issue erstellt". Pro Erkennung wird ein eigener, dem Issue über `IssueReferenz` zugeordneter Aufbereitungsvorgang als Aufgabe angelegt und ein internes, trigger-freies `ScmIncomingEvent` für diese Aufgabe eingereiht — sichtbar in der Ereignisübersicht, mit deduplizierendem `EventKey`, ohne Keyword-Validierung und ohne den Trigger-Pfad externer Ereignisse zu verändern. Die Aufbereitungsaufgabe ist als solche kennzeichbar (Zweck-Markierung) und bleibt von der kanonischen Issue-Ereignisauflösung isoliert (siehe Entscheidung 4): Ihre `IssueReferenz` trägt zwar `IssueNummer`, Titel und URL des Issues, aber einen `SourceKey` aus eigenem Namensraum (`…/IssueAufbereitung/{number}` statt des kanonischen `…/Issue/{number}`), und als Aufbereitungsvorgang markierte Aufgaben werden in `ScmEventTaskResolver.ResolveIssueAsync` (Auflösung erfolgt über `IssueNummer`, nicht `SourceKey`) sowie `FindActiveContextTaskIdsAsync` ausdrücklich ausgeschlossen — so können externe Trigger-Ereignisse weder auf der Aufbereitungs-Aufgabe landen noch nach Anlage einer regulären Arbeitsaufgabe zum selben Issue in einer `Ambiguous`-Sperre enden. Erkennung und Einreihung lesen die persistierte Konfiguration (globaler Funktionsschalter, Repository-Flag, Funktions-Plugin-Schlüssel, Kommentar-Schalter); die zugehörige Oberfläche folgt in Schritt 6. Von der Anwendung selbst ersetzte Beschreibungen lösen keine Folgeanalyse aus.

**Akzeptanzkriterien:**

- [ ] Ein neu angelegtes Issue in einem für die Aufbereitung freigeschalteten, aktiven Repository erzeugt genau einen Aufbereitungsvorgang (Aufgabe + internes Ereignis) — nachweisbar in Ereignis- und Aufgabenbestand.
- [ ] Erstbeobachtung/Altbestand, deaktivierte Funktion, nicht freigeschaltete Repositories und Repositories ohne ereignisfähiges SCM-Plugin lösen nichts aus; erneutes Beobachten desselben Issues erzeugt keinen zweiten Auftrag (Dedup).
- [ ] Das interne Ereignis umgeht die Trigger-Validierung, bleibt aber im regulären Status-/Wartemodell (`AufgabeId` gesetzt); externe Trigger-Ereignisse sind unverändert.
- [ ] Selbst verursachte Issue-Änderungen (spätere Beschreibungsersetzung) lösen keine erneute Aufbereitung aus.
- [ ] Isolation der Aufbereitungs-Aufgabe: Trigger-Ereignisse auf demselben Issue werden nach Anlage der Aufbereitungs-Aufgabe weiterhin regulär aufgelöst — keine `Ambiguous`-Sperre, keine Zuordnung zur Aufbereitungs-Aufgabe; deren `SourceKey` liegt im eigenen Namensraum und kollidiert nicht mit dem kanonischen `…/Issue/{number}`-Schlüssel (Unique-Index unverletzt, spätere Anlage der Stamm-Aufgabe bleibt möglich).
- [ ] Persistenz: nötige Entity-/Flag-Erweiterungen (`GitRepository`-Flag, Kennzeichnung des Aufbereitungsvorgangs) mit EF-Migration; Integrationstests über die Mock-SCM-Quelle (`ScmDispatchFixture`), inkl. Baseline-, Dedup- und Schleifenfällen.

**Abhängigkeiten:** 1

**Betroffene Bereiche:** `ScmEventMonitoringService` (Erkennung im Beobachtungspfad), `ScmEventStore`/Aufnahme-Pfad, `ScmIncomingEvent` (ggf. Zweck-Kennzeichnung), `ScmEventTaskResolver`/`AufgabeService`/`IssueReferenz` (Aufbereitungs-Aufgabe), `GitRepository` + Migration, `AppEinstellungService` (neue Schlüssel), Ereignisübersicht, Tests.

---

### Schritt 6: Issue-Aufbereitung — KI-Analyse, Beschreibungsersetzung, optionaler Analyse-Kommentar, Einstellungen-UI (#412, Teil 2)

Ein eingereihter Aufbereitungsauftrag startet automatisch eine KI-Analyse als reguläre, in die #410-Begrenzung einzählende CLI-Ausführung seiner Aufgabe. Titel und Beschreibung des Issues werden mit einem fest hinterlegten Analyse-/Verbesserungs-Prompt (Kriterien: Verständlichkeit, sprachliche Qualität, Vollständigkeit, fehlende Informationen, offene Fragen, technische und fachliche Risiken, Zielkonflikte, alternative Lösungsansätze, mögliche Fehlinterpretationen) an das konfigurierte Funktions-Plugin übergeben — mit Fallback auf das globale Standard-Plugin analog der #411-Kette; ist kein ausführbares Plugin verfügbar, startet die Ausführung nicht und der Vorgang bleibt mit Wartegrund eingereiht. Bei erfolgreicher Analyse ersetzt die Anwendung die Issue-Beschreibung über die Schnittstelle aus Schritt 4 (Titel unverändert); ist der Kommentar-Schalter aktiv, dokumentiert zusätzlich ein Kommentar die Analyseergebnisse (Schwachstellen, offene Fragen, Lösungsansätze, Risiken, Zielkonflikte, Verbesserungsvorschläge). Fehler während Analyse oder Rückschreibung werden im Anwendungs-Log und als Protokolleintrag am Vorgang festgehalten; das Issue bleibt dann unverändert, kein Fehler-Kommentar am Issue. Die Einstellungen-Oberfläche erhält den Funktionsschalter, die Plugin-Auswahl für die Aufbereitung, die Repository-Auswahl und den Kommentar-Schalter.

**Akzeptanzkriterien (vollständige Issue-#412-Akzeptanz):**

- [ ] Funktion aktivier-/deaktivierbar; Standard-KI-Plugin für die Aufbereitung konfigurierbar mit Fallback auf das globale Standard-Plugin; überwachte Repositories auswählbar; Kommentar-Erstellung separat schaltbar — alles persistiert und in den Einstellungen bedienbar.
- [ ] Neu angelegte Issues werden automatisch analysiert; Titel und Beschreibung gehen an das KI-Plugin; der Prompt adressiert die definierten Qualitätskriterien; das Plugin erzeugt eine optimierte Anforderungsbeschreibung.
- [ ] Die ursprüngliche Issue-Beschreibung wird remote durch die optimierte Fassung ersetzt; der Titel bleibt unverändert; bei aktivem Kommentar-Schalter wird ein dokumentierender Kommentar erzeugt, sonst keiner.
- [ ] Die Analyse-Ausführung unterliegt der Ausführungsbegrenzung aus Schritt 2 (zählt global und pro Plugin).
- [ ] Fehler werden protokolliert (Log + Protokolleintrag am Vorgang) und führen zu keiner Änderung des Issues.
- [ ] Unit-/Integrationstests für Prompt, Ausführungspfad, Ergebnisanwendung, Kommentar-Option und Fehlerfälle; `SettingsViewModel`-Tests; E2E-Abdeckung (FlaUI, konsolidiert auf minimale Methodenzahl) des Benutzerflusses „Aufbereitung in Einstellungen konfigurieren".

**Abhängigkeiten:** 1, 2, 4, 5

**Betroffene Bereiche:** `ScmEventOrchestrator`/Aufbereitungs-Ausführungspfad, `EntwicklungsprozessService`/`KiAusfuehrungsService` (Aufgaben-CLI-Start), Exchange-/Antwortkanal + `IScmEventResponder`/Outbox (Body-Update, Kommentar), `PluginSelectionService` (Funktions-Plugin + Fallback), `AppEinstellungService`, `SettingsViewModel`/`SettingsView.xaml` und Repository-Konfiguration, `ProtokollService`, Tests + E2E.

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | Plugin-Fallback für SCM-Ereignisausführungen (#411) | — | PluginSelectionService, ScmEventOrchestrator, PluginActivation-/DefaultSettings-Service, Wartegrund/Protokoll, Tests |
| 2 | Ausführungsbegrenzung: Limits + Startprüfung + Priorisierung (#410 T1) | 1 | ScmEventMonitoringService, ScmEventOrchestrator, KiAusfuehrungsService/Aufgabe.KiPluginPrefix, AppEinstellungService, SettingsViewModel/-View, Plugin-Einstellbereich, Ereignisübersicht, Tests+E2E |
| 3 | Ereignisgesteuerte Startprüfung bei Beendet/Pausiert (#410 T2) | 2 | RunningCountChanged/Laufsignale, AufgabeService/EntwicklungsprozessService-Pausenpfade, Startprüfung, DI-Verdrahtung, Tests |
| 4 | SCM-Schnittstelle: Issue-Beschreibung aktualisieren (#412 Grundlage) | — | Plugin.Contracts (ScmResponseKind/IScmEventResponder), GitHub-Plugin, ScmEventOutbox/Reconcile, Tests |
| 5 | „Issue erstellt"-Erkennung + Aufbereitungsauftrag (#412 T1) | 1 | ScmEventMonitoringService, ScmEventStore/ScmIncomingEvent, ScmEventTaskResolver/AufgabeService/IssueReferenz, GitRepository+Migration, AppEinstellungService, Tests |
| 6 | Issue-Aufbereitung: Analyse, Ersetzung, Kommentar, Einstellungen-UI (#412 T2) | 1, 2, 4, 5 | Ausführungspfad (Orchestrator/Entwicklungsprozess/KiAusfuehrung), Exchange/Outbox/Responder, PluginSelectionService, Einstellungen (Service/ViewModel/View), Protokoll, Tests+E2E |

## Offene Punkte

Keine. Alle 15 offenen Fragen wurden gemäß ihren verbindlichen Vorschlägen in die Planung übernommen; die im Codebestand verifizierten Fakten (u. a. `AufgabeId`-Pflicht, fehlendes `PluginPrefix` am `CliProcessHandle`, fehlende Body-Update-Schnittstelle, Beobachtungskanal als „Issue erstellt"-Quelle, Bypass-Charakter der Einmal-Textgenerierung) sind mit keinem Vorschlag unvereinbar.
