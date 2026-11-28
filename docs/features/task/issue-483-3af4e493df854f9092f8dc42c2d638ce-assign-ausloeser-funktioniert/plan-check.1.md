# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Der Plan deckt die Anforderung inhaltlich vollständig ab: beide Schutzebenen sind als Umsetzungsschritte konkret beschrieben (Vorfilter in `AddObservation` auf Kommentar-Arten beschränken; Marker-Ablehnung aus `GetAssignRejection` entfernen), alle Ablehnungs-/Fehlerfälle bleiben benannt, die Testplanung weist jedem Akzeptanzpunkt einen konkreten Nachweis zu und die Hilfedoku ist als Schritt enthalten. Einziger Mangel: Der Status von Scope-Ausschluss 2 ist im Plan widersprüchlich — die Scope-Tabelle (plan.md Z. 21) sagt `bestätigt`, der Abschnitt „Offene Punkte" (Z. 179) nennt denselben Ausschluss `Status offen` und verweist auf eine Bestätigung „in Schritt 5a", die in der Umsetzungsreihenfolge nicht existiert. Der offizielle Status des Ausschlusses ist damit uneindeutig.

## Abgleich Akzeptanzkriterien

Aus der Anforderung abgeleitete prüfpflichtige Punkte (requirement.md enthält keinen expliziten Abschnitt „Akzeptanzkriterien"; die Kriterien folgen aus Fachlicher Zusammenfassung, Fachlichem Scope und den Nicht-Anforderungen):

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Marker-tragende, vom Überwachungskonto stammende Beschreibung wird bei berechtigtem Assign als Beobachtung ausgeliefert (Plugin-Ebene) | Schritt 1: Vorfilter in `GitHubPlugin.AddObservation` (Z. 384) auf `IssueComment`/`ReviewComment`/`PullRequestReview` beschränken, `Description` stets liefern | `OwnMarkedDescription_IsDelivered` (`GitHubEventSourceTests`) | Abgedeckt |
| Berechtigte Zuweisung erzeugt `ScmIncomingEvent` inkl. Aufgabenbindung trotz Marker (Host-Ebene) | Schritt 2: Marker-Glied Z. 106–107 aus `GetAssignRejection` entfernen; Akzeptanzkette = nicht-leer + bestimmbarer Zuweisender + Kreis | `GetRejection_AssignOwnResponseMarker_Accepted` (Umwandlung), Integrationstest `…_AssignViewerAuthoredMarkerDescription_CreatesTaskAndDispatches` | Abgedeckt |
| Zustellung über `DispatchCoreAsync` mit `AssigneePromptPrefix`; Zustellungs-Gegenprüfung akzeptiert dieselbe persistierte Beobachtung | Programmablauf Schritt 6 (Trigger aus `ObservationJson` läuft erneut durch `GetAssignRejection`); keine eigene Codeänderung nötig | Integrationstest prüft Prompt mit `AssigneePromptPrefix` und `Trigger.Reason == "assign"` | Abgedeckt |
| Zuweisender nicht bestimmbar / kein wirksames Zuweisungsereignis → `AssignerUnknownRejection` unverändert | Scope-Klärung „Umgang mit fehlenden Objekten"; Szenario 2 | Bestehender `Monitor_SeparatesAccount_AssignUndeterminable_Rejected`, `…_AssignWithoutCapability_Rejected` | Abgedeckt |
| Zuweisender außerhalb {Owner, Viewer, `TrustedAutomationLogins`} → `AssignerNotAllowedRejection`; Texttrigger-Fallback bleibt wirksam, legitimiert Marker-Body aber nicht | Scope-Klärung; Programmablauf „Assign mit unberechtigtem Zuweisenden"; Szenario 3 | Neuer `GetRejection_AssignForeignAssignerWithMarker_StaysRejected`; bestehende `…_AssignByForeignLogin_Rejected`, `GetRejection_AssignForeignAssignerWithMentionAndMarker_StaysRejected` | Abgedeckt |
| Leere Beschreibung → `AssignEmptyDescriptionRejection`; leerer Batch → `EmptyDescriptionBatchDiagnostic` | Scope-Klärung; Validierungsregeln; Szenario 5 | Bestehende `…_AssignEmptyDescription_CreatesNoTask`, `…_AssignEmptyDescriptionBatch_Discarded` (Kommentaranpassung eingeplant) | Abgedeckt |
| Marker-Beschreibung ohne Assign-Kontext (Mention-/Keyword-/Enumerations-Read) → weiterhin „Eigene Antwort"/„Kein Trigger", kein Ereignis | Scope-Ausschluss 1; Programmablauf „Marker-tragende Beschreibung ohne Assign-Kontext"; Szenario 4 | `SeparatesAccount_OwnResponseMarkerStillRejected`, `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop` | Abgedeckt |
| Geltung für Issue- und Pull-Request-Beschreibungen (gemeinsamer `issues/{n}/events`-Pfad) | Designentscheidung „Geltung für Pull-Request-Beschreibungen"; Scope-Klärung Verarbeitungseinheit | `GetAssignRejection` ist referenzart-agnostisch; bestehender `Monitor_SeparatesAccount_AssignOnPullRequest_CreatesTask` deckt den PR-Assign-Pfad | Abgedeckt |
| Keine Änderung an Zuweisender-Kreis, `AlreadyReactedRejection`, Mention-/Keyword-Trigger, ContentKey-/Deduplizierungsmodell | Scope-Ausschluss 3 (bestätigt) | Indirekt über bestehende Suite | Abgedeckt |
| Kein rückwirkender Fix historischer Checkpoints | Scope-Ausschluss 4 (bestätigt) | n/a (Nicht-Anforderung) | Abgedeckt |
| Keine neue Konfiguration, keine UI-Änderung, keine EF-Migration, keine Vertragsänderung (`ScmEventReadRequest`) | Scope-Ausschluss 5 (bestätigt); Abschnitte „Konfigurationsänderungen"/„Datenbankmigrationen" = Keine | n/a | Abgedeckt |
| Dokumentation der Schutzverschiebung (`ablauf-technisch.md`, `business-rules.md`, `troubleshooting.md`) | Schritt 6 mit allen drei Dateien und konkreten Änderungsthemen | n/a | Abgedeckt |
| Präzisierung der Leer-Batch-Diagnose-Kommentare (`ScmEventStore` Z. 25–31, 141–145) | Schritt 5; Abschnitt „Geänderte Dokumentation" | n/a | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — die Testplanung ist vollständig: Jeder geänderte Verhaltenspunkt hat einen konkreten Test (Plugin-Ebene, Validierungsebene, End-to-end-Integration über `ScmDispatchFixture`), betroffene Bestandstests sind benannt, notwendige Fixtures/Hilfsmethoden (`ConfigureDescriptionObservations(body, authorId, authorLogin)`, `ConfigureAssignmentSource`, `BotRepository` mit `BOT`/`scm-bot`) existieren und sind zugeordnet. Negative Fälle (fremder Zuweisender + Marker, Marker ohne Assign-Kontext, leere Beschreibung) sind abgedeckt.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Assign-Auslöser mit marker-tragender Beschreibung (Notification → Read → Validierung → Aufgabe → Dispatch) | Kein FlaUI-E2E; End-to-end-Nachweis über Integrationstest `…_AssignViewerAuthoredMarkerDescription_CreatesTaskAndDispatches` (echte Services, echte SQLite, realer `ScmEventOrchestrator`-Dispatch mit Prompt-Erfassung) | Nicht erforderlich mit Begründung — kein UI-/Nutzerfluss berührt; Auslöser ist eine externe GitHub-Notification, die Wirkung liegt in Hintergrunddiensten. Die Begründung im Plan (Z. 163) ist nachvollziehbar und benennt die kanonische Nachweisebene für diesen Pfad. |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Widersprüchlicher Status von Scope-Ausschluss 2 (keine Mindest-Inhaltsbedingung):** Die Scope-Ausschluss-Tabelle (plan.md Z. 21) trägt den Status `bestätigt (2026-11-27, Issue-Kommentar 7228823021)`, während der Abschnitt „Offene Punkte" (Z. 179) denselben Ausschluss mit `Status offen` führt und eine Bestätigung „in Schritt 5a" ankündigt — einen Schritt, den es in der Umsetzungsreihenfolge (Schritte 1–7) nicht gibt. Zusätzlich steht die Überschrift „Keine" offene Punkte im Widerspruch zu einer darunter genannten noch ausstehenden Bestätigung. Zu vereinheitlichen: entweder den Ausschluss als bestätigt führen und die Zeile in „Offene Punkte" bereinigen, oder — falls die Bestätigung tatsächlich noch aussteht — den Status in der Tabelle auf `offen` korrigieren und den Bestätigungsschritt real in die Umsetzungsreihenfolge aufnehmen.

## Hinweise

- Die PR-Geltung der Aufweichung ist als Designentscheidung festgelegt und über den gemeinsamen Code-Pfad abgesichert (`GetAssignRejection` kennt keine Referenzart); ein dedizierter Test „PR + Marker-Body + berechtigter Assign" ist nicht eingeplant, aber durch die Kombination aus referenzart-agnostischem Unit-Test und bestehendem PR-Assign-Integrationstest kompositional gedeckt. Bei Bedarf ließe sich der neue Integrationstest mit `ScmReferenceKind.PullRequest` spiegeln — kein Lückenbefund.
- Die übrigen drei offenen Fragen der Anforderung sind verbindlich aufgelöst: Plugin-Variante (a) begründet in „Designentscheidungen", Diagnose-Kommentare in Schritt 5, PR-Geltung in Scope-Klärung/Designentscheidungen.
- Seiteneffekte sind vollständig benannt und bewertet (zusätzliche abgelehnte Snapshots ohne Checkpoint-Sichtbarkeit; Schleifenschutz der Issue-Aufbereitung über `SkipEvaluationAsync`/Baseline/Endzustand mit Regressionstest `Monitor_DescriptionChangeAfterPreparation_CreatesNoLoop`; Kommentar-Vorfilter bleibt).
- Umsetzungsreihenfolge ist ausführbar: Schritte 1–7 mit Voraussetzungen; die beiden Codeänderungen sind unabhängig, wirken aber nur gemeinsam — korrekt vermerkt. Verifikationsschritt 7 enthält die projektkonformen Lanes inkl. `SOFTWARESCHMIEDE_SKIP_CONPTY_TESTS=1` und `dotnet format --verify-no-changes`.
