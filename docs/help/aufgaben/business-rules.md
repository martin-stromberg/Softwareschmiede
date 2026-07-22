# Aufgaben & KI-Entwicklungsprozess — Business Rules

## Exklusiver KI-Lauf

**Beschreibung:** Für eine Aufgabe kann immer nur ein KI-Lauf gleichzeitig aktiv sein.

**Bedingungen:**
- `KiAusfuehrungsService.IsRunning(aufgabeId)` gibt `true` zurück.

**Verhalten:**
- Wenn aktiv: Zweiter `StartKiLauf`-Aufruf wird mit `LogWarning` abgewiesen, kein zweiter Task gestartet.
- Wenn nicht aktiv: Neuer Lauf wird gestartet.

**Umsetzung:** `KiAusfuehrungsService.StartKiLauf` — Guards gegen doppelte Ausführung im In-Memory-Dictionary.

---

## Branch-Strategie beim Prozessstart

**Beschreibung:** Die Branch-Wahl hängt davon ab, ob ein vorhandener Remote-Branch angegeben wurde und ob dieser der Hauptbranch ist.

**Bedingungen:**
- `basisBranchName` leer oder null → immer neuer `task/`-Branch.
- `basisBranchName` == Hauptbranch → neuer `task/`-Branch (kein direkter Commit auf Main).
- `basisBranchName` != Hauptbranch → vorhandener Branch wird ausgecheckt.
- Aufgabe mit `IssueReferenz.IssueNummer` → neuer Branch enthält die Issue-Nummer.
- Aufgabe ohne `IssueReferenz` → neuer Branch enthält nur Aufgaben-ID und Titel-Slug.

**Umsetzung:** `EntwicklungsprozessService.ProzessStartenAsync` — `nutzeExistierendenBranch`-Flag.

---

## Aufgaben ohne Issue-Bezug

**Beschreibung:** Aufgaben, die aus einem Git-Plugin ohne verknüpftes Issue erstellt wurden, sind start- und ausführbar.

**Bedingungen:**
- Die Aufgabe hat keine `IssueReferenz`.
- Ein Repository kann über Aufgaben-, Projekt- oder eindeutigen Projektkontext aufgelöst werden.

**Verhalten:**
- Der Prozessstart setzt keine Issue-Referenz voraus.
- Der Branchname wird im Format `task/{aufgabe.Id:N}-{slug}` erzeugt.
- Die lokale `issue.md` wird weiterhin erstellt; sie enthält die Aufgabenstammdaten und die Anforderungsbeschreibung, aber keine verpflichtende Issue-Verknüpfung.
- Mehrdeutige Repository-Auflösung bleibt ein Fehlerfall.

**Umsetzung:** `EntwicklungsprozessService.ErstelleTaskBranchName`, `ProzessStartenAsync` und `CreateIssueFileAsync`.

---

## Issue-Anlage aus der Aufgabendetailansicht

**Beschreibung:** Ein neues Issue kann aus einer Aufgabe angelegt und anschließend genau einmal mit dieser Aufgabe verknüpft werden.

**Bedingungen:**
- Die Aufgabe besitzt noch keine `IssueReferenz`.
- Ein zugeordnetes Repository und ein Provider mit `IIssueCreateProvider` sind verfügbar.
- Der Provider meldet die Issue-Anlage für das Repository als unterstützt.

**Verhalten:**
- Der Ribbon-Button und der Dialog werden nur bei verfügbarer Issue-Anlage angeboten.
- Titel und Anforderungsbeschreibung werden als editierbare Initialwerte übernommen.
- Provider-Templates sind optional. Bei einem ausgewählten Template werden Template-Inhalt, Trennlinie und `Originalanforderung:` zusammengesetzt; der Text bleibt bearbeitbar.
- Die KI-Ausfüllhilfe ist optional und verwendet nur Provider mit `IIssueTemplateTextGenerator`.
- Das externe Issue wird vor der lokalen Speicherung erstellt. Die lokale `IssueReferenz` wird nur bei erfolgreicher Anlage und erfolgreicher konkurrenzsicherer Zuordnung gespeichert.
- Nach erfolgreicher Zuordnung wird die Aufgabe neu geladen; der Button „Issue anlegen" verschwindet.
- Abbruch, nicht unterstützte Templates, Providerfehler und Validierungsfehler führen nicht zu einer lokalen Issue-Zuordnung. Ein Fehler bei der lokalen Speicherung nach externer Anlage wird mit Issue-URL oder -Nummer angezeigt; ein allgemeines Provider-Rollback ist nicht garantiert.

**Umsetzung:** `TaskDetailViewModel.IssueAnlegenAsync`, `IssueCreateDialogViewModel`, `AufgabeService.TryAssignIssueReferenzIfNoneAsync` sowie `IIssueCreateProvider` und optionale Template-/KI-Fähigkeiten.

---

## Automatische Issue-Schliessung bei Pull Requests

**Beschreibung:** Wenn eine Aufgabe aus einem GitHub-Issue erstellt wurde, bleibt die Issue-Referenz an der Aufgabe erhalten und wird beim Erstellen eines Pull Requests automatisch in den Pull-Request-Body uebernommen.

**Bedingungen:**
- Die Aufgabe hat eine `IssueReferenz`.
- `IssueReferenz.IssueNummer` ist groesser als 0.
- Fuer dieselbe Issue existiert im Pull-Request-Body noch keine Closing-Direktive.

**Verhalten:**
- Der Pull-Request-Body aus der Aufgabendetailansicht enthaelt primaer die Commits des Aufgabenbranches statt der urspruenglichen Anforderungsbeschreibung.
- Der Pull-Request-Body wird um `Closes #<IssueNummer>` ergaenzt.
- Ein leerer oder nur aus Whitespace bestehender Body wird durch die Closing-Direktive ersetzt.
- Bereits vorhandene Direktiven fuer dieselbe Issue, z. B. `Fixes #17`, `Closed #17` oder `Fixes owner/repo#17`, werden nicht dupliziert.
- Direktiven fuer andere Issues bleiben erhalten; die aktuelle Issue wird zusaetzlich ergaenzt.
- Aufgaben ohne Issue-Referenz oder ohne gueltige Issue-Nummer behalten das bisherige Pull-Request-Verhalten.

**Umsetzung:** `PullRequestBodyBuilder`, `GitOrchestrationService.PullRequestErstellenAsync` und der aeltere PR-Pfad `EntwicklungsprozessService.PullRequestErstellenAsync`.

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

**Umsetzung:** `MainWindowViewModel.Title`, `TaskDetailViewModel.DetailTitelAenderungAction` und `TaskDetailViewModel.AktiverCliName`.

---

## Kontextkomprimierung

**Beschreibung:** Wird die Kontextdatei zu groß, komprimiert die KI sie selbst auf ein strukturiertes Markdown-Dokument.

**Bedingungen:**
- `context.Length > SoftLimit` (Standard: 12 000 Zeichen): Komprimierung wird ausgelöst.
- `context.Length > HardLimit` (Standard: 20 000 Zeichen): Warneintrag im Protokoll, KI-Lauf läuft trotzdem.

**Verhalten:**
- Soft-Limit überschritten: `CompressContextAsync` startet einen separaten KI-Lauf mit Komprimierungsprompt. Das Ergebnis muss die Pflichtabschnitte „Ziel", „Offene Punkte" und „Letzte Entscheidungen" enthalten.
- Pflichtabschnitte fehlen oder Ergebnis leer: Exception, KI-Lauf wird nicht gestartet.

**Umsetzung:** `EntwicklungsprozessService.EnsureContextWithinLimitsAsync`, `CompressContextAsync`, `ContainsMandatoryCompressionSections`.

---

## Rate-Limit-Erkennung und Vorschlag

**Beschreibung:** Erkennt die KI ein Rate-Limit in der CLI-Ausgabe, wird automatisch ein Prompt-Vorschlag mit Ausführungszeitpunkt gespeichert. Der Erkennungspfad läuft über `ProtokollService.AddCliOutputAsync`, das von der automatischen ConPTY-Ausgabeprotokollierung aufgerufen wird.

**Bedingungen:**
- Ausgabezeile beginnt mit `[[SOFTWARESCHMIEDE_RATE_LIMIT]]`.
- Enthält `resetUtc=<ISO-Zeit>` und optional `prompt=<Text>`.

**Verhalten:**
- Vorschlag und Zeitpunkt werden in `Aufgabe.VorschlagPrompt` / `VorschlagAusfuehrenAbUtc` gespeichert.
- In der UI erscheint der Vorschlag vorausgefüllt mit dem gespeicherten Ausführungszeitpunkt.
- Kein automatischer Neustart — der Anwender muss manuell senden oder den Zeitpunkt anpassen.

**Umsetzung:** `EntwicklungsprozessService.TryParseRateLimitSuggestion`, `AufgabeService.SavePromptVorschlagAsync`.

---

## Automatische CLI-Ausgabeprotokollierung

**Beschreibung:** CLI-Ausgaben einer ConPTY-Sitzung werden automatisch dem Protokoll der zugehörigen Aufgabe zugeordnet.

**Bedingungen:**
- Der CLI-Prozess wird über `KiAusfuehrungsService.StartWithPseudoConsoleAsync` gestartet.
- Die `PseudoConsoleSession` liefert Output-Bytes aus ihrer internen Leseschleife.

**Verhalten:**
- Pro ConPTY-Start wird ein `CliOutputProtokollWriter` für genau eine `aufgabeId` erzeugt.
- Die Ausgabe wird zeilenweise als `ProtokollTyp.CliOutput` gespeichert.
- Die Reihenfolge innerhalb einer Session bleibt durch einen sequenziellen Hintergrund-Worker erhalten.
- Die Protokollierung ist UI-unabhängig und läuft weiter, wenn kein `TerminalControl` gebunden ist.
- Persistenzfehler werden geloggt, brechen den CLI-Prozess aber nicht ab.
- Bei hoher Ausgaberate begrenzt eine bounded Queue den Speicherverbrauch und erzeugt Backpressure.
- Beim Abschluss wartet die Senke auf die aktive Queue-Phase eines bereits dekodierten Chunks, bevor der Channel geschlossen wird.

**Umsetzung:** `ITerminalOutputSink`, `CliOutputLineAccumulator`, `CliOutputProtokollWriter`, `PseudoConsoleSession.ReadLoopAsync`, `KiAusfuehrungsService.StartWithPseudoConsoleAsync`, `ProtokollService.AddCliOutputAsync`.

---

## Aufgaben-Recovery

**Beschreibung:** Eine Aufgabe im Status `InBearbeitung` oder `KiAktiv` kann manuell wiederhergestellt werden, wenn der Prozess nicht mehr läuft.

**Bedingungen:**
- Status ist `InBearbeitung` oder `KiAktiv`.
- Kein aktiver KI-Lauf im `KiAusfuehrungsService`.
- `LastHeartbeatUtc` ist älter als 5 Minuten oder nicht gesetzt.

**Verhalten:**
- Wenn alle Bedingungen erfüllt: Button „🩹 Aufgabe wiederherstellen" ist klickbar, Status wird auf `InBearbeitung` zurückgesetzt.
- Wenn Bedingungen nicht erfüllt: Button deaktiviert mit erklärendem Hinweistext.

**Umsetzung:** `AufgabeRecoveryService`, `AufgabeDetail.razor.cs._recoveryAllowed`.

---

## Klonpfad-Bereinigung

**Beschreibung:** Das lokale Klonverzeichnis enthält unter Windows schreibgeschützte Git-Pack-Dateien, die normales `Directory.Delete` verhindern.

**Verhalten:**
- Vor dem Löschen werden alle Dateien im Verzeichnis auf `FileAttributes.Normal` gesetzt.
- Danach erfolgt `Directory.Delete(path, recursive: true)`.

**Umsetzung:** `EntwicklungsprozessService.DeleteDirectoryForce` — notwendig für `.git/objects/pack/*.idx`-Dateien unter Windows.

---

## Zeitgesteuerter Prompt-Versand

**Beschreibung:** Pro Aufgabe kann maximal ein Prompt zeitgesteuert geplant sein. Ein erneut geplanter Prompt ersetzt den vorherigen.

**Bedingungen:**
- Eine CLI für die Aufgabe läuft (`IsCliRunning == true`).
- Eine Promptvorlage ist ausgewählt.
- Mindestens eines der Zeitfelder (Stunde oder Minute) ist gesetzt.
- Die Zeit ist valide (Stunde: 0–23, Minute: 0–59).

**Verhalten:**
- Liegt die Zielzeit in der Vergangenheit/Gegenwart: Prompt wird sofort versendet (kein Eintrag in der Warteschlange).
- Liegt die Zielzeit in der Zukunft: Prompt wird gepuffert; ein Timer wird gestartet; bei Fälligkeit wird der Prompt automatisch versendet.
- Ein bereits geplanter Prompt für dieselbe Aufgabe wird ersetzt (vorheriger Timer wird abgebrochen).
- Falls die CLI zur Zielzeit nicht mehr aktiv ist, wird der Prompt still verworfen (keine `FehlerMeldung`, nur Log-Warnung).
- Beim Wechsel der Aufgabendetailansicht, beim `Dispose` des ViewModels oder beim Aufgabenabschluss wird ein geplanter Prompt storniert.
- Die Planung ist rein sitzungsgebunden und wird nicht persistiert — ein App-Neustart löscht alle geplanten Prompts.

**Umsetzung:** `PromptZeitVersandService` — verwaltet geplante Prompts pro Aufgabe in `Dictionary<Guid, ScheduledPromptEntry>` mit pro-Eintrag-Timer via `TimeProvider.CreateTimer`. `TaskDetailViewModel.SchedulePromptAsync` mit UI-Validierung und Status-Rendering.
