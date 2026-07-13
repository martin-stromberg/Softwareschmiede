# Detailinventar - UI und Fallback-Verhalten

## Repository-Zuweisung

`RepositoryAssignViewModel` verwaltet die Arbeitsverzeichnis-Auswahl ueber:

- `AvailableWorkingDirectories` ab Zeile 69
- `SelectedWorkingDirectory` ab Zeile 76
- `LoadDirectoryStructureAsync` ab Zeile 210

Nach Repository-Auswahl wird `SelectedWorkingDirectory` auf `null` gesetzt und der Abruf gestartet. Nach erfolgreichem Ruecklauf wird die Collection neu befuellt und `SelectedWorkingDirectory = "."` gesetzt.

`RepositoryAssignDialog.xaml` bindet ab Zeile 95 direkt eine `ComboBox` an `AvailableWorkingDirectories` und `SelectedWorkingDirectory`. Ein alternatives Eingabefeld existiert nicht.

## Arbeitsverzeichnis nachtraeglich bearbeiten

`ArbeitsverzeichnisBearbeitenViewModel` verwendet dieselbe Grundlogik:

- `AvailableWorkingDirectories` ab Zeile 22
- `SelectedWorkingDirectory` ab Zeile 29
- `LoadDirectoryStructureAsync` ab Zeile 75

Besonderheit: Wenn ein bereits gespeichertes Arbeitsverzeichnis nicht in der geladenen Struktur vorkommt, wird es der Liste hinzugefuegt und vorausgewaehlt. Damit geht ein vorhandener manueller Wert nicht verloren.

`ArbeitsverzeichnisBearbeitenDialog.xaml` bindet ab Zeile 32 ebenfalls ausschliesslich eine `ComboBox`.

## Gemeinsamer Ladehelfer

`DirectoryStructureLoadHelper.LoadWorkingDirectoriesAsync` beginnt bei Zeile 17.

- Initialisiert das Ergebnis immer mit `"."` (Zeile 24).
- Ruft `DirectoryStructureBrowserService.GetDirectoriesAsync` auf.
- Faengt alle Nicht-Cancellation-Exceptions und gibt trotzdem das bisherige Ergebnis zurueck.

`LoadWithLoadingStateAsync` beginnt bei Zeile 58 und gibt nur bei erwarteter Cancellation `null` zurueck.

## Fallback-Luecke

Die Anforderung verlangt: Wenn die Verzeichnisstruktur nicht abgerufen werden kann, soll statt der Auswahlbox ein normales Eingabefeld angezeigt werden.

Der aktuelle UI-Zustand kann diese Entscheidung nicht treffen:

- Eine leere Unterverzeichnisliste nach erfolgreichem Abruf wird zu `["."]`.
- Ein technischer Fehler im Service wird zu `[]` und danach zu `["."]`.
- Eine Exception im Helper wird ebenfalls zu `["."]`.
- Die XAML-Views haben keine `TextBox` und kein Visibility-Binding fuer einen Fehler-/Fallbackmodus.

## Naheliegender Zielzustand

Ein robuster Umbau sollte in den ViewModels einen expliziten Zustand fuehren, z. B.:

- `IsWorkingDirectoryManualInput`
- `WorkingDirectoryInputText`
- `DirectoryStructureLoadFailed`
- optional `DirectoryStructureLoadMessage`

Oder zentraler: ein Ergebnisobjekt aus dem Service, z. B. `DirectoryStructureLoadResult`, mit `Directories`, `Status` und optionaler Fehlermeldung.

Die Dialoge koennen dann per Visibility zwischen `ComboBox` und `TextBox` umschalten. Beide Dialoge sollten dieselbe Semantik nutzen, weil Repository-Zuweisung und spaetere Bearbeitung fachlich denselben Fallback brauchen.

