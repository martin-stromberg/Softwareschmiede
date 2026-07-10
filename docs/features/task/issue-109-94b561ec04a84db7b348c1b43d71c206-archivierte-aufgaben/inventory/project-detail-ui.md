# Projektdetailansicht und Aufgabenliste

## Dateien

- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml`
- `src/Softwareschmiede.App/Views/ProjectDetailView.xaml.cs`

## Aktueller Aufbau

`ProjectDetailViewModel` ist das zentrale ViewModel fuer die Projektdetailansicht. Beim Setzen von `ProjektId` wird `LadenAsync` gestartet. Dort werden Projektdetails ueber `ProjektService.GetDetailAsync()` und Aufgaben ueber `AufgabeService.GetByProjektAsync()` geladen.

Die Aufgaben werden in `Aufgaben` gespeichert. Die UI bindet nicht direkt daran, sondern an `GefilterteAufgaben`. `AktualisiereGefilterteAufgaben()` fuellt diese Collection anhand von `AufgabenFilter` neu.

Aktuelle Filterlogik:

- `Alle`: alle geladenen Aufgaben
- `Aktiv`: alle geladenen Aufgaben mit `Status != Archiviert`
- `Archiviert`: Aufgaben mit `Status == Archiviert`

Da `AufgabeService.GetByProjektAsync()` bereits `Archiviert` ausschliesst, kann der Filter `Archiviert` in der Projektdetailansicht mit der aktuellen Ladequelle faktisch keine archivierten Aufgaben anzeigen.

## Aktuelle XAML-Struktur

`ProjectDetailView.xaml` enthaelt eine Aufgaben-Kachel mit:

- Titel `Aufgaben`
- `ListBox` mit `AutomationProperties.Name="AufgabenListe"`
- `ItemsSource="{Binding GefilterteAufgaben}"`
- Item-Template mit Titel und Status
- Doppelklick auf `ListBoxItem` oeffnet die Aufgabe ueber Code-behind/Event-Handler

Es gibt derzeit keinen separaten Bereich fuer `AufgabeStatus.Beendet` und keine initial zugeklappte Liste in dieser Ansicht.

## Navigation und Aktualisierung

Aufgaben werden ueber `AufgabeOeffnenCommand` in die separate `TaskDetailView` geoeffnet. Beim Oeffnen wird `TaskDetailViewModel.AufgabeListeAktualisierenCallback` auf `ReloadAufgabenListAsync` gesetzt. Nach Aenderungen an einer Aufgabe wird die konkrete Aufgabe ueber `AufgabeService.GetByIdAsync()` nachgeladen, in `Aufgaben` ersetzt oder ergaenzt und danach `AktualisiereGefilterteAufgaben()` ausgefuehrt.

Fuer die neue Trennung ist wichtig, dass diese Aktualisierung auch die getrennten Collections/Properties konsistent aktualisiert, wenn eine Aufgabe ihren Status zu `Beendet` wechselt.

## Risiken und offene Punkte fuer die Planung

- Die bestehende `GefilterteAufgaben`-Collection ist eine einzelne UI-Liste. Wenn zwei getrennte Bereiche eingefuehrt werden, sollte die Filterlogik entweder erweitert oder ersetzt werden, statt Statusfilter in XAML zu verteilen.
- Die Bedeutung des bestehenden Filters `Aktiv` ist nicht deckungsgleich mit "nicht beendet". Aktuell meint `Aktiv` "nicht archiviert"; die Anforderung meint "nicht beendet".
- Der vorhandene `Archiviert`-Filter ist im Zusammenspiel mit `GetByProjektAsync()` inkonsistent, aber nicht direkt Teil der Anforderung.
