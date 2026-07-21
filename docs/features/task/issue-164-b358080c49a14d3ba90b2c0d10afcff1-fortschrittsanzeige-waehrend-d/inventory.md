# Bestandsaufnahme

## Kurzfazit

Die Repository-Vorbereitung beim Aufgabenstart laeuft aktuell synchron ueber `TaskDetailViewModel.StartenAsync()` -> `EntwicklungsprozessService.ProzessStartenUndCliStartenAsync()` -> `ProzessStartenAsync()` -> `PrepareCloneDirectoryAsync()` -> `IGitPlugin.CloneRepositoryAsync(...)`.

In der Fusszeile der Aufgabendetailansicht gibt es bereits einen mittleren Statustext (`CliStatusText`), der aktuell ausschliesslich CLI-Laufzeitstatus anzeigt. Waehrend der Repository-Vorbereitung wird dort noch kein Vorbereitungsstatus gesetzt. Ein Mindeststatus `Bereit Repository vor...` ist ohne groesseren Umbau direkt im Task-Start-ViewModel oder ueber einen kleinen Statusdienst moeglich.

Echte Fortschrittswerte sind derzeit nicht zentral verfuegbar: Der `IGitPlugin`-Contract enthaelt fuer `CloneRepositoryAsync` keinen Progress-Parameter, und die Remote-Git-Plugins verwenden `ICliRunner.RunAsync`, das stdout/stderr erst nach Prozessende als Komplettresultat zurueckgibt. Fortschritt kann deshalb kurzfristig nur als Aktivitaetstext umgesetzt werden; belastbare Prozentwerte erfordern eine Contract-/Runner-Erweiterung oder eine separate optionale Progress-Schnittstelle.

## Detaildokumente

- [Ablauf und Orchestrierung](inventory/ablauf-und-orchestrierung.md)
- [UI und Fusszeilenstatus](inventory/ui-und-fusszeilenstatus.md)
- [Plugin- und Fortschrittsfaehigkeit](inventory/plugin-und-fortschritt.md)
- [Tests und Absicherung](inventory/tests.md)

## Betroffene Hauptstellen

| Bereich | Datei | Relevanz |
|---|---|---|
| Aufgabenstart UI | `src/Softwareschmiede.App/ViewModels/TaskDetailViewModel.cs` | Startet den kombinierten Repository-/CLI-Ablauf und besitzt den Footer-Text `CliStatusText`. |
| Statusleiste | `src/Softwareschmiede.App/Views/TaskDetailView.xaml` | Bindet den Fusszeilen-Mitteltext an `CliStatusText`. |
| Start-Orchestrierung | `src/Softwareschmiede/Application/Services/EntwicklungsprozessService.cs` | Fuehrt Clone, Working-Directory-Validierung, Branch-Erstellung, `issue.md`, Statuspersistenz und CLI-Start aus. |
| Git-Contract | `src/Softwareschmiede.Plugin.Contracts/Domain/Interfaces/IGitPlugin.cs` | `CloneRepositoryAsync` hat aktuell keinen Fortschrittskanal. |
| Prozessrunner | `src/Softwareschmiede/Infrastructure/Services/CliRunner.cs` | `RunAsync` liefert Ergebnis erst nach Prozessende; `StreamAsync` existiert, wird fuer `git clone` nicht genutzt. |
| GitHub/Bitbucket Plugins | `plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`, `plugins/Softwareschmiede.Plugin.BitBucket/BitBucketPlugin.cs` | Klonen per `git clone` ueber `RunAsync`. |
| LocalDirectory Plugin | `plugins/Softwareschmiede.Plugin.LocalDirectory/LocalDirectoryPlugin.cs` | Bereitet Arbeitsverzeichnis durch Pointer-Datei oder Dateikopie vor; interne Kopierzaehler werden nicht gemeldet. |

## Risiken und Hinweise fuer die Planung

- Der Text aus der Anforderung ist exakt `Bereit Repository vor...`; die Implementierung sollte diesen String unveraendert ausgeben.
- Der bestehende Name `CliStatusText` ist fachlich zu eng fuer Repository-Vorbereitung. Eine Umbenennung kann Tests und Bindings beruehren; ein zusaetzlicher abstrakterer Status-Property-Name waere sauberer, ist aber mehr Arbeit.
- Fehler- und Abbruchpfade muessen den Vorbereitungsstatus sicher zuruecksetzen. Der zentrale `try/catch` in `TaskDetailViewModel.StartenAsync()` ist dafuer der naheliegende Ort.
- Wenn spaeter echter Fortschritt umgesetzt wird, sollte er optional sein, damit bestehende externe Plugins weiter kompilieren koennen.
