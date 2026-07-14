# HTTP- und GitHub-Release-Zugriff

## Vorhandener GitHub-Zugriff

Das vorhandene GitHub-Plugin (`plugins/Softwareschmiede.Plugin.GitHub/GitHubPlugin.cs`) nutzt `gh` CLI und `git` CLI, keinen direkten `HttpClient`.

Relevante Stellen:

- `GitHubPlugin.cs:11-14`: Plugin nutzt `gh CLI` und `git CLI`.
- `GitHubPlugin.cs:17-23`: Token, `ICliRunner`, `ICredentialStore`.
- `GitHubPlugin.cs:76-85`: `GH_TOKEN`-Umgebung.
- `GitHubPlugin.cs:164-217`: Git-Remote-Credentials.

Dieser Code ist fuer SCM-Funktionen gedacht und erfordert Plugin-Konfiguration/Token. Fuer Updates gegen ein oeffentliches Release-Repository ist das kein guter direkter Anknuepfpunkt:

- Update-Pruefung soll global fuer die App laufen, nicht projekt-/pluginabhaengig.
- GitHub-Releases des eigenen Repositories sollten ohne Benutzer-Token lesbar sein.
- Der bestehende `ICliRunner`-Pfad wuerde ein installiertes `gh` CLI voraussetzen.

## Direkter HTTP-Zugriff

Im Codebase wurde kein produktiver direkter `HttpClient`-/Octokit-/REST-Client fuer GitHub Releases gefunden. Treffer zu GitHub beziehen sich primär auf Plugin-CLI-Aufrufe, Release-Dokumentation und CI.

Implikation: Fuer das Feature sollte ein neuer Update-/Release-Client entstehen, z. B.:

- `IGitHubReleaseClient` oder `IUpdateReleaseClient` in Application/Infrastructure.
- `HttpClient` mit GitHub REST API `repos/martin-stromberg/Softwareschmiede/releases/latest` oder Releases-Liste, falls Pre-Releases gefiltert werden sollen.
- `User-Agent` setzen, GitHub API erwartet dies praktisch.
- Asset-Auswahl anhand `release.zip` oder konfigurierbarem Namen.
- Fehler tolerant behandeln: Netzwerkfehler, 404, Rate-Limit, fehlendes Asset.

## Release-Auswahl

GitHub API `latest` ignoriert Pre-Releases. Wenn Pre-Releases explizit ignoriert werden sollen, passt `latest`. Wenn Pre-Releases optional beruecksichtigt werden sollen, braucht der Client die Releases-Liste und eigene Filterlogik.

Wichtige Felder fuer einen minimalen Client:

- `tag_name`: Versionsvergleich, typischerweise `vX.Y.Z`.
- `prerelease`: Filter.
- `assets[].name`: `release.zip`.
- `assets[].browser_download_url`: Download-URL.

## Download

Das Release-Asset kann per `browser_download_url` heruntergeladen werden. Fuer grosse Dateien sollte streaming in eine Datei genutzt werden, nicht `ReadAsByteArrayAsync` fuer den gesamten Inhalt.

Empfohlene Fehlergrenzen:

- HTTP-Status pruefen.
- Temporäre `.download`-Datei verwenden und erst nach Erfolg atomar umbenennen.
- Dateigroesse > 0 pruefen.
- ZIP per `ZipFile.ExtractToDirectory` in ein frisches Temp-Unterverzeichnis entpacken.

## Konfiguration

Der Repository-Name `martin-stromberg/Softwareschmiede` und Asset-Name `release.zip` koennen hart kodiert werden, wenn das Feature bewusst nur die eigene App betrifft. Testbarkeit wird besser, wenn diese Werte ueber Options-Klasse konfigurierbar sind.
