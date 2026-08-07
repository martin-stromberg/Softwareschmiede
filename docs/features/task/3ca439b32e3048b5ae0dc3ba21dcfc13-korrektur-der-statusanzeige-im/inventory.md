# Bestandsaufnahme - Korrektur der Statusanzeige im Menue

## Zusammenfassung

Die Statusanzeige im Programmmenue wird ueber die gemeinsame aktive Aufgabenliste des `MainWindowViewModel` gerendert. Die Kachel bindet an `AktiveAufgabePanelItem` und nutzt denselben `KiAusfuehrungsStatusConverter`, der auch `Aufgabe`-Instanzen verarbeiten kann. Die fachliche Statusquelle fuer "Laeuft" ist nicht der grobe `AufgabeStatus`, sondern die Kombination aus `AktiveRunId`, aktuellem `LastHeartbeatUtc` und optionalem `LaufStatus`.

Die Fusszeile der Detailansicht nutzt dagegen direkt den lokalen `PseudoConsoleSession.RuntimeStatus` der laufenden CLI-Sitzung. Diese Quelle ist unmittelbarer als die Menueanzeige, die erst nach Persistenz und Refresh der aktiven Aufgabenliste den geaenderten Runtime-Status sieht.

Der naheliegende Risikobereich fuer die Anforderung ist deshalb nicht die reine Textableitung im Converter, sondern die Aktualisierung sichtbarer Menueeintraege bei einer Laufstatus-Aenderung, die die Anzahl laufender Automatisierungen nicht veraendert.

## Detaildokumente

- [Menue und Navigation](inventory/menue-und-navigation.md)
- [Laufstatus und Runtime-Status](inventory/laufstatus-und-runtime-status.md)
- [Tests und Abdeckung](inventory/tests-und-abdeckung.md)

## Relevante Dateien

| Bereich | Dateien |
|---------|---------|
| Menue/Navigation | `src/Softwareschmiede.App/Views/MainWindow.xaml`, `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml`, `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs`, `src/Softwareschmiede.App/ViewModels/DashboardViewModel.cs`, `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs` |
| Statusableitung UI | `src/Softwareschmiede.App/Converters/AppConverters.cs` |
| Detailansicht/Fusszeile | `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` |
| Persistenter Aufgaben-/Laufstatus | `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`, `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`, `src/Softwareschmiede/Application/Services/AufgabeLaufAktivitaet.cs`, `src/Softwareschmiede/Application/Services/AufgabeService.cs` |
| Runtime-Status | `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`, `src/Softwareschmiede/Application/Services/CliProcessManager.cs`, `src/Softwareschmiede/Infrastructure/Terminal/PseudoConsoleSession.cs`, `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs` |
| Tests | `src/Softwareschmiede.Tests/App/Converters/KiAusfuehrungsStatusConverterTests.cs`, `src/Softwareschmiede.Tests/App/ViewModels/MainWindowViewModelTests.cs`, `src/Softwareschmiede.Tests/Application/Services/AufgabeServiceTests_AktiverLauf.cs`, `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_AktiverLauf.cs`, `src/Softwareschmiede.Tests/Application/Services/CliProcessManagerTests_LaufStatus.cs`, `src/Softwareschmiede.Tests/Infrastructure/Terminal/CliRuntimeStatusEvaluatorTests.cs`, `src/Softwareschmiede.Tests/E2E/E2E_ArbeitsstatusAktualisierung.cs`, `src/Softwareschmiede.Tests/E2E/E2E_TaskWechselUeberMenue.cs` |

## Fachlicher Statusfluss

1. CLI startet ueber `KiAusfuehrungsService`; der Service loest `RunningCountChanged` und `CliProcessStatusChanged(Gestartet)` aus.
2. `CliProcessManager` reagiert auf `Gestartet`, setzt Heartbeat, persistiert `AktiveRunId`, `LastHeartbeatUtc`, `LetzterCliStartUtc` und initial `LaufStatus = Laeuft`.
3. `MainWindowViewModel` reagiert auf `RunningCountChanged` und aktualisiert die `AktiveAufgabenListe` ueber `GetAktiveAufgabenAsync`.
4. `ActiveTasksListControl` zeigt den Status mit `KiAusfuehrungsStatusConverter` an.
5. Die Detailansicht bindet die Fusszeile an `TaskDetailViewModel.CliStatusText`, das direkt aus `PseudoConsoleSession.RuntimeStatus` aktualisiert wird.
6. Laufende Runtime-Wechsel `Laeuft`/`WartetAufEingabe` werden durch `CliProcessManager` persistiert, loesen aber nach aktuellem Befund kein eigenes Refresh-Event fuer `MainWindowViewModel` aus.

## Risiken und Implementierungshinweise

- `AktiveAufgabePanelItem` besitzt fuer Statusdaten `init`-Properties. Ein bereits sichtbarer Eintrag kann daher nicht gezielt per Property-Change auf neuen `LaufStatus`, `AktiveRunId` oder `LastHeartbeatUtc` aktualisiert werden; die Liste wird aktuell komplett ersetzt.
- `MainWindowViewModel` aktualisiert event-getrieben nur bei `RunningCountChanged` und ansonsten per 5-Sekunden-`DispatcherTimer`. Ein reiner Runtime-Statuswechsel innerhalb eines laufenden Prozesses veraendert die Running-Count-Zahl nicht.
- Der Converter ist bereits in der Lage, aus einem `AktiveAufgabePanelItem` mit aktiver Run-ID und aktuellem Heartbeat "Laeuft" statt "Bereit" abzuleiten. Wenn im Menue trotzdem "Bereit" erscheint, ist vorrangig zu pruefen, ob die Panel-Items rechtzeitig mit aktuellen Laufdaten ersetzt oder aktualisiert werden.
- Fuer die Anforderung sollte die Menueanzeige dieselbe fachliche Statusquelle nutzen wie bisher der Converter, aber die Aktualisierung sichtbarer Eintraege bei Laufstatus-/Heartbeat-Aenderungen explizit abgesichert werden.

## Offene Punkte fuer die Planung

- Soll die Menueanzeige weiterhin kurze Texte wie "▶ Läuft" zeigen oder exakt den Fusszeilentext "CLI-Status: Ausführung läuft"? Die bestehende UI verwendet bewusst Kurzstatus.
- Soll fuer Runtime-Statuswechsel ein neues Event eingefuehrt werden, oder reicht eine gezielte Aktualisierung der bestehenden `AktiveAufgabenListe` ueber den bestehenden `CliProcessStatusChanged`/Timer-Pfad mit reduziertem Intervall nicht aus?
