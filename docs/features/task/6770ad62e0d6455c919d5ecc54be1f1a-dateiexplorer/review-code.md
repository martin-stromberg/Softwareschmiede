# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

### FileExplorerViewModel.cs (FileExplorerViewModel)

- **Fehlerbehandlung / Konsistenz (Korrektheit)** — ~~`DateiMitStandardanwendungOeffnen` (Zeilen 355–373) ignoriert `knoten.CommitSha` und öffnet immer die Arbeitskopie unter `Path.Combine(_repositoryPath, knoten.RelativePath)`.~~ **Erledigt:** `KannAktuelleDateiOeffnen` (Zeile 95) prüft jetzt zusätzlich `CommitSha: null`, sodass der Öffnen-Button im Vergleichsmodus für Commit-Knoten deaktiviert ist (nur reine Arbeitsbaum-Knoten lassen sich öffnen, wo Vorschau und Datei auf der Platte übereinstimmen). Regressionstest ergänzt: `FileExplorerViewModelTests_DateiOeffnen.DateiMitStandardanwendungOeffnenCommand_CommitKnoten_CanExecuteFalse`.

- **Doppelter Code** — ~~Die „öffnbar"-Bedingung `_ausgewaehlterKnoten is { IsDirectory: false, IsDeleted: false }` existiert doppelt.~~ **Erledigt:** `DateiMitStandardanwendungOeffnen` nutzt jetzt `if (string.IsNullOrWhiteSpace(_repositoryPath) || !KannAktuelleDateiOeffnen) return; var knoten = _ausgewaehlterKnoten!;` und referenziert damit die einzige Definition in `KannAktuelleDateiOeffnen`.

### FileExplorerView.xaml (FileExplorerView)

- **Konsistenz (Stil der bestehenden View)** — ~~Der neue Öffnen-Button `FileExplorerDateiOeffnenButton` (Content `📂`) verwendet `FontSize="12"`.~~ **Erledigt:** `FontSize` des `📂`-Buttons auf `14` angeglichen, konsistent mit `📁`/`🔀`/`🔄`.

## Geprüfte Dateien

- `src/Softwareschmiede.App/ViewModels/FileExplorerViewModel.cs`
- `src/Softwareschmiede.App/ViewModels/ViewModelBase.cs` (RelayCommand-Verifikation)
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml`
- `src/Softwareschmiede.App/Views/FileExplorerView.xaml.cs`
- `src/Softwareschmiede.App/Controls/DiffViewer.xaml.cs`
- `src/Softwareschmiede.App/Converters/WorkspaceFileNodeIconConverter.cs`
- `src/Softwareschmiede.App/Converters/WorkspaceFileNodeStatusIconConverter.cs`
- `src/Softwareschmiede.App/ViewModels/ProjectDetailViewModel.cs` (Konventionsabgleich Process.Start)
- `src/Softwareschmiede/Domain/ValueObjects/WorkspaceFileNode.cs`
- `src/Softwareschmiede.Tests/App/ViewModels/FileExplorerViewModelTests_DateiOeffnen.cs`
