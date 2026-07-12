# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Verifikation der Vorbefunde (Durchlauf 1)

Die vier Befunde aus `review-code.1.md` sind behoben:

1. **Stornierung bei CLI-Stopp** — behoben. `OnCliProcessStatusChanged` (TaskDetailViewModel.cs, Zeile 864) ruft im Zweig `status != CliProcessStatus.Gestartet` nun `_promptZeitVersandService.CancelScheduledPrompt(aufgabeId)` auf, bevor `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` genullt werden. Durch einen dedizierten Test (`OnCliProcessStatusChanged_CliGestoppt_StorniertGeplantenPrompt`) abgesichert.
2. **Change-Notification für `CanSchedulePrompt`** — behoben. Der `IsCliRunning`-Setter (Zeile 151) und der `SelectedPromptVorlage`-Setter (Zeile 209) feuern jetzt beide `OnPropertyChanged(nameof(CanSchedulePrompt))`, konsistent zu den Zeitfeld-Settern. Zwei neue Tests decken beide Pfade ab.
3. **Einheitliche Sprach-Namenskonvention im Service** — behoben. `PromptZeitVersandService` verwendet durchgängig englische Member (`PromptSent`, `SchedulePromptAsync`, `CancelScheduledPrompt`, `SendPromptAsync`, `HandleTimerElapsedAsync`, `GetScheduledPromptStatus`). Keine deutsch/englisch-gemischten Verb-Bezeichner mehr innerhalb der Klasse.
4. **Zeitabhängiger flaky Test** — nur teilweise behoben, siehe Befund unten.

## Befunde

### TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs (TaskDetailViewModelTests_ZeitgesteuerterPrompt)

- **Testqualität (zeitabhängige Flakiness, Restfenster)** — `SchedulePrompt_GueltigeZeit_RuftServiceAuf` (Zeile 153–182): Der Mitternachts-Guard (Zeile 168–170) reduziert das Problem, eliminiert es aber nicht. Läuft der Test zwischen 23:59:00 und 23:59:59, liegt `jetzt.AddMinutes(2)` am Folgetag, der Guard weicht auf die feste heutige Zielzeit 23:59:00 aus — diese ist zu diesem Zeitpunkt jedoch `<= now`. Die Produktivlogik (`PromptZeitVersandService.SchedulePromptAsync`) versendet dann sofort und legt keinen Warteschlangeneintrag an, wodurch `GetScheduledPromptStatus(...)` `null` liefert und die Assertion `NotBeNull` (Zeile 178) fehlschlägt. Ursache bleibt, dass das ViewModel `DateTime.Now` in `SchedulePromptAsync` (TaskDetailViewModel.cs, Zeile 1135) fest verdrahtet und keinen `TimeProvider` injiziert bekommt — die im Vorbefund empfohlene deterministische Lösung wurde nicht umgesetzt, nur ein verkleinertes Zeitfenster.

  Empfehlung: Das ViewModel wie den Service über einen injizierten `TimeProvider` steuerbar machen und im Test einen `FakeTimeProvider` mit fester Uhrzeit setzen (der `PromptZeitVersandServiceTests` nutzt bereits `FakeTimeProvider` — dasselbe Muster auf das ViewModel anwenden). Damit entfällt jede Zeitfenster-Abhängigkeit vollständig.

### TaskDetailViewModel.cs (TaskDetailViewModel)

- **Fehlerbehandlung / Zustands-Konsistenz** — `SchedulePromptAsync` (Zeile 1113–1151) validiert nur die Wertebereiche von Stunde/Minute, aber nicht, ob die aus dem heutigen Datum konstruierte `targetTime` (Zeile 1138) überhaupt in der Zukunft liegt. Gibt der Nutzer eine bereits vergangene Uhrzeit des heutigen Tages ein (z. B. aktuell 15:00 Uhr, Eingabe 10:00), versendet der Service den Prompt sofort. Anschließend setzt das ViewModel dennoch unbedingt `ScheduledPromptStatus = "Prompt in Wartestellung"` und `ScheduledPromptTimeDisplay` auf die vergangene Uhrzeit (Zeile 1146–1147). Das `PromptSent`-Event (→ `OnPromptSent`, Zeile 1153) nullt diese Anzeige zwar wieder, aber erst über `_dispatcherInvoke` — die resultierende Reihenfolge und damit der Endzustand hängen davon ab, ob der Dispatcher synchron (Tests: irreführender Endzustand „in Wartestellung") oder asynchron (Produktion: kurzes Flackern) ausgeführt wird. In beiden Fällen zeigt die UI dem Nutzer für eine bereits gesendete, gar nicht gepufferte Eingabe „Wartestellung" an.

  Empfehlung: Vor dem Planen prüfen, ob `targetTime` in der Zukunft liegt (analog zur Service-Logik). Liegt sie in Vergangenheit/Gegenwart, entweder eine Fehlermeldung setzen (z. B. „Zielzeit liegt in der Vergangenheit") und nicht planen, oder den Wartestellungs-Status nur setzen, wenn tatsächlich gepuffert wurde (z. B. anhand des Rückgabewerts/`GetScheduledPromptStatus`).

### PseudoConsoleSession.cs (PseudoConsoleSession) / PromptZeitVersandService.cs (PromptZeitVersandService)

- **Fehlerbehandlung (fehlende Behandlung disposed Session, niedrige Priorität)** — `WritePromptAsync` (PseudoConsoleSession.cs, Zeile 267–272) schreibt ohne jede Ausnahmebehandlung direkt auf `InputStream`. Der aufrufende `SendPromptAsync` (PromptZeitVersandService.cs, Zeile 109–122) prüft zwar auf `session is null`, aber die Session kann zwischen Prüfung und Schreibvorgang bzw. bis zum verzögerten Timer-Feuern disposed sein (Prozess beendet), sodass `WriteAsync`/`FlushAsync` eine `ObjectDisposedException`/`IOException` werfen. Der bestehende analoge Schreibpfad `KiAusfuehrungsService.SendCommandDelayedAsync` (Zeile 618–638) fängt `ObjectDisposedException` bewusst als gutartigen Race ab und loggt nur auf Debug-Level; hier propagiert die Ausnahme stattdessen bis in `SafeFireAndForget` (Timer-Pfad) bzw. in das `SchedulePromptCommand` und wird als Fehler geloggt. Durch den in Durchlauf 1 ergänzten `CancelScheduledPrompt`-Aufruf bei CLI-Stopp ist das Fenster klein, aber nicht geschlossen (Prozess kann unabhängig vom Status-Event enden).

  Empfehlung: In `SendPromptAsync` den `WritePromptAsync`-Aufruf in einen `try/catch` fassen, der `ObjectDisposedException` (und ggf. `IOException`) als gutartig behandelt und lediglich auf Debug/Warning loggt — konsistent zum bestehenden `SendCommandDelayedAsync`.

### E2E_ZeitgesteuerterPrompt.cs (E2E_ZeitgesteuerterPrompt)

- **Testqualität (latente Mitternachts-Flakiness, niedrige Priorität)** — `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` (Zeile 35) berechnet die Zielzeit als `DateTime.Now.AddMinutes(5)` und übergibt nur Stunde/Minute. Im Fenster 23:55–23:59 rollt `AddMinutes(5)` auf den Folgetag; die Produktivlogik baut daraus eine bereits vergangene heutige Zielzeit → Sofortversand → die erwartete Statusanzeige „Prompt in Wartestellung" erscheint nicht (bzw. nur flüchtig), das `WaitForElement` (Zeile 52) läuft in ein Timeout. Gleiche Grundursache wie beim Unit-Test, hier ungeschützt.

  Empfehlung: Nach Umstellung des ViewModels auf einen injizierbaren `TimeProvider` ist der E2E-Test hiervon nicht direkt betreffbar; alternativ im Test einen garantiert-heute-in-der-Zukunft liegenden Zeitpunkt wählen bzw. das Mitternachtsfenster ausschließen.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`
- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs` (neu, untracked)
- `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs` (neu, untracked)
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs` (neu, untracked)
- `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs` (neu, untracked)
- `src/Softwareschmiede.Tests/E2E/E2E_ZeitgesteuerterPrompt.cs` (neu, untracked)
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
