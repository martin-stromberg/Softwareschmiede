# Projektanforderung – SCM-Ereignisverarbeitung & Abschlussüberwachung

Quelle: GitHub-Issues martin-stromberg/Softwareschmiede #456, #452, #451, #436, #433, #457
(Anforderungen sind den Issue-Bodies entnommen; Annahmen sind als solche markiert.)

## Ziel

Die SCM-Ereignis- und Issue-Aufbereitungsverarbeitung wird an sechs Stellen korrigiert
und erweitert: Identitätskonsistenz im SeparatesKonto-Modus, Re-Checks bei
geschlossenen Issues, Unterscheidbarkeit und Gruppierbarkeit der Auftragsart von
Aufgaben, vereinheitlichte Ereignisabläufe ohne Doppelaufgaben, sowie begrenzte,
sichtbare Wiederholung der Abschlussüberwachung.

## Teil A – Konsistente Repository-Identität (Issues #436 + #456, gleiches Defektbild)

**Beobachtung (belegt):** Im SeparatesKonto-Modus persistiert die Issue-Aufbereitung
eine `ScmEventRepository`-Referenz mit `Konto = SeparatesKonto`, aber `ViewerId`/
`ViewerLogin`/`CanWrite` des Benutzerkontos (Tagging in
`ScmIssuePreparationMonitoringService` ~Z. 194: `issue with { Repository =
issue.Repository with { Account = accountMode } }` nach Auflösung unter
`UserAccount` ~Z. 84–85). `GitHubPlugin.ExecuteResponseAsync` (~Z. 49–52) löst unter
`repository.Account` neu auf und vergleicht `ViewerId` → persistierte
Remote-Antworten (`issueBodyUpdate`, `comment`) schlagen mit
`ScmProviderException(Permission)` „Repository- oder Benutzeridentität hat sich
geändert." fehl — bei **jeder** Issue-Aufbereitung im SeparatesKonto-Modus.
Nebenbefund: `ReconcileResponseAsync` (~Z. 33) und
`ReconcileReviewReactionAsync` (~Z. 128) prüfen `IsViewer` gegen die falsche
Viewer-Identität → Doppelpost-Risiko.

**Anforderungen:**

- Beim Tagging im `SeparateAccount`-Fall das Repository unter dem Ausführungskonto
  erneut auflösen (`GetEventRepositoryAsync` mit `accountMode`) und den
  vollständig konsistenten Snapshot übernehmen (ViewerId/ViewerLogin/CanWrite,
  kanonisierte Owner/Name, OwnerId, NumericRepositoryId); RepositoryId gegen die
  Entdeckungs-Auflösung abgleichen.
- Entdeckung/Enumeration bleibt weiterhin unter `Benutzerkonto` (fachlich korrekt,
  Notifications-Inbox des separaten Kontos kann die Enumeration nicht ersetzen).
- Der `ViewerId`-Mismatch-Schutz bleibt für echte Konto-/Repository-Wechsel
  blockierend — Snapshot-Korrektur, keine Abschwächung der Prüfung.
- Reconcile-Pfad erkennt vom Ausführungskonto geschriebene Kommentare als eigene
  (kein Doppelpost).
- Keine Verhaltensänderung im Benutzerkonto-Modus.
- **Bestandsdaten (#436):** Bereits persistierte `IssueAufbereitung`-Ereignisse mit
  gemischter Identität bleiben auch nach dem Codefix fehlerhaft. Es wird eine
  kontrollierte Reparatur benötigt (Neuauflösung und Korrektur der
  `ObservationJson` oder Verwerfen + Checkpoint-Reset + Neueinreihung). Keine
  doppelten Remote-Aktionen für bereits manuell erledigte Änderungen.
- Fehlschlägt die Neuauflösung unter dem separaten Konto (fehlender Zugriff), soll
  das ehrlich sichtbar werden — offen: je Issue vs. einmalig je Repository/Zyklus
  (Planungsfestlegung).

**Akzeptanzkriterien (aus #456/#436):** Body-Update und Kommentar laufen im
SeparatesKonto-Modus ohne Identitäts-Fehlalarm; kein Verhaltensunterschied im
Benutzerkonto-Modus; Reconcile erkennt eigene Aktionen; echter Mismatch bleibt
blockierend; Bestandsdaten-Strategie umgesetzt und dokumentiert.

## Teil B – Re-Checks bei geschlossenem Issue (Issue #452)

**Ist-Stand (belegt):** `DispatchPreparationAsync` liest das Issue vor jeder
Zustellung live (`ReadIssueAsync` → `Issue.IsClosed`), solange kein
`DeliveryAttemptId` gesetzt ist; bei geschlossen → Ereignis `"Verarbeitet"` ohne
Agent-Start + Protokolleintrag; bei nicht lesbar → begründet warten + retry.
`DetectAndEnqueueAsync` wertet `IsClosed` beim Einreihen nicht aus.

**Anforderungen:**

- Zweiter Re-Check unmittelbar vor `exchange.PrepareAsync`.
- Re-Check in `ReconcilePreparationAttemptAsync` vor Remote-Ausführung gelieferter
  `issueBodyUpdate`-/`comment`-Aktionen (einmal je Versuchauswertung, nicht pro
  Aktion). Geschlossen → keine Remote-Ausführung, dem Agenten `success=false` mit
  Begründung zurückmelden, Versuch dokumentiert als verworfen.
- Erkennung: bereits geschlossenes Issue erzeugt keinen regulären Auftrag —
  Festlegung treffen (überspringen vs. sofort verworfen einreihen) und begründen.
- Nicht lesbar (`ReadIssueAsync == null`) an allen Re-Check-Stellen: begründet
  warten + erneut prüfen; nie stillschweigend verwerfen/ausführen.
- Verworfene Aufbereitungen in Ereignisansicht und Protokoll eindeutig als
  „verworfen (Issue geschlossen)" erkennbar (Status/`Reason`-Festlegung; Abgleich
  mit `IsCancelledAsync`-Semantik).
- Manuelle Abbrüche (`IsCancelledAsync`) haben weiterhin Vorrang.

## Teil C – Auftragsart an Aufgaben (Issue #451)

**Anforderungen:**

- Jede Aufgabe trägt erkennbar ihre Auftragsart: „Issueprüfung" (Issue-Aufbereitung)
  oder „Anweisung" (@-Mention/Trigger-Keyword). Klassifikation übriger Ursprünge
  (manuell, PR-Ereignisse `CreateFromPullRequestAsync`, SCM-Alerts
  `CreateFromAlertAsync`) ist offen (OP1).
- Aufgabenliste (Seitenleiste + Dashboard) nach Auftragsart gruppierbar —
  naheliegend neuer `WorkItemListMode`-Wert, Persistenz über
  `WorkItemListModeService`/`WorkItemListModeBinding`; ergänzend/alternativ
  Typ-Markierung je Eintrag (OP5: Gruppierung vs. Markierung; OP6: Anzeigeort).
- Typwechsel: Erhält eine Issueprüfungs-Aufgabe eine Anweisung, wechselt ihre
  Auftragsart auf „Anweisung". Dazu muss die Ereignisauflösung die
  Aufbereitungs-Aufgabe als Zielkandidaten finden können (heute ausdrücklich
  ausgeschlossen via `Purpose == null`-Filter); Rangfolge vs. kanonische Aufgabe,
  Koexistenz (Ambiguous-Vermeidung) und `Purpose`-Semantik (OP4) festlegen;
  Zeitpunkt (OP3) und Auslöser (OP2) des Wechsels festlegen.
- Wechsel im Aufgabenprotokoll vermerken (`LogEntryType.SystemMessage`, Vorschlag).
- Seiteneffekte dokumentieren: Isolation der `IssueAufbereitung`-`Purpose`,
  Ereigniszuordnung nach Wechsel, bereits eingereihte Ereignisse behalten
  `ScmIncomingEvent.Purpose` → Aufbereitungspfad. Dokumentation in
  `docs/help/plugins/github-ereignisse/`.
- Bei neuem Feld: EF-Migration + Füllregel für Bestandsaufgaben.

## Teil D – Vereinheitlichte Ereignisabläufe / Doppelaufgaben (Issue #457)

**Problem (belegt):** Durch unterschiedliche Ereignisbehandlung entstehen mehrere
Instanzen einer Aufgabe (z. B. zweite kanonische Aufgabe neben
Aufbereitungs-Aufgabe).

**Anforderungen (interpretiert — Umfang mit Nutzer abzustimmen):**

- Abläufe der Ereignisarten dokumentieren/vereinheitlichen: Issue-Anlage
  (Aufbereitung), Trigger-Kommentar in Issue, Trigger-Kommentar in PR (→
  Issue-Zuordnung), Issue geschlossen (Wiedervorlage → CLI-Stop → Aufgaben-Ende).
- Kernschritte je Ereignis: Informationen lesen → Repository/Projekt/CLI-Plugin
  ermitteln → Ausführungsregeln prüfen (ggf. Wartezustand) → **die** Aufgabe zum
  Issue ermitteln (keine Doppelanlage) → ggf. anlegen/starten → CLI starten →
  Access-Probe bei frischer CLI → Prompt zustellen; bei aktivem CLI → Wartezustand.
- Ereignis „Issue geschlossen": Wiedervorlage per Einstellung; aktive CLI →
  neuer Wiedervorlagezeitpunkt; wartende CLI → beenden; danach Aufgabe beenden.
- Konkret: Trigger-Ereignis auf Issue mit existierender Aufbereitungs-Aufgabe darf
  keine zweite Aufgabe erzeugen (Überschneidung mit Teil C, Routing).

## Teil E – Begrenztes Retry der Abschlussüberwachung (Issue #433)

**Beobachtung (belegt):** Zwei Dauerschleifen im ~60-s-Takt
(`ScmEventOptions.PollInterval`):
1. `ScmTaskCompletionMonitoringService.CheckTaskAsync` →
   `DevelopmentProcessService.CompleteAsync` → `IOException` in
   `DeleteDirectoryForce` (Klon-Verzeichnis durch fremden Prozess gesperrt) →
   ~950 identische Fehlversuche/Tag, `ScmCompletionDetectedUtc` bleibt
   persistiert, Retry ohne Zähler/Backoff/Sichtbarkeit.
2. `SCM-Abschlussprüfung … fehlgeschlagen.` im selben Takt (catch in
   `RunOnceAsync`); Exception-Typ/Ursache nicht im Log enthalten (offen).

**Anforderungen:**

- Begrenztes Retry-Budget + exponentielles Backoff statt festem Minutentakt, je
  Aufgabe, in beiden Schleifen (Vorschlag: Start 1 min, ×2, max 60 min, ~10
  Versuche — Schwellwerte final festlegen).
- Nach Budget-Erschöpfung: sichtbarer Fehlerzustand (Aufgaben-Logeintrag +
  Kennzeichen) und/oder Benachrichtigung statt stummer Endlosschleife;
  Wiederanstoß-Weg definieren (offen: manuell, App-Start, Statusänderung).
- Log-Dedupe: identische wiederholte Fehlschläge nicht minütlich neu ins
  Anwendungsprotokoll schreiben.
- `IOException` auf Klon-Verzeichnis: Diagnose des blockierenden Prozesses
  dokumentieren (nice-to-have: blockierenden Prozess ermitteln und ins
  Aufgaben-Log schreiben).
- Fall-2-Ursache klären (vollständiger Stacktrace fehlt im Issue); die Maßnahme
  soll transiente und dauerhafte Fehler sinnvoll unterscheiden oder zumindest
  nicht verschlechtern.

## Explizite Nicht-Ziele (Annahme — aus Issue-Bodies abgeleitet)

- Keine Abschwächung des `ViewerId`-Umleitungsschutzes.
- Keine generische Retry-Infrastruktur für alle `PeriodicBackgroundService`-Dienste
  (lokal an den zwei betroffenen Schleifen, gemäß Issue-Analyse).
- Kein Umbau des SeparatesKonto-Entdeckungspfads (UserAccount-Auflösung bleibt).
- Kein Push/Merge nach `main` — der PR geht gegen `staging`; Issue-Schließung
  erfolgt durch die reguläre Promotion.

## Offene Fragen (für Nutzerentscheidung gebündelt)

1. **#451 OP1:** Klassifikation der übrigen Aufgaben-Ursprünge (manuell / PR /
   Alert)? Optionen: (a) alle Nicht-Issueprüfungs = „Anweisung",
   (b) eigene Werte/Fallback-Gruppe, (c) nur SCM-gesteuerte typisieren.
2. **#451 OP2/OP3:** Auslöser und Zeitpunkt des Typwechsels (nur SCM-Trigger vs.
   jede Anweisung; bei Ereignis-Aufnahme vs. Dispatch vs. Zustellung)?
3. **#451 OP4/OP5:** Träger der Auftragsart (eigenes Enum-Feld vs. `Purpose` vs.
   abgeleitet aus `IssueReference.SourceKey`) und Gruppierung vs. Markierung?
4. **#436:** Bestandsdaten-Reparatur — Code-Mechanismus (Repair beim Lesen/Start)
   oder dokumentiertes Verwerfen+Neueinreihung (Ops-Anleitung)?
5. **#433:** Fehlerzustand persistiert am `WorkItem` (Migration) oder
   in-memory + Log/UI-Hinweis? Ursache Fall 2 im Rahmen dieses Projekts klären
   oder als separate Klärung ausstehend lassen?
6. **#457:** Umfang — nur Doku-Vereinheitlichung der Abläufe, nur
   Doppelaufgaben-Fix (Deckung mit Teil C), oder beides?

## Abhängigkeiten zwischen den Teilen (Analyse)

- A vor B: gleiche Dateien im Aufbereitungspfad.
- C und D fachlich verschränkt (Routing/Zielauflösung = gemeinsame Änderung);
  C-UI separat implementierbar, aber selbe Schritt-Lieferung sinnvoll.
- E vollständig unabhängig.
