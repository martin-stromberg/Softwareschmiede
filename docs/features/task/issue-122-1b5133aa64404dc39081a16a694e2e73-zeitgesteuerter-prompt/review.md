# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Klassen

- [x] `ScheduledPromptInfo` (`record`, Value Object) — angelegt in `src/Softwareschmiede/Application/Services/ScheduledPromptInfo.cs` mit Feldern `AufgabeId` (Guid), `PromptText` (string), `TargetTime` (DateTimeOffset)
- [x] `PromptZeitVersandService` (Service, Singleton) — angelegt in `src/Softwareschmiede/Application/Services/PromptZeitVersandService.cs`
- [x] `PromptZeitVersandServiceTests` (Testklasse) — angelegt in `src/Softwareschmiede.Tests/Application/Services/PromptZeitVersandServiceTests.cs` (6 Tests)
- [x] `E2E_ZeitgesteuerterPrompt` (E2E-Testklasse) — angelegt in `src/Softwareschmiede.Tests/E2E/E2E_ZeitgesteuerterPrompt.cs`

### `PromptZeitVersandService` — Methoden/Verhalten

- [x] Feld `Dictionary<Guid, ScheduledPromptEntry> _scheduledPrompts` + `object _lock` — vorhanden (Timer pro Eintrag)
- [x] Timer ausschließlich über `TimeProvider.CreateTimer` — vorhanden (Z. 57)
- [x] Event `PromptVersendet` (`Action<Guid>`) — vorhanden; kein Fehler-Event (plankonform)
- [x] Methode `SchedulePromptAsync(Guid, string, DateTimeOffset)` — vorhanden; Vergangenheit/Jetzt → sofortiger Versand (Z. 47–52)
- [x] Ersetzen vorhandener Einträge inkl. Timer-Dispose (max. ein Prompt pro Aufgabe) — vorhanden (Z. 49/54)
- [x] Methode `CancelScheduledPrompt(Guid)` — vorhanden
- [x] Methode `GetScheduledPromptStatus(Guid)` — vorhanden
- [x] Timer-Callback `AufTimerFaelligAsync` — vorhanden; fehlende Session → stilles Verwerfen mit Log-Warnung, kein Event (Z. 112–118)

### `PseudoConsoleSession`

- [x] Methode `WritePromptAsync(string prompt, CancellationToken ct)` (public) — vorhanden (Z. 267–273); kapselt Encoding, WriteAsync, FlushAsync, MarkInputActivity

### `TaskDetailViewModel`

- [x] Property `ScheduledPromptTargetHours` (`int?`) — vorhanden
- [x] Property `ScheduledPromptTargetMinutes` (`int?`) — vorhanden
- [x] Property `ScheduledPromptStatus` (`string?`) — vorhanden
- [x] Property `ScheduledPromptTimeDisplay` (`string?`) — vorhanden
- [x] Property `CanSchedulePrompt` (`bool`) — vorhanden (CLI läuft + Vorlage + Zeit)
- [x] Command `SchedulePromptCommand` (`AsyncRelayCommand`, CanExecute = `CanSchedulePrompt`) — vorhanden
- [x] Private Methode `SchedulePromptAsync(CancellationToken)` — vorhanden; Validierung, Zielzeitberechnung (`DateTime.Now`), Platzhalterauflösung, Service-Aufruf, Statuspflege, Feldreset
- [x] `PromptVorlageAuswaehlenAsync` — Sofortversand nur bei beiden leeren Zeitfeldern; nutzt `WritePromptAsync`
- [x] Konstruktor — nimmt `PromptZeitVersandService` entgegen, abonniert `PromptVersendet`
- [x] Event-Handler `OnPromptVersendet` — gefiltert auf `_aufgabeId`, via Dispatcher → Status null + CLI-Ansicht
- [x] `Dispose` — meldet Handler ab + ruft `CancelScheduledPrompt`
- [x] `AufgabeAbschliessenAsync` — ruft `CancelScheduledPrompt`
- [x] `OnCliProcessStatusChanged` — räumt `ScheduledPromptStatus`/`ScheduledPromptTimeDisplay` bei CLI-Stopp

### DI / Konfiguration

- [x] `App.xaml.cs` — `services.AddSingleton(TimeProvider.System)` (Z. 192) und `services.AddSingleton<PromptZeitVersandService>()` (Z. 193)

### UI (`TaskDetailView.xaml`)

- [x] Zwei TextBoxen (Stunde/Minute), `AutomationProperties.Name` = `ScheduledPromptStunde` / `ScheduledPromptMinute`, gebunden an `ScheduledPromptTargetHours`/`Minutes` mit `UpdateSourceTrigger=PropertyChanged`, `IsEnabled` an `KannPromptVorlageSenden`
- [x] Button „Zeitgesteuert senden" (Icon ⏰), `AutomationName="ZeitgesteuertSenden"`, `Command` an `SchedulePromptCommand`
- [x] Statusanzeige `ScheduledPromptStatus` (+ `ScheduledPromptTimeDisplay`), sichtbar via `NullOrEmptyToVisibilityConverter`, `AutomationProperties.Name="ScheduledPromptStatus"`

### Testinfrastruktur

- [x] `TaskDetailViewModelTestFactory` — erzeugt und übergibt `PromptZeitVersandService`
- [x] `TaskDetailViewModelTests` (bestehende Setup-Klasse) — an neuen Konstruktor angepasst
- [x] `Softwareschmiede.Tests.csproj` — `PackageReference Microsoft.Extensions.TimeProvider.Testing` (Version 10.7.0)

### Tests (Plan-Soll → Umsetzung)

- [x] `SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort` — vorhanden
- [x] `SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt` — vorhanden
- [x] `Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch` — vorhanden (`FakeTimeProvider.Advance`)
- [x] `CancelScheduledPrompt_EntferntGeplantenPrompt` — vorhanden
- [x] `SchedulePromptAsync_ZweiterPromptFuerSelbeAufgabe_ErsetztErsten` — vorhanden
- [x] `Timer_OhneSession_VerwirftPromptStill` — vorhanden
- [x] `ScheduledPromptTargetHours_Binding_SetztProperty` — vorhanden
- [x] `SchedulePrompt_LeereFelder_KeinScheduling` — vorhanden
- [x] `SchedulePrompt_GueltigeZeit_RuftServiceAuf` — vorhanden
- [x] `SchedulePrompt_UngueltigeStunde_SetztFehlerMeldung` — vorhanden
- [x] `Dispose_StorniertGeplantePrompts` — vorhanden
- [x] E2E `ZeitgesteuerterPrompt_NachPlanen_ZeigtWartestellungStatus_E2E` — vorhanden

## Offene Aufgaben

Keine. Alle Planelemente sind im Code auffindbar und vollständig umgesetzt.

## Hinweise

- **Testklassen-Aufteilung:** Die im Plan als Erweiterung von `TaskDetailViewModelTests` beschriebenen ViewModel-Tests wurden in einer eigenen, fokussierten Klasse `TaskDetailViewModelTests_ZeitgesteuerterPrompt` umgesetzt (statt in die bestehende `TaskDetailViewModelTests` eingemischt). Inhaltlich alle geforderten Testfälle vorhanden; die separate Datei entspricht der projektüblichen Test-Class-Structure-Konvention und ist keine Lücke.
- **UI-Button-Konvention:** Der Button nutzt das projekteigene Control `controls:RibbonLargeButton` mit `ButtonCommand`/`AutomationName` statt eines rohen `Button` mit `Command`/`AutomationProperties.Name`. Funktional identisch und im Repo etabliert.
- **Zielzeit-Berechnung:** `SchedulePromptAsync` im ViewModel berechnet `TargetTime` aus `DateTime.Now` mit `new DateTimeOffset(new DateTime(...))` (lokale Zeit, plankonform). Der Service arbeitet intern mit `TimeProvider.GetUtcNow()`; der Vergleich `targetTime <= now` ist über `DateTimeOffset` zeitzonensicher.
- **Verifikationsstand:** Dieses Review ist eine statische Prüfung (Existenz und Zuordnung von Plan-Elementen zu Code/Tests). Ein vollständiger Build- und Testlauf zur Bestätigung, dass die Tests tatsächlich grün laufen, wurde im Rahmen des Plan-Reviews bewusst nicht durchgeführt (Sandbox-Regeln zu Build-Lock und Self-Hosting) und ist der nächste sinnvolle Schritt vor dem Merge.
