# Projektplan – SCM-Ereignisverarbeitung & Abschlussüberwachung

## Übersicht

**Projekttitel:** SCM-Ereignisverarbeitung: Identitätskonsistenz, Geschlossen-Prüfung,
Auftragsart und Retry-Disziplin

**Gesamtziel:** Sechs Issues (#456, #452, #451, #436, #433, #457) werden in fünf
Entwicklungsschritten umgesetzt: konsistente SCM-Repository-Identität im
SeparatesKonto-Modus inkl. Bestandsdaten-Strategie, Re-Checks auf geschlossene
Issues im Aufbereitungspfad, typisierte/gruppierbare Auftragsart mit
Typwechsel-Routing, vereinheitlichte dokumentierte Ereignisabläufe ohne
Doppelaufgaben sowie begrenzte sichtbare Wiederholung der Abschlussüberwachung.

**Betroffene Bereiche:** `src/Softwareschmiede/Application/Services/`
(ScmIssuePreparationMonitoringService, ScmIssuePreparationService,
ScmEventOrchestrator, ScmEventTaskResolver, ScmTaskCompletionMonitoringService),
`plugins/Softwareschmiede.Plugin.GitHub/` (EventResponses, Events),
`src/Softwareschmiede.App/` (WorkItemListMode/Layout, ActiveTasksListControl,
PanelItem), Domain (`WorkItem`, ggf. `ScmIncomingEventStatus`), EF-Migrationen,
`docs/help/plugins/github-ereignisse/`, Unit-/Integrationstests.

## Grobe Vorgehensentscheidungen

- **Snapshot-Korrektur statt Prüfungs-Abschwächung (A):** Beim Tagging der
  Issue-Referenz wird der Repository-Snapshot im SeparatesKonto-Fall unter dem
  Ausführungskonto neu aufgelöst (Cache je Konto → transportlos nach Warmup); der
  `ViewerId`-Umleitungsschutz bleibt unverändert scharf. Fix-Ort: je Issue beim
  Tagging (einfachste konsistente Stelle; Cache macht Repository-Variante
  unnötig — Festlegung im Schritt, beide äquivalent).
- **Re-Checks als eng begrenzte Zusatzpunkte (B):** Ein Re-Check unmittelbar vor
  `exchange.PrepareAsync`, einer einmalig je Versuchauswertung vor der
  Remote-Aktionsausführung; `null`-Lesung = begründet warten (nie verwerfen).
  Verwerfen-Kennzeichnung über Status/`Reason` nach Festlegung im Schritt.
  Erkennungspfad: bereits geschlossenes Issue wird **übersprungen** (Annahme —
  Aufgabe+Ereignis entstehen nicht; Auswertungs-Checkpoint dokumentiert die
  Entscheidung), weil ein sofort verworfener Auftrag nur Rauschen erzeugte.
- **Auftragsart als eigenes Feld (C, entschieden):** `WorkItem.Purpose` bleibt
  Isolations-/Verhaltensmerkmal; die Auftragsart wird als eigenes Enum-Feld an
  `WorkItem` geführt (EF-Migration + Backfill), alle Anlagepfade typisiert.
  Der Typwechsel wirkt bei **erfolgreicher Zustellung** des Trigger-Ereignisses
  und hebt die Isolation kontrolliert auf (`Purpose` wird dann entfernt).
- **Ereignisfluss-Doku nach Umsetzung (D):** Die #457-Ablaufbeschreibungen werden
  als verbindliche Referenz in `docs/help/plugins/github-ereignisse/` festgehalten,
  nachdem das Routing geändert ist; der Doppelaufgaben-Fix entsteht in Schritt 4.
- **Lokaler, sichtbarer Retry-Schutz (E):** Backoff/Fehlerzähler lokal im
  `ScmTaskCompletionMonitoringService` je Aufgabe — keine generische
  Infrastruktur; Eskalation über Aufgabenprotokoll (LogEntry) statt stummer
  Endlosschleife. In-memory-Zähler (kein Schema-Eingriff), gedeckelt auf ein
  Maximalintervall, damit Selbstheilung möglich bleibt (Annahme).

## Entwicklungsschritte

### Schritt 1: Konsistente SCM-Repository-Identität im SeparatesKonto-Modus

Behebt #436 und #456 (gleiches Defektbild). Im `SeparateAccount`-Fall wird die
Issue-Referenz beim Tagging (`ScmIssuePreparationMonitoringService.EvaluateIssueAsync`)
unter dem Ausführungskonto neu aufgelöst und der vollständig konsistente Snapshot
(`ViewerId`/`ViewerLogin`/`CanWrite`, kanonisierte `Owner`/`Name`, `OwnerId`,
`NumericRepositoryId`) übernommen; `RepositoryId` wird gegen die
Entdeckungs-Auflösung abgeglichen (Mismatch → ehrlicher Fehler am
Issue-Checkpoint). Entdeckung bleibt unter `UserAccount`. Zusätzlich:
Bestandsdaten (#436, **entschieden: dokumentierte Ops-Reparatur**) — betroffene
persistierte Aufbereitungsereignisse verwerfen, je-Issue-Auswertungs-Checkpoints
zurücksetzen und sauber neu einreihbar machen; die Vorgehensweise wird in der
Doku festgehalten, kein Code-Reparaturmechanismus. Reconcile-Pfade (`IsViewer`)
profitieren implizit.

**Akzeptanzkriterien:**
- `issueBodyUpdate`/`comment` laufen im SeparatesKonto-Modus ohne
  Identitäts-Fehlalarm aus (Integrationstest mit zwei unterschiedlichen
  Viewer-Identitäten, inkl. Plugin-Ausführungsprüfung).
- Benutzerkonto-Modus verhält sich unverändert.
- Reconcile erkennt eigene Kommentare/Reaktionen des Ausführungskontos.
- Echter `ViewerId`-/`RepositoryId`-Mismatch bleibt blockierend.
- Bestandsdaten-Strategie umgesetzt und dokumentiert (Form je Entscheidung).
- Fehlschlag der Ausführungskonto-Auflösung ist ehrlich sichtbar (Issue-Checkpoint
  bzw. Repository-Ebene je Festlegung).

**Abhängigkeiten:** Keine. **Bereiche:** ScmIssuePreparationMonitoringService,
ScmIssuePreparationService, ggf. ScmEventStore/Repair, GitHub-Plugin-Tests,
Integrationstests, Doku.

### Schritt 2: Re-Checks bei geschlossenem Issue in der Aufbereitung

Setzt #452 um: (a) zweiter Live-Re-Check unmittelbar vor `exchange.PrepareAsync`;
(b) einmaliger Re-Check je Versuchauswertung in `ReconcilePreparationAttemptAsync`
vor Remote-Ausführung der `issueBodyUpdate`-/`comment`-Aktionen — geschlossen →
Aktionen werden nicht ausgeführt, dem Agenten `success=false` mit Begründung
zurückgemeldet, Versuch dokumentiert als verworfen; (c) Erkennungspfad:
geschlossenes Issue beim Einreihen überspringen (Festlegung, begründet);
(d) `null`-Lesung an allen Re-Check-Stellen = begründet warten + erneut prüfen;
(e) verworfene Aufbereitungen in Ereignisansicht + Protokoll als „verworfen
(Issue geschlossen)" erkennbar (Status-/`Reason`-Festlegung im Abgleich mit
`IsCancelledAsync`-/`NonBlocking`-Semantik und `ScmIncomingEventStatusTests`).

**Akzeptanzkriterien:** (aus #452) Geschlossen vor Zustellungsvorbereitung → keine
Analyse; geschlossen zwischen Zustellung und Remote-Ausführung → keine
Kommentare/Beschreibungsänderungen, Aktionen als verworfen rückgemeldet; letzter
Re-Check zeitnah vor erstem sichtbarem Effekt; offenes Issue unverändert; nicht
lesbar = warten; Verwerfen nachvollziehbar; Erkennungspfad-Festlegung dokumentiert.

**Abhängigkeiten:** Schritt 1 (gleiche Dateien/Tests). **Bereiche:**
ScmEventOrchestrator, ScmIssuePreparationService, ScmIncomingEventStatus,
Ereignis-UI (Status-Anzeige), Tests, Doku.

### Schritt 3: Begrenzte, sichtbare Wiederholung der Abschlussüberwachung

Setzt #433 um: Pro-Aufgaben-Fehlerzähler mit exponentiellem Backoff
(Start = Poll-Intervall, Faktor 2, Max 60 min, Eskalation nach ~10 aufeinanderfolgenden
Fehlschlägen — Annahme, im Schritt final festzulegen) für beide Schleifen
(Abschluss-Ausführung in `CheckTaskAsync` und Referenzprüfung in `RunOnceAsync`);
identische Fehlersignatur wird nicht minütlich neu geloggt (Dedupe); nach
Eskalationsschwelle einmaliger, ehrlicher Protokolleintrag am Vorgang
(`LogEntryType.SystemMessage`) + weitere Versuche im Maximalintervall
(Selbstheilung bleibt möglich); Zähler-Reset bei Erfolg/Statuswechsel.
Zusätzlich: Ursache von Fall 2 diagnostizierbar machen (Exception-Typ in die
Logmeldung ist vorhanden — konkrete Ursache im Rahmen des Schritts anhand
verbesserter Diagnose klären); optional Diagnose des sperrenden Prozesses beim
Klon-Delete (nice-to-have, kein Blocker).

**Akzeptanzkriterien:** (aus #433) begrenztes Budget + Backoff statt Minutentakt in
beiden Schleifen; sichtbarer Fehlerzustand nach Erschöpfung; keine
Minuten-Logflut bei identischen Fehlern; Diagnosehinweis bei `IOException` auf
dem Klon-Verzeichnis; Wiederanstoß-Weg dokumentiert.

**Abhängigkeiten:** Keine. **Bereiche:** ScmTaskCompletionMonitoringService,
DevelopmentProcessService (Diagnose), ggf. ScmEventOptions/AppSettings,
LogService, Tests.

### Schritt 4: Auftragsart an Aufgaben — Typisierung, Gruppierung, Typwechsel

Setzt #451 um (**entschieden**: eigenes Enum-Feld mit allen Ursprüngen typisiert;
Typwechsel bei erfolgreicher Zustellung; Gruppierungsmodus + Markierung in beiden
Listen):
- Neues Enum-Feld `Auftragsart` an `WorkItem` (EF-Migration + Backfill):
  `Issueprüfung`, `Anweisung` sowie eigene Werte für die übrigen Anlagepfade
  (manuell, Pull-Request-Ereignis, SCM-Alert — Benennung im Schritt); alle
  Anlagepfade setzen ihren Wert.
- Gruppierung nach Auftragsart in beiden Aufgabenlisten: neuer
  `WorkItemListMode`-Wert + `WorkItemListLayout`-Sort/GroupDescriptions +
  `ActiveWorkItemPanelItem`-Gruppeneigenschaften (Fallback-Gruppe nach Muster)
  **plus** sichtbare Typ-Markierung je Eintrag — Seitenleiste und Dashboard.
- Routing: `ScmEventTaskResolver.ResolveIssueAsync` findet die
  Aufbereitungs-Aufgabe desselben Issues als Zielkandidaten (Rangfolge:
  kanonische Aufgabe vor Aufbereitungs-Aufgabe; Koexistenz ohne
  `Ambiguous`-Regression); wird ein Trigger-Ereignis der Aufbereitungs-Aufgabe
  zugeordnet und **erfolgreich zugestellt**, wechselt deren `Auftragsart` auf
  `Anweisung` und `Purpose` wird entfernt (Isolation endet kontrolliert);
  Protokolleintrag am Vorgang (`LogEntryType.SystemMessage`).
- Seiteneffekte dokumentieren: verknüpfte Beobachtungsquelle nach Wechsel,
  eingereihte Ereignisse mit `ScmIncomingEvent.Purpose` bleiben auf dem
  Aufbereitungspfad; `business-rules.md` aktualisieren.

**Akzeptanzkriterien:** (aus #451) jede Aufgabe eindeutig typisiert;
Gruppierung/Markierung in Seitenleiste + Dashboard; Anweisung an
Issueprüfungs-Aufgabe → Typwechsel + Anzeige in Anweisungs-Gruppe; kein
`Ambiguous`-Deadlock bei Koexistenz; Typwechsel im Protokoll; Doku aktualisiert;
Migration + Backfill bei neuem Feld.

**Abhängigkeiten:** Schritte 1, 2 (Routing/Isolation baut auf dem korrigierten
Aufbereitungspfad auf; gemeinsame Dateien). **Bereiche:** ScmEventTaskResolver,
WorkItem + Migration, ScmEventOrchestrator/-Store, App-Listen-UI, LogService,
Tests, Doku.

### Schritt 5: Ereignisabläufe vereinheitlichen und dokumentieren

Setzt #457 um: Verbindliche Ablaufbeschreibung aller SCM-Ereignisarten in
`docs/help/plugins/github-ereignisse/` (Issue-Anlage/Aufbereitung,
Trigger-Kommentar in Issue, Trigger-Kommentar in PR → Issue-Zuordnung,
Issue geschlossen → Wiedervorlage/CLI-Stop/Abschluss), abgeglichen gegen die
tatsächliche Implementierung; Verifikation, dass je Issue genau eine
Zielaufgabe entsteht (Dedupe-Regressionstest über die Ereignisarten hinweg,
inkl. Aufbereitungs-Aufgabe nach Schritt 4); verbleibende Ablauflücken aus dem
#457-Flow (z. B. Access-Probe-Reihenfolge, Wartezustände) gegen den Ist-Stand
prüfen und dokumentierte Abweichungen entweder umsetzen oder als begründete
Nicht-Ziele festhalten.

**Akzeptanzkriterien:** Ablaufdoku vollständig und mit Code übereinstimmend;
keine Ereigniskombination erzeugt Doppelaufgaben (Test); #457-Checkliste je
Ereignisart abgehakt.

**Abhängigkeiten:** Schritt 4 (Routing-Entscheidungen sind Dokumentsgegenstand).
**Bereiche:** Doku, Tests, ggf. kleine Verhaltenskorrekturen.

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | Konsistente SCM-Repository-Identität im SeparatesKonto-Modus | Keine | Monitoring-Service, Plugin, Tests, Doku |
| 2 | Re-Checks bei geschlossenem Issue in der Aufbereitung | 1 | Orchestrator, Status, UI, Tests |
| 3 | Begrenzte, sichtbare Wiederholung der Abschlussüberwachung | Keine | Completion-Monitoring, DevelopmentProcess, Tests |
| 4 | Auftragsart an Aufgaben — Typisierung, Gruppierung, Typwechsel | 1, 2 | Resolver, WorkItem/Migration, App-UI, Doku |
| 5 | Ereignisabläufe vereinheitlichen und dokumentieren | 4 | Doku, Tests |

## Offene Punkte

Alle entscheidungsbedürftigen Punkte wurden mit dem Nutzer geklärt bzw. als
begründete Annahmen festgelegt:

- **OP-A (#451 OP1/OP4) — ENTSCHIEDEN:** Eigenes Enum-Feld an `WorkItem` mit
  EF-Migration + Backfill; alle Anlagepfade typisiert (Issueprüfung, Anweisung,
  manuell, Pull-Request-Ereignis, SCM-Alert).
- **OP-B (#451 OP2/OP3) — ENTSCHIEDEN:** Typwechsel wirkt bei **erfolgreicher
  Zustellung** des Trigger-Ereignisses an die Aufbereitungs-Aufgabe.
- **OP-C (#451 OP5/OP6) — ENTSCHIEDEN:** Neuer `WorkItemListMode`-Gruppierungswert
  **plus** Typ-Markierung je Eintrag, in Seitenleiste und Dashboard.
- **OP-D (#436) — ENTSCHIEDEN:** Dokumentierte Ops-Reparatur (betroffene
  Ereignisse verwerfen + je-Issue-Checkpoints zurücksetzen + Neueinreihung),
  kein Code-Reparaturmechanismus.
- **OP-E (#433) — Annahme:** in-memory Zähler/Backoff + einmalige
  LogEntry-Eskalation + Maximalintervall statt persistiertem Fehlerstatus
  (kein Schema-Eingriff; Neustart = Reset).
- **OP-F (#452) — Annahme:** geschlossenes Issue wird beim Einreihen
  übersprungen; der Auswertungs-Checkpoint dokumentiert die Auswertung.
- **OP-G (#457) — Annahme:** Ablaufdoku + Doppelaufgaben-Verifikation/-Fix
  (Schritt 5), keine eigene Verhaltensneuentwicklung darüber hinaus.
