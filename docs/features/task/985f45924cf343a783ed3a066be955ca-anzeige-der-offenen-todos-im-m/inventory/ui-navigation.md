# UI und Navigation

## Aktive Aufgabenliste

`src/Softwareschmiede.App/Controls/ActiveTasksListControl.xaml` ist die zentrale UI fuer aktive Aufgaben. Das Control besitzt:

- `AufgabenKachelInhaltTemplate` fuer Titel, Projektname, SCM-/KI-Plugin und Ausfuehrungsstatus.
- `AufgabenKachelMitNavigationButtonTemplate` fuer Seitenleisten-Kacheln mit separatem Navigationsbutton.
- `AufgabenKachelVollflaechigKlickbarTemplate` fuer Dashboard-Kacheln, bei denen die gesamte Kachel navigiert.
- Dependency Properties `ItemsSource`, `NavigateCommand` und `ShowNavigationButton`.

Die Todo-Anzeige sollte in `AufgabenKachelInhaltTemplate` oder in einem gemeinsamen Unterlayout ergaenzt werden, damit beide Darstellungsvarianten dieselbe fachliche Information zeigen.

## Einbindung in MainWindow und Dashboard

`src/Softwareschmiede.App/Views/MainWindow.xaml` bindet die Seitenleistenliste an:

- `ItemsSource="{Binding AktiveAufgabenListe}"`
- `NavigateCommand="{Binding NavigateZuAufgabeCommand}"`

Die Seitenleiste ist ausgeblendet, wenn das Dashboard sichtbar ist. Auf dem Dashboard nutzt `src/Softwareschmiede.App/Views/DashboardView.xaml` dasselbe Control mit:

- `ItemsSource="{Binding AktiveAufgabenListe}"`
- `NavigateCommand="{Binding NavigateZuAufgabeCommand}"`
- `ShowNavigationButton="False"`

Damit ist `ActiveTasksListControl` der richtige UI-Aenderungspunkt, wenn die Anzeige im Programmmenue und Dashboard konsistent sein soll.

## ViewModel-Fluss

`MainWindowViewModel` enthaelt:

- `AktiveAufgabenListe` als gemeinsame `ObservableCollection<AktiveAufgabePanelItem>`.
- `AktiveAufgabenAktualisierenAsync`, das `IAktiveAufgabenService.GetAktiveAufgabenAsync` aufruft und die Collection per `ReplaceAll` neu setzt.
- `MapAktiveAufgabePanelItem`, das `Aufgabe` auf das UI-Item projiziert.
- `NavigateZuAufgabeCommand`, der auf die Aufgabendetailansicht navigiert.

Ein Todo-Label-Klick sollte fachlich nicht mit dem bestehenden Navigationskommando kollidieren. Wenn das Label innerhalb der vollflaechig klickbaren Dashboard-Kachel liegt, muss die Eingabe so gestaltet werden, dass der Label-Klick den Todo-Dialog oeffnet und nicht versehentlich zur Aufgabe navigiert.

## UI-Risiken

- Das Dashboard-Template hat die gesamte Kachel als `MouseBinding`; ein eingebetteter Button/Label braucht saubere Eventbehandlung.
- Die Seitenleisten-Kachel ist kompakt. Ein weiteres Label muss Texttrimming und Layoutbreite beruecksichtigen.
- Bestehende Automation-Namen fuer aktive Aufgaben koennen in E2E-Tests genutzt werden; fuer das neue Label sollte ein stabiler Automation-Name gesetzt werden, z. B. aufgabenbezogen.

