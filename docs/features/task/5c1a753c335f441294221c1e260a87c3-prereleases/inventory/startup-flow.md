# Detailinventar: Programmstart und Anwendung

## Bestehende Aufrufer

`src/Softwareschmiede.App/ViewModels/MainWindowViewModel.cs` ruft `IUpdateService.CheckForUpdateAsync` in zwei Fällen auf:

- Hintergrundprüfung über `UpdatePruefenImHintergrund`
- manuelle Prüfung bzw. Update-Start über `UpdateStartenAsync`

Beim Start wird ein Update bei bestehendem Hintergrundaufruf aktuell nur geprüft; die vorhandene Updatevorbereitung und Installation ist an den expliziten Startpfad gebunden.

## Installationspfad

Für die Installation prüft `MainWindowViewModel` zunächst das Ergebnis, fragt bei riskanten laufenden CLI-Aufgaben eine Bestätigung ab, zeigt den Fortschrittsdialog, bereitet das Paket vor, startet das externe Skript und beendet danach die Anwendung über `IApplicationShutdownService`.

Eine automatische Installation beim Programmstart muss diesen Pfad entweder wiederverwenden oder seine Sicherheits- und UI-Anforderungen bewusst festlegen. Besonders relevant ist, dass `WpfUpdateProgressDialogService` und Bestätigungsdialoge während des Startens verfügbar sein müssen.

## Dependency Injection

`src/Softwareschmiede.App/App.xaml.cs` registriert `AppEinstellungService` scoped, `IUpdateService` singleton und `IUpdateReleaseClient` sowie Paket-/Skriptservices als Singleton. `SettingsViewModel` wird transient erzeugt. Bei neuer Updateoption-Abhängigkeit ist auf die bestehende Lifetime-Grenze zwischen scoped Datenbankservice und singleton Updateservice zu achten.
