# Übersetzte Anforderung: Abschlussüberwachung — terminal ungeklärtes Ereignis blockiert Auto-Completion dauerhaft

**Quelle:** GitHub-Issue #441
**Branch:** `task/issue-441-cf908fe2f4634f37862b1ba43b0e8ae8-abschlussueberwachung-terminal`

## Fachliche Zusammenfassung

Der `ScmTaskCompletionMonitoringService` bricht die Abschlussprüfung einer Aufgabe (`CheckTaskAsync`) vorzeitig ab, sobald **irgendein** zugeordnetes `ScmIncomingEvents`-Ereignis einen anderen Status als `Verarbeitet`/`Abgebrochen` trägt. Der Status `VerarbeitungUngeklärt` ist jedoch ein terminales Diagnose-Stadium ohne automatischen Neuversand — die Abschlussprüfung ist für eine betroffene Aufgabe damit permanent tot (`ScmCompletionDetectedUtc`, DB-Spalte `ScmAbschlussErkanntUtc`, wird nie gesetzt). Zusätzlich hängt die Registrierung eines im Abschluss gemeldeten Pull Requests (`pullRequestNumber` aus `complete.json` → `PullRequestReference`) aktuell am selben gestörten Abgleichpfad und soll davon entkoppelt werden.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die zyklische *Abschlussprüfung einer einzelnen Aufgabe* (`WorkItem`) mit SCM-Referenz (`IssueReference` oder `PullRequestReference`), deren zugeordnete `ScmIncomingEvents`-Einträge als Gate vor der Referenzbeobachtung wirken. Zum Scope gehört ferner die *Registrierung der `pullRequestNumber` aus `complete.json`* als `PullRequestReference` (Rolle `CreatedByTask`) am selben Ereignisfluss.
- **Auslösende Ereignisse:**
  - Systemereignis: Poll-Zyklus des `ScmTaskCompletionMonitoringService` (`PeriodicBackgroundService`, Intervall aus `ScmEventOptions.PollInterval`) — bei aktivierter Abschlussüberwachung (`scm.taskcompletion.enabled`) und Aufgabe mit SCM-Referenz im konfigurierten Prüfmodus (`ScmTaskCompletionCheckMode`, `scm.taskcompletion.scope`).
  - Entstehung des Fehlerfalls: Beenden/Neustart der Hostanwendung oder der CLI-Sitzung während eines offenen Zustellversuchs → `ScmEventOrchestrator` setzt `VerarbeitungUngeklärt` (u. a. Reason „Korrelierter Abschluss fehlt oder CLI-Sitzung beendet.", `ScmEventOrchestrator.cs` ~Zeilen 587–597).
- **Erwartete Wirkung:** Eine Aufgabe, deren SCM-Referenz remote geschlossen/gemergt ist, erreicht `ScmCompletionDetectedUtc` ≠ `null` und wird nach Ablauf der Wartezeit (`scm.taskcompletion.wait.minutes`) automatisch auf `WorkItemStatus.Completed` gesetzt — auch wenn ein terminal ungeklärtes Ereignis existiert. Alternativ (als gleichwertige Akzeptanzoption) ist der blockierte Zustand für den Anwender sichtbar und lösbar. Zudem wird ein in `complete.json` gemeldetes `pullRequestNumber`-Feld auch bei nicht sauber abgeschlossenem Ereignis als `PullRequestReference` registriert und im Aufgabenprotokoll vermerkt.
- **Umgang mit fehlenden Objekten:**
  - *`PullRequestReference` für die gemeldete PR-Nummer fehlt* (weil der Abgleich die Verknüpfung nie erreichte): nachträglich erzeugen/registrieren — die Verknüpfung über `PullRequestReferenceService.SaveReportedAsync` ist idempotent und darf bei wiederholtem Abgleich erneut erfolgen.
  - *`ScmIncomingEvents`-Eintrag auf `VerarbeitungUngeklärt`*: nicht automatisch neu zustellen (Bestandsregel „kein automatischer Neuversand" bleibt); die Klärung erfolgt weiterhin manuell über die SCM-Ereignisübersicht („Erneut prüfen"/„Erneut senden"/„Abbrechen") oder entfällt als Blocker — die Aufgabe selbst wird nicht verworfen.
  - *Antwortkanal/`ExchangePath` nicht mehr vorhanden:* `complete.json` ist dann nicht mehr einlesbar; eine nachträgliche Registrierung ist nur für lesbare Kanaldaten ableitbar (Annahme — siehe Offene Fragen).
- **Scope-Ausschlüsse:**
  - Kein automatischer Neuversand oder automatische Statusaufhebung für `VerarbeitungUngeklärt`-Ereignisse — das akzeptanzkriterien-konforme Ziel ist die Entblockung der *Abschlussprüfung*, nicht die automatische Klärung des Ereignisses (optionaler Klärungs-/Timeout-Pfad siehe Offene Fragen).
  - Keine Änderung an der Blockwirkung *transienter* Stati: Ereignisse in laufender/wartender Verarbeitung sollen die Referenzbeobachtung weiterhin zurückstellen (verfrühte Abschlusserkennung vermeiden); die konkrete Statusliste ist zu definieren.
  - Kein Eingriff in die Reihenfolge-Regel der Ereigniszustellung (`ScmEventOrchestrator.cs` ~Zeile 182: „Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden.") — Ausnahme nur, falls in den Offenen Fragen anders entschieden.
  - Keine Änderung an `Verarbeitet`/`Abgebrochen`-Semantik, am Abbruch (`CancelAsync`) oder an der Wartezeit-Logik selbst.
  - Aufgaben ohne SCM-Referenz bleiben unbeobachtet (Bestand).

## Betroffene Klassen und Komponenten

### Bestehende, voraussichtlich zu ändernde Artefakte

- `src/Softwareschmiede/Application/Services/ScmTaskCompletionMonitoringService.cs` — `CheckTaskAsync`: Gate vor der Referenzprüfung (Zeilen ~84–86) sowie die identische Wiederholungsprüfung innerhalb der `ScmTaskCoordinator.Shared.RunAsync`-Sperre (Zeilen ~133–135). Die Bedingung `Status != "Verarbeitet" && Status != "Abgebrochen"` ist auf eine transient/terminal-Differenzierung umzustellen.
- `src/Softwareschmiede/Application/Services/ScmEventOrchestrator.cs` — `ReconcileAttemptAsync` (Zeilen ~485–600): Die PR-Verknüpfung `LinkReportedPullRequestAsync` (Zeilen ~976–991, Aufruf Zeilen ~578–579) liegt aktuell innerhalb `if (messages.Accepted is not null)` — Entkopplung der `pullRequestNumber`-Auswertung vom sauberen Abschlusspfad. Betrifft ggf. analog `ReconcilePreparationAttemptAsync` (Aufbereitungspfad, eigenes `complete`-Schema — zu prüfen, ob dort `pullRequestNumber` überhaupt vorkommt; Annahme: nein).
- `src/Softwareschmiede/Application/Services/ScmEventExchangeProtocol.cs` — `ReadAsync`/`ScmExchangeMessages`: `Complete` wird bereits unabhängig von `Accepted` gelesen (Zeile ~359); die Auswertung im Orchestrator ist der zu ändernde Teil, das Protokoll leseseitig vermutlich unverändert (Annahme).
- `src/Softwareschmiede/Application/Services/PullRequestReferenceService.cs` — `SaveReportedAsync` bleibt der Registrierungspfad; voraussichtlich keine Änderung nötig (Annahme).
- `src/Softwareschmiede/Domain/Entities/ScmIncomingEvent.cs` — `Status` ist ein String-Feld (keine Enum); die Menge der Statuswerte (`Aufgenommen`, `Verzögert`, `Wartend`, `ZustellungVorbereitet`, `Zugestellt`, `ZustellungUngewiss`, `InBearbeitung`, `VerarbeitungUngeklärt`, `Verarbeitet`, `Abgebrochen`) ist für die transient/terminal-Einteilung verbindlich festzulegen. Optional: zentrale Status-Konstanten-/Klassifikationsstelle (z. B. „blockiert Abschlussprüfung: ja/nein"), damit die Einteilung nicht an mehreren Stellen divergiert (Vorschlag, keine Anforderung).
- `src/Softwareschmiede.App/ViewModels/ScmEventQueueViewModel.cs` — nur falls die Sichtbarkeits-Variante gewählt wird (Indikator/Klärungsaktion am Ereignis bzw. an der Aufgabe); bestehende Commands `CheckStatusCommand`, `ResendCommand`, `CancelCommand` und `CanResend`/`IsFinal`-Regeln sind der Kontext.
- Dokumentation (Akzeptanzkriterium „Verhalten für alle Ereignis-Stati dokumentiert"): `docs/help/plugins/github-ereignisse/business-rules.md` (Abschnitt „Abschlussüberwachung von Aufgaben mit SCM-Referenz", dokumentiert heute pauschal „die Ereignisverarbeitung einer Aufgabe [muss] abgeschlossen sein"), ggf. `ablauf-anwender.md`, `ablauf-technisch.md`, `datenmodell.md`, `troubleshooting.md` sowie `docs/help/aufgaben/datenmodell.md`.

### Tests

- `src/Softwareschmiede.IntegrationTests/Services/ScmTaskCompletionMonitoringServiceTests.cs` — neue Fälle: Aufgabe mit `VerarbeitungUngeklärt`-Ereignis + geschlossener Referenz → `ScmCompletionDetectedUtc` wird gesetzt / automatischer Abschluss läuft; je blockierendem Status ein Fall (parametrisierbar über die Statusliste); unverändertes Blockieren für transiente Stati.
- `src/Softwareschmiede.IntegrationTests/Services/ScmEventDispatchTests*.cs` bzw. `src/Softwareschmiede.Tests/Application/Services/ScmEventOrchestratorTests.cs` — `pullRequestNumber` wird registriert, auch wenn `accepted.json` fehlt oder das Ereignis auf `VerarbeitungUngeklärt` endet (je nach gewählter Entkopplung).
- `src/Softwareschmiede.Tests/Application/Services/ScmEventQueueViewModelTests.cs` — nur bei UI-Anpassung.

## Implementierungsansatz

- **Gate-Differenzierung in `CheckTaskAsync`:** Statt „alle Stati außer `Verarbeitet`/`Abgebrochen` blockieren" nur noch transient offene Stati blockieren lassen (z. B. `Aufgenommen`, `Verzögert`, `Wartend`, `ZustellungVorbereitet`, `Zugestellt`, `InBearbeitung` — Whitelist-Ansatz) oder gezielt terminale Diagnose-Stati passieren lassen (`VerarbeitungUngeklärt`; `ZustellungUngewiss` ist ebenfalls ohne automatischen Neuversand und grenzwertig — siehe Offene Fragen). Die gleiche Regel ist in der zweiten Prüfung innerhalb der `RunAsync`-Sperre zu spiegeln. Empfehlung: die Einteilung an einer Stelle (Hilfsfunktion/Konstanten) kapseln, da dieselbe Statuslogik auch in `ScmEventQueueViewModel`/Dispatch-Auswahl vorkommt (Vorschlag).
- **Entkopplung der PR-Registrierung:** `messages.Complete?.PullRequestNumber` in `ReconcileAttemptAsync` außerhalb des `Accepted`-Blocks auswerten, sodass ein korrelierter Abschluss mit PR-Nummer auch registriert wird, wenn `accepted.json` fehlt oder der Ereignisstatus anschließend `VerarbeitungUngeklärt` wird. Die Verknüpfung bleibt idempotent über `LinkReportedPullRequestAsync` → `PullRequestReferenceService.SaveReportedAsync`; der Protokolleintrag (`LogEntryType.GitAction`) bleibt bestehen.
- **Alternative Ergänzpfade aus dem Issue (nicht alle erforderlich):** (a) Klärungs-/Timeout-Pfad für `VerarbeitungUngeklärt` (automatischer Übergang oder explizite UI-Klärungsaktion), (b) sichtbare Meldung des blockierten Zustands (`LogService`/Protokolleintrag oder UI-Indikator) statt stillem Warten. Welche Kombination umgesetzt wird, ist zu entscheiden — die Akzeptanzkriterien verlangen mindestens die Entblockung bzw. Sichtbarkeit.
- **Abhängigkeiten:** `SoftwareschmiedeDbContext` (`ScmIncomingEvents`, `WorkItems`, `PullRequestReferences`), `ScmEventStore`, `ScmEventExchangeProtocol`, `PullRequestReferenceService`, `LogService`, `ScmTaskCoordinator`, `AppSettingService` (bestehende `scm.taskcompletion.*`-Schalter).

## Konfiguration

- Bestehende relevante Schalter (unverändert nutzbar): `scm.taskcompletion.enabled`, `scm.taskcompletion.scope` (`ScmTaskCompletionCheckMode`), `scm.taskcompletion.wait.minutes` — alle via `AppSettingService`, gesteuert unter Einstellungen → Automatisierung → Aufgaben beenden.
- Aus der Anforderung ergibt sich **kein zwingender** neuer Konfigurationspunkt. Falls die Variante „Timeout-/Klärungsfrist für `VerarbeitungUngeklärt`" gewählt wird, wäre die Frist als neuer `scm.*`-Schlüssel (via `AppSettingService`/`ScmEventOptions`) naheliegend — Entscheidung offen.

## Offene Fragen

1. **Welche Stati gelten als „blockierend"?** Konkrete Einteilung aller `ScmIncomingEvent.Status`-Werte (`Aufgenommen`, `Verzögert`, `Wartend`, `ZustellungVorbereitet`, `Zugestellt`, `ZustellungUngewiss`, `InBearbeitung`, `VerarbeitungUngeklärt`) in „blockiert die Abschlussprüfung" vs. „nicht blockierend". Insbesondere: Soll `ZustellungUngewiss` (ebenfalls ohne automatischen Neuversand) wie `VerarbeitungUngeklärt` behandelt werden?
2. **Ist die Entblockung allein ausreichend**, oder soll zusätzlich ein Klärungspfad für `VerarbeitungUngeklärt` eingeführt werden (automatischer Übergang nach Frist / explizite Klärungsaktion in der SCM-Ereignisübersicht)? Die Issue-Alternativen sind „nicht abschließend" genannt — eine Auswahl ist zu treffen.
3. **Sichtbarkeit:** Soll die Abschlussüberwachung einen blockierten Zustand (transient wartende Ereignisse) sichtbar melden — Protokolleintrag auf der Aufgabe und/oder Indikator in der UI —, oder genügt das stille Warten wie bisher?
4. **`pullRequestNumber` ohne lesbaren Kanal:** Soll die Registrierung nur erfolgen, wenn `complete.json` beim Abgleich noch lesbar ist (Entkopplung innerhalb `ReconcileAttemptAsync`), oder ist ein vom `Accepted`-Pfad unabhängiger Lesepfad gewünscht? Und: Darf die Registrierung auch erfolgen, wenn das Ereignis bereits `VerarbeitungUngeklärt` ist und `complete.json` erst später auftaucht (wird beim nächsten Abgleich ohnehin erneut gelesen — Annahme: ja)?
5. **Reihenfolge-Regel im Dispatch:** Ein `VerarbeitungUngeklärt`-Ereignis blockiert aktuell auch *nachfolgende* Ereignisse derselben Aufgabe (Wartegrund „Ein früheres Ereignis dieser Aufgabe muss zuerst abgeschlossen oder geklärt werden."). Bleibt diese Blockade bewusst bestehen (andere Fachlogik: Ereignisreihenfolge vs. Abschlussbeobachtung), oder soll sie analog gelockert werden?
6. **Aufbereitungspfad (`Purpose == "IssueAufbereitung"`, `DispatchPreparationAsync`/`ReconcilePreparationAttemptAsync`):** Gilt die Gate-Änderung auch für `VerarbeitungUngeklärt`-Aufbereitungsereignisse an einer regulären Aufgabe — oder werden zweckmarkierte Ereignisse von der Blockwirkung generell ausgenommen?
