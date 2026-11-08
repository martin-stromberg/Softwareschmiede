# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PromptZeitVersandService.cs (PromptZeitVersandService)

- **Thread-Safety / Lost-Update** — `SchedulePromptAsync`, Sofortversand-Fehlerpfad (Zeilen 83–87): `UpsertDatensatzAsync` läuft unbedingt, bevor `MarkiereWartetAufSession` seinen Guard prüft. Wird während des in-flight-Sofortversands (`await SendPromptAsync`, Zeile 83) oder während des Upserts ein neuer Plan P2 für dieselbe Aufgabe geplant, hat P2 den Datensatz bereits per eigenem `UpsertDatensatzAsync` auf P2 gesetzt und einen neuen Eintrag mit armiertem Timer eingetragen. Der fehlgeschlagene Alt-Versand überschreibt danach den Datensatz mit dem alten Plan P1 — In-Memory bleibt korrekt P2 (Guard greift), der Datensatz ist aber P1. Folgekette: P2s Timer versendet später P2; `LoescheDatensatzWennAktuellAsync` findet dann einen Mismatch (Datensatz P1 ≠ Referenz P2) und löscht nicht — der längst ersetzte P1-Datensatz überlebt permanent und wird nach einem Neustart als „wartet auf Session" zugestellt. Das ist dieselbe Race-Klasse wie Befund 2 des ersten Reviews; der Lost-Update-Schutz wurde auf der In-Memory- und der Lösch-Seite gesetzt, nicht aber auf der Schreib-Seite des neuen Fehlerpfads.

  Empfehlung: Den Upsert im Fehlerpfad ebenfalls konditional machen — z. B. eine Store-Methode `UpsertWennUebereinstimmendAsync`, die nur schreibt, wenn der aktuelle Datensatz noch dem erwarteten Vorgängerstand entspricht (`ersetzterEintrag?.Info`; bei `ersetzterEintrag == null`: nur schreiben, wenn kein Datensatz existiert — ein vorhandener Datensatz ohne ersetzten Eintrag kann nur von einem zwischenzeitlichen neuen Plan stammen). Alternativ als Minimalabsicherung den Upsert an das Ergebnis der Markierungs-Guard koppeln (nur upserten, wenn der Eintrag tatsächlich zurückgelegt wurde) — das schließt das Hauptfenster, lässt aber ein Restfenster zwischen Markierung und Upsert offen; der konditionale Schreibzugriff ist die vollständige Lösung.

- **Fehlerbehandlung / Datenkonsistenz** — `LoescheDatensatzWennAktuellAsync` bzw. `ZeitgesteuerterPromptStore.LoeschenWennUebereinstimmendAsync` (Store, Zeilen 76–92): Der Referenzvergleich kann einen Datensatz, der fachlich zum versendeten Plan gehört, fälschlich nicht löschen — immer dann, wenn ein früherer Store-Schreibvorgang fehlschlug (`MitStoreAsync` loggt solche Fehler nur als Warning) und der Datensatz dadurch vom In-Memory-Stand abgedriftet ist. Konkrete Pfade: (a) `UpdateZielZeitpunktAsync` nach einer Pausenverschiebung schlug fehl → Datensatz trägt die alte Zielzeit, `entry.Info.TargetTime` das Pausenende → Versand zum Pausenende erfolgreich, Vergleich scheitert → der bereits versendete Prompt wird nach Neustart als „wartet auf Session" wiederhergestellt und ein zweites Mal zugestellt. (b) `UpsertAsync` beim Ersetzen schlug fehl → Datensatz enthält noch den alten Plan → Versand des neuen Plans erfolgreich, Vergleich scheitert → der vom Nutzer ersetzte alte Prompt wird nach Neustart zugestellt. Zusätzlich ist der Mismatch-Fall im Store ein stiller `return` ohne Log — die Drift bleibt diagnostisch unsichtbar.

  Empfehlung: Den Mismatch-Fall mindestens loggen (LogWarning mit Datensatz- vs. Referenzwerten). Fachlich sauberer: Löschung zulassen, wenn zum Löschzeitpunkt kein In-Memory-Eintrag mehr für die Aufgabe existiert — nach einem erfolgreichen Versand kann ein verbleibender Datensatz dann keinem lebenden neueren Plan gehören (jeder In-Memory-Plan persistiert seinen Datensatz beim Planen), ein Mismatch zeigt in dem Fall eine abgedriftete Leiche an, die gelöscht werden sollte.

- **Fehlerbehandlung / Ressourcen-Lebenszyklus** — `Dispose` (Zeile 52): Die Implementierung löst nur das `CliProcessStatusChanged`-Abo, lässt aber armierte Timer und Einträge in `_scheduledPrompts` bestehen. Eine „gestoppte" Instanz kann danach weiterhin Timer-Callbacks feuern und Prompts versenden — die für den dokumentierten Zweck (mehrere Service-Instanzen, simulierter App-Neustart) intendierte Trennung ist damit unvollständig: Ein simulierter Neustart mit noch zukünftigem Plan würde über den Alt-Timer des „beendeten" Service eine Doppelzustellung erzeugen (im aktuellen Test `Restore_NachNeustart_LiefertBeiSessionStartZu` nicht sichtbar, weil dessen Plan beim Advance bereits fällig wurde und kein armierte Timer mehr existiert).

  Empfehlung: In `Dispose` zusätzlich unter `_lock` alle Einträge aus `_scheduledPrompts` entfernen und deren Timer disposen, damit eine disposed Instanz vollständig inert ist.

## Verifikation der Befunde aus dem ersten Review

Alle sechs Befunde aus `review-code.1.md` sind behoben:

- **Persistenz-Lücke Sofortversand:** `UpsertDatensatzAsync` wird im Fehlerpfad vor `MarkiereWartetAufSession` aufgerufen (Zeilen 85–86). ✔
- **Lost-Update (Versand-Löschung):** `LoescheDatensatzWennAktuellAsync` + `LoeschenWennUebereinstimmendAsync` mit korrekter Referenzwahl (`ersetzterEintrag?.Info ?? sofortInfo`). Die Unix-ms-Normalisierung ist korrekt: `ZielZeitpunktUtc` läuft durch `UnixMillisConverter` (`ToUnixTimeMilliseconds()`, SoftwareschmiededDbContext.cs Zeilen 13–15, 250), der Vergleich normiert beide Seiten auf dieselbe Granularität. ✔
- **Lost-Update (Wartestellungs-Markierung):** `!_scheduledPrompts.ContainsKey`-Guard in `MarkiereWartetAufSession` (Zeile 258) analog zur Pausen-Umplanung; `ScheduledPromptChanged` feuert nur bei tatsächlicher Markierung. ✔
- **Restore-Catch-Scope:** try/catch pro Schleifeniteration (Zeilen 164–229); fehlgeschlagene Timer-Armiierung entfernt den eingetragenen Eintrag wieder (Zeilen 209–220). ✔
- **Doppelter Store-Code:** `MitStoreAsync` extrahiert (Zeilen 489–506), alle drei Store-Helfer nutzen es. ✔
- **Manueller Status-Set im VM:** Ersetzt durch `AktualisiereWartestellungsAnzeige()` (TaskDetailViewModel.cs Zeile 1845); Anzeige inkl. „Prompt wartet auf CLI-Start" wird vollständig aus dem Service-Zustand befüllt. ✔
- **Test-Event-Leak:** Service implementiert `IDisposable`; `Restore_NachNeustart_LiefertBeiSessionStartZu` ruft `ersterService.Dispose()` und verifiziert `WritePromptAsync` mit `Times.Once`. ✔

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`
- `src/Softwareschmiede/Application/Services/ZeitgesteuerterPromptStore.cs`
- `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs`
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs`
- `src/Softwareschmiede/Domain/Entities/ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`
- `src/Softwareschmiede/Migrations/20261107211018_AddZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede/Migrations/20261107211018_AddZeitgesteuerterPrompt.Designer.cs`
- `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs`
- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/E2E/MainTest.cs`
- `src/Softwareschmiede.Tests/E2E/Views/TaskDetailView.cs`
