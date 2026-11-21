# Aufgaben & KI-Entwicklungsprozess — Business Rules

## Exklusiver KI-Lauf

**Beschreibung:** Für eine Aufgabe kann immer nur ein KI-Lauf gleichzeitig aktiv sein.

**Bedingungen:**
- `AiExecutionService.IsRunning(workItemId)` gibt `true` zurück.

**Verhalten:**
- Wenn aktiv: Zweiter `StartCliAsync`-Aufruf wird mit `LogWarning` abgewiesen, kein zweiter Task gestartet.
- Wenn nicht aktiv: Neuer Lauf wird gestartet.

**Umsetzung:** `AiExecutionService.StartCliAsync` — Guards gegen doppelte Ausführung im In-Memory-Dictionary.

---

## Statusanzeige folgt dem faktischen Prozesszustand

**Beschreibung:** Die Aufgabenkachel (Seitenleiste, Dashboard) und die Fußzeile der Aufgabendetailansicht zeigen für denselben KI-Lauf stets denselben fachlichen Zustand. Maßgeblich ist der faktische Prozesszustand — nicht allein der zuletzt gespeicherte Datenstand.

**Bedingungen:**
- Für die Aufgabe läuft ein gehandelter CLI-Prozess (`AiExecutionService.IsRunning` bzw. `GetRunningWorkItemIds`).

**Verhalten:**
- Läuft der Prozess nachweislich, zeigt die Kachel `▶ Läuft` bzw. `⏸ Wartet`: Der Live-Substatus (`ActiveWorkItemPanelItem.LiveRunStatus`, befüllt aus `IRunningAutomationStatusSource.GetLiveRunStatus`) schlägt in `AiExecutionStatusClassification` vor der Auswertung der persistierten Laufdaten immer aufwärts durch — niemals auf `Ready`. `Paused` und `PromptQueued` behalten Vorrang.
- Fehlende persistierte Laufdaten werden durch `WorkItemService.EnsureActiveRunAsync` wiederhergestellt — ereignisgetrieben (`CliRunActive` auf Session-Wiederverwendungspfaden, `RuntimeStatusChanged`, Heartbeat-Tick) und spätestens über den 30-s-Abgleich des `CliProcessManager`.
- Ein persistierter „aktiver" Lauf ohne laufenden Prozess verfällt weiterhin über die Heartbeat-Regel (älter als 5 Minuten → `✓ Bereit` + Recovery-Kandidat): Der Live-Zweig liefert ohne Prozess `null` und greift nicht — verwaiste Zustände erscheinen nicht wieder aktiv.
- Alle Laufdaten-Mutationen laufen unter der prozessweiten `WorkItemService.RunGates`-Sperre; `runningGuard`/`runStatusProvider` werden innerhalb der Sperre ausgewertet, damit kein Phantom-Lauf nach einem Beenden entsteht und kein veralteter Substatus als letzter Schreiber landet.
- Eine nicht beendbare CLI bleibt als laufend sichtbar: `StopCliAsync` beendet die Laufdaten nur bei tatsächlich beendetem Prozess — andernfalls erscheint eine Fehlermeldung und `CanStopCli` bleibt erneut auslösbar.

**Umsetzung:** `AiExecutionService.GetLiveRunStatus`/`CliRunActive`/`NotifyRunActive`/`GetRunningWorkItemIds`, `CliProcessManager.EnsureRunDataAsync`/`ReconcileTickAsync`, `WorkItemService.EnsureActiveRunAsync`/`RunGates`, `MainWindowViewModel.MapActiveWorkItemPanelItem`, `AiExecutionStatusClassification`, `TaskDetailViewModel.StopCliAsync`/`StartCliAndUpdateStateAsync`.

---

## Branch-Strategie beim Prozessstart

**Beschreibung:** Die Branch-Wahl hängt davon ab, ob ein vorhandener Remote-Branch angegeben wurde und ob dieser der Hauptbranch ist.

**Bedingungen:**
- `baseBranchName` leer oder null → immer neuer `task/`-Branch.
- `baseBranchName` == Hauptbranch → neuer `task/`-Branch (kein direkter Commit auf Main).
- `baseBranchName` != Hauptbranch → vorhandener Branch wird ausgecheckt.
- Aufgabe mit `IssueReference.IssueNumber` → neuer Branch enthält die Issue-Nummer.
- Aufgabe ohne `IssueReference` → neuer Branch enthält nur Aufgaben-ID und Titel-Slug.

**Umsetzung:** `DevelopmentProcessService.ProzessStartenAsync` — `useExistingBranch`-Flag.

---

## Aufgaben ohne Issue-Bezug

**Beschreibung:** Aufgaben, die aus einem Git-Plugin ohne verknüpftes Issue erstellt wurden, sind start- und ausführbar.

**Bedingungen:**
- Die Aufgabe hat keine `IssueReference`.
- Ein Repository kann über Aufgaben-, Projekt- oder eindeutigen Projektkontext aufgelöst werden.

**Verhalten:**
- Der Prozessstart setzt keine Issue-Referenz voraus.
- Der Branchname wird im Format `task/{aufgabe.Id:N}-{slug}` erzeugt.
- Die lokale `issue.md` wird weiterhin erstellt; sie enthält die Aufgabenstammdaten und die Anforderungsbeschreibung, aber keine verpflichtende Issue-Verknüpfung.
- Mehrdeutige Repository-Auflösung bleibt ein Fehlerfall.

**Umsetzung:** `DevelopmentProcessService.CreateTaskBranchName`, `ProzessStartenAsync` und `CreateIssueFileAsync`.

---

## Issue-Referenz in der issue.md

**Beschreibung:** Beim Prozessstart wird die generierte `issue.md` um einen optionalen Abschnitt `## Verknüpftes Issue` ergänzt, wenn die Aufgabe eine Issue-Referenz mit gültiger Nummer trägt.

**Bedingungen:**
- `aufgabe.IssueReference != null`
- `IssueReference.IssueNumber > 0`

**Verhalten:**
- Wenn beide Bedingungen erfüllt: Der Abschnitt `## Verknüpftes Issue` wird zwischen dem Metadaten-Block (`**Erstellt:**`) und dem Abschnitt `## Anforderung` eingefügt. Inhalt: `**Kennung:** #<IssueNumber>` und `**Title:** <Title>`.
- Wenn `IssueReference` `null` ist: Kein Issue-Abschnitt wird ausgegeben; die Datei enthält nur Metadaten und Anforderung.
- Wenn `IssueNumber` `null` oder ≤ 0 ist: Kein Issue-Abschnitt wird ausgegeben — auch wenn `IssueReference` selbst gesetzt ist.

**Umsetzung:** `DevelopmentProcessService.CreateIssueFileAsync` — wertet `aufgabe.IssueReference is { IssueNumber: > 0 }` aus und fügt den Block nur bei positivem Match ein. `IssueReference` wird bereits durch Eager Loading in `WorkItemService.GetDetailAsync` vollständig geladen, sodass kein zusätzliches Datenbankzugriff nötig ist.

---

**Beschreibung:** Ein neues Issue kann aus einer Aufgabe angelegt und anschließend genau einmal mit dieser Aufgabe verknüpft werden.

**Bedingungen:**
- Die Aufgabe besitzt noch keine `IssueReference`.
- Ein zugeordnetes Repository und ein Provider mit `IIssueCreateProvider` sind verfügbar.
- Der Provider meldet die Issue-Anlage für das Repository als unterstützt.

**Verhalten:**
- Der Ribbon-Button und der Dialog werden nur bei verfügbarer Issue-Anlage angeboten.
- Titel und Anforderungsbeschreibung werden als editierbare Initialwerte übernommen.
- Provider-Templates sind optional. Bei einem ausgewählten Template werden Template-Inhalt, Trennlinie und `Originalanforderung:` zusammengesetzt; der Text bleibt bearbeitbar.
- Die KI-Ausfüllhilfe ist optional und verwendet nur Provider mit `IIssueTemplateTextGenerator`.
- Das externe Issue wird vor der lokalen Speicherung erstellt. Die lokale `IssueReference` wird nur bei erfolgreicher Anlage und erfolgreicher konkurrenzsicherer Zuordnung gespeichert.
- Nach erfolgreicher Zuordnung wird die Aufgabe neu geladen; der Button „Issue anlegen" verschwindet.
- Abbruch, nicht unterstützte Templates, Providerfehler und Validierungsfehler führen nicht zu einer lokalen Issue-Zuordnung. Ein Fehler bei der lokalen Speicherung nach externer Anlage wird mit Issue-URL oder -Nummer angezeigt; ein allgemeines Provider-Rollback ist nicht garantiert.

**Umsetzung:** `TaskDetailViewModel.IssueAnlegenAsync`, `IssueCreateDialogViewModel`, `WorkItemService.TryAssignIssueReferenceIfNoneAsync` sowie `IIssueCreateProvider` und optionale Template-/KI-Fähigkeiten.

---

## Aufgabe aus GitHub-Code-Scanning-Alert

**Beschreibung:** Ein GitHub-Code-Scanning-Alert kann aus der Projektdetailansicht in eine lokale Aufgabe überführt werden. Dabei entsteht zuerst ein neues GitHub-Issue, danach die lokale Aufgabe mit Issue- und Alert-Herkunft.

**Bedingungen:**
- Das Projekt besitzt ein GitHub-Repository.
- Das GitHub-Plugin liefert offene Code-Scanning-Alerts.
- Das GitHub-Plugin unterstützt die Issue-Anlage für das Repository.
- Für die `AlertReference.SourceKey` existiert noch keine lokale Aufgabe.

**Verhalten:**
- Code-Scanning-Alerts werden unter **Offene Anforderungen** gemeinsam mit normalen Issues angezeigt, aber als Alert gekennzeichnet.
- Beim Auswählen eines Alerts wird ein GitHub-Issue mit deterministischem Titel `Code scanning alert: <Rule/Alert-Title>` und einem Body aus Alert-Typ, Severity, Tool, Rule, Ort, URL und Beschreibung erstellt.
- Erst nach erfolgreicher externer Issue-Anlage wird lokal eine Aufgabe erstellt.
- Die lokale Aufgabe erhält eine `IssueReference` auf das neu erzeugte GitHub-Issue und eine `AlertReference` auf den ursprünglichen Alert.
- Derselbe Alert wird über `AlertReference.SourceKey` lokal als konvertiert erkannt und nicht erneut angeboten.
- Das ursprüngliche GitHub-Code-Scanning-Alert wird nicht geschlossen und nicht anderweitig verändert.
- Dependabot- und Secret-Scanning-Alerts sind in dieser Ausbaustufe nicht angebunden.
- Bitbucket/Jira und andere SCM-Plugins müssen keine Alerts liefern.

**Umsetzung:** `ProjectDetailViewModel`, `WorkItemService.CreateFromAlertAsync`, `AlertReference`, `IScmAlertProvider`, `GitHubPlugin.GetAlertsAsync` und `IIssueCreateProvider.CreateIssueAsync`.

---

## Automatische Issue-Schliessung bei Pull Requests

**Beschreibung:** Wenn eine Aufgabe aus einem GitHub-Issue erstellt wurde, bleibt die Issue-Referenz an der Aufgabe erhalten und wird beim Erstellen eines Pull Requests automatisch in den Pull-Request-Body uebernommen.

**Bedingungen:**
- Die Aufgabe hat eine `IssueReference`.
- `IssueReference.IssueNumber` ist groesser als 0.
- Fuer dieselbe Issue existiert im Pull-Request-Body noch keine Closing-Direktive.

**Verhalten:**
- Der Pull-Request-Body aus der Aufgabendetailansicht enthaelt primaer die Commits des Aufgabenbranches statt der urspruenglichen Anforderungsbeschreibung.
- Der Pull-Request-Body wird um `Closes #<IssueNumber>` ergaenzt.
- Ein leerer oder nur aus Whitespace bestehender Body wird durch die Closing-Direktive ersetzt.
- Bereits vorhandene Direktiven fuer dieselbe Issue, z. B. `Fixes #17`, `Closed #17` oder `Fixes owner/repo#17`, werden nicht dupliziert.
- Direktiven fuer andere Issues bleiben erhalten; die aktuelle Issue wird zusaetzlich ergaenzt.
- Aufgaben ohne Issue-Referenz oder ohne gueltige Issue-Nummer behalten das bisherige Pull-Request-Verhalten.

**Umsetzung:** `PullRequestBodyBuilder`, `GitOrchestrationService.PullRequestErstellenAsync` und der aeltere PR-Pfad `DevelopmentProcessService.PullRequestErstellenAsync`.

---

## Pull-Request-Persistenz und Monitoring

**Beschreibung:** Pull Requests, die aus einer Aufgabe heraus erstellt werden, bleiben an dieser Aufgabe gespeichert und werden im PR-Bereich angezeigt.

**Bedingungen:**
- Die Pull-Request-Erstellung liefert Provider, Repository, PR-Nummer und URL.
- Der Provider unterstuetzt Status- und Workflow-Abfragen; initial ist das GitHub.

**Verhalten:**
- Nach erfolgreicher Erstellung wird eine `PullRequestReference` mit Aufgabe, Provider, Repository, Nummer, Branches, URL und Head-SHA gespeichert.
- Mehrere Pull Requests koennen derselben Aufgabe zugeordnet sein.
- Workflow-Runs werden ueber Provider-Run-ID upserted und bleiben der lokalen PR-Referenz zugeordnet.
- Der PR-Bereich zeigt PR-Status, Merge-Status, Monitoring-Phase, letzte Pruefung, Workflow-Runs und sichtbare Fehler- oder Blockierungsgruende.
- Ein fehlgeschlagener oder nicht berechtigter Statusabruf beendet die Aufgabe nicht; der Fehler wird am Pull Request sichtbar gespeichert.

**Umsetzung:** `PullRequestReferenceService`, `PullRequestMonitoringService`, `TaskDetailViewModel` und `TaskDetailView`.

---

## Automatischer PR-Abschluss

**Beschreibung:** GitHub-Pull-Requests koennen nach erfolgreichen zugeordneten Pre-Merge-Actions automatisch abgeschlossen werden, wenn die GitHub-Plugin-Einstellung aktiviert ist.

**Bedingungen:**
- `AutoCompletePullRequests` ist im GitHub-Plugin aktiviert.
- Die relevanten Pre-Merge-Workflow-Runs sind abgeschlossen und nicht fehlgeschlagen.
- Der Pull Request ist noch nicht gemergt.

**Verhalten:**
- Standardmaessig ist der automatische Abschluss deaktiviert.
- Erfolgreiche und uebersprungene Workflow-Runs gelten als unkritisch; fehlgeschlagene Runs blockieren den Abschluss.
- `Merge` fuehrt einen direkten Merge mit der konfigurierten Merge-Methode aus.
- `AutoMerge` aktiviert GitHub Auto-Merge; ein Abschluss gilt erst dann als gemergt, wenn eine anschliessende Statusabfrage `Merged` meldet.
- `ApprovalOnly` genehmigt den Pull Request, laesst ihn aber offen und fuehrt nicht automatisch einen Merge aus.
- Falls Branch Protection, fehlende Berechtigungen oder Self-Approval-Regeln den Abschluss verhindern, wird der Pull Request als blockiert beziehungsweise mit sichtbarem Fehler gespeichert.
- Nach einem echten Merge werden zuordenbare Post-Merge-Workflow-Runs weiter ueberwacht.

**Umsetzung:** `PullRequestMonitoringService`, `IGitPlugin.CompletePullRequestAsync` und `GitHubPlugin`.

---

## CLI-Panel-Sichtbarkeit

**Beschreibung:** Das CLI-Panel wird in der Aufgabendetailansicht angezeigt oder ausgeblendet, abhängig vom Aufgabenstatus und dem persistierten Ausführungsstatus.

**Bedingungen:**
- `WorkItem.Status == Started` oder `Waiting` (Aufgabe ist aktiv oder wartet)
- `WorkItem.ExecutionStatus == Aktiv` oder `Completed` (KI-Ausführung läuft oder wurde beendet)

**Verhalten:**
- CLI-Panel wird sichtbar, wenn beide Bedingungen erfüllt sind.
- CLI-Panel wird ausgeblendet, wenn `WorkItem.Status == New`, `Completed` oder `Archived` ist (unabhängig vom `ExecutionStatus`).
- CLI-Panel wird ausgeblendet, wenn `ExecutionStatus == NichtStarted` ist (unabhängig vom `Status`).
- Nach Beendigung der CLI-Ausführung (natürlich oder durch Benutzer) bleibt das CLI-Panel sichtbar, solange die Aufgabe noch im Status `Started` oder `Waiting` ist. Der Benutzer kann die letzte Ausgabe anschauen und die CLI manuell neu starten.
- Bei Plugin-Wechsel wird die laufende CLI beendet (`ExecutionStatus` → `Completed`); das Panel bleibt sichtbar und wird mit der neuen Session neu befüllt.

**Umsetzung:** `WorkItemExecutionStatusExtensions.SollCliAnzeigen(ExecutionStatus, WorkItemStatus)` — prüft beide Bedingungen und gibt `true` zurück, wenn die CLI-Ansicht angezeigt werden soll.

---

## Sichtbarer Aufgabenkontext

**Beschreibung:** Die Aufgabendetailansicht zeigt den Kontext der aktuell geöffneten Aufgabe und der laufenden CLI-Ausführung.

**Bedingungen:**
- Eine Aufgabe wird in der Detailansicht geladen.
- Optional läuft eine KI-CLI für diese Aufgabe.

**Verhalten:**
- Der Fenstertitel wechselt nach dem Laden der Aufgabe auf `Softwareschmiede – {Aufgabentitel}`.
- Beim Wechsel zu Dashboard, Projektliste oder Einstellungen wird der jeweilige Ansichtstitel gesetzt.
- Die Fußzeile zeigt den aktiven CLI-Namen nur während einer laufenden CLI-Ausführung.
- Bei Stop, Fehler oder beendetem Prozess wird der CLI-Name aus der Fußzeile entfernt.

**Umsetzung:** `MainWindowViewModel.Title`, `TaskDetailViewModel.DetailTitleChangeAction` und `TaskDetailViewModel.ActiveCliName`.

---

## Kontextkomprimierung

**Beschreibung:** Wird die Kontextdatei zu groß, komprimiert die KI sie selbst auf ein strukturiertes Markdown-Dokument.

**Bedingungen:**
- `context.Length > SoftLimit` (Standard: 12 000 Zeichen): Komprimierung wird ausgelöst.
- `context.Length > HardLimit` (Standard: 20 000 Zeichen): Warneintrag im Protokoll, KI-Lauf läuft trotzdem.

**Verhalten:**
- Soft-Limit überschritten: `CompressContextAsync` startet einen separaten KI-Lauf mit Komprimierungsprompt. Das Ergebnis muss die Pflichtabschnitte „Ziel", „Offene Punkte" und „Letzte Entscheidungen" enthalten.
- Pflichtabschnitte fehlen oder Ergebnis leer: Exception, KI-Lauf wird nicht gestartet.

**Umsetzung:** `DevelopmentProcessService.EnsureContextWithinLimitsAsync`, `CompressContextAsync`, `ContainsMandatoryCompressionSections`.

---

## Aufgaben-Pause

**Beschreibung:** Eine reguläre Aufgabe kann bis zu einem frei wählbaren Zeitpunkt pausiert werden. Die Pause ist ein zeitbasiertes Overlay über `WorkItem.PausedUntilUtc` — es gibt keinen eigenen Status `Paused`.

**Bedingungen:**
- `Status` ist `New`, `Started` oder `Waiting`.
- Beim Setzen muss der gewählte Zeitpunkt in der Zukunft liegen; `null` hebt die Pause auf.

**Verhalten:**
- Eine Aufgabe gilt als pausiert, solange `PausedUntilUtc > UtcNow` gilt; abgelaufene Werte wirken nicht mehr (passives Ablaufen, kein Status-Übergang, kein automatischer Restart). Das Kriterium ist in der Extension `AiExecutionStatusClassification.IsCurrentlyPaused` als Referenzdefinition gebündelt — alle Pausen-Auswertungen (Kachel-Klassifikation, Start-/Neustart-Guards, Recovery, SCM-Limit-Belegung, SCM-Dispatch-`BlockReason`) verwenden dieselbe Prüfung `pausiertBisUtc > jetzt` (striktes Größer-als; `null` und abgelaufene Zeitpunkte gelten als nicht pausiert).
- Während einer aktiven Pause sind blockiert: Prozessstart, CLI-Neustart (inkl. Plugin-Wechsel-Pfad), automatische und manuelle Recovery, Senden und Planen von Prompts über die Detailansicht.
- Ein bereits geplanter Prompt wird auf das Pausenende verschoben statt verworfen.
- Ein laufender CLI-Prozess wird niemals durch die Pause unterbrochen.
- Eine pausierte Aufgabe belegt keinen Platz in der SCM-Ausführungsbegrenzung — weder im globalen Zähler noch im Plugin-Zähler: `ScmEventExecutionLimitService.GetOccupancyAsync` filtert sie trotz weiterlaufendem Prozess heraus; das nächste wartende SCM-Ereignis kann den frei werdenden Platz über die ereignisgesteuerte Startprüfung sofort übernehmen (Details: [GitHub-Ereignisüberwachung — Business Rules](../plugins/github-ereignisse/business-rules.md)). Nach Ablauf der Pause zählt die Aufgabe wieder als belegt.
- UI: Buttons werden deaktiviert (`CanPause`, `!IstPausiert` in den CanExecute-Prüfungen); die Seitenleisten-Kachel zeigt das `⏸`-Glyph mit `Opacity = 0.55` (Countdown-Volltext im Tooltip/`HelpText`); im Ribbon steht `⏸ Pausiert bis …`.
- Jede Setzen-/Aufheben-Operation schreibt einen `LogEntryType.SystemMessage`-Eintrag und fordert anschließend die ereignisgesteuerte SCM-Startprüfung an (`ScmStartCheckTrigger`), damit ein frei werdender Ausführungsplatz ohne Poll-Durchlauf nachgefüllt wird.

**Umsetzung:** `WorkItemService.SetPauseAsync`, `DevelopmentProcessService.ThrowIfPaused`, `WorkItemRecoveryService`, `PromptTimeDispatchService`, `TaskDetailViewModel.SetPauseAsync`, `WorkItemPauseDialogViewModel`, `AiExecutionStatusConverter`, `AiExecutionStatusClassification.IsCurrentlyPaused` (Referenz-Pausenkriterium), `ScmStartCheckTrigger`.

---

## Session-Limit-Erkennung und automatische Pause

**Beschreibung:** Meldet ein KI-CLI ein Session-Limit über den Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]`, wird der Reset-Zeitpunkt pro `AiPluginPrefix` persistiert und alle aktiv laufenden regulären Aufgaben desselben Plugins automatisch pausiert. Der Erkennungspfad läuft über `LogService.AddCliOutputAsync` bzw. `CliOutputLogWriter` in der automatischen Terminal-Ausgabeprotokollierung.

**Bedingungen:**
- Eine CLI-Ausgabezeile enthält den Marker `[[SOFTWARESCHMIEDE_RATE_LIMIT:<ISO8601>]]` mit gültigem Zeitstempel.
- Die auslösende Aufgabe besitzt ein `AiPluginPrefix`.
- Kandidaten für die automatische Pause: `Status` aktiv oder wartend, `ExecutionStatus == Aktiv`, identisches `AiPluginPrefix` (`OrdinalIgnoreCase`).

**Verhalten:**
- Der Marker erzeugt an der auslösenden Aufgabe einen `LogEntryType.RateLimit`-Eintrag — auch ohne gültigen Zeitstempel.
- Bei gültigem Zeitstempel wird der Wert als `AppSetting` unter `plugins.sessionlimit.<AiPluginPrefix>` (ISO-8601-Roundtrip, UTC) persistiert — Laufzeitstatus, keine Anwender-Konfiguration.
- Liegt der gemeldete Zeitpunkt in der Vergangenheit, wird er persistiert, löst aber keine Pause aus.
- Max-Semantik: `PausedUntilUtc` wird auf `max(bisheriger Wert, resetUtc)` gesetzt — eine längere manuelle Pause wird nicht verkürzt.
- Pro pausierter Aufgabe wird ein `SystemMessage`-Eintrag geschrieben und die Seitenleiste über `WorkItemRunDataChangedNotifier` aktualisiert.
- Laufende CLI-Prozesse werden nicht angefasst; es findet kein automatischer Neustart nach Limit-Ablauf statt.
- **Update-Sicherheit:** `CliUpdateSafetyService.CheckAsync` behandelt Aufgaben mit zukünftigem Plugin-Session-Limit als nicht riskant — die Heartbeat-Toleranz (`WorkItemRunActivity.IstAktiv`) entfällt für sie komplett. Aufgaben ohne bekanntes Limit werden wie bisher per Heartbeat bewertet.

**Umsetzung:** `LogService.TryParseRateLimitMarker`/`AddCliOutputAsync`, `CliOutputLogWriter.PersistLineAsync`, `AiPluginLimitService.VerarbeiteRateLimitAsync`/`GetAktiveSessionLimitsAsync`, `CliUpdateSafetyService`.

---

## Automatische CLI-Ausgabeprotokollierung

**Beschreibung:** CLI-Ausgaben einer Terminal-Sitzung (ConPTY oder diagnostizierter Pipe-Fallback) werden automatisch dem Protokoll der zugehörigen Aufgabe zugeordnet.

**Bedingungen:**
- Der CLI-Prozess wird über `AiExecutionService.StartTerminalSessionAsync` gestartet.
- Die `ITerminalSession` (Implementierung: `PseudoConsoleSession`) liefert Output-Bytes aus ihrer internen Leseschleife.

**Verhalten:**
- Pro Terminal-Session-Start wird ein `CliOutputLogWriter` für genau eine `workItemId` erzeugt.
- Die Ausgabe wird zeilenweise als `LogEntryType.CliOutput` gespeichert.
- Die Reihenfolge innerhalb einer Session bleibt durch einen sequenziellen Hintergrund-Worker erhalten.
- Die Protokollierung ist UI-unabhängig und läuft weiter, wenn kein `TerminalControl` gebunden ist.
- Persistenzfehler werden geloggt, brechen den CLI-Prozess aber nicht ab.
- Bei hoher Ausgaberate begrenzt eine bounded Queue den Speicherverbrauch und erzeugt Backpressure.
- Beim Abschluss wartet die Senke auf die aktive Queue-Phase eines bereits dekodierten Chunks, bevor der Channel geschlossen wird.

**Umsetzung:** `ITerminalOutputSink`, `CliOutputLineAccumulator`, `CliOutputLogWriter`, `PseudoConsoleSession.ReadLoopAsync`, `AiExecutionService.StartTerminalSessionAsync`, `LogService.AddCliOutputAsync`.

---

## CLI-Rohausgabe-Export

**Beschreibung:** Die exportierte `.raw`-Datei enthält ausschließlich die bereits gespeicherten CLI-Ausgabezeilen der Aufgabe.

**Bedingungen:**
- Der Export wird aus der Aufgabendetailansicht gestartet.
- Eine Aufgabe ist geladen und besitzt gespeicherte `LogEntryType.CliOutput`-Einträge.

**Verhalten:**
- Wenn der Speichern-Dialog abgebrochen wird: kein Export, keine Fehlermeldung.
- Wenn der Zielpfad nicht auf `.raw` endet: das ViewModel bricht mit einer sichtbaren Fehlermeldung ab.
- Wenn der Dateischreibvorgang fehlschlägt: der Fehler wird geloggt und dem Benutzer als Exportfehler angezeigt.
- Andere Protokolltypen wie `Prompt`, `KiAntwort` oder `SystemMessage` werden nicht exportiert.

**Umsetzung:** `TaskDetailViewModel.ExportCliRawAsync` und `CliRawExportService.ExportCliRawAsync` — das ViewModel steuert Dialog und Validierung, der Service schreibt die gefilterten `CliOutput`-Zeilen in UTF-8 ohne BOM.

---

## CLI-Aufzeichnungs-Export (.clireplay)

**Beschreibung:** Der Export-Button **Aufzeichnung exportieren** schreibt den automatisch mitgeschnittenen Rohbyte-Mitschnitt der letzten Terminal-Session der Aufgabe als `.clireplay`-Binärdatei. Er setzt einen vorhandenen Mitschnitt voraus — der Mitschnitt läuft beim Session-Start im `AiExecutionService` mit, nicht erst beim Export.

**Bedingungen:**
- Der Export wird aus der Aufgabendetailansicht gestartet (`ExportCanCliReplay` = Aufgabe geladen).
- Für die Aufgabe liegt ein Mitschnitt vor (`CliReplayExportService.HasRecording`): mindestens ein Terminal-Session-Start mit aktivem `AufzeichnungByteBudget`, und die Aufgabe gehört zu den letzten 8 mit Mitschnitt.

**Verhalten:**
- Wenn keine Aufzeichnung vorliegt: `ErrorMessage` „Für diese Aufgabe liegt noch keine Aufzeichnung vor — sie wird während einer CLI-Ausführung automatisch mitgeschnitten." — der Speicherdialog wird nicht geöffnet.
- Wenn der Speichern-Dialog abgebrochen wird: kein Export, keine Fehlermeldung.
- Wenn der Zielpfad nicht auf `.clireplay` endet: `ErrorMessage` „Export-Zielpfad muss auf .clireplay enden."
- Wenn der Schreibvorgang fehlschlägt: der Fehler wird geloggt und als Exportfehler angezeigt.
- Der Mitschnitt ist flüchtig: Budget-Überschreitung kürzt ihn auf das Präfix (`IstVollstaendig = false` im Datei-Header), nur die letzten 8 Aufgaben behalten einen Mitschnitt, App-Ende verwirft alle.
- `.raw`- und `.clireplay`-Export bestehen parallel und decken unterschiedliche Zwecke ab (zeilennormalisierte Sicht vs. byte-exaktes, zeitreales Replay).

**Umsetzung:** `TaskDetailViewModel.ExportCliReplayAsync` (Vorab-Prüfung, Dialog, Endungsvalidierung), `CliReplayExportService` (`HasRecording`/`ExportCliReplayAsync` über `AiExecutionService.GetCliAufzeichnung` + `CliReplayRecordingStore`), `CliOutputRecorder` (Mitschnitt beim Session-Start).

---

## Aufgaben-Recovery

**Beschreibung:** Eine Aufgabe, deren KI-Ausführung hängen geblieben ist (Heartbeat abgelaufen, kein laufender Prozess), kann über das Recovery-Banner auf dem Dashboard wiederhergestellt werden.

**Bedingungen (Recovery-Kandidat):**
- `Status` ist `Started` oder `Waiting` und `ExecutionStatus == Aktiv`.
- Kein aktiver KI-Lauf im `AiExecutionService` (Laufzeitprüfung).
- `LastHeartbeatUtc` ist älter als `WorkItemRecoveryService.HeartbeatTimeoutMinutes` (5 Minuten).
- Keine aktive Pause: `PausedUntilUtc` ist `null` oder liegt nicht in der Zukunft — pausierte Aufgaben wurden bewusst angehalten und gelten nicht als „festhängend".

**Verhalten:**
- Wenn alle Bedingungen erfüllt: Die Aufgabe erscheint im Banner „X Aufgabe(n) benötigen Wiederherstellung"; **Wiederherstellen** setzt den Status zurück.
- Bei der manuellen Recovery wird jede Ablehnung mit `TaskRecoveryRejected`-Logeintrag und `ReasonCode` (`NotFound`, `InvalidState`, `Paused`, `RunningStatusUnavailable`, …) protokolliert; eine aktive Pause führt zu `ReasonCode=Paused` und `InvalidOperationException`.

**Umsetzung:** `WorkItemRecoveryService.ScanForRecoveryCandidatesAsync` und `RecoverManuellAsync`.

---

## Klonpfad-Bereinigung

**Beschreibung:** Das lokale Klonverzeichnis enthält unter Windows schreibgeschützte Git-Pack-Dateien, die normales `Directory.Delete` verhindern.

**Verhalten:**
- Vor dem Löschen werden alle Dateien im Verzeichnis auf `FileAttributes.Normal` gesetzt.
- Danach erfolgt `Directory.Delete(path, recursive: true)`.

**Umsetzung:** `DevelopmentProcessService.DeleteDirectoryForce` — notwendig für `.git/objects/pack/*.idx`-Dateien unter Windows.

---

## Aufgabenabschluss mit To-Do-Validierung

**Beschreibung:** Eine Aufgabe kann nur beendet (Status: `Completed`) werden, wenn alle ihre To-Dos erledigt sind.

**Bedingungen:**
- Aufgabe hat den Status `Started` oder `Waiting` und ist ein Abschluss-Kandidat.
- `WorkItem.Todos` enthält eine Liste aller To-Dos.

**Verhalten:**
- Benutzer klickt „Beenden"-Button im Ribbon → `WorkItemService.CanCompleteTaskAsync()` wird aufgerufen.
- Methode prüft: Gibt es To-Dos mit `CompletedAt == null` (offen)?
  - Falls ja: Methode gibt `false` zurück; Abschluss wird blockiert; `ErrorMessage` zeigt Anzahl offener To-Dos.
  - Falls nein: Methode gibt `true` zurück; Abschluss wird erlaubt; `DevelopmentProcessService.CompleteAsync()` wird aufgerufen.
- Im Ribbon zeigt das Badge (Gruppe „Aufgabe") die Anzahl offener To-Dos (z. B. „⚠ 3 offen"). Badge ist verborgen wenn `OffeneTodoCount == 0`.
- Erstellte To-Dos bleiben auch nach Aufgabenabschluss gespeichert und sind in der `Todos`-Ansicht einsehbar.

**Umsetzung:**
- `WorkItemService.CanCompleteTaskAsync(Guid workItemId, CancellationToken ct)` — prüft offene To-Dos vor dem Abschluss.
- `TaskDetailViewModel.CompleteWorkItemAsync()` — prüft `CanCompleteTaskAsync()` vor dem Serviceaufruf.
- `TaskDetailViewModel.OffeneTodoCount` — wird bei jedem To-Do-Wechsel aktualisiert und steuert Ribbon-Badge.

---

## Zeitgesteuerter Prompt-Versand

**Beschreibung:** Pro Aufgabe kann maximal ein Prompt zeitgesteuert geplant sein. Ein erneut geplanter Prompt ersetzt den vorherigen. Der Plan wird persistiert (Tabelle `ZeitgesteuertePrompts`) und überlebt das Verlassen der Detailansicht, einen CLI-Stopp und einen App-Neustart.

**Bedingungen:**
- Eine CLI für die Aufgabe läuft (`IsCliRunning == true`) — gilt nur für das Planen; die Zustellung erfolgt auch an eine später (neu) gestartete Session.
- Eine Promptvorlage ist ausgewählt.
- Mindestens eines der Zeitfelder (Stunde oder Minute) ist gesetzt.
- Die Zeit ist valide (Stunde: 0–23, Minute: 0–59).

**Verhalten:**
- Liegt die Zielzeit in der Vergangenheit/Gegenwart: Prompt wird sofort versendet (kein Eintrag in der Warteschlange, kein persistierter Datensatz). Schlägt auch der Sofortversand fehl, wird der Plan persistiert und wartet auf die nächste Session.
- Liegt die Zielzeit in der Zukunft: Der Plan wird zuerst als `ScheduledPrompt`-Datensatz persistiert, danach wird ein Timer gestartet; bei Fälligkeit wird der Prompt automatisch versendet und der Datensatz gelöscht.
- Ein bereits geplanter Prompt für dieselbe Aufgabe wird ersetzt (vorheriger Timer wird abgebrochen, Datensatz per Upsert überschrieben); die Ersetzungsregel ist datenbankseitig über einen Unique-Index auf `WorkItemId` abgesichert.
- Ist die Aufgabe zum Planungs- oder Fälligkeitszeitpunkt pausiert (`PausedUntilUtc` in der Zukunft), wird die Zielzeit auf das Pausenende verschoben — der Prompt bleibt geplant, der persistierte Zielzeitpunkt wird mitaktualisiert.
- Ist zum Fälligkeitszeitpunkt keine CLI-Session aktiv oder schlägt der Versand fehl, wird der Plan **nicht** verworfen: Er geht in den Zustand „wartet auf Session" über (Statusanzeige „Prompt wartet auf CLI-Start") und wird beim nächsten CLI-Start der Aufgabe zugestellt (`CliProcessStatusChanged` → `Started`). Es gibt keinen Retry-Timer und keinen Verfall — ein wartender Plan bleibt unbegrenzt bestehen.
- Beim App-Start werden alle persistierten Pläne wiederhergestellt: zukünftige Zielzeiten armierten einen neuen Timer, verstrichene Zielzeiten gehen in „wartet auf Session" über (nach einem Neustart existiert noch keine Session). Datensätze zu nicht mehr vorhandenen Aufgaben werden verworfen.
- Verlassen oder Schließen der Detailansicht sowie ein CLI-Stopp stornieren den Plan **nicht**. Storniert wird er über den Button „Wartestellung aufheben", durch Ersetzen, beim Aufgabenabschluss oder beim Löschen der Aufgabe (Cascade).
- Versendet der Timer- oder Sofortpfad ohne geöffnete Detailansicht bzw. erfolgt eine Nachzustellung nach einem CLI-Start, wird ein `LogEntryType.SystemMessage`-Eintrag auf der Aufgabe geschrieben; ein Fehler beim Protokollieren beeinflusst den erfolgreichen Versand nicht.
- Es findet **kein** Versand bei vollständig beendeter App statt und **kein** automatischer CLI-Start, um einen fälligen Plan zuzustellen; die Zielzeit bleibt auf eine Uhrzeit (HH:mm) des aktuellen Tages beschränkt (keine Datumsauswahl).

**Umsetzung:** `PromptTimeDispatchService` — verwaltet geplante Prompts pro Aufgabe in `Dictionary<Guid, ScheduledPromptEntry>` mit pro-Eintrag-Timer via `TimeProvider.CreateTimer`; persistiert über den scoped `ScheduledPromptStore` (aufgelöst per `IServiceScopeFactory`), stellt beim App-Start via `RestoreScheduledPromptsAsync` wieder her und liefert wartende Pläne über ein `CliProcessStatusChanged`-Abonnement zu. `TaskDetailViewModel.SchedulePromptAsync` mit UI-Validierung; `CancelScheduledPromptCommand` für den Button „Wartestellung aufheben"; `ScheduledPromptChanged`-Event zur UI-Synchronisation.

## Anzeigemodus und Gruppierung der aktiven Aufgabenliste

**Beschreibung:** Die aktive Aufgabenliste (Seitenleiste und Dashboard) lässt sich zwischen drei Anzeigemodi umschalten; Seitenleiste und Dashboard führen je einen eigenen, getrennt persistierten Modus-Zustand.

**Bedingungen:**
- Modusauswahl über die `RadioButton`s `AufgabenModus:{Datum|Repository|KI}` (Seitenleiste) bzw. `DashboardAufgabenModus:{…}` (Dashboard).
- Die Modus-Buttons in der Seitenleiste sind nur bei ausgeklappter Navigation sichtbar (`IsNavigationExpanded`).

**Verhalten:**
- Modus `Datum`: flache Liste, sortiert absteigend nach `SortierZeitpunkt` (`LastCliStartUtc ?? CreatedAt`), Tie-Breaker `Title`, `Id` — identisch zur Sortierung der Datenquelle `WorkItemService.GetActiveWorkItemsAsync`.
- Modus `Repository`: Gruppierung nach `GitRepository.RepositoryName`; Gruppen alphabetisch, Fallback-Gruppe `Ohne Repository` (Aufgaben ohne `GitRepository`-Verknüpfung) immer am Ende; innerhalb der Gruppe wie im Modus `Datum`.
- Modus `Ki`: Gruppierung nach dem aufgelösten KI-Plugin-Namen aus `WorkItem.AiPluginPrefix`; Gruppen alphabetisch, Fallback-Gruppe `Nicht ausgeführt` (leeres Prefix) immer am Ende; ein gesetzter, aber nicht auflösbarer Prefix (Plugin nicht installiert) bildet eine eigene Gruppe unter dem Prefix-Namen — er fällt **nicht** unter `Nicht ausgeführt`.
- Der Untertitel der Kachel zeigt den Repository-Namen; bei Aufgaben ohne Repository-Verknüpfung der Projektname — analog bleibt `RepositoryGruppenName` aber die Fallback-Gruppe (der Projektname wird nicht als Gruppenname verwendet).
- Modus-Persistenz: Jede Ansicht speichert ihren Modus unter einem eigenen `AppSetting`-Schlüssel (`aktiveaufgaben.listenmodus` bzw. `aktiveaufgaben.listenmodus.dashboard`, Wert = Enum-Name) und stellt ihn beim nächsten Start wieder her. Fehlender oder ungültiger Wert → Default `Datum`. Eine inzwischen erfolgte Benutzerumschaltung wird von einer verspäteten Wiederherstellung nicht überschrieben; der wiederhergestellte Wert wird nicht zurückgeschrieben.

**Umsetzung:** `WorkItemListLayout` (Deskriptoren je Modus auf instanz-eigener `ListCollectionView` pro `ActiveTasksListControl` — die geteilte `ActiveWorkItemsList` darf keine gemeinsame Default-View gruppieren); `WorkItemListModeBindung` (gemeinsame Zustands-/Race-Guard-Logik) + `WorkItemListModeService` (Persistenz in eigenem DI-Scope, Muster `DarkModeService`); Mapping der Gruppenschlüssel in `MainWindowViewModel.MapActiveWorkItemPanelItem`.
