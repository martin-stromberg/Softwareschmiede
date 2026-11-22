# Umsetzungsplan: Abschlussüberwachung — terminal ungeklärtes Ereignis blockiert Auto-Completion dauerhaft

## Übersicht

Das Gate der Abschlussprüfung (`ScmTaskCompletionMonitoringService.CheckTaskAsync`, zwei Stellen) wird von der bisherigen Negativliste („alles außer `Verarbeitet`/`Abgebrochen` blockiert") auf eine verbindlich definierte, zentral gekapselte **Freigabeliste** nicht blockierender Stati umgestellt (Prüfrichtung `NOT IN`): neben den finalen Stati geben auch die terminalen Klärungs-Stati `VerarbeitungUngeklärt` und `ZustellungUngewiss` die Referenzbeobachtung frei; jeder andere — auch ein unbekannter oder zukünftiger — Status blockiert konservativ weiter. Dieselbe Einteilung differenziert die **Reihenfolge-Regel** in `ScmEventOrchestrator.DispatchCoreAsync`: nur noch Vorgänger außerhalb der Freigabeliste (transiente und unbekannte Stati) halten ein Folgeereignis auf `Wartend` zurück — ein Folgeereignis hinter einem terminal ungeklärten Vorgänger geht in die reguläre Zustellung über, statt dauerhaft zu versauern und die Aufgabe zu sperren (Anwender-Entscheidung zu Offener Frage 5 der Anforderung, 2026-11-22). Zusätzlich wird die Auswertung von `messages.Complete.PullRequestNumber` in `ScmEventOrchestrator.ReconcileAttemptAsync` aus dem `Accepted`-Block herausgelöst und mit einer Dedupe versehen, damit ein im Abschluss gemeldeter Pull Request auch bei fehlendem `accepted.json` oder ungeklärtem Ereignisstatus registriert wird. Es gibt keine UI-Änderung, keine Migration und keinen neuen Konfigurationsschalter; das Akzeptanzkriterium „Verhalten für alle Ereignis-Stati dokumentiert" wird über die Hilfe-Dokumentation erfüllt.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die zyklische *Abschlussprüfung einer einzelnen Aufgabe* (`CheckTaskAsync`) mit SCM-Referenz, die *Reihenfolge-Regel der Ereigniszustellung* in `DispatchCoreAsync` (transiente vs. terminal ungeklärte Vorgänger) sowie die *Registrierung der `pullRequestNumber` aus `complete.json`* als `PullRequestReference` (Rolle `CreatedByTask`) im Kanalabgleich `ReconcileAttemptAsync`. |
| Auslösende Ereignisse | Poll-Zyklus des `ScmTaskCompletionMonitoringService` (`ScmEventOptions.PollInterval`, bei aktivem `scm.taskcompletion.enabled`); Dispatch-Poll `ScmEventOrchestrator.DispatchDueEventsAsync` (greift `Wartend`-Ereignisse in jedem Zyklus erneut auf, da `WaitAsync` kein `NextAttemptUtc` setzt); 2-s-Kanalabgleich des `ScmEventExchangeService` → `ScmEventOrchestrator.DispatchAsync` → `ReconcileAttemptAsync` für alle nicht-finalen Ereignisse mit `DeliveryAttemptId`; manuelle Klärungsaktionen in der SCM-Ereignisübersicht (unverändert). |
| Erwartete Wirkung | Eine Aufgabe mit remote geschlossener/gemergter Referenz erreicht `ScmCompletionDetectedUtc` ≠ `null` und wird nach `scm.taskcompletion.wait.minutes` automatisch auf `WorkItemStatus.Completed` gesetzt — auch bei Ereignissen auf `VerarbeitungUngeklärt`/`ZustellungUngewiss`. Ein `Wartend`-Folgeereignis hinter einem terminal ungeklärten Vorgänger wird beim nächsten Dispatch-Lauf zugestellt und regulär verarbeitet (die Reihenfolge unter den Folgeereignissen bleibt gewahrt, weil jedes freigegebene Ereignis bis zu seinem eigenen Abschluss selbst transient blockierend wirkt). Eine im `complete.json` gemeldete `pullRequestNumber` wird als `PullRequestReference` registriert und einmalig im Aufgabenprotokoll vermerkt, auch wenn `accepted.json` fehlt oder das Ereignis ungeklärt endet. |
| Umgang mit fehlenden Objekten | *`PullRequestReference` fehlt:* nachträglich über `SaveReportedAsync` erzeugen (idempotent, wiederholbar). *`ScmIncomingEvent` auf `VerarbeitungUngeklärt`/`ZustellungUngewiss`:* belassen — kein automatischer Neuversand, manuelle Klärung über „Erneut prüfen"/„Erneut senden"/„Abbrechen"; blockiert weder die Abschlussprüfung noch die Zustellung nachfolgender Ereignisse. *`complete.json`/Kanal nicht lesbar:* keine Registrierung ableitbar — kein separater Lesepfad; ein später lesbares `complete.json` wird vom laufenden 2-s-Abgleich nachgeholt. *Aufgabe zwischenzeitlich `Completed`/`Archived`:* Ereignis bleibt ungeklärt in der Queue, kein Eingriff. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein automatischer Neuversand und keine automatische Statusaufhebung für `VerarbeitungUngeklärt`/`ZustellungUngewiss` | Bestandsregel der Anforderung; Ziel ist Entblockung der Abschlussprüfung, nicht automatische Ereignisklärung | bestätigt |
| 2 | Kein Klärungs-/Timeout-Pfad für `VerarbeitungUngeklärt` (kein automatischer Übergang nach Frist, keine neue UI-Klärungsaktion) | Issue nennt die Option „nicht abschließend"; die minimal-invasive Variante (Entblockung) erfüllt die Akzeptanzkriterien — ein Anwender könnte jedoch eine automatische Klärung erwarten | bestätigt (2026-11-22) |
| 3 | Keine Sichtbarkeits-Variante (kein UI-Indikator, kein periodischer Protokolleintrag für transient wartende Ereignisse) | Die Akzeptanzkriterien verlangen Entblockung *oder* Sichtbarkeit; Entblockung ist gewählt. Ein periodischer Protokolleintrag je Poll-Zyklus würde das Aufgabenprotokoll fluten | bestätigt (2026-11-22) |
| 4 | Keine generelle Aufhebung der Ereignisreihenfolge: *transiente* Vorgänger (`Aufgenommen`, `Verzögert`, `Wartend`, `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung`) halten Folgeereignisse weiterhin auf `Wartend` zurück — nur terminal ungeklärte (`VerarbeitungUngeklärt`/`ZustellungUngewiss`) und finale Vorgänger geben frei | Anwender-Entscheidung zu Offener Frage 5 der Anforderung (2026-11-22): ein Folgeereignis darf nicht ewig auf einen Vorgänger warten, der sich ohne manuellen Eingriff nie weiterbewegt. Die Reihenfolge-Garantie selbst bleibt fachlich gewollt — aufgeweicht wird nur die Dauerblockade | bestätigt (2026-11-22) |
| 5 | Keine Änderung an der Blockwirkung *transienter* Stati, an `Verarbeitet`/`Abgebrochen`-Semantik, am Abbruch (`CancelAsync`) oder an der Wartezeit-Logik | Bestand der Anforderung | bestätigt |
| 6 | Aufgaben ohne SCM-Referenz bleiben unbeobachtet | Bestand der Anforderung | bestätigt |
| 7 | Keine Sonderbehandlung für `Wartend`-Ereignisse mit `NextAttemptUtc = long.MaxValue` (Provider-Authentifizierungs-/Berechtigungsfehler Z. 336 sowie Konfigurations-/Startfehler Z. 342): sie bewegen sich ohne Benutzereingriff ebenfalls nicht weiter, bleiben aber blockierend eingestuft | Entspricht dem bestätigten Minimal-Scope — das Issue verlangt Entblockung nur für die terminalen Klärungs-Stati. Die Ereignisse sind in der Queue sichtbar und über „Erneut prüfen" (nach Korrektur) lösbar; eine Aufweichung würde dieselbe Dauerblocker-Problematik erneut öffnen und neue Klärungspfade erfordern. Als bewusste Ausnahme der Faustregel „transient = bewegt sich ohne Eingriff weiter" in der Doku-Status-Tabelle zu vermerken | bestätigt |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Status-Klassifikation | Neue zentrale statische Klasse `ScmIncomingEventStatus` (Domain, neben `ScmIncomingEvent`) mit Konstanten aller zehn Statuswerte, der Freigabeliste `NonBlocking` (`IReadOnlyCollection<string>`) und den Klassifikationsfunktionen `BlocksTaskCompletion(string)` und `IsTransient(string)` — beide implementiert als `!NonBlocking.Contains(status)` und damit deckungsgleich; `IsTransient` ist der fachlich passende Aufrufname für die Reihenfolge-Regel | Es gibt heute keine zentrale Statusstelle; die Einteilung würde sonst an mehreren Stellen divergieren können. Die Prüfrichtung ist bewusst *negativ* (Freigabeliste statt Sperrliste): jeder Status, der nicht ausdrücklich freigegeben ist — einschließlich unbekannter/zukünftiger Werte — blockiert konservativ. Eine positive Sperrliste wäre permissiv und liefe der Sicherheitsabsicht entgegen. Abschluss-Gate und Reihenfolge-Regel benötigen exakt dieselbe Einteilung — beide greifen auf die eine Menge zu. |
| **Verbindliche Einteilung** aller `Status`-Werte | **Nicht blockierend (Freigabeliste `NonBlocking`):** `Verarbeitet`, `Abgebrochen` (final) sowie `VerarbeitungUngeklärt`, `ZustellungUngewiss` (terminal, Klärung nur manuell). **Blockierend:** alles Übrige — die transienten Stati `Aufgenommen`, `Verzögert`, `Wartend`, `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung` sowie jeder unbekannte/zukünftige Wert (konservativer Default). | `ZustellungUngewiss` und `VerarbeitungUngeklärt` teilen dieselbe Klärungsklasse: kein automatischer Neuversand, identische manuelle Rückwege (`ResendAsync`/`CancelAsync`). `ZustellungUngewiss` als Dauerblocker stehen zu lassen, würde denselben Defekt über einen zweiten Status offenhalten. Das Restrisiko (Schreibversuch kam doch an) ist klein: Der 2-s-Kanalabgleich liest ein nachträglich auftauchendes `accepted.json`/`complete.json` weiterhin ein und hebt den Status dann nach `InBearbeitung`/`Verarbeitet`; zudem puffert die Abschluss-Wartezeit (Standard 60 min). *Bekannte Ausnahme der Transient-Faustregel:* `Wartend` mit `NextAttemptUtc = long.MaxValue` (Auth-/Konfigurationsfehler) bewegt sich ohne Eingriff ebenfalls nicht weiter, bleibt aber bewusst blockierend (Scope-Ausschluss 7). |
| Gate-Umbau in `CheckTaskAsync` | Beide `AnyAsync`-Prüfungen (Z. 84–86 und Z. 133–135) fragen `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` ab statt `!= "Verarbeitet" && != "Abgebrochen"`; der Kommentar wird auf die transient/terminal-Differenzierung aktualisiert | Identische Regel an beiden Stellen, an einer Klassifikationsstelle gekapselt; EF übersetzt `Contains` auf einer `IReadOnlyCollection<string>` in `IN (...)` — mit Negation also `NOT IN (...)`, sodass auch unbekannte Statuswerte blockieren. |
| Reihenfolge-Regel in `DispatchCoreAsync` (Richtung A) | Die Vorgänger-Prüfung (Z. 182) fragt `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` ab statt `!= "Verarbeitet" && != "Abgebrochen"`: nur Vorgänger außerhalb der Freigabeliste (transiente oder unbekannte Stati) halten das Ereignis auf `Wartend`; `VerarbeitungUngeklärt`/`ZustellungUngewiss`/`Verarbeitet`/`Abgebrochen` geben die Zustellung frei. **EF-Regel:** `IsTransient(string)` darf in diesem `AnyAsync`-Ausdruck **nicht** verwendet werden — ein statischer Methodenaufruf ist zur Laufzeit nicht nach SQL übersetzbar; `IsTransient` bleibt Komfortmethode für Nicht-EF-Kontexte | Anwender-Entscheidung (Offene Frage 5, 2026-11-22): ein `Wartend`-Folgeereignis soll nicht ewig auf einen Vorgänger warten, der ohne manuelle Klärung terminal ist — die CLI steht nach der Ergebnismeldung still und könnte den Folgeprompt aufnehmen. Richtung B (nur gate-seitig nicht blockierend) verworfen: sie ließe das Folgeereignis unzustellbar versauern, was der Anwender ausdrücklich nicht will. *Reihenfolge unter Folgeereignissen:* gewahrt — ein freigegebenes Ereignis steht ab Zustellbeginn selbst in einem transienten Status (`ZustellungVorbereitet` → `Zugestellt` → `InBearbeitung`, bzw. `Wartend` mit anderem transientem Grund) und hält spätere Ereignisse zurück, bis es selbst final/terminal ist; endet es `VerarbeitungUngeklärt`, wird das nächste freigegeben (Kaskade sichtbarer Einzelklärungen). *Zustellbarkeit:* beendete Sitzung → `StartProcessAndCliAsync`/`RestartCliAsync` startet neu; beschäftigte Sitzung → `WaitForInputReadyAsync`/`IsBusy` ordnet den Folgeprompt derselben Sitzung hinten an. Bewusst akzeptiertes Restrisiko: ein später manuell neu gesendeter Vorgänger kann eine fachlich bereits bearbeitete Anfrage erneut ausführen — dem manuellen Neuversand ohnehin inhärent. |
| Entkopplung der PR-Registrierung | `messages.Complete is { PullRequestNumber: > 0 }` wird in `ReconcileAttemptAsync` **außerhalb** des `if (messages.Accepted is not null)`-Blocks ausgewertet — nach dem Block, mit `IsCancelledAsync`-Guard; `LinkReportedPullRequestAsync` erhält eine Dedupe-Vorprüfung | `complete.json` wird von `ReadAsync` bereits unabhängig von `accepted.json` korreliert und validiert; der Ausschluss `Diagnostic ≠ null → Complete = null` bleibt als Gültigkeitsfilter erhalten. Da `ScmEventExchangeService` alle nicht-finalen Ereignisse mit `DeliveryAttemptId` im 2-s-Takt erneut abgleicht, würde ein entkoppelter Aufruf sonst alle 2 s einen `GitAction`-Protokolleintrag erzeugen — die Dedupe ist Pflicht, kein Extra. |
| Dedupe in `LinkReportedPullRequestAsync` | Vorab-Lookup auf `PullRequestReferences` (Unique-Key `Provider`+`RepositoryId`+`PullRequestNumber`): bereits dieser Aufgabe zugeordnet → Abbruch ohne Speicherung und ohne Protokolleintrag; fremd zugeordnet → Protokolleintrag nur, wenn noch kein identischer `LogEntries`-Eintrag für die Aufgabe existiert; nicht vorhanden → `SaveReportedAsync` + Protokolleintrag wie bisher | Registrierung bleibt idempotent, Log bleibt wahrheitsgemäß einmalig. Verworfene Alternative: ein „Verknüpfung erledigt"-Flag als neue Spalte am `ScmIncomingEvent` — unverhältnismäßige Schemaänderung für ein Dedup-Problem. |
| Aufbereitungspfad (`Purpose == "IssueAufbereitung"`) | Keine Sonderregel: die Gate-Klassifikation wirkt statusbasiert und damit automatisch auch auf Aufbereitungsereignisse; `ReconcilePreparationAttemptAsync` bleibt unverändert (dort kommt `pullRequestNumber` nicht vor) | Ein `VerarbeitungUngeklärt`-Aufbereitungsereignis an einer regulären Aufgabe verursacht denselben Dauerblocker — die statusbasierte Regel löst ihn mit, ohne den Aufbereitungspfad anzufassen. |
| Lesepfad für `pullRequestNumber` | Nur innerhalb `ReconcileAttemptAsync`; kein unabhängiger Zweitlesepfad des Kanals | Der Exchange-Pfad (`ExchangePath`) ist versuchsgebunden und ohne laufenden Abgleich nicht sinnvoll auswertbar; ein nachträglich auftauchendes `complete.json` wird vom ohnehin laufenden 2-s-Abgleich nachgeholt. |
| Sichtbarkeits-/UI-Variante | Nicht gewählt — keine Änderung an `ScmEventQueueViewModel` oder anderen UI-Komponenten | Die Akzeptanzoption „Entblockung" genügt; `ScmEventQueueItem` zeigt `VerarbeitungUngeklärt`/`ZustellungUngewiss` bereits mit Klärungsaktionen an. |

## Programmabläufe

### Zyklische Abschlussprüfung mit differenziertem Gate

1. `ScmTaskCompletionMonitoringService.RunOnceAsync` wählt Kandidaten wie bisher (Schalter `scm.taskcompletion.enabled`, `scm.taskcompletion.scope`, SCM-Referenz erforderlich) und ruft je Aufgabe `CheckTaskAsync`.
2. `CheckTaskAsync` prüft **Gate 1**: existiert ein `ScmIncomingEvent` der Aufgabe mit `Status` ∉ `NonBlocking` (`AnyAsync(x => !ScmIncomingEventStatus.NonBlocking.Contains(x.Status))` → SQL `NOT IN`) → Abbruch wie bisher. Ereignisse auf `VerarbeitungUngeklärt`/`ZustellungUngewiss`/`Verarbeitet`/`Abgebrochen` passieren das Gate; jeder andere Status — einschließlich unbekannter Werte — blockiert.
3. `DetermineClosureAsync` prüft unverändert die Referenzen (PR-Vorrang, Issue-Fallback unter `ScmAccountMode.UserAccount`); offen → ggf. Countdown-Reset; geschlossen/gemergt → `ScmCompletionDetectedUtc`/`ScmCompletionReference` setzen (Erst-Erkennung) bzw. Wartezeit prüfen.
4. Nach Ablauf der Wartezeit: Abschluss unter `ScmTaskCoordinator.Shared.RunAsync` — Reload, Zeitprüfung, **Gate 2** mit identischer `!NonBlocking`-Abfrage, dann `DevelopmentProcessService.CompleteAsync` + `SystemMessage`-Protokolleintrag.
5. Ein zurückgelassenes `VerarbeitungUngeklärt`-Ereignis der beendeten Aufgabe bleibt sichtbar ungeklärt und manuell klärbar; `CompleteAsync` räumt Ereignisse nicht auf (Bestand).

### Freigabe `Wartend`-Folgeereignisse hinter terminalem Vorgänger

1. `DispatchDueEventsAsync` wählt `Wartend`-Ereignisse in jedem Poll-Zyklus erneut aus (`WaitAsync` setzt kein `NextAttemptUtc`; `Status != "Verarbeitet"` + `NextAttemptUtc == null` ist fällig) → `DispatchAsync` → `DispatchCoreAsync`.
2. Die Reihenfolge-Prüfung (Z. 182) fragt `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` für alle Vorgänger (`Sequence < item.Sequence`) ab — EF-übersetzbar als `NOT IN`, kein statischer Methodenaufruf im Ausdruck: ein Vorgänger auf `VerarbeitungUngeklärt`/`ZustellungUngewiss`/`Verarbeitet`/`Abgebrochen` blockiert nicht → das Folgeereignis läuft in den regulären Zustellpfad (Schreibrechte, Trigger-Revalidierung, CLI-Auflösung/Start bzw. lebende Sitzung, `WaitForInputReadyAsync`, `exchange.PrepareAsync`, `WritePromptAsync` → `Zugestellt`).
3. Ist die CLI-Sitzung des ungeklärten Vorgängers beendet, startet der Pfad eine neue Sitzung (`StartProcessAndCliAsync`/`RestartCliAsync`) — das Folgeereignis endet nicht selbst als neuer Blocker. Läuft die Sitzung noch und verarbeitet (z. B. war ein `ZustellungUngewiss`-Prompt faktisch angekommen), ordnen `WaitForInputReadyAsync`/`IsBusy` den Folgeprompt derselben Sitzung hinten an — die Eingangsreihenfolge in der Sitzung bleibt gewahrt; versuchsgetrennte Kanäle (`attemptId`-Verzeichnisse) verhindern Antwortvermischung.
4. Mehrere Folgeereignisse: jedes freigegebene Ereignis steht ab Zustellbeginn selbst in einem transienten Status und hält spätere Ereignisse derselben Aufgabe zurück, bis es final/terminal ist; endet auch das Folgeereignis `VerarbeitungUngeklärt`, wird das nächste freigegeben — eine Kaskade einzeln sichtbarer, manuell klärbarer Zustände statt eines kollektiven Stillstands.
5. Kann das Folgeereignis nicht zugestellt werden (Aufgabe pausiert/beendet, Plugin/CLI nicht verfügbar, `IsBusy`, Schreibrechte fehlen, Ausführungslimit), geht es wie bisher mit begründetem `Reason` zurück auf `Wartend` — transient und im nächsten Poll erneut geprüft. Der Reihenfolge-Wartegrund „Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden." kommt nur noch für *transiente* Vorgänger zur Anwendung.

### Kanalabgleich mit entkoppelter PR-Registrierung

1. `ReconcileAttemptAsync` baut `ScmExchangeRequest`/`ScmExchangeAttempt` aus den persistierten Versuchsfeldern; `exchange.ReadAsync` liefert `ScmExchangeMessages` (`Accepted`, `Actions`, `Complete`, `Diagnostic`).
2. Abbruch-Guard `IsCancelledAsync`; `Diagnostic` → `VerarbeitungUngeklärt` (Bestand).
3. `Accepted`-Block unverändert: `AcceptedUtc`, `InBearbeitung`, Aktionsabgleich, `Complete`-Mengenprüfung → `Verarbeitet`, Attachments-Einsammlung.
4. **Neu — nach dem Block:** ist `messages.Complete.PullRequestNumber > 0`, wird `LinkReportedPullRequestAsync` aufgerufen — unabhängig davon, ob `accepted.json` existierte und unabhängig vom erreichten Ereignisstatus (inkl. `VerarbeitungUngeklärt`).
5. `LinkReportedPullRequestAsync`: Lookup auf `PullRequestReferences` → bereits verknüpft: Rückkehr ohne Aktion. Fremd zugeordnet: einmaliger `GitAction`-Eintrag (Dedupe über `LogEntries`). Neu: `SaveReportedAsync` (Provider via `PullRequestProviderDescriptor`, `RepositoryId` normalisiert, Rolle `CreatedByTask`) + `GitAction`-Eintrag.
6. Timeout-/Sitzungsende-Auswertung (`Zugestellt`/`InBearbeitung` → `VerarbeitungUngeklärt`) und `SaveChangesAsync` unverändert am Ende.

### Nachregistrierung bei verspätet eintreffendem `complete.json` (Wiederaufnahmeweg)

1. Ein `VerarbeitungUngeklärt`-Ereignis mit `DeliveryAttemptId` wird weiterhin von `ScmEventExchangeService` im 2-s-Takt nach `ReconcileAttemptAsync` getrieben.
2. Taucht `complete.json` mit `pullRequestNumber` später auf (z. B. weil der Agent vor dem Sitzungsende noch schrieb), wird die Referenz beim nächsten Abgleich registriert — genau einmal dank Dedupe.
3. Ist `accepted.json` ebenfalls lesbar, hebt der `Accepted`-Block den Status zusätzlich nach `InBearbeitung`/`Verarbeitet`; ohne `accepted.json` bleibt das Ereignis ungeklärt, die PR-Verknüpfung ist aber erfolgt.

### Manuelle Klärung (unverändert)

`CheckStatusCommand` → `DispatchAsync` (erneuter Abgleich), `ResendCommand` → `ResendAsync` → `Aufgenommen`, `CancelCommand` → `CancelAsync` → `Abgebrochen` — für `ZustellungUngewiss`/`VerarbeitungUngeklärt` weiterhin der Weg aus dem Klärungsstatus.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ScmIncomingEventStatus` | Statische Klasse (`src/Softwareschmiede/Domain/Entities/ScmIncomingEventStatus.cs`, neben `ScmIncomingEvent`) | Zentrale Statuswerte-Konstanten (`Aufgenommen` … `Abgebrochen`) plus `NonBlocking` (`IReadOnlyCollection<string>`: `Verarbeitet`, `Abgebrochen`, `VerarbeitungUngeklärt`, `ZustellungUngewiss`), `BlocksTaskCompletion(string)` und `IsTransient(string)` (beide `!NonBlocking.Contains(status)`) — die verbindliche Freigabe-Einteilung an einer Stelle, geteilt von Abschluss-Gate und Reihenfolge-Regel; alles außerhalb `NonBlocking` blockiert konservativ. |

## Änderungen an bestehenden Klassen

### `ScmTaskCompletionMonitoringService` (Service)
- Geänderte Methoden:
  - `CheckTaskAsync`: Gate 1 (Z. 84–86) und Gate 2 innerhalb der `ScmTaskCoordinator.Shared.RunAsync`-Sperre (Z. 133–135) fragen `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` ab statt der `!= "Verarbeitet" && != "Abgebrochen"`-Negativliste; Kommentar an die transient/terminal-Regel angepasst.
- Keine neuen Methoden/Properties/Events.

### `ScmEventOrchestrator` (Service)
- Geänderte Methoden:
  - `DispatchCoreAsync` (Reihenfolge-Prüfung, Z. 182): Vorgänger-`AnyAsync` fragt `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` statt `!= "Verarbeitet" && != "Abgebrochen"` — `IsTransient` kommt hier **nicht** zum Einsatz, weil ein statischer Methodenaufruf nicht nach SQL übersetzbar ist; terminal ungeklärte Vorgänger geben Folgeereignisse zur Zustellung frei; Wartegrund-Text und `WaitWithCommentAsync`-Aufruf unverändert.
  - `ReconcileAttemptAsync` (Z. 485–600): Aufruf von `LinkReportedPullRequestAsync` aus dem `if (messages.Accepted is not null)`-Block (Z. 578–579) entfernt und nach dem Block neu positioniert — Auswertung `messages.Complete is { PullRequestNumber: > 0 }` nun unabhängig von `Accepted`, mit `IsCancelledAsync`-Guard.
  - `LinkReportedPullRequestAsync` (Z. 976–991): Dedupe-Vorprüfung neu — Lookup auf `PullRequestReferences` (bereits verknüpft → keine Aktion/kein Log; fremd zugeordnet → einmaliger `GitAction`-Eintrag via `LogEntries`-Dedupe) vor dem Aufruf von `SaveReportedAsync`.
- Keine neuen Methoden/Properties/Events.

### Dokumentation (keine Klasse)
- `docs/help/plugins/github-ereignisse/business-rules.md` — Abschnitt „Abschlussüberwachung von Aufgaben mit SCM-Referenz" (Z. 112–114): pauschale Regel „Ereignisverarbeitung muss abgeschlossen sein" durch die verbindliche Status-Einteilung ersetzen (Freigabeliste `Verarbeitet`/`Abgebrochen`/`VerarbeitungUngeklärt`/`ZustellungUngewiss`; alle übrigen — auch unbekannte — Stati blockieren); Abschnitt „Zustellung und Abschluss" (Z. 104) um „Verknüpfung unabhängig vom sauberen Abschlusspfad" ergänzen; Warteschlangen-/Reihenfolgeregel (Kontext Z. 96, 122: „früheres Ereignis blockiert die Warteschlange") um die transient/terminal-Differenzierung ergänzen — terminal ungeklärte Vorgänger blockieren die Warteschlange nicht mehr. In der Status-Tabelle außerdem den Sonderfall `Wartend` mit `NextAttemptUtc = long.MaxValue` vermerken (Provider-Auth-/Berechtigungs- und Konfigurations-/Startfehler): bewegt sich ohne Benutzereingriff ebenfalls nicht weiter, bleibt bewusst blockierend — sichtbar in der Queue, über „Erneut prüfen" lösbar (Scope-Ausschluss 7).
- `docs/help/plugins/github-ereignisse/ablauf-technisch.md` — Passagen zu `ScmTaskCompletionMonitoringService` (Z. 61), `LinkReportedPullRequestAsync` (Z. 57) und der Vorgängerprüfung der Warteschlange (Z. 69: „behandelt `Abgebrochen` wie `Verarbeitet`" → nun: alle nicht-transienten Stati geben frei) an die neue Regel anpassen.
- `docs/help/plugins/github-ereignisse/datenmodell.md`, `beschreibung.md`, `ablauf-anwender.md`, `troubleshooting.md` sowie `docs/help/aufgaben/datenmodell.md` — Status-/Abschlusswirkung prüfen und wo nötig ergänzen (u. a. Troubleshooting-Hinweis „Aufgabe wird nicht automatisch beendet" und Folgeereignis-Zustellung hinter ungeklärtem Vorgänger).

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---|---|---|
| `complete.json` / `ScmExchangeComplete` | Unverändert: `pullRequestNumber ≤ 0` macht den Abschluss ungültig (`Diagnostic`, `Complete` = `null`); Registrierung nur aus validem `Complete` | `ScmEventExchangeProtocol.ReadAsync` (Bestand) |
| `pullRequestNumber` in `SaveReportedAsync` | Unverändert: `ArgumentOutOfRangeException` bei `≤ 0`; fremd zugeordnete Referenz → `null` | `PullRequestReferenceService` (Bestand) |

## Konfigurationsänderungen

Keine. Bestehende Schalter `scm.taskcompletion.enabled`, `scm.taskcompletion.scope`, `scm.taskcompletion.wait.minutes` werden unverändert genutzt.

## Seiteneffekte und Risiken

- **Verfrühter Countdown bei `ZustellungUngewiss`:** War die Prompt-Zustellung ungewiss, kam sie aber faktisch an, kann die Abschlussbeobachtung starten, bevor der Agent antwortet. Gemildert durch den 2-s-Kanalabgleich (Status hebt bei eintreffendem `accepted.json` nach `InBearbeitung` und blockiert wieder) und durch die Wartezeit vor dem tatsächlichen Abschluss.
- **Zustellung trotz ungeklärtem Vorgänger:** Ein Folgeereignis hinter `VerarbeitungUngeklärt`/`ZustellungUngewiss` wird zugestellt, obwohl der faktische Zustellstand des Vorgängers unbekannt ist (Anwender-Entscheidung). War der Vorgänger-Prompt faktisch angekommen und die Sitzung lebt noch, ordnen `WaitForInputReadyAsync`/`IsBusy` den Folgeprompt derselben Sitzung hinten an — die Eingangsreihenfolge bleibt gewahrt; versuchsgetrennte Kanäle (`attemptId`-Verzeichnisse) verhindern Antwortvermischung. Verbleibendes Restrisiko: ein später manuell neu gesendeter Vorgänger (`ResendAsync`) kann eine fachlich bereits bearbeitete Anfrage erneut ausführen — dem manuellen Neuversand ohnehin inhärent.
- **Kaskade freigegebener Folgeereignisse:** Bei mehreren `Wartend`-Folgeereignissen hinter einem terminalem Vorgänger wird jeweils nur das älteste freigegeben; seine transienten Stati sperren die jüngeren weiter. Endet auch das Folgeereignis terminal ungeklärt (z. B. weil die Ursache des Vorgänger-Fehlers fortbesteht), kaskadiert die Freigabe weiter — jede Stufe bleibt einzeln in der Queue sicht- und klärbar statt eines kollektiven, unsichtbaren Stillstands.
- **Verwaistes ungeklärtes Ereignis:** Wird eine Aufgabe automatisch abgeschlossen, bleibt ein offenes `VerarbeitungUngeklärt`-/`ZustellungUngewiss`-Ereignis sichtbar in der Queue (weiter manuell klärbar); `DevelopmentProcessService.CompleteAsync` räumt `ScmIncomingEvents` nicht auf (verifizierter Bestand).
- **Zusätzlicher DB-Lookup:** Die Dedupe führt je Abgleich mit gemeldeter `pullRequestNumber` eine `PullRequestReferences`-Query aus — nur bei vorhandenem `complete.json` mit PR-Nummer, vernachlässigbar.
- **Divergenzrisiko Literal-Stati:** Die übrigen Literal-Vergleiche (`ScmEventQueueViewModel`, `MainWindowViewModel`, `ScmEventExchangeService`, `ScmDispatchPriority`, Orchestrator-intern) werden bewusst nicht migriert — `ScmIncomingEventStatus` ist vorerst nur an den Gates und der Vorgängerprüfung verbindlich; spätere Statusänderungen müssen die Klassifikation mitpflegen (in Doku/Kommentar festzuhalten).
- **Fremd-zugeordnete PR-Referenz:** Wiederholter `GitAction`-Eintrag wird über `LogEntries`-Dedupe verhindert; liegt kein identischer Eintrag vor (z. B. Altbestand vor dem Change), entsteht genau ein neuer Eintrag — akzeptabel.

## Umsetzungsreihenfolge

1. **`ScmIncomingEventStatus`-Klassifikation anlegen**
   - Voraussetzungen: keine
   - Beschreibung: statische Klasse mit Konstanten aller zehn `Status`-Werte, `NonBlocking`-Freigabeliste (`Verarbeitet`, `Abgebrochen`, `VerarbeitungUngeklärt`, `ZustellungUngewiss`), `BlocksTaskCompletion` und `IsTransient` (beide `!NonBlocking.Contains(status)`); Kommentar begründet die negative Prüfrichtung (unbekannte Werte blockieren konservativ), nennt beide Verwender (Abschluss-Gate, Vorgängerprüfung) und das EF-Verbot für die Komfortmethoden in Query-Ausdrücken.
2. **Gate-Umbau in `ScmTaskCompletionMonitoringService.CheckTaskAsync`**
   - Voraussetzungen: Schritt 1
   - Beschreibung: beide `AnyAsync`-Prüfungen auf `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` umstellen (SQL `NOT IN`), Kommentare aktualisieren.
3. **Reihenfolge-Prüfung in `ScmEventOrchestrator.DispatchCoreAsync` differenzieren**
   - Voraussetzungen: Schritt 1
   - Beschreibung: Vorgänger-`AnyAsync` (Z. 182) auf `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` umstellen — **nicht** `IsTransient`, da ein statischer Methodenaufruf im EF-Ausdruck nicht nach SQL übersetzbar ist; Wartegrund-Text bleibt.
4. **Dedupe in `LinkReportedPullRequestAsync`**
   - Voraussetzungen: keine
   - Beschreibung: Vorab-Lookup auf `PullRequestReferences`; bereits verknüpft → still; fremd zugeordnet → einmaliger Protokolleintrag (`LogEntries`-Dedupe); sonst `SaveReportedAsync` + Eintrag wie bisher.
5. **PR-Registrierung in `ReconcileAttemptAsync` entkoppeln**
   - Voraussetzungen: Schritt 4
   - Beschreibung: `pullRequestNumber`-Auswertung aus dem `Accepted`-Block hinter den Block verschieben, `IsCancelledAsync`-Guard ergänzen.
6. **Integrationstests Gate-Differenzierung**
   - Voraussetzungen: Schritt 2
   - Beschreibung: parametrisierte Fälle je Status in `ScmTaskCompletionMonitoringServiceTests` (blockierend vs. nicht blockierend, inkl. Auto-Abschluss nach Wartezeit).
7. **Integrationstests Reihenfolge-Regel**
   - Voraussetzungen: Schritt 3
   - Beschreibung: neue `ScmEventDispatchTests_Ordering.cs` — Folgeereignis hinter terminalem Vorgänger wird zugestellt; hinter transientem Vorgänger bleibt es `Wartend`; Reihenfolge unter mehreren Folgeereignissen.
8. **Integrationstests PR-Entkopplung**
   - Voraussetzungen: Schritt 5
   - Beschreibung: `complete.json` mit `pullRequestNumber` ohne `accepted.json` → `PullRequestReference` + genau ein Protokolleintrag; Wiederholungsabgleich dedupliziert; verspätetes `complete.json` wird nachgeholt; `accepted.json` + `complete.json` mit `pullRequestNumber` bei scheiternder `Complete`-Mengenprüfung → `VerarbeitungUngeklärt`, PR wird trotzdem registriert.
9. **Unit-Test Status-Klassifikation**
   - Voraussetzungen: Schritt 1
   - Beschreibung: alle zehn Werte gegen die verbindliche Einteilung prüfen (`NonBlocking` = exakt die vier Freigabe-Stati; unbekannter Wert → blockierend); `BlocksTaskCompletion`/`IsTransient` decken die Negativmenge konsistent ab.
10. **Dokumentation des Status-Verhaltens (Akzeptanzkriterium 3)**
    - Voraussetzungen: Schritte 2, 3 und 5 (fachlich fixierte Regeln)
    - Beschreibung: `business-rules.md` (Status-Tabelle/Wirkung je Status + entkoppelte Verknüpfung + Warteschlangenregel), `ablauf-technisch.md` (u. a. Vorgängerprüfung), `datenmodell.md` und ggf. `beschreibung.md`/`ablauf-anwender.md`/`troubleshooting.md`/`docs/help/aufgaben/datenmodell.md` aktualisieren.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| `NonBlocking_ClassifiesAllKnownStatuses` | `ScmIncomingEventStatusTests` (`src/Softwareschmiede.Tests`, neu) | `NonBlocking` enthält exakt die vier Freigabe-Stati (`Verarbeitet`, `Abgebrochen`, `VerarbeitungUngeklärt`, `ZustellungUngewiss`); alle zehn Statuswerte verbindlich eingeordnet; unbekannter Status blockiert (ist nicht in `NonBlocking`); `BlocksTaskCompletion`/`IsTransient` geben für alle zehn Werte `true` außerhalb bzw. `false` innerhalb der Freigabeliste. |
| `Monitor_EventWithStatus_CompletionGate` (Theory, MemberData über alle Stati) | `ScmTaskCompletionMonitoringServiceTests` (`src/Softwareschmiede.IntegrationTests`) | Aufgabe mit geschlossener Referenz + Ereignis in Status X: `VerarbeitungUngeklärt`/`ZustellungUngewiss`/`Verarbeitet`/`Abgebrochen` → `ScmCompletionDetectedUtc` gesetzt; `Aufgenommen`/`Verzögert`/`Wartend`/`ZustellungVorbereitet`/`Zugestellt`/`InBearbeitung` → nicht gesetzt. |
| `Monitor_UnresolvedEvent_CompletesTaskAfterWait` | `ScmTaskCompletionMonitoringServiceTests` | `VerarbeitungUngeklärt`-Ereignis + gemergte PR-Referenz → nach `wait.minutes` `WorkItemStatus.Completed` (Happy Path des Issues). |
| `Dispatch_FollowUpBehindTerminalUnresolved_IsDelivered` (Theory über `VerarbeitungUngeklärt`/`ZustellungUngewiss`/`Verarbeitet`/`Abgebrochen`) | `ScmEventDispatchTests_Ordering.cs` (`src/Softwareschmiede.IntegrationTests`, neu) | Zweites Ereignis derselben Aufgabe (Vorgänger-Status per `ExecuteUpdate` gesetzt, Folgeereignis ohne `DeliveryAttemptId`) verlässt `Wartend` und geht in die Zustellung (`Zugestellt`/Prompt am Simulator) — kein Dauer-`Wartend` hinter einem terminalen/finalen Vorgänger. |
| `Dispatch_FollowUpBehindTransientPredecessor_StaysWaiting` (Theory über `Aufgenommen`/`Verzögert`/`Wartend`/`ZustellungVorbereitet`/`Zugestellt`/`InBearbeitung`) | `ScmEventDispatchTests_Ordering.cs` | Folgeereignis bleibt `Wartend` mit dem Reihenfolge-Wartegrund „Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden." — Reihenfolge-Garantie bei transienten Vorgängern unverändert. |
| `Dispatch_MultipleFollowUps_KeepRelativeOrder` | `ScmEventDispatchTests_Ordering.cs` | Zwei Folgeereignisse hinter `VerarbeitungUngeklärt`-Vorgänger: das ältere wird zugestellt, das jüngere bleibt `Wartend`, bis das ältere final/terminal ist — Reihenfolge unter Folgeereignissen gewahrt; nach `Verarbeitet` des Älteren wird das Jüngere freigegeben. |
| Hilfsmethode `AddEventToTaskAsync(taskId)` | `ScmEventDispatchTests_Ordering.cs` | Zweites/drittes `ScmIncomingEvent` auf derselben Aufgabe über `ScmEventStore.AcceptBatchAsync` anlegen (größere `Sequence`); Vorgänger-Status per `ExecuteUpdate` setzen (`ScmEventDispatchTests_HostRetry`-Muster). |
| `Reconcile_CompleteWithPullRequestNumberWithoutAccepted_RegistersReference` | neue oder bestehende `ScmEventDispatchTests_*` (`src/Softwareschmiede.IntegrationTests`) | Kanal enthält nur `complete.json` mit `pullRequestNumber` (kein `accepted.json`) → `PullRequestReference` (Rolle `CreatedByTask`) + genau ein `GitAction`-Protokolleintrag; Ereignisstatus bleibt ungeklärt. |
| `Reconcile_ReportedPullRequest_RepeatedReconcile_LogsOnce` | `ScmEventDispatchTests_*` | Zweiter Abgleich desselben Ereignisses erzeugt keinen zweiten Protokolleintrag und keine zweite Referenz (Dedupe). |
| `Reconcile_LateCompleteAfterUnresolved_RegistersReference` | `ScmEventDispatchTests_*` | `complete.json` mit `pullRequestNumber` taucht erst nach dem `VerarbeitungUngeklärt`-Übergang auf → Registrierung beim Folgeabgleich. |
| `Reconcile_FailedCompletenessCheck_StillRegistersReference` | `ScmEventDispatchTests_*` | `accepted.json` vorhanden + `complete.json` mit `pullRequestNumber`, aber die `Complete`-Mengenprüfung scheitert (gemeldete `ActionIds` unvollständig/fehlgeschlagen) → Ereignis endet `VerarbeitungUngeklärt`, die `PullRequestReference` wird trotzdem registriert (entkoppelter Pfad). |
| Hilfsmethode `SeedEventWithExchangeAsync(status, completeJson?)` | `ScmTaskCompletionMonitoringServiceTests` / `ScmDispatchFixture` | Ereignis mit gesetztem `DeliveryAttemptId`/`CliRunId`/`ExchangePath` im gewünschten Status anlegen (per `ExecuteUpdate`-Muster wie in `ScmEventDispatchTests_HostRetry`) und Dateien ins Versuchsverzeichnis schreiben. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|---|---|
| `Monitor_OpenPullRequestOrPendingEvent_KeepTaskRunning` (`ScmTaskCompletionMonitoringServiceTests`) | Prüft das Gate für `Aufgenommen` — bleibt blockierend, Test besteht unverändert (Regressionssicherung). |
| `Complete_ReportedPullRequestNumber_LeftReferenceToTask` (`ScmTaskCompletionMonitoringServiceTests`) | Sauberer `Accepted`-Pfad bleibt funktional; durch Dedupe weiterhin genau ein Protokolleintrag — besteht unverändert. |
| `ScmEventQueueViewModelTests` | Keine UI-Änderung — unverändert. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|---|---|---|---|---|
| — | Keine neuen E2E-Tests | — | — | Die Änderung liegt vollständig in Hintergrunddiensten (`ScmTaskCompletionMonitoringService`, `ScmEventOrchestrator`); es gibt **keinen neuen oder geänderten UI-Eingriffspunkt** — weder sichtbare Aktion noch Anzeige ändert sich. Die Integrationstests fahren den kompletten Dienstpfad mit echter SQLite-DB, allen realen Services und gemockten Plugins und weisen die sichtbaren Wirkungen (`WorkItemStatus.Completed`, `PullRequestReference`-Zeile, `LogEntries`) nach. Ein FlaUI-E2E würde denselben Dienstpfad nur über die App-Hülle wiederholen, ohne eine zusätzliche Benutzerhandlung zu prüfen. |

Betroffene bestehende E2E-Tests: **keine.** `E2E_ScmEvents` deckt Dispatch-, Neuversand- und Statuspfade ab — Statuswertemenge, `ResendAsync`/`CancelAsync`-Verhalten und Queue-Anzeige bleiben unverändert; die Datei enthält keinen Abschlussüberwachungs-Fall. `E2E_SettingsFeatureFlags` prüft nur die Sichtbarkeit der Schalter `IsScmTaskCompletionEnabled`/`ScmTaskCompletionWaitMinutes` — unverändert.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|-----------------|----------|-------------------|----------|
| 1 | Auto-Abschluss trotz ungeklärtem Ereignis (Happy Path) | Host-/CLI-Neustart während offener Zustellung → `VerarbeitungUngeklärt`; Issue/PR remote geschlossen | Aufgabe `Started` mit `IssueReference`, Ereignis `VerarbeitungUngeklärt` („Korrelierter Abschluss fehlt oder CLI-Sitzung beendet."), `ScmCompletionDetectedUtc` = `null` | 1. Poll `ScmTaskCompletionMonitoringService` → Gate lässt durch → `DetermineClosureAsync` meldet geschlossen. 2. `ScmCompletionDetectedUtc`/`ScmCompletionReference` werden persistiert + `SystemMessage`-Eintrag. 3. Nach `wait.minutes`: Abschluss unter `ScmTaskCoordinator`-Sperre → `CompleteAsync`. | Aufgabe `Completed`; Protokoll zeigt Erkennungs- und Abschlusseintrag; ungeklärtes Ereignis bleibt sichtbar in der Queue | Integrationstest `Monitor_UnresolvedEvent_CompletesTaskAfterWait`; DB-Felder `ScmAbschlussErkanntUtc`, `WorkItem.Status` |
| 2 | PR-Registrierung ohne `accepted.json` | `complete.json` mit `pullRequestNumber` im Versuchskanal, `accepted.json` fehlt | Ereignis `VerarbeitungUngeklärt` mit `DeliveryAttemptId`/`ExchangePath`; keine `PullRequestReference` vorhanden | 1. `ScmEventExchangeService`-Abgleich → `ReconcileAttemptAsync`. 2. `Accepted` = `null`, `Complete.PullRequestNumber > 0` → `LinkReportedPullRequestAsync` → `SaveReportedAsync`. | `PullRequestReference` (Rolle `CreatedByTask`, Status `Open`) an der Aufgabe; ein `GitAction`-Eintrag „Pull Request #N … verknüpft"; Ereignis bleibt ungeklärt; Folgeabgleiche erzeugen keine weiteren Einträge | Integrationstests `Reconcile_CompleteWithPullRequestNumberWithoutAccepted_RegistersReference`, `Reconcile_ReportedPullRequest_RepeatedReconcile_LogsOnce` |
| 3 | Transientes Ereignis blockiert weiterhin | Zustellung läuft regulär | Aufgabe mit geschlossener Referenz + Ereignis `Zugestellt` (oder anderer transienter Status) | Poll → Gate 1: Status ∉ `NonBlocking` → `return` | Kein `ScmCompletionDetectedUtc`, kein Countdown, kein Abschluss | Theory `Monitor_EventWithStatus_CompletionGate` (sechs blockierende Fälle) |
| 4 | Verspätetes `complete.json` wird nachgeholt (Wiederaufnahme) | Agent schreibt `complete.json` nach dem `VerarbeitungUngeklärt`-Übergang | Ereignis `VerarbeitungUngeklärt`, Kanal anfangs ohne `complete.json` | 1. Abgleiche ohne `Complete` → keine Registrierung. 2. `complete.json` mit `pullRequestNumber` erscheint. 3. Nächster 2-s-Abgleich registriert. | `PullRequestReference` nachträglich angelegt + ein Protokolleintrag | Integrationstest `Reconcile_LateCompleteAfterUnresolved_RegistersReference` |
| 5 | Wieder geöffnete Referenz setzt Countdown zurück (Bestand, neue Ausgangslage) | Referenz öffnet sich nach Erst-Erkennung erneut | Aufgabe mit `ScmCompletionDetectedUtc` gesetzt + ungeklärtem Ereignis | Poll → `DetermineClosureAsync` = `null` → Reset + `SystemMessage` | `ScmCompletionDetectedUtc`/`ScmCompletionReference` = `null`, Protokolleintrag „wieder offen" | Bestandstest `Monitor_ReopenedPullRequest_ResetsCountdown` (analoge Abdeckung) |
| 6 | `Wartend`-Folgeereignis hinter terminalem Vorgänger wird zugestellt | Ereignis 1 endet `VerarbeitungUngeklärt` (z. B. CLI-Sitzung beendet); Ereignis 2 derselben Aufgabe steht auf `Wartend` | Zwei `ScmIncomingEvents` einer Aufgabe: E1 `VerarbeitungUngeklärt` (kleinere `Sequence`), E2 `Wartend` ohne `DeliveryAttemptId` | 1. Nächster `DispatchDueEventsAsync`-Poll wählt E2 (kein `NextAttemptUtc`). 2. Vorgängerprüfung: E1 ist nicht transient → keine Reihenfolge-Sperre. 3. E2 läuft in den regulären Zustellpfad (CLI-Start bzw. lebende Sitzung) → `Zugestellt`. | Folgeprompt wird verarbeitet statt ewig zu warten; E2 durchläuft `ZustellungVorbereitet`→`Zugestellt`→`InBearbeitung`→`Verarbeitet` und blockiert dabei die Abschlussprüfung regulär | Integrationstest `Dispatch_FollowUpBehindTerminalUnresolved_IsDelivered` |
| 7 | Reihenfolge unter Folgeereignissen bleibt gewahrt | E1 `VerarbeitungUngeklärt`, zwei Folgeereignisse E2/E3 `Wartend` | Drei Ereignisse einer Aufgabe, `Sequence` E1 < E2 < E3 | 1. Poll: E2 wird zugestellt (E1 nicht transient), E3 bleibt `Wartend` (E2 transient). 2. E2 erreicht `Verarbeitet`. 3. Nächster Poll gibt E3 frei. | Prompts erreichen die CLI in Eingangsreihenfolge; kein Überholen unter den Folgeereignissen | Integrationstest `Dispatch_MultipleFollowUps_KeepRelativeOrder` |
| 8 | Transienter Vorgänger hält Folgeereignis weiterhin zurück (Bestand) | Zustellung von E1 läuft regulär (`Zugestellt`/`InBearbeitung`) | E1 transient, E2 `Wartend` | Poll → Vorgängerprüfung erkennt transienten Vorgänger → `Wartend` mit Reihenfolge-Wartegrund | Keine Zustellung von E2 vor Abschluss von E1 | Theory `Dispatch_FollowUpBehindTransientPredecessor_StaysWaiting` |

## Offene Punkte

Keine.
