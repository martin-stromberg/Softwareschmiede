# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- **„Aufgabe starten" mit Claude-CLI → Kachel zeigt denselben fachlichen Zustand wie die Fußzeile** → unauffällig. Die Kachel-Klassifikation (`KiAusfuehrungsStatusKlassifikation.Klassifiziere`, AppConverters.cs:179–184) wertet jetzt den Live-Substatus (`LiveLaufStatus`) vor den persistierten Laufdaten aus: Läuft nachweislich ein Prozess, zeigt die Kachel „▶ Läuft" bzw. „⏸ Wartet" — niemals „✓ Bereit" bei laufender Ausführung. Zusätzlich stellt `LaufdatenSicherstellenAsync` (CliProcessManager) die persistierten Laufdaten auf allen Pfaden nach (Wiederverwendungs-Early-Return via `CliLaufAktiv`, `RuntimeStatusChanged`, Heartbeat-Tick, 30-s-Abgleich). Kein Nutzereingriff nötig.
- **„Prompt absenden" → Status bleibt synchron** → unauffällig. Der bestehende `RuntimeStatusChanged`-Pfad ruft jetzt `LaufdatenSicherstellenAsync`, das auch bei fehlender `AktiveRunId` den Lauf-Zustand komplett persistiert statt nur den Substatus (vorher No-Op ohne `AktiveRunId`).
- **„CLI stoppen" → Erfolg: Kachel „✓ Bereit"** → unauffällig. Unveränderter Erfolgspfad (`IsCliRunning = false`, `AktivenLaufBeendenAsync`).
- **„CLI stoppen" schlägt fehl (Prozess läuft weiter) → verständliche Rückmeldung** → unauffällig. Neue Meldung „Der CLI-Prozess konnte nicht beendet werden und läuft weiter. Bitte erneut versuchen." (TaskDetailViewModel.cs:927) erscheint im bestehenden, gut sichtbaren Fehlerbanner (TaskDetailView.xaml:265–272, ErrorBrush). Der Stopp-Button bleibt auslösbar (`KannCliStoppen => _isCliRunning` bleibt `true`, Zeile 232 — `IsCliRunning = false` wird nur im Erfolgszweig gesetzt), die Aufforderung „erneut versuchen" ist also tatsächlich ausführbar. Kachel und Fußzeile zeigen weiterhin den real laufenden Prozess statt „Bereit".
- **Automatische Aktualisierung ohne manuelles Neuladen, in Seitenleiste und Dashboard** → unauffällig. Beide Darstellungsorte nutzen dieselbe `AktiveAufgabenListe`/denselben `ActiveTasksListControl`; `LiveLaufStatus` wird bei jedem Refresh neu gemappt (MainWindowViewModel.cs:403). Refresh-Auslöser: `LaufdatenChanged` (feuert jetzt nach jeder erfolgreichen Sicherstellung, CliProcessManager.cs:333), `RunningCountChanged` und der 5-s-`DispatcherTimer`-Fallback — im schlechtesten Fall also maximal wenige Sekunden Verzögerung, nie ein dauerhaft falscher Zustand.
- **CLI-Neustart bei wiederverwendeter Session** → unauffällig. `CliNeustartenCoordinatedAsync` meldet lebende Sessions über `NotifyLaufAktiv`, und der Neustart-Pfad im TaskDetailViewModel persistiert `AktivenLaufSicherstellenAsync` + `NotifyLaufdatenChanged` — gleiche Anzeige wie ein echter Start.

## Hinweise zur Prüfung

- Keine internen/technischen Kennungen erforderlich: Es wurde kein Eingabefeld hinzugefügt; alle Interaktionen laufen über die vorhandenen Bedienelemente (Kachel, Stopp-/Start-Buttons, Fehlerbanner).
- Keine Auswahl-/Suchaufgaben in der Anforderung enthalten.
- Etablierte Muster wiederverwendet: Fehlermeldung über das bestehende `FehlerMeldung`-Banner, Statusanzeige über die bestehende Converter-Pipeline; das Status-Vokabular („▶ Läuft", „⏸ Wartet", „✓ Bereit") ist unverändert — die Meldung „CLI-Prozess" entspricht dem ohnehin etablierten App-Vokabular („CLI-Status: …", „CLI-Stoppfehler: …").
- Kachel-Darstellung (nur Glyph + Volltext als Tooltip) und Fußzeilen-Texte sind unverändert und laut Anforderung bewusst nicht Teil der Korrektur.

## Geprüfte Dateien

Geänderte UI-relevante Dateien:

- `src/Softwareschmiede.App/Converters/AppConverters.cs` (Statusklassifikation, Live-Zweig)
- `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs` (neues `LiveLaufStatus`-Feld)
- `src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` (Mapping `LiveLaufStatus`)
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` (Stopp-Fehlermeldung, Neustart-Sicherstellung)

Zur Erreichbarkeits-/Wirksamkeitsprüfung zusätzlich herangezogen (unveränderte Darstellungsorte und Zulieferer):

- `src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` (Kachel-Statuszeile, Zeilen 35–43)
- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` (Fehlerbanner, Zeilen 264–272; `CliStatusText`-Fußzeile)
- `src/Softwareschmiede/Application/Services/CliProcessManager.cs` (Selbstheilung/`LaufdatenSicherstellenAsync`, `CliLaufAktiv`, Abgleich-Timer)
- `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (`CliLaufAktiv`, `GetLiveLaufStatus`, `GetLaufendeAufgabenIds`)
- `src/Softwareschmiede/Application/Services/AufgabeService.cs` (`AktivenLaufSicherstellenAsync`)
- `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` (`NotifyLaufAktiv` im Wiederverwendungspfad)
- `src/Softwareschmiede/Domain/Interfaces/IRunningAutomationStatusSource.cs` (`GetLiveLaufStatus`)
