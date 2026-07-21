# Plugin- und Fortschrittsfaehigkeit

## Aktueller Git-Contract

`IGitPlugin.CloneRepositoryAsync(string repositoryUrl, string targetPath, CancellationToken ct = default)` ist der zentrale Contract fuer die Repository-Vorbereitung (`IGitPlugin.cs`, Zeilen 22-26). Es gibt keinen `IProgress<T>`, kein Event und kein Rueckgabeobjekt fuer Zwischenstaende.

Auch `GitPluginBase<TPlugin>` deklariert `CloneRepositoryAsync` abstrakt ohne Fortschrittskanal (`GitPluginBase.cs`, Zeilen 34-35). Jede Contract-Aenderung betrifft interne und externe Plugins.

## Remote-Git-Plugins

Das GitHub-Plugin ruft fuer den Klon `git clone` ueber `_cliRunner.RunAsync(...)` auf (`GitHubPlugin.cs`, Zeilen 553-578). Fehler werden nach Prozessende ausgewertet; bei Erfolg werden anschliessend Credentials konfiguriert (`GitHubPlugin.cs`, Zeilen 579-592).

Das Bitbucket-Plugin nutzt denselben Grundmechanismus: Credentials aufloesen, URL bauen, `git clone` ueber `_cliRunner.RunAsync(...)`, Fehler nach Prozessende (`BitBucketPlugin.cs`, Zeilen 278-305).

`ICliRunner.RunAsync(...)` liest stdout und stderr parallel, liefert aber erst nach Ende ein `CliResult` (`ICliRunner.cs`, Zeilen 17-22; `CliRunner.cs`, Zeilen 24-59). Damit kann die UI bei den Remote-Plugins aktuell keine laufenden Git-Fortschrittszeilen sehen.

`ICliRunner.StreamAsync(...)` existiert und streamt stdout/stderr zeilenweise (`ICliRunner.cs`, Zeilen 24-35; `CliRunner.cs`, Zeilen 62-166). Es wird fuer die bestehenden `git clone`-Aufrufe nicht verwendet. Git-Clone-Fortschritt erscheint typischerweise auf stderr und kann je nach Terminal/TTY als carriage-return-basierte Ausgabe kommen; die vorhandene zeilenweise Streaming-API koennte dafuer unvollstaendig sein.

## LocalDirectory-Plugin

`LocalDirectoryPlugin.CloneRepositoryAsync(...)` hat zwei Modi:

- `InSourceDirectory`: Quellpfad validieren, Git initialisieren, Pointer-Datei schreiben, sofort zurueckkehren (`LocalDirectoryPlugin.cs`, Zeilen 139-158).
- `SeparateWorkingDirectory`: Ziel absichern, Quellverzeichnis kopieren, Arbeitsverzeichnis initialisieren und Initial-Commit erzeugen (`LocalDirectoryPlugin.cs`, Zeilen 161-168).

Beim Kopieren fuehrt `CopyDirectoryWithGuardrailsAsync(...)` bereits interne Zaehler `copiedFiles` und `copiedBytes` (`LocalDirectoryPlugin.cs`, Zeilen 507-522). Diese Werte dienen aktuell Guardrails und werden nicht nach aussen gemeldet. Fuer dieses Plugin waere ein belastbarer Fortschritt technisch einfacher ableitbar als fuer Remote-Git, weil die Quelle lokal enumeriert und kopiert wird.

## Bewertung fuer Fortschrittsanzeige

Kurzfristig verfuegbar:

- Ein Aktivitaetstext `Bereit Repository vor...` waehrend des gesamten Await auf Repository-/CLI-Start.
- Optional eine grobe Phasenmeldung, falls gewuenscht, aber die Anforderung verlangt mindestens exakt den genannten Text.

Nicht ohne Erweiterung verfuegbar:

- Prozentwerte fuer GitHub/Bitbucket-Klons.
- Fortschrittswerte auf `IGitPlugin`-Ebene.
- Zentrale Fehler-/Progress-Ereignisse fuer alle SCM-Plugins.

Moegliche Erweiterungen:

- Eine optionale Schnittstelle wie `IProgressGitPlugin` oder ein optionaler Parameter in einer neuen Overload-Methode, um externe Plugins kompatibel zu halten.
- Ein Wertobjekt fuer Fortschritt, z. B. Phase, Nachricht, Prozent optional.
- Remote-Plugins koennten `git clone --progress` mit einem Runner nutzen, der stderr inkrementell und carriage-return-sensitiv auswertet.
- LocalDirectoryPlugin koennte Datei-/Byte-Fortschritt ueber seine vorhandenen Zaehler melden.

Fuer diese Anforderung sollte der Plan klar trennen: Mindeststatus sofort umsetzen; echte Prozentwerte nur, wenn die Planung bewusst Contract-/Runner-Arbeit einschliesst.
