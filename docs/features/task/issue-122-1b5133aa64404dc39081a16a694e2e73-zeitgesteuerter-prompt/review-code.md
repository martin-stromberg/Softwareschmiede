# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Verifikation der Vorbefunde (Durchlauf 2)

Die vier gemeldeten Fixes wurden gegen den aktuellen Diff geprüft:

1. **Test-Flakiness durch `DateTime.Now` (injizierter `TimeProvider`)** — sauber behoben.
   Das ViewModel besitzt jetzt ein `TimeProvider _timeProvider`-Feld (TaskDetailViewModel.cs, Zeile 41) und liest die aktuelle Zeit in `SchedulePromptAsync` deterministisch über `_timeProvider.GetLocalNow()` (Zeile 1138); `DateTime.Now` ist aus diesem Pfad entfernt. In der DI-Registrierung ist `TimeProvider.System` als Singleton eingetragen (App.xaml.cs, Zeile 192). Alle drei Konstruktionsstellen des ViewModels wurden konsistent nachgezogen: `TaskDetailViewModelTests_ZeitgesteuerterPrompt` injiziert einen `FakeTimeProvider` mit fixer Uhrzeit `2026-07-12 10:00:00` (Zeile 29, 42, 100), `TaskDetailViewModelTestFactory` (Zeile 59) und `TaskDetailViewModelTests` (Zeile 141) nutzen `TimeProvider.System`. Der zuvor flaky Test `SchedulePrompt_GueltigeZeit_RuftServiceAuf` rechnet nun gegen die fixe Fake-Uhr (`GetLocalNow().AddMinutes(2)` = 10:02, immer in der Zukunft), die Mitternachts-Sonderbehandlung ist entfallen. Zeitfenster-Abhängigkeit im Unit-Test vollständig eliminiert.

2. **Fehlende Zukunfts-Validierung (Wartestellung-Status nur bei tatsächlicher Pufferung)** — sauber behoben.
   `SchedulePromptAsync` setzt `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` jetzt nur noch, wenn `_promptZeitVersandService.GetScheduledPromptStatus(_aufgabeId) is not null` (TaskDetailViewModel.cs, Zeile 1153–1157) — d. h. wenn der Service tatsächlich einen Warteschlangeneintrag angelegt hat. Bei einer bereits vergangenen Uhrzeit versendet der Service sofort (PromptZeitVersandService.cs, Zeile 47–52) und legt keinen Eintrag an, sodass der irreführende „Wartestellung"-Status nicht mehr gesetzt wird. Der Kommentar (Zeile 1149–1152) dokumentiert die Absicht sauber. Servicetest `SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort` deckt den Sofortversand-Pfad ab.

3. **`ObjectDisposedException`-Behandlung in `SendPromptAsync`** — sauber behoben.
   `SendPromptAsync` fasst `WritePromptAsync` + `PromptSent?.Invoke` jetzt in try/catch (PromptZeitVersandService.cs, Zeile 120–139): `OperationCanceledException` still, `ObjectDisposedException` auf Debug-Level mit Kontext, sonstige Ausnahmen auf Warning-Level. Konsistent zum bestehenden Muster in `KiAusfuehrungsService.SendCommandDelayedAsync`. Die Ausnahme propagiert damit weder in den Timer-Pfad (`SafeFireAndForget`) noch in das `SchedulePromptCommand`.

4. **E2E-Test-Flakiness (Mitternachts-Guard)** — überwiegend behoben, kleines Restfenster bleibt (siehe Befund unten).

## Befunde

### E2E_ZeitgesteuerterPrompt.cs (E2E_ZeitgesteuerterPrompt)

- **Testqualität (Rest-Mitternachts-Flakiness, niedrige Priorität)** — Der ergänzte Guard (Zeile 40–44) begrenzt `jetzt.AddMinutes(5)` bei Tagesüberlauf auf die feste heutige Zielzeit `23:59:00`. Das reduziert das flaky Fenster von ehemals 5 Minuten (23:55:00–23:59:59) auf 1 Minute, schließt es aber nicht vollständig: Läuft der Test zwischen `23:59:00` und `23:59:59`, ist die auf `23:59:00` begrenzte Zielzeit bereits vergangen (`targetTime <= now`, da die App nur Stunde/Minute mit Sekunde 0 verwendet). Die Produktivlogik versendet dann sofort statt zu puffern, `ScheduledPromptStatus` wird nie gesetzt, und `WaitForElement` (Zeile 61) läuft in ein Timeout. In diesem letzten Tagesminutenfenster existiert prinzipiell kein zukünftiger Minuten-Zeitpunkt mehr für „heute", weshalb der Guard das Problem hier nicht lösen kann.

  Empfehlung: Für dieses terminale Fenster den Test überspringen statt eine unerreichbare Zielzeit zu wählen, z. B. `Skip.If(jetzt.Hour == 23 && jetzt.Minute >= 59, "Kein zukünftiger heutiger Minuten-Zeitpunkt im letzten Tagesminutenfenster verfügbar");` vor der Zielzeitberechnung. Damit meldet der Test in diesem 1-Minuten-Fenster ein ehrliches „Skipped" statt eines Timeouts. Priorität niedrig (trifft ≈1 Minute/Tag), aber die Grundursache aus Durchlauf 2 ist nur verkleinert, nicht beseitigt.

## Geprüfte Dateien

- `src/Softwareschmiede.App/App.xaml.cs`
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml`
- `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`
- `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`
- `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/TaskDetailViewModelTests_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs`
- `src/Softwareschmiede.Tests/E2E/E2E_ZeitgesteuerterPrompt.cs`
- `src/Softwareschmiede.Tests/Helpers/TaskDetailViewModelTestFactory.cs`
- `src/Softwareschmiede.Tests/Softwareschmiede.Tests.csproj`
