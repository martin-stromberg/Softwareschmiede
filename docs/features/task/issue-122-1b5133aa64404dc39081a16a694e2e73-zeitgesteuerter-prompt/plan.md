# Umsetzungsplan: Zeitgesteuerter Prompt

## Übersicht

Die Promptvorlagen-Auswahl im CLI-Ribbon der Aufgabendetailansicht wird um eine zeitverzögerte Versendung erweitert. Zwei Eingabefelder (Stunde/Minute) plus ein „Zeitgesteuert senden"-Button erlauben es, einen aufgelösten Prompt bis zu einer Zieluhrzeit in einer Laufzeit-Warteschlange zu puffern und dann automatisch an die laufende CLI-Session zu schreiben. Betroffen sind ein neuer `PromptZeitVersandService`, das `TaskDetailViewModel`, `PseudoConsoleSession`, die `TaskDetailView.xaml` sowie die DI-Registrierung und Testinfrastruktur. Es gibt keine Persistierung — die Verzögerung ist rein sitzungsgebunden.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `PromptZeitVersandService` | Service Layer, Singleton, mit `Dictionary<Guid, ScheduledPromptInfo>` und **pro Eintrag einem `ITimer`** (`TimeProvider.CreateTimer`) statt globalem 100-ms-Polling | Ereignisgesteuertes Feuern ist ressourcenschonender und deterministischer als eine Polling-Schleife; die vom Projekt bereits genutzte `TimeProvider`-Abstraktion (siehe `PseudoConsoleSession`) macht den Timer im Test austauschbar. |
| Trigger-Aufteilung (Sofort vs. Zeitgesteuert) | ComboBox-Auswahl sendet **sofort nur, wenn beide Zeitfelder leer sind** (bestehendes Verhalten). Sind Zeitfelder befüllt, erfolgt kein Sofortversand; der Versand wird ausschließlich über den neuen Button `SchedulePromptCommand` geplant. | Vermeidet den Konflikt „Auswahl sendet sofort, bevor der Nutzer eine Zielzeit bestätigen kann". Hält die bestehende Sofort-Versand-UX (und deren Tests) unverändert. |
| Prompt-Schreiblogik | Extraktion einer Methode `PseudoConsoleSession.WritePromptAsync(prompt, ct)`, die Encoding, `WriteAsync`, `FlushAsync` und `MarkInputActivity` kapselt; sowohl der Sofort-Pfad im ViewModel als auch der zeitgesteuerte Pfad im Service rufen sie auf | Entfernt die sonst doppelte Byte-/Stream-Logik (DRY); der Versandvorgang wird zum Verhalten der Session (Domain-nahes Value/Behavior statt Transaction-Script-Duplikat). |
| Platzhalterauflösung | Erfolgt weiterhin **vor** dem Scheduling im `TaskDetailViewModel` (benötigt `_aufgabe`); der Service erhält bereits aufgelösten `promptText` | `PromptVorlagenPlatzhalterService.Resolve` braucht die geladene `Aufgabe`, die im Singleton-Service nicht vorliegt. Der Service bleibt aufgaben-/UI-unabhängig. |
| Vergangene Zielzeit | Option A: sofort versenden (kein Fehler) | Entspricht der Empfehlung der Anforderung und ist für den Nutzer erwartungskonform (Zeit „schon vorbei" = jetzt). |
| Zeitzone | Lokale Zeit über `DateTime.Now`/`DateTimeOffset.Now` | Empfehlung der Anforderung; Eingabefelder repräsentieren die Wanduhrzeit des Nutzers. |
| Persistierung | Keine — nur Laufzeit/Session | Anforderung und Bestandsaufnahme (`models.md`) stellen ausdrücklich fest, dass die Verzögerung nicht persistiert wird. |
| Parallelität | Maximal **ein** geplanter Prompt pro Aufgabe; erneutes Planen ersetzt den vorhandenen Eintrag (alter Timer wird abgebrochen) | Empfehlung der Anforderung; vermeidet Verwirrung durch mehrere gepufferte Prompts. |
| `ScheduledPromptInfo` | Unveränderliches `record` (Value Object) mit `AufgabeId`, `PromptText`, `TargetTime` | Reiner Datencontainer ohne Verhalten; Records sind projektüblich für solche Werte. |
| UI-Feedback nach dem Planen | **Keine** zusätzliche Toast-/Statusbar-Meldung; das Feedback beschränkt sich auf die `ScheduledPromptStatus`-Anzeige („Prompt in Wartestellung") inkl. `ScheduledPromptTimeDisplay` (HH:mm) | Geklärte Vorgabe: die vorhandene Statusanzeige inkl. Zielzeit ist als Rückmeldung ausreichend; kein zusätzlicher UI-Kanal nötig. |
| Verhalten bei Versandfehler zur Zielzeit (Session zwischenzeitlich beendet) | Prompt wird **still verworfen**: kein `PromptVersandFehlgeschlagen`-Event, keine `FehlerMeldung`, kein UI-sichtbarer Hinweis — lediglich eine technische Log-Warnung. Der Dictionary-Eintrag wird entfernt und der Timer disposed. | Geklärte Vorgabe: ein zur Zielzeit nicht mehr zustellbarer Prompt soll den Nutzer nicht stören; die Log-Warnung genügt für die Diagnose. |
| Deterministische Zeit im Timer-Unit-Test | Test-NuGet `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`); der Service erzeugt seinen Timer ausschließlich über `TimeProvider.CreateTimer`, sodass `FakeTimeProvider.Advance(...)` das Fälligwerden deterministisch und ohne reale Wartezeit auslöst | Geklärte Vorgabe: schnelle, deterministische Tests statt realer Wartezeiten/Poll-Assertions. |

## Programmabläufe

### Sofortiger Versand (unverändert, backward-compatible)

1. Nutzer wählt eine Vorlage in der ComboBox → Setter von `SelectedPromptVorlage` löst `PromptVorlageAuswaehlenCommand` aus.
2. `PromptVorlageAuswaehlenAsync` prüft: Beide Zeitfelder (`ScheduledPromptTargetHours`, `ScheduledPromptTargetMinutes`) leer.
3. Session wird via `_kiService.GetPseudoConsoleSession` geholt; Prompt via `PromptVorlagenPlatzhalterService.Resolve` aufgelöst.
4. `PseudoConsoleSession.WritePromptAsync` schreibt den Prompt; Ansicht wechselt zur CLI; `SelectedPromptVorlage` wird zurückgesetzt; `PromptVorlageGesendet` wird gefeuert.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `PromptVorlagenPlatzhalterService`, `KiAusfuehrungsService`, `PseudoConsoleSession`.

### Zeitgesteuerter Versand planen

1. Nutzer trägt Stunde und/oder Minute in die Zeitfelder ein und wählt eine Vorlage; die ComboBox-Auswahl sendet **nicht** sofort (Zeitfelder befüllt).
2. Nutzer klickt den Button „Zeitgesteuert senden" → `SchedulePromptCommand` ruft `SchedulePromptAsync` (privat im ViewModel) auf.
3. Validierung der Zeit (Stunde 0–23, Minute 0–59). Bei Ungültigkeit: `FehlerMeldung` setzen, Abbruch.
4. `TargetTime` wird aus heutigem Datum + eingegebener Uhrzeit (lokal) berechnet.
5. Prompt via `PromptVorlagenPlatzhalterService.Resolve(_aufgabe)` aufgelöst.
6. `_promptZeitVersandService.SchedulePromptAsync(aufgabeId, promptText, targetTime)` wird aufgerufen.
   - Liegt `targetTime` in der Vergangenheit/jetzt → Service sendet sofort (siehe nächster Ablauf, ohne Timer).
   - Sonst legt der Service `ScheduledPromptInfo` im Dictionary ab (ersetzt evtl. vorhandenen Eintrag, bricht dessen Timer ab) und startet einen `ITimer` mit Restlaufzeit.
7. ViewModel setzt `ScheduledPromptStatus = "Prompt in Wartestellung"`, aktualisiert `ScheduledPromptTimeDisplay`, leert die Zeitfelder und setzt `SelectedPromptVorlage` zurück.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `PromptZeitVersandService`, `PromptVorlagenPlatzhalterService`.

### Automatischer Versand bei Erreichen der Zielzeit

1. Der `ITimer` des Eintrags feuert → interner Callback `AufTimerFaelligAsync` des `PromptZeitVersandService`.
2. Service holt die aktive Session via `_kiService.GetPseudoConsoleSession(aufgabeId)`.
   - Session vorhanden → `PseudoConsoleSession.WritePromptAsync(promptText, ct)`; anschließend `PromptVersendet(aufgabeId)`.
   - Session `null` (CLI zwischenzeitlich beendet) → Versand wird **still verworfen**: nur Log-Warnung, **kein** Event, **keine** `FehlerMeldung`.
3. Eintrag wird in beiden Fällen aus dem Dictionary entfernt, Timer disposed.
4. Nur im Erfolgsfall: das abonnierende `TaskDetailViewModel` (Filter auf `_aufgabeId`) setzt `ScheduledPromptStatus = null` über den Dispatcher und wechselt zur CLI-Ansicht.
5. Im Verwerfen-Fall gibt es keinen Event-basierten Statuswechsel; eine ggf. noch angezeigte „Wartestellung" wird spätestens beim CLI-Stopp (`IsCliRunning` → false in `OnCliProcessStatusChanged`) geräumt.

Beteiligte Klassen/Komponenten: `PromptZeitVersandService`, `KiAusfuehrungsService`, `PseudoConsoleSession`, `TaskDetailViewModel`.

### Stornierung (Ansichtswechsel / Dispose / Aufgabenabschluss)

1. `TaskDetailViewModel.Dispose` (bzw. beim Wechsel der `AufgabeId` und beim Abschließen der Aufgabe) ruft `_promptZeitVersandService.CancelScheduledPrompt(_aufgabeId)`.
2. Service entfernt den Dictionary-Eintrag, bricht den Timer ab und disposed ihn.
3. ViewModel entfernt seine Event-Abonnements vom Service und setzt `ScheduledPromptStatus = null`.

Beteiligte Klassen/Komponenten: `TaskDetailViewModel`, `PromptZeitVersandService`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PromptZeitVersandService` | Service (Service Layer, Singleton) | Verwaltet die Laufzeit-Warteschlange zeitgesteuerter Prompts pro Aufgabe, feuert bei Zielzeit den Versand an die Session. |
| `ScheduledPromptInfo` | `record` (Value Object) | Container für `AufgabeId` (Guid), `PromptText` (string), `TargetTime` (DateTimeOffset). |
| `PromptZeitVersandServiceTests` | Testklasse | Unit-Tests für den neuen Service. |
| `E2E_ZeitgesteuerterPrompt` | E2E-Testklasse | Deckt den Happy-Path der neuen UI-Interaktion ab. |

## Änderungen an bestehenden Klassen

### `PseudoConsoleSession` (Infrastructure/Terminal)

- **Neue Methoden:** `WritePromptAsync(string prompt, CancellationToken ct)` — kapselt `Encoding.UTF8.GetBytes(prompt + Environment.NewLine)`, `InputStream.WriteAsync`, `InputStream.FlushAsync` und `MarkInputActivity`. Rückgabe `Task`. Wird von ViewModel (Sofort) und Service (zeitgesteuert) genutzt.

### `TaskDetailViewModel` (App/ViewModels)

- **Neue Eigenschaften:**
  - `ScheduledPromptTargetHours` (`int?`) — Bindung Stunde-Eingabefeld (null = leer).
  - `ScheduledPromptTargetMinutes` (`int?`) — Bindung Minute-Eingabefeld (null = leer).
  - `ScheduledPromptStatus` (`string?`) — Anzeigetext („Prompt in Wartestellung" oder null).
  - `ScheduledPromptTimeDisplay` (`string?`) — berechnete Zielzeit im Format `HH:mm`.
  - `CanSchedulePrompt` (`bool`) — true, wenn CLI läuft, eine Vorlage gewählt ist und eine gültige Zeit eingegeben wurde.
- **Neue Commands:** `SchedulePromptCommand` (`AsyncRelayCommand`) — plant den Versand der aktuell gewählten Vorlage; `CanExecute` = `CanSchedulePrompt`.
- **Geänderte Methoden:**
  - `PromptVorlageAuswaehlenAsync` — sendet nur noch sofort, wenn beide Zeitfelder leer sind; nutzt neu `PseudoConsoleSession.WritePromptAsync`.
  - `Dispose` — ruft zusätzlich `_promptZeitVersandService.CancelScheduledPrompt(_aufgabeId)` und meldet Event-Handler ab.
  - Konstruktor — nimmt zusätzlich `PromptZeitVersandService` entgegen und abonniert dessen Events.
  - `AufgabeAbschliessenAsync` — storniert vor/bei Abschluss den geplanten Prompt (`CancelScheduledPrompt`).
  - `OnCliProcessStatusChanged` — räumt `ScheduledPromptStatus` (setzt auf null), sobald die CLI nicht mehr läuft (`IsCliRunning` → false), damit eine still verworfene „Wartestellung" nicht stehen bleibt.
- **Neue private Methode:** `SchedulePromptAsync(CancellationToken)` — Validierung, Zielzeitberechnung, Platzhalterauflösung, Aufruf `SchedulePromptAsync` des Service, Statuspflege, Feldreset.
- **Neue Event-Handler:** Reaktion auf `PromptZeitVersandService.PromptVersendet` (gefiltert auf `_aufgabeId`, via `_dispatcherInvoke`) → `ScheduledPromptStatus = null` und Wechsel zur CLI-Ansicht. Es gibt **kein** Fehler-Event; der Verwerfen-Fall wird nicht abonniert.

### `App.xaml.cs` (DI-Registrierung)

- **Neue Registrierung:** `services.AddSingleton<PromptZeitVersandService>();` (Singleton, analog `KiAusfuehrungsService`). Abhängigkeiten: `KiAusfuehrungsService`, `TimeProvider` (bzw. `TimeProvider.System` als Default), `ILogger<PromptZeitVersandService>`. Falls `TimeProvider` nicht bereits registriert ist: `services.AddSingleton(TimeProvider.System);`.

### `TaskDetailView.xaml` (App/Views)

- **CLI-Ribbon-Gruppe:** Neben der `PromptVorlagen`-ComboBox zwei `TextBox`-Felder (Stunde/Minute) mit `AutomationProperties.Name` (z. B. `ScheduledPromptStunde`, `ScheduledPromptMinute`), gebunden an `ScheduledPromptTargetHours`/`ScheduledPromptTargetMinutes` (`UpdateSourceTrigger=PropertyChanged`), `IsEnabled="{Binding KannPromptVorlageSenden}"`.
- **Button:** „Zeitgesteuert senden" (Icon z. B. „⏰"), `AutomationName="ZeitgesteuertSenden"`, `Command="{Binding SchedulePromptCommand}"`.
- **Statusanzeige:** `TextBlock` gebunden an `ScheduledPromptStatus` (sichtbar via `NullOrEmptyToVisibilityConverter`), `AutomationProperties.Name="ScheduledPromptStatus"`; optional `ScheduledPromptTimeDisplay` daneben.

### `TaskDetailViewModelTestFactory` (Tests/Helpers)

- **Geändert:** Erzeugt und übergibt zusätzlich eine `PromptZeitVersandService`-Instanz an den ViewModel-Konstruktor.

## Datenbankmigrationen

Keine. Die zeitgesteuerte Versendung ist rein sitzungsgebunden; `PromptVorlage` bleibt unverändert.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ScheduledPromptTargetHours` | Ganzzahl 0–23 (wenn gesetzt) | `FehlerMeldung`: „Ungültige Stunde (0–23)", kein Versand |
| `ScheduledPromptTargetMinutes` | Ganzzahl 0–59 (wenn gesetzt) | `FehlerMeldung`: „Ungültige Minute (0–59)", kein Versand |
| Zeitangabe gesamt (Scheduling) | Mindestens eines der beiden Felder gesetzt; fehlendes Gegenstück wird als `0` interpretiert | Beide leer → kein Scheduling (Button `CanExecute` false); Sofortversand nur über ComboBox |
| Vorlagenauswahl (Scheduling) | `SelectedPromptVorlage` ≠ null und `Prompttext` nicht leer | Button `CanExecute` false |

## Konfigurationsänderungen

Keine. Durch pro-Eintrag-Timer entfällt ein Polling-Intervall-Setting; die Begrenzung auf einen Prompt pro Aufgabe ist als Invariante im Service fest verdrahtet.

## Seiteneffekte und Risiken

- **`PromptVorlageAuswaehlenAsync` (Sofort-Versand):** Verhalten ändert sich nur, wenn Zeitfelder befüllt sind; bei leeren Feldern identisch. Bestehende Tests (`LadenAsync_LaedtPromptVorlagenFuerAuswahl`, `PromptVorlageAuswaehlenCommand_OhneLaufendeSession_StuerztNichtAb`) bleiben grün, sofern der Konstruktor angepasst wird.
- **Konstruktor-Signatur `TaskDetailViewModel`:** Neuer Pflichtparameter bricht alle direkten Instanziierungen — betrifft `TaskDetailViewModelTestFactory`, `TaskDetailViewModelTests`-Setup und die DI-Registrierung. Müssen mitgezogen werden.
- **Singleton-Lebenszyklus:** `PromptZeitVersandService` überlebt die transienten ViewModels. Nicht stornierte Timer würden nach Verlassen der Ansicht weiterfeuern → Dispose/Abschluss-Stornierung ist Pflicht, sonst schreibt ein „verwaister" Prompt in eine evtl. fremde Session.
- **Thread-Sicherheit:** Timer-Callbacks laufen auf Thread-Pool-Threads; Dictionary-Zugriffe müssen per `lock` geschützt werden. UI-Statusänderungen im ViewModel nur über `_dispatcherInvoke`.
- **Self-Hosting/CLI:** Es wird ausschließlich auf `InputStream` geschrieben; kein Prozess wird beendet — kein Konflikt mit der Self-Hosting-Regel.

## Umsetzungsreihenfolge

1. **`ScheduledPromptInfo`-Record anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: Value-Object-Record mit `AufgabeId`, `PromptText`, `TargetTime` im Application-Services-Namespace.

2. **`PseudoConsoleSession.WritePromptAsync` extrahieren**
   - Voraussetzungen: Keine (bestehende `InputStream`/`MarkInputActivity`).
   - Beschreibung: Schreibmethode kapseln; bestehender Inline-Code im ViewModel wird in Schritt 5 darauf umgestellt.

3. **`PromptZeitVersandService` implementieren**
   - Voraussetzungen: `ScheduledPromptInfo` (Schritt 1), `PseudoConsoleSession.WritePromptAsync` (Schritt 2), `KiAusfuehrungsService.GetPseudoConsoleSession` (vorhanden), `TimeProvider` (vorhanden, .NET 8).
   - Beschreibung: Singleton-Service mit `SchedulePromptAsync`, `CancelScheduledPrompt`, `GetScheduledPromptStatus`, internem Dictionary + `ITimer` (ausschließlich über `TimeProvider.CreateTimer`, damit test-steuerbar), `lock`, **einem** Event `PromptVersendet` (Parameter: `Guid aufgabeId`). Vergangene Zielzeit → sofortiger Versand. Fehlende Session zur Fälligkeit → stilles Verwerfen mit Log-Warnung, kein Event.

4. **DI-Registrierung ergänzen**
   - Voraussetzungen: `PromptZeitVersandService` (Schritt 3).
   - Beschreibung: In `App.xaml.cs` `AddSingleton<PromptZeitVersandService>()` und ggf. `AddSingleton(TimeProvider.System)`.

5. **`TaskDetailViewModel` erweitern**
   - Voraussetzungen: `PromptZeitVersandService` (Schritt 3), `WritePromptAsync` (Schritt 2).
   - Beschreibung: Konstruktorparameter, neue Properties/Command, `SchedulePromptAsync`, geänderte `PromptVorlageAuswaehlenAsync`/`Dispose`/`AufgabeAbschliessenAsync`, Event-Abos.

6. **`TaskDetailViewModelTestFactory` und Test-Setup anpassen**
   - Voraussetzungen: geänderter Konstruktor (Schritt 5).
   - Beschreibung: `PromptZeitVersandService`-Instanz erzeugen und übergeben; `TaskDetailViewModelTests`-Konstruktor entsprechend.

7. **UI in `TaskDetailView.xaml` ergänzen**
   - Voraussetzungen: neue ViewModel-Properties/Command (Schritt 5).
   - Beschreibung: Zwei TextBoxen, Button, Statusanzeige in der CLI-Ribbon-Gruppe.

8. **Test-NuGet `Microsoft.Extensions.TimeProvider.Testing` hinzufügen**
   - Voraussetzungen: Keine.
   - Beschreibung: `PackageReference` in `Softwareschmiede.Tests.csproj` ergänzen (stellt `FakeTimeProvider` für deterministische Timer-Tests bereit).

9. **Unit-Tests schreiben**
   - Voraussetzungen: Service (Schritt 3), ViewModel-Erweiterungen (Schritt 5), angepasste Factory (Schritt 6), `FakeTimeProvider` (Schritt 8).
   - Beschreibung: `PromptZeitVersandServiceTests` (mit `FakeTimeProvider` + `Advance` für den Timer-Test) + neue `TaskDetailViewModelTests`.

10. **E2E-Test schreiben**
    - Voraussetzungen: UI (Schritt 7), lauffähiger App-Build.
    - Beschreibung: `E2E_ZeitgesteuerterPrompt` nach Muster bestehender ConPTY-E2E-Tests.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort` | `PromptZeitVersandServiceTests` | Vergangene Zielzeit → sofortiger Schreibvorgang auf die Session, kein persistenter Eintrag. |
| `SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt` | `PromptZeitVersandServiceTests` | Zukünftige Zielzeit → Eintrag bleibt in Warteschlange, Status verfügbar über `GetScheduledPromptStatus`. |
| `Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch` | `PromptZeitVersandServiceTests` | Zukünftige Zielzeit; `FakeTimeProvider.Advance` bis zur Zielzeit → `WritePromptAsync` wird aufgerufen und `PromptVersendet` gefeuert (deterministisch, ohne reale Wartezeit). |
| `CancelScheduledPrompt_EntferntGeplantenPrompt` | `PromptZeitVersandServiceTests` | Storno entfernt Eintrag; auch nach `Advance` über die Zielzeit hinaus erfolgt kein Versand. |
| `SchedulePromptAsync_ZweiterPromptFuerSelbeAufgabe_ErsetztErsten` | `PromptZeitVersandServiceTests` | Nur ein Prompt pro Aufgabe; erneutes Planen ersetzt und storniert den alten Timer. |
| `Timer_OhneSession_VerwirftPromptStill` | `PromptZeitVersandServiceTests` | Fehlende Session bei Fälligkeit → kein Event, keine Exception; Eintrag wird entfernt (stilles Verwerfen, nur Log-Warnung). |
| `ScheduledPromptTargetHours_Binding_SetztProperty` | `TaskDetailViewModelTests` | Stunde/Minute-Properties feuern `PropertyChanged` und speichern Werte. |
| `SchedulePrompt_LeereFelder_KeinScheduling` | `TaskDetailViewModelTests` | Leere Zeitfelder → `CanSchedulePrompt` false, ComboBox-Sofortversand unverändert. |
| `SchedulePrompt_GueltigeZeit_RuftServiceAuf` | `TaskDetailViewModelTests` | Gültige Zeit + gewählte Vorlage → `SchedulePromptAsync` des (gemockten/echten) Service wird aufgerufen, Status = „Prompt in Wartestellung". |
| `SchedulePrompt_UngueltigeStunde_SetztFehlerMeldung` | `TaskDetailViewModelTests` | Stunde 25 → `FehlerMeldung` gesetzt, kein Scheduling. |
| `Dispose_StorniertGeplantePrompts` | `TaskDetailViewModelTests` | `Dispose` ruft `CancelScheduledPrompt` für die Aufgabe. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TaskDetailViewModelTests` (Konstruktor/Setup) | Neuer Pflicht-Konstruktorparameter `PromptZeitVersandService`. |
| `TaskDetailViewModelTestFactory` | Muss den neuen Service erzeugen und übergeben. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Nutzer startet CLI, trägt Zielzeit ein, wählt Vorlage, klickt „Zeitgesteuert senden" → Status „Prompt in Wartestellung" erscheint, kein Fehlerbanner | `E2E_ZeitgesteuerterPrompt` | Zeitgesteuerte Planung inkl. Statusanzeige (Happy Path der neuen Interaktion). Der eigentliche automatische Versand bei Fälligkeit ist durch die Service-Unit-Tests deterministisch abgedeckt. |

Welche bestehenden E2E-Tests müssen angepasst werden? Keine.

## Offene Punkte

Keine. Alle zuvor offenen Punkte (UI-Feedback, Verhalten bei Versandfehler, deterministische Zeit im Timer-Test) sind geklärt und in den Plan eingearbeitet.
