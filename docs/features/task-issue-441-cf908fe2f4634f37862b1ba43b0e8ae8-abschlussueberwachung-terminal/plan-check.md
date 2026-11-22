# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Der überarbeitete Plan deckt alle drei Akzeptanzkriterien ab, trifft verbindliche Entscheidungen zu allen sechs Offenen Fragen der Anforderung, enthält einen vollständigen Happy Path, sieben Scope-Ausschlüsse jeweils mit Begründung und Status, benannte Seiteneffekte/Risiken, eine ausführbare Umsetzungsreihenfolge mit Voraussetzungen sowie eine nachvollziehbare E2E-Begründung. Beide Lücken aus `plan-check.1.md` sind geschlossen — die Status-Klassifikation ist jetzt konsistent als negative Prüfrichtung über die Freigabeliste `NonBlocking` (`NOT IN`) spezifiziert, und alle EF-Ausdrücke (Gate 1, Gate 2, Vorgängerprüfung) verwenden ausschließlich den SQL-übersetzbaren `!NonBlocking.Contains(x.Status)`-Ausdruck; das EF-Verbot für die statischen Komfortmethoden ist explizit dokumentiert. Die Umstellung hat keine neuen Inkonsistenzen eingeführt; drei Hinweise aus der ersten Prüfrunde (Residual-Blocker `Wartend` + `NextAttemptUtc = long.MaxValue`, Verweisfehler auf die Offene Frage, optionaler Testfall zur fehlschlagenden Mengenprüfung) wurden zusätzlich aufgegriffen.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Auto-Abschluss trotz terminalem `VerarbeitungUngeklärt`-Ereignis ODER sichtbarer/lösbarer Blocker | Beide Gates in `CheckTaskAsync` (Z. 84–86, Z. 133–135) fragen `!ScmIncomingEventStatus.NonBlocking.Contains(x.Status)` ab; `VerarbeitungUngeklärt`/`ZustellungUngewiss` stehen verbindlich in der Freigabeliste (Designentscheidung „Verbindliche Einteilung", neue Klasse `ScmIncomingEventStatus`). Sichtbarkeits-Variante bewusst nicht gewählt (Scope-Ausschluss 3, „bestätigt") — zulässig, da das Kriterium eine ODER-Alternative ist | `Monitor_UnresolvedEvent_CompletesTaskAfterWait` (vollständiger Happy Path inkl. Gate 2 und Auto-Abschluss), `Monitor_EventWithStatus_CompletionGate` (Theory über alle zehn Stati: vier Freigabe-Stati → `ScmCompletionDetectedUtc` gesetzt, sechs transiente → nicht gesetzt) | Abgedeckt |
| `pullRequestNumber`-Registrierung auch bei unsauberem Ereignisabschluss | `messages.Complete is { PullRequestNumber: > 0 }` wird in `ReconcileAttemptAsync` hinter den `Accepted`-Block verschoben (`IsCancelledAsync`-Guard); Dedupe-Vorprüfung in `LinkReportedPullRequestAsync` verhindert Referenz-/Log-Duplikate beim 2-s-Wiederholungsabgleich; verspätetes `complete.json` wird vom laufenden Abgleich nachgeholt (Wiederaufnahmeweg dokumentiert) | `Reconcile_CompleteWithPullRequestNumberWithoutAccepted_RegistersReference`, `Reconcile_ReportedPullRequest_RepeatedReconcile_LogsOnce`, `Reconcile_LateCompleteAfterUnresolved_RegistersReference`, `Reconcile_FailedCompletenessCheck_StillRegistersReference`; Bestandstest `Complete_ReportedPullRequestNumber_LeftReferenceToTask` bleibt unverändert gültig | Abgedeckt |
| Dokumentiertes Verhalten für alle Ereignis-Stati | Umsetzungsschritt 10: verbindliche Status-Einteilung in `business-rules.md` (Z. 112–114, Z. 104, Z. 96/122 inkl. `Wartend`-/`NextAttemptUtc`-Sonderfall), `ablauf-technisch.md` (Z. 57, 61, 69), `datenmodell.md`, ggf. `beschreibung.md`/`ablauf-anwender.md`/`troubleshooting.md`/`docs/help/aufgaben/datenmodell.md` | Doku-Lieferung als Nachweis (kein automatisierter Test möglich); die Einteilung selbst ist über `NonBlocking_ClassifiesAllKnownStatuses` abgesichert | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

—

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Auto-Abschluss trotz ungeklärtem Ereignis (Akzeptanzkriterium 1) | Kein neuer E2E-Test | Nicht erforderlich mit Begründung: Die Änderung liegt vollständig in Hintergrunddiensten (`ScmTaskCompletionMonitoringService`, `ScmEventOrchestrator`); es gibt keinen neuen oder geänderten UI-Eingriffspunkt. Die Integrationstests fahren den kompletten Dienstpfad mit echter SQLite-DB und gemockten Plugins und weisen `WorkItemStatus.Completed`/`ScmAbschlussErkanntUtc` nach. |
| PR-Registrierung bei unsauberem Abschluss (Akzeptanzkriterium 2) | Kein neuer E2E-Test | Nicht erforderlich mit Begründung: reiner Kanalabgleich-Pfad ohne Nutzeraktion; `PullRequestReference`-Zeile und `LogEntries` werden auf Service-Ebene nachgewiesen. |
| Folgeereignis-Zustellung hinter terminalem Vorgänger (mitbeschlossene Scope-Erweiterung aus Offener Frage 5) | Kein neuer E2E-Test | Nicht erforderlich mit Begründung: Dispatch-Zustellpfad ohne UI-Interaktion; Integrationstests in `ScmEventDispatchTests_Ordering.cs` weisen `Zugestellt`/Prompt am Simulator nach. |
| Bestehende E2E-Tests (`E2E_ScmEvents`, `E2E_SettingsFeatureFlags`) | — | Unverändert, plausibel begründet (Statuswertemenge, `ResendAsync`/`CancelAsync`-Verhalten, Queue-Anzeige und Schalter-Sichtbarkeit unverändert; kein Abschlussüberwachungs-Fall in `E2E_ScmEvents`). |

## Fehlende oder unvollständige Planbestandteile

—

## Hinweise

- **Prüfung der Lückenschließung (Runde 2):** Beide Befunde aus `plan-check.1.md` sind adressiert — (1) die Prüfrichtung ist jetzt konsistent negativ (`NOT IN` auf `NonBlocking`) in Übersicht, Designentscheidung, verbindlicher Einteilung, beiden Gates, Vorgängerprüfung, `Neue Klassen`, Umsetzungsschritten 1–3, dem Unit-Test und dem Doku-Plan; „unbekannter Status blockiert" ist damit korrekt. (2) `IsTransient` kommt in keinem EF-Ausdruck mehr vor; das Verbot statischer Methodenaufrufe in Query-Ausdrücken ist an vier Stellen dokumentiert.
- **`BlocksTaskCompletion`/`IsTransient` haben keinen geplanten Aufrufer:** Beide Gates und die Vorgängerprüfung müssen EF-übersetzbare Ausdrücke verwenden; die Komfortmethoden existieren nur für hypothetische Nicht-EF-Aufrufer. Bewusste, dokumentierte Entscheidung — beim Implementieren ggf. prüfen, ob der doppelte Satz Komfortmethoden nötig ist oder eine Methode genügt (beide sind deckungsgleich).
- **Optionaler zusätzlicher Gate-Nachweis:** `Monitor_EventWithStatus_CompletionGate` iteriert über die zehn bekannten Stati; ein zusätzlicher MemberData-Fall mit einem unbekannten Statuswert würde den konservativen Default auch ende-zu-end am Gate belegen (Klassifikations-Unit-Test deckt ihn bereits ab).
- **Konsistenz geprüft, kein Befund:** `ScmEventExchangeService` (nicht-finale Ereignisse → 2-s-Abgleich), `MainWindowViewModel`-Zähler und `ScmEventQueueViewModel`-Filter bleiben bewusst auf Literal-Vergleichen — `VerarbeitungUngeklärt`-/`ZustellungUngewiss`-Ereignisse bleiben wie geplant sichtbar, klärbar und nachgeholt; das Divergenzrisiko ist unter „Seiteneffekte und Risiken" dokumentiert. Die Umstellung der Vorgängerprüfung ist durch die dokumentierte Anwender-Entscheidung zu Offener Frage 5 (2026-11-22) gedeckt und hebelt die Anforderungs-Ausschlussregel nicht ohne Grundlage aus.
