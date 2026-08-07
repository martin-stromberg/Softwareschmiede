# Tests und Abdeckung

## Converter-Tests

`KiAusfuehrungsStatusConverterTests` deckt ab:

- aktive Run-ID und frischer Heartbeat -> "▶ Läuft"
- `AufgabeStatus.Wartend` -> "⏸ Wartet"
- aktiver Lauf plus `LaufStatus.WartetAufEingabe` -> "⏸ Wartet"
- aktiver Lauf plus `LaufStatus.Laeuft` oder `null` -> "▶ Läuft"
- abgelaufener Heartbeat -> "✓ Bereit"
- `AktiveAufgabePanelItem` mit aktivem Lauf -> "▶ Läuft"

Eine explizite Assertion fuer `AktiveAufgabePanelItem` plus `LaufStatus.WartetAufEingabe` ist nicht sichtbar. Fuer den konkreten Fehler "sichtbares Menue-Item zeigt Bereit trotz laufender CLI" prueft der Converter nur die reine Ableitung, nicht die Aktualisierung eines vorhandenen Items.

## MainWindowViewModel-Tests

`MainWindowViewModelTests` deckt ab:

- `AktiveAufgabenAktualisierenAsync` befuellt die aktive Aufgabenliste
- Todo-Anzahlen werden gemappt
- Navigation zur Aufgabe erzeugt ein `TaskDetailViewModel`
- `CurrentView`-Wechsel loest einen Hintergrund-Refresh aus
- Dashboard und MainWindow teilen dieselbe aktive Aufgabenliste
- aktive Markierung (`IsAktiv`) wandert bei Navigation
- `RunningCountChanged` laedt die aktive Aufgabenliste neu
- Re-Entrancy-Schutz verhindert parallele Refreshes

Nicht erkennbar abgedeckt:

- `AktiveAufgabenAktualisierenAsync` mappt `AktiveRunId`, `LastHeartbeatUtc` und `LaufStatus` explizit korrekt in `AktiveAufgabePanelItem`.
- Eine bereits sichtbare Menuekachel wechselt von "Bereit" auf "Laeuft", wenn nur die Laufdaten in der Datenbank aktualisiert wurden.
- Runtime-Substatuswechsel ohne Running-Count-Aenderung triggert einen zeitnahen Menue-Refresh.

## Service-Tests aktiver Lauf

`AufgabeServiceTests_AktiverLauf` deckt die Persistenzlogik ab:

- `AktivenLaufSetzenAsync` setzt `AktiveRunId`, Heartbeat, `LetzterCliStartUtc` und `LaufStatus.Laeuft`
- `AktivenLaufBeendenAsync` entfernt `AktiveRunId` und `LaufStatus`
- `AktualisiereLaufStatusAsync` setzt `WartetAufEingabe`, solange ein aktiver Lauf existiert
- spaete Laufstatusupdates ohne aktive Run-ID werden ignoriert
- voller Zyklus Bereit -> Laeuft -> Wartet -> Bereit

`CliProcessManagerTests_AktiverLauf` deckt Start/Stopp/Fehler aus dem Prozessmanager ab:

- `Gestartet` setzt sofort aktive Run-Daten
- `Gestoppt` und `Fehler` entfernen aktive Run-Daten

`CliProcessManagerTests_LaufStatus` deckt die Runtime-Subscription ab:

- `PseudoConsoleSession.RuntimeStatusChanged` wird in `Aufgabe.LaufStatus` persistiert
- nach Stopp werden spaete Runtime-Events nicht mehr persistiert

## Runtime-Evaluator-Tests

`CliRuntimeStatusEvaluatorTests` prueft:

- beendeter Prozess -> `Inaktiv`
- frische Ausgabe -> `Laeuft`
- stale Aktivitaet -> `WartetAufEingabe`
- frische Eingabe -> `Laeuft`

Damit ist die technische Ableitung "arbeitet vs. wartet" isoliert abgesichert.

## E2E-Tests

`E2E_ArbeitsstatusAktualisierung` prueft einen echten Start-/Stopp-Pfad mit dem KiSimulator:

- nach CLI-Start erscheint die Seitenleistenkachel mit "▶ Läuft"
- nach CLI-Stopp wechselt sie auf "✓ Bereit"

Der Test dokumentiert den frueheren Issue-108-Fall, bei dem die Fusszeile korrekt war, die Seitenleiste aber wegen fehlender `AktiveRunId` "Bereit" blieb.

`E2E_TaskWechselUeberMenue` prueft den Wechsel zwischen aktiven Aufgaben ueber die Seitenleiste und stellt sicher, dass die Detailansicht inklusive Terminal-Session zur gewaehlten Aufgabe passt.

## Testluecke fuer die aktuelle Anforderung

Die aktuelle Anforderung fokussiert darauf, dass im Menue bei aktiver CLI-Ausfuehrung nicht "Bereit" stehen darf, auch wenn die Aufgabe bereits im Menue sichtbar war, bevor ihr Laufstatus aktualisiert wurde. Dafuer fehlt nach Bestandsaufnahme ein gezielter Unit-Test oder E2E-Test, der eine vorhandene `AktiveAufgabePanelItem`-Anzeige mit initial alten Laufdaten beobachtet und danach einen Laufdaten-/Runtime-Statuswechsel ohne Navigation und ohne manuelles Neuladen erzwingt.

Empfohlene Absicherung:

- Unit-Test im `MainWindowViewModelTests`, der nach initialem Refresh eine Aufgabe mit altem/fehlendem Laufstatus sichtbar macht, dann `AktivenLaufSetzenAsync` oder `AktualisiereLaufStatusAsync` ausfuehrt und einen vorgesehenen Refresh-/Eventpfad ausloest.
- Converter-Test fuer `AktiveAufgabePanelItem` mit `LaufStatus.WartetAufEingabe`, falls die Planung auch Wartestatus im Menue beruehrt.
- Optional E2E-Erweiterung des Arbeitsstatus-Tests fuer "bereits sichtbare Kachel startet/arbeitet weiter".
