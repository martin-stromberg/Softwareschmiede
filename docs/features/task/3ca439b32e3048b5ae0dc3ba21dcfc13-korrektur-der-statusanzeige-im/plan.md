# Umsetzungsplan - Korrektur der Statusanzeige im Menue

## Zielbild

Die aktive Aufgabenliste im Programmmenue zeigt fuer sichtbare Aufgaben zeitnah denselben fachlichen CLI-Laufzustand wie die Detailansicht/Fusszeile. Wenn fuer eine Aufgabe ein aktiver CLI-Lauf persistiert ist oder gerade persistiert wurde, darf die Menuekachel nicht mehr auf alten Panel-Daten stehen bleiben und faelschlich "Bereit" anzeigen.

Die bestehenden Kurztexte im Menue bleiben erhalten:

- aktiver Lauf mit `LaufStatus.Laeuft` oder ohne Substatus: `Laeuft`
- aktiver Lauf mit `LaufStatus.WartetAufEingabe`: `Wartet`
- kein aktiver Lauf und Aufgabe nicht wartend: `Bereit`

## Leitentscheidung

Die Statusableitung im `KiAusfuehrungsStatusConverter` wird nicht neu erfunden. Der Converter wertet bereits `AktiveRunId`, `LastHeartbeatUtc` und `LaufStatus` fuer `AktiveAufgabePanelItem` aus. Die Korrektur soll deshalb die Aktualisierung der sichtbaren `AktiveAufgabenListe` nach persistierten Laufdaten absichern.

Der Refresh darf nicht allein von `RunningCountChanged` abhaengen, weil dieses Event vor der asynchronen Persistenz eintreffen kann und Runtime-Statuswechsel innerhalb eines laufenden Prozesses die Anzahl laufender Automatisierungen nicht aendern.

## Umsetzungsschritte

### 1. Benachrichtigung fuer persistierte Laufdaten einfuehren

Fuehre in der Application-Schicht einen kleinen, UI-neutralen Notifier fuer geaenderte Aufgaben-Laufdaten ein, z. B.:

- Datei: `src/Softwareschmiede/Application/Services/AufgabeLaufdatenChangedNotifier.cs`
- Interface oder Klasse mit Event: `Action<Guid>? LaufdatenChanged`
- Methode: `NotifyLaufdatenChanged(Guid aufgabeId)`

Der Notifier muss als Singleton im DI-Container registriert werden, damit `CliProcessManager` und `MainWindowViewModel` dieselbe Instanz verwenden.

Keine Domain-Abhaengigkeit auf WPF oder ViewModels einfuehren.

### 2. Notifier nach erfolgreicher Persistenz ausloesen

Erweitere `CliProcessManager`, sodass nach erfolgreich persistierten Laufdaten ein Event fuer die betroffene Aufgabe ausgeloest wird:

- nach `AufgabeService.AktivenLaufSetzenAsync(...)`
- nach `AufgabeService.AktualisiereLaufStatusAsync(...)`
- nach `AufgabeService.AktivenLaufBeendenAsync(...)`

Wichtig: Das Event erst nach dem erfolgreichen `SaveChangesAsync`-Pfad ausloesen. So liest das Menue beim anschliessenden Refresh keine alten Daten.

Fehlerfall: Wenn die Persistenz fehlschlaegt, wird kein Laufdaten-Event gemeldet; bestehendes Logging und bestehende Fehlerbehandlung bleiben massgeblich.

### 3. MainWindowViewModel auf Laufdaten-Events reagieren lassen

Erweitere `MainWindowViewModel` um die Notifier-Abhaengigkeit und abonniere das neue Event im Konstruktor.

Bei `LaufdatenChanged`:

- per vorhandenem `_dispatcherInvoke` auf den UI-Kontext wechseln
- `AktiveAufgabenImHintergrundAktualisieren()` aufrufen
- den bestehenden `_refreshGate`-Schutz unveraendert nutzen

In `Dispose()` muss das Event wieder abgemeldet werden.

Der bestehende 5-Sekunden-`DispatcherTimer` bleibt als Fallback erhalten. Das bestehende `RunningCountChanged` bleibt ebenfalls erhalten, weil es andere UI-Aktualisierungen und Start/Stop-Pfade bereits bedient.

### 4. Mapping pruefen und explizit absichern

Pruefe `MainWindowViewModel.MapAktiveAufgabePanelItem(...)` und stelle sicher, dass folgende Werte aus `Aufgabe` in `AktiveAufgabePanelItem` uebernommen werden:

- `Status`
- `AktiveRunId`
- `LastHeartbeatUtc`
- `LaufStatus`
- `LetzterCliStartUtc`
- `HasScheduledPrompt`

Falls einer dieser Werte fehlt, ergaenzen. Falls alle vorhanden sind, keine unnoetige Codeaenderung vornehmen und die Absicherung nur ueber Tests dokumentieren.

### 5. Converter-Test fuer Wartestatus im Panel ergaenzen

Ergaenze `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs` um einen Test fuer:

- `AktiveAufgabePanelItem`
- frische `AktiveRunId`/`LastHeartbeatUtc`
- `LaufStatus = AufgabeLaufStatus.WartetAufEingabe`
- erwarteter Status: `Wartet`

Damit ist abgesichert, dass die Menuekachel auch den Substatus korrekt darstellt, sobald die Liste aktualisiert wurde.

### 6. MainWindowViewModel-Test fuer sichtbare Kachel nach Laufdatenwechsel ergaenzen

Ergaenze `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs` um einen gezielten Test:

1. Aufgabe anlegen/starten, aber ohne aktive Run-Daten in der initialen Menue-Liste anzeigen lassen.
2. `sut.AktiveAufgabenAktualisierenAsync()` aufrufen und bestaetigen, dass das Panel-Item noch keine aktive Run-ID hat bzw. der Converter `Bereit` liefern wuerde.
3. Laufdaten ueber `AufgabeService.AktivenLaufSetzenAsync(...)` oder einen Fake-Service aktualisieren.
4. Neues `LaufdatenChanged`-Event fuer die Aufgabe ausloesen.
5. Warten, bis `AktiveAufgabenListe` aktualisiert wurde.
6. Bestaetigen, dass das Panel-Item nun `AktiveRunId`, frischen Heartbeat und `LaufStatus.Laeuft` hat und der Converter nicht mehr `Bereit`, sondern `Laeuft` liefert.

Der Test soll ohne echten Timer und ohne echte CLI-Prozesse laufen. Bestehende Testmuster mit `Moq.Raise`, direktem Dispatcher-Delegate `action => action()` und kurzer Wait-Schleife koennen wiederverwendet werden.

### 7. Test fuer Abmeldung in Dispose ergaenzen oder erweitern

Erweitere den vorhandenen Dispose-Test oder fuege einen neuen Test hinzu:

- Nach `sut.Dispose()` darf ein `LaufdatenChanged`-Event keinen Refresh mehr ausloesen und keine Exception werfen.

Das ist wichtig, weil `MainWindowViewModel` langlebige Singleton-/Service-Events abonniert.

### 8. Bestehende Tests ausfuehren

Fuehre mindestens aus:

```powershell
dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj --filter "FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~KiAusfuehrungsStatusConverterTests"
```

Wenn die Filter-Syntax in der Umgebung nicht greift, ersatzweise:

```powershell
dotnet test src\Softwareschmiede.Tests\Softwareschmiede.Tests.csproj
```

## Akzeptanzkriterien

- Eine bereits sichtbare Aufgabe im Programmmenue wechselt zeitnah von `Bereit` auf `Laeuft`, sobald ein aktiver CLI-Lauf fuer diese Aufgabe persistiert wurde.
- Ein Runtime-Wechsel zwischen `Laeuft` und `WartetAufEingabe` aktualisiert die sichtbare Menuekachel ohne Abwarten auf den 5-Sekunden-Timer.
- Die Detailansicht/Fusszeile und das Programmmenue widersprechen sich fuer aktive CLI-Ausfuehrungen nicht mehr fachlich.
- Tatsaechlich bereite Aufgaben bleiben weiterhin als `Bereit` sichtbar.
- Bestehende Start/Stop-, Timer- und Navigation-Refreshpfade bleiben erhalten.

## Nicht umsetzen

- Keine neuen Statusbegriffe im Menue einfuehren.
- Keine Aenderung an der CLI-Prozesssteuerung oder am Runtime-Evaluator.
- Keine Umgestaltung von `ActiveTasksListControl.xaml`.
- Keine Verkuerzung des globalen 5-Sekunden-Timers als alleinige Loesung.
- Keine direkte Kopplung der WPF-App an `PseudoConsoleSession`.

## Risiken und Gegenmassnahmen

| Risiko | Gegenmassnahme |
|--------|----------------|
| Event wird vor Persistenz ausgeloest und das Menue liest alte Daten. | Event nur nach erfolgreichem Persistenzaufruf senden. |
| Zusaetzliche Events fuehren zu ueberlappenden Refreshes. | Bestehenden `_refreshGate` in `MainWindowViewModel` weiterverwenden. |
| ViewModel bleibt nach Fenster-Schliessung am Event haengen. | In `Dispose()` sauber abmelden und per Test absichern. |
| Runtime-Statuswechsel spammt Refreshes. | Nur bei tatsaechlichen Runtime-Statuswechseln aus `CliProcessManager` melden; keine Heartbeat-Timer-Ticks als UI-Event verwenden. |
| Menue zeigt alten Status, obwohl Daten korrekt persistiert sind. | `MainWindowViewModel`-Test beobachtet genau den sichtbaren Vorher/Nachher-Fall. |

## Offene Punkte

Keine.
