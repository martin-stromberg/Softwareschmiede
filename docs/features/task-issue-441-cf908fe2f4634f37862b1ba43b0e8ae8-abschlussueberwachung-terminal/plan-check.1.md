# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Der Plan deckt alle drei Akzeptanzkriterien inhaltlich ab, trifft verbindliche Entscheidungen zu allen sechs Offenen Fragen der Anforderung, enthält einen vollständigen Happy Path, dokumentierte Scope-Ausschlüsse mit Begründung und Status sowie eine nachvollziehbare E2E-Begründung. Eine Lücke besteht in der widersprüchlichen Spezifikation der Status-Klassifikation (Behandlung unbekannter Statuswerte) sowie der daraus folgenden nicht umsetzbaren Formulierung der EF-Prüfungen.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Auto-Abschluss trotz terminalem `VerarbeitungUngeklärt`-Ereignis ODER sichtbarer/lösbarer Blocker | Gate-Differenzierung in `CheckTaskAsync` (beide Prüfungen, Z. 84–86 und Z. 133–135) von Negativliste auf `CompletionBlocking`-Whitelist; `VerarbeitungUngeklärt`/`ZustellungUngewiss` werden als nicht blockierend eingestuft (Designentscheidung „Verbindliche Einteilung"); Sichtbarkeits-Variante bewusst nicht gewählt (Scope-Ausschluss 3, „bestätigt") — zulässig, da das Kriterium eine ODER-Alternative ist | `Monitor_EventWithStatus_CompletionGate` (Theory über alle zehn Stati), `Monitor_UnresolvedEvent_CompletesTaskAfterWait` (Happy Path inkl. Auto-Abschluss) | Abgedeckt |
| `pullRequestNumber`-Registrierung auch bei unsauberem Ereignisabschluss | `messages.Complete.PullRequestNumber`-Auswertung wird in `ReconcileAttemptAsync` aus dem `Accepted`-Block herausgelöst (`IsCancelledAsync`-Guard); Dedupe-Vorprüfung in `LinkReportedPullRequestAsync` verhindert Log-/Referenz-Duplikate beim 2-s-Wiederholungsabgleich | `Reconcile_CompleteWithPullRequestNumberWithoutAccepted_RegistersReference`, `Reconcile_ReportedPullRequest_RepeatedReconcile_LogsOnce`, `Reconcile_LateCompleteAfterUnresolved_RegistersReference`; Bestandstest `Complete_ReportedPullRequestNumber_LeftReferenceToTask` bleibt grün | Abgedeckt |
| Dokumentiertes Verhalten für alle Ereignis-Stati | Schritt 10 der Umsetzungsreihenfolge: Status-Tabelle/Wirkung je Status in `business-rules.md` (Z. 112–114, 104, 96/122), `ablauf-technisch.md` (Z. 57, 61, 69), `datenmodell.md`, ggf. `beschreibung.md`/`ablauf-anwender.md`/`troubleshooting.md`/`docs/help/aufgaben/datenmodell.md` | Doku-Lieferung als Nachweis (kein automatisierter Test möglich); die verbindliche Einteilung ist zusätzlich per `CompletionBlocking_ClassifiesAllKnownStatuses` abgesichert | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] `CompletionBlocking_ClassifiesAllKnownStatuses` spezifiziert „unbekannter Status blockiert" — mit der im Plan festgelegten Gate-Implementierung `CompletionBlocking.Contains(x.Status)` (Whitelist) ist ein unbekannter Status jedoch nicht in der Menge und blockiert damit **nicht**. Der Test ist wie beschrieben gegen die beschriebene Implementierung nicht erfüllbar (Bezug: Akzeptanzkriterium 1).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Auto-Abschluss trotz ungeklärtem Ereignis (Akzeptanzkriterium 1) | Kein neuer E2E-Test | Nicht erforderlich mit Begründung: Die Änderung liegt vollständig in Hintergrunddiensten (`ScmTaskCompletionMonitoringService`, `ScmEventOrchestrator`); es gibt keinen neuen/geänderten UI-Eingriffspunkt und keinen Benutzerfluss. Die Integrationstests fahren den kompletten Dienstpfad mit echter SQLite-DB und gemockten Plugins und weisen `WorkItemStatus.Completed`/`ScmAbschlussErkanntUtc` nach. |
| PR-Registrierung bei unsauberem Abschluss (Akzeptanzkriterium 2) | Kein neuer E2E-Test | Nicht erforderlich mit Begründung: reiner Kanalabgleich-Pfad ohne Nutzeraktion; `PullRequestReference`-Zeile und `LogEntries` werden auf Service-Ebene nachgewiesen. |
| Folgeereignis-Zustellung hinter terminalem Vorgänger (mitbeschlossene Scope-Erweiterung) | Kein neuer E2E-Test | Nicht erforderlich mit Begründung: Dispatch-Zustellpfad ohne UI-Interaktion; Integrationstests in `ScmEventDispatchTests_Ordering.cs` weisen `Zugestellt`/Prompt am Simulator nach. |
| Bestehende E2E-Tests (`E2E_ScmEvents`, `E2E_SettingsFeatureFlags`) | — | Unverändert, plausibel begründet (Statuswertemenge, Resend/Cancel-Verhalten, Queue-Anzeige und Schalter-Sichtbarkeit unverändert). |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Widersprüchliche Festlegung für unbekannte/zukünftige Statuswerte.** Die Designentscheidung begründet den Whitelist-Ansatz mit „unbekannte/zukünftige Stati blockieren konservativ", und der Unit-Test spezifiziert „unbekannter Status blockiert". Beide Gate-Prüfungen (Umsetzungsschritte 2 und 3) sind aber als `CompletionBlocking.Contains(x.Status)` beschrieben — ein unbekannter Status ist nicht in der Whitelist und passiert das Gate damit (Whitelist = permissiv, nicht konservativ). Zu entscheiden ist: entweder „unbekannter Status blockiert" aus Begründung und Test streichen, oder die Prüfrichtung umkehren (Negativliste `Status NOT IN ('ZustellungUngewiss','VerarbeitungUngeklärt','Verarbeitet','Abgebrochen')` bzw. `!NonBlocking.Contains(x.Status)`), damit Klassifikation, Gate und Test übereinstimmen.
- [ ] **`IsTransient(x.Status)` in der Vorgänger-`AnyAsync` (Umsetzungsschritt 3) ist als EF-Prädikat nicht nach SQL übersetzbar** — ein statischer Methodenaufruf in `AnyAsync`/`Where` schlägt zur Laufzeit fehl. Der Plan muss hier (wie am Gate) eine übersetzbare Mengenprüfung vorsehen (`CompletionBlocking.Contains` oder die Negativform — abhängig von der Entscheidung zum vorherigen Punkt).

## Hinweise

- **Residual-Blocker über `Wartend` + `NextAttemptUtc = long.MaxValue`:** Provider-Authentifizierungs-/Berechtigungsfehler und Konfigurations-/Startfehler setzen `Status = "Wartend"` mit `NextAttemptUtc = long.MaxValue` (`ScmEventOrchestrator.cs` Z. 336/342) — diese Ereignisse können sich ebenfalls **nicht ohne Benutzereingriff** weiterbewegen, bleiben aber als „transient/blockierend" eingestuft. Konservativ vertretbar (sie sind in der Queue sichtbar und über „Erneut prüfen" lösbar), aber dasselbe Dauerblocker-Muster wie `VerarbeitungUngeklärt` — die Begründung „transient = kann sich ohne Benutzereingriff weiterbewegen" stimmt für diesen Fall nicht. Empfehlung: einen Satz dazu in der Doku-Status-Tabelle bzw. den Scope-Ausschlüssen festhalten.
- **Verweis-Fehler:** Scope-Ausschluss 4 nennt „ehemals Offenen Punkt 1" — gemeint ist die Offene Frage 5 (Reihenfolge-Regel) der Anforderung. Die Aufweichung der Reihenfolge-Regel ist gegenüber dem Scope-Ausschluss der Anforderung nur über die dokumentierte Anwender-Entscheidung gedeckt; diese sollte im Planungscommit nachvollziehbar bleiben.
- **Optionaler zusätzlicher Testfall:** `accepted.json` vorhanden + `complete.json` mit `pullRequestNumber`, aber fehlschlagende Mengenprüfung → Ereignis endet `VerarbeitungUngeklärt`, PR wird trotzdem registriert. Vom entkoppelten Pfad abgedeckt, aber nicht explizit benannt.
- **Verifizierte Planannahmen (stichprobenartig gegen den Code):** beide Gates in `CheckTaskAsync` (Z. 84–86, 133–135) und die Vorgängerprüfung (Z. 182) existieren wie beschrieben; `LinkReportedPullRequestAsync` liegt innerhalb des `Accepted`-Blocks (Z. 578–579); `WaitAsync` setzt kein `NextAttemptUtc` (Z. 1002–1007), sodass `Wartend`-Ereignisse von `DispatchDueEventsAsync` (Z. 44) erneut gewählt werden; `ReconcileAttemptAsync` wird für `VerarbeitungUngeklärt`-Ereignisse mit `DeliveryAttemptId` weiter im 2-s-Takt getrieben (`ScmEventExchangeService` Z. 27) — der Wiederaufnahmeweg für verspätetes `complete.json` ist real. Keine bestehenden Tests gefunden, die das alte Blockierverhalten für `VerarbeitungUngeklärt`-/`ZustellungUngewiss`-Vorgänger oder -Ereignisse festschreiben; die im Plan genannten betroffenen Bestandstests sind plausibel vollständig.
