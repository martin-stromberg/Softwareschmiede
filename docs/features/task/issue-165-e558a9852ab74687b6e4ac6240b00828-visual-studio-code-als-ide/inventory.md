# Inventory - Visual Studio Code als IDE-Fallback

## Zusammenfassung

Die Aktion `IDE oeffnen` ist bereits als Ribbon-Button in der Aufgabendetailansicht vorhanden und oeffnet aktuell ausschliesslich gefundene `*.sln`-Dateien ueber den OS-Standardhandler. Ohne Solution ist der Command deaktiviert; deshalb kann der geforderte VS-Code-Fallback derzeit nicht erreicht werden.

Die Persistenz fuer globale boolesche App-Einstellungen existiert bereits ueber `AppEinstellungService` und `AppEinstellungen`. Die Settings-UI hat einen Allgemein-Tab, in dem eine neue Checkbox fachlich gut platziert werden kann. Die Prozessstart-Abstraktion `IProzessStarter` ist vorhanden und testbar; fuer VS Code fehlt aber noch eine Erkennung von `code`/`code.cmd` bzw. bekannten Installationspfaden und eine Service-Methode zum Oeffnen eines Arbeitsverzeichnisses in VS Code.

## Detaildokumente

- [UI und Command-Fluss](inventory/ui-command.md)
- [IDE-Service und Prozessstart](inventory/ide-service-process.md)
- [Settings und Persistenz](inventory/settings.md)
- [Tests](inventory/tests.md)
- [Dokumentation](inventory/documentation.md)

## Relevante Codepfade

- `src/Softwareschmiede.App/Views/TaskDetailView.xaml` - Ribbon-Button `IDE oeffnen`, AutomationName `IdeOeffnen`.
- `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` - cached `_solutionPfade`, `SolutionsVorhanden`, `OeffneIdeCommand`, `OeffneIdeAsync`.
- `src/Softwareschmiede/Application/Services/IdeOeffnenService.cs` - `FindeSolutions()` und `OeffneSolution()`.
- `src/Softwareschmiede/Application/Services/AppEinstellungService.cs` - globale Key/Value-App-Einstellungen inkl. `GetBoolSettingAsync()`/`SetBoolSettingAsync()`.
- `src/Softwareschmiede.App/ViewModels/SettingsViewModel.cs` und `src/Softwareschmiede.App/Views/SettingsView.xaml` - Einstellungsseite mit Allgemein-Tab.
- `src/Softwareschmiede/Domain/Interfaces/IProzessStarter.cs`, `src/Softwareschmiede/Domain/ValueObjects/ProzessStartAnfrage.cs`, `src/Softwareschmiede/Infrastructure/Services/SystemProzessStarter.cs` - Prozessstart-Gateway.
- `src/Softwareschmiede.App/App.xaml.cs` - DI-Registrierung von `IdeOeffnenService`, `AppEinstellungService`, `IProzessStarter`.

## Aktuelles Verhalten

1. Beim Setzen von `TaskDetailViewModel.Aufgabe` wird `_showFileExplorerPanel` anhand `LokalerKlonPfad` und `Directory.Exists()` gesetzt.
2. Danach wird `_solutionPfade = ErmittleSolutionPfade(value)` gefuellt.
3. `ErmittleSolutionPfade()` ruft `IdeOeffnenService.FindeSolutions()` nur auf, wenn das Arbeitsverzeichnis existiert.
4. `SolutionsVorhanden` ist nur `true`, wenn mindestens eine `*.sln` auf oberster Ebene gefunden wurde.
5. `OeffneIdeCommand` ist mit `CanExecute => SolutionsVorhanden` registriert.
6. `OeffneIdeAsync()` kehrt bei `_solutionPfade.Count == 0` sofort zurueck.
7. Bei einer Solution wird direkt `IdeOeffnenService.OeffneSolution()` aufgerufen; bei mehreren Solutions wird vorher `IDialogService.ShowSolutionSelectionDialogAsync()` genutzt.

## Umsetzungshinweise aus dem Inventory

- Der Fallback benoetigt eine Aenderung an der Command-Aktivierung: `IDE oeffnen` darf bei vorhandenem Arbeitsverzeichnis und aktiviertem Fallback nicht mehr allein von `SolutionsVorhanden` abhaengen.
- Der bestehende Vorrang fuer Visual-Studio-Solutions kann erhalten bleiben, indem `OeffneIdeAsync()` zuerst die bisherigen Solution-Zweige ausfuehrt und erst bei `0` Solutions den VS-Code-Fallback prueft.
- Die neue Einstellung sollte als boolescher AppEinstellung-Key mit Default `false` modelliert werden. Ein fehlender oder unparsbarer Wert muss wie `false` behandelt werden, damit Bestandsnutzer kein geaendertes Verhalten erhalten.
- Fuer VS-Code-Verfuegbarkeit sollte die Erkennung testbar hinter Service-Logik liegen. `IProzessStarter` kann den eigentlichen Start kapseln, deckt aber noch keine "Executable existiert / in PATH auffindbar"-Pruefung ab.
- Fehlermeldungen laufen in der Aufgabendetailansicht ueber `FehlerMeldung`. Das passt fuer den Fall "Fallback aktiviert, aber VS Code nicht verfuegbar".

## Offene technische Punkte

- Ob VS Code nur via `PATH` oder zusaetzlich ueber bekannte Windows-Pfade erkannt werden soll, ist in der Anforderung offen. Fuer Robustheit spricht: erst `code.cmd`/`code` via PATH, danach typische Benutzer-/Systeminstallationspfade.
- `SystemProzessStarter` unterstuetzt aktuell `FileName`, `Arguments` und `UseShellExecute`, aber kein `WorkingDirectory`. Fuer `code "<arbeitsverzeichnis>"` reicht das voraussichtlich; falls spaeter das Prozess-Arbeitsverzeichnis relevant wird, muesste `ProzessStartAnfrage` erweitert werden.
- Der bestehende E2E-Test erwartet explizit, dass `IDE oeffnen` ohne `*.sln` deaktiviert ist. Dieser Test muss fuer die neue Opt-in-Einstellung angepasst oder um einen deaktivierten-Fallback-Fall ergaenzt werden.
