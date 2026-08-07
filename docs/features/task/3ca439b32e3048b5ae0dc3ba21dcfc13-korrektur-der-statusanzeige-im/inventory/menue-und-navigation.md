# Menue und Navigation

## Aufbau der Seitenleiste

`MainWindow.xaml` definiert die linke Seitenleiste und blendet die Sektion "Aktive Aufgaben" aus, solange das Dashboard sichtbar ist. In der Detailansicht wird `ActiveTasksListControl` mit `ItemsSource="{Binding AktiveAufgabenListe}"` und `NavigateCommand="{Binding NavigateZuAufgabeCommand}"` eingebunden.

`ActiveTasksListControl.xaml` rendert pro Aufgabe eine Kachel. Der Status-Text der Kachel ist:

```xml
Text="{Binding ., Converter={StaticResource KiAusfuehrungsStatusConverter}}"
```

Der gleiche Converter wird auch fuer `AutomationProperties.HelpText` verwendet. E2E-Tests lesen diesen HelpText aus.

## ViewModel-Datenquelle

`MainWindowViewModel` besitzt eine zentrale `ObservableCollection<AktiveAufgabePanelItem> AktiveAufgabenListe`. Das Dashboard bekommt ueber `DashboardViewModel.Initialize(...)` dieselbe Collection. Damit sind Dashboard und Seitenleiste eine gemeinsame Datenquelle.

`AktiveAufgabenAktualisierenAsync` ruft `IAktiveAufgabenService.GetAktiveAufgabenAsync` ab, laedt offene Todo-Anzahlen und ersetzt den Collection-Inhalt per:

```csharp
AktiveAufgabenListe.ReplaceAll(aufgaben.Select(a => MapAktiveAufgabePanelItem(a, offeneTodoCounts)));
```

`ReplaceAll` leert die Collection und fuegt neue Items hinzu. Es gibt keine Merge-/Update-Logik fuer bestehende Items.

## Panel-Item

`AktiveAufgabePanelItem` ist das Presentation Model fuer die Menuekachel. Es enthaelt:

- `Status`
- `AktiveRunId`
- `LastHeartbeatUtc`
- `LaufStatus`
- `LetzterCliStartUtc`
- `HasScheduledPrompt`
- `IsAktiv`
- Plugin-, Titel- und Todo-Anzeigedaten

Fast alle fachlichen Daten sind `init`-Properties. Nur `IsAktiv` ist nachtraeglich setzbar und benachrichtigt Property-Changes.

## Aktualisierungsausloeser

`MainWindowViewModel` aktualisiert die aktive Aufgabenliste in diesen Situationen:

- im `CurrentView`-Setter nach Navigation
- bei `IRunningAutomationStatusSource.RunningCountChanged`
- periodisch ueber einen `DispatcherTimer` alle 5 Sekunden

Der Re-Entrancy-Schutz (`SemaphoreSlim _refreshGate`) ueberspringt ueberlappende Refreshes.

## Relevanz fuer die Anforderung

Das Menue benutzt bereits `AktiveRunId`, `LastHeartbeatUtc` und `LaufStatus`, aber nur den Stand, der zuletzt in `AktiveAufgabePanelItem` hineingemappt wurde. Wenn die Aufgabe bereits sichtbar ist, bevor `AktiveRunId`, Heartbeat oder `LaufStatus` persistiert wurden, bleibt der sichtbare Eintrag bis zum naechsten Refresh beim alten Wert.

Damit ist fuer "CLI arbeitet, Menue zeigt Bereit" besonders relevant:

- Wird nach jedem relevanten Laufstatuswechsel ein Refresh der `AktiveAufgabenListe` ausgeloest?
- Kann ein reiner Runtime-Statuswechsel ohne Running-Count-Aenderung zeitnah im Menue sichtbar werden?
- Soll die Kachel weiter durch komplette Ersetzung aktualisiert werden oder sollen Statusdaten im bestehenden `AktiveAufgabePanelItem` veraenderbar werden?
