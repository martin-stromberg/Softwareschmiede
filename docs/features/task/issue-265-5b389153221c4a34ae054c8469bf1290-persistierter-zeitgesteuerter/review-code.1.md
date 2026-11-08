# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PromptZeitVersandService.cs (PromptZeitVersandService)

- **Fehlerbehandlung / Datenkonsistenz** — `SchedulePromptAsync`, Sofortversand-Pfad (Zeilen 71–80): Schlägt `SendPromptAsync` fehl, legt `MarkiereWartetAufSession` einen Wartestellungs-Eintrag mit dem **neuen** Prompt an — ein für dieselbe Aufgabe bereits persistierter Datensatz (aus einem früheren Plan) wird in diesem Pfad aber nicht per `UpsertDatensatzAsync` aktualisiert. Nach einem App-Neustart stellt `RestoreScheduledPromptsAsync` dann den **alten** Prompttext/Zielzeitpunkt wieder her; der neue Plan geht verloren. Betroffen sind nur Sofortversand-Versuche (`targetTime <= now`), die auf einen vorhandenen persistierten Plan treffen — genau der Ersetzungsfall, den das Feature abdecken soll.

  Empfehlung: Im Fehlerpfad vor `MarkiereWartetAufSession` zusätzlich `await UpsertDatensatzAsync(aufgabeId, promptText, targetTime)` aufrufen (bzw. `MarkiereWartetAufSession` um die Persistenz des übergebenen `ScheduledPromptInfo` erweitern).

- **Thread-Safety / Lost-Update** — Zwei Race-Fenster zwischen einem in-flight-Versand und einem parallelen Neuplanen derselben Aufgabe:
  1. `SendPromptAsync` (Zeile 367) löscht nach erfolgreichem Versand unbedingt per `LoescheDatensatzAsync(aufgabeId)` den Datensatz — auch dann, wenn zwischen dem Entfernen des Eintrags (`HandleTimerElapsedAsync`/`ZustellungNachSessionStartAsync`) und dem Versand bereits ein **neuer** Plan per `SchedulePromptAsync` → `UpsertDatensatzAsync` persistiert wurde. Der neue Datensatz wird vom alten Versand mitgelöscht; der neue In-Memory-Plan überlebt dann keinen Neustart.
  2. `MarkiereWartetAufSession` (Zeilen 212–225) schreibt `_scheduledPrompts[aufgabeId]` unbedingt und überschreibt damit einen zwischenzeitlich neu geplanten Eintrag inkl. Timer-Referenz. Der neue Timer bleibt armiert, ist aber verwaist: Feuert er später, findet `HandleTimerElapsedAsync` den veralteten Wartestellungs-Eintrag und versendet den **alten** Prompttext zur neuen Zielzeit. Die Pausen-Umplanung (Zeilen 273–288) hat für genau diesen Fall eine `!_scheduledPrompts.ContainsKey(aufgabeId)`-Guard — die Wartestellungs-Markierung nicht.

  Empfehlung: Dieselbe Guard-Logik in `MarkiereWartetAufSession` (nur zurücklegen, wenn kein neuerer Eintrag existiert) und bei `LoescheDatensatzAsync` nach dem Versand einen Abgleich vornehmen, dass der gelöschte Datensatz zum versendeten Plan gehört (z. B. Vergleich über `ZielZeitpunktUtc`/`PromptText` oder eine Generationsmarke im Eintrag).

- **Fehlerbehandlung** — `RestoreScheduledPromptsAsync` (Zeilen 148–195): Die `try/catch`-Hülle umschließt die gesamte `foreach`-Schleife. Schlägt die Verarbeitung eines einzelnen Datensatzes fehl (`aufgabeService.GetByIdAsync`, `store.UpdateZielZeitpunktAsync`, Timer-Anlage), werden alle **nachfolgenden** Pläne nicht wiederhergestellt — ein einzelner defekter Datensatz blockiert die komplette Wiederherstellung, obwohl genau „kein geplanter Prompt geht verloren" das Kernziel ist.

  Empfehlung: `try/catch` pro Schleifeniteration (Fehler loggen, mit nächstem Datensatz fortfahren), den äußeren Catch nur für das Laden der Gesamtliste behalten.

- **Doppelter Code** — `UpsertDatensatzAsync`, `LoescheDatensatzAsync` und `UpdateZielZeitpunktDatensatzAsync` (Zeilen 406–470) sind strukturell identisch: Scope erzeugen → `GetService<ZeitgesteuerterPromptStore>` → Aktion → `catch (Exception)` + `LogWarning`. Nur Aktion und Logtext unterscheiden sich.

  Empfehlung: Gemeinsame Hilfsmethode `MitStoreAsync(Func<ZeitgesteuerterPromptStore, Task> aktion, string fehlerText)` extrahieren.

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Doppelter Code / Anzeige-Inkonsistenz** — `SchedulePromptAsync` (Zeilen 1841–1849): Der manuelle Set von `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` dupliziert `AktualisiereWartestellungsAnzeige()`. Der Service feuert `ScheduledPromptChanged` synchron am Ende von `SchedulePromptAsync`; über `OnScheduledPromptChanged` → `_dispatcherInvoke` (`Dispatcher.Invoke`, synchron auf dem UI-Thread) läuft `AktualisiereWartestellungsAnzeige` also bereits vor dem manuellen Set. Danach überschreibt `ScheduledPromptTimeDisplay = targetTime.ToString("HH:mm")` die Anzeige mit der **unadjustierten** `targetTime` — hat der Service die Zielzeit wegen einer aktiven Pause auf das Pausenende verschoben (`status.TargetTime != targetTime`), zeigt die UI bis zum nächsten Event die falsche Uhrzeit.

  Empfehlung: Den Block durch `AktualisiereWartestellungsAnzeige()` ersetzen (deckt beide Fälle ab, inkl. Sofortversand ohne Warteschlangeneintrag) oder komplett entfernen, da das Event die Anzeige bereits synchronisiert.

### PromptZeitVersandServiceTests.cs (PromptZeitVersandServiceTests)

- **Testqualität** — `Restore_NachNeustart_LiefertBeiSessionStartZu` (Zeilen 773–813): Der „erste" Service (`ersterService`) bleibt am gemeinsamen `_kiService.CliProcessStatusChanged` abonniert — `PromptZeitVersandService` meldet das im Konstruktor gesetzte Abo nie ab und hat kein `Dispose`. Nach `_timeProvider.Advance(6min)` ist sein In-Memory-Eintrag in Wartestellung; `RaiseCliProcessStatusChanged` löst daher die Zustellung in **beiden** Services aus — `WritePromptAsync` wird auf der Session zweimal aufgerufen. Der Test prüft nur `zweiterService.PromptSent` und die leere Datenbank; eine Doppelzustellung fiele nicht auf.

  Empfehlung: Entweder das Abo der ersten Instanz vor dem „Neustart" lösen (Unsubscribe-Mechanismus oder Reflection analog `RaiseCliProcessStatusChanged`) oder zusätzlich auf der registrierten Session `WritePromptAsync` mit `Times.Once` verifizieren.

## Geprüfte Dateien

- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`
- `src/Softwareschmiede/Application/Services/ZeitgesteuerterPromptStore.cs`
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
