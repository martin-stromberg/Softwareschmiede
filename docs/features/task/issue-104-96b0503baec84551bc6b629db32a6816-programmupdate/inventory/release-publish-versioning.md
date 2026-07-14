# Release, Publish und Versionierung

## GitHub-Release-Pipeline

`.github/workflows/release.yml` erstellt Releases fuer zwei Faelle:

- Push/Merge auf `main`: Job `release`, Semantic Release berechnet Version und erstellt GitHub Release.
- Tag `v*.*.*`: Job `release-manual-tag`, erstellt direkt ein Release fuer den Tag.

Relevante Stellen:

- `release.yml:3-6`: Trigger fuer `main` und Tags.
- `release.yml:24-55`: Semantic-Release-Job.
- `release.yml:57-82`: manueller Tag-Release-Job.
- `release.yml:20-22`: Concurrency-Guard.

## Build- und Paketierungsartefakt

Die Composite Action `.github/actions/build-and-package/action.yml` ist fuer den Update-Download besonders relevant:

- Setup .NET 10.
- `dotnet restore`.
- `dotnet publish src/Softwareschmiede.App/Softwareschmiede.App.csproj -c Release -o publish/`.
- Rename `publish/Softwareschmiede.App.exe` nach `publish/Softwareschmiede.exe`.
- Guard: `Softwareschmiede.exe` und Plugin-DLLs muessen vorhanden sein.
- `Compress-Archive -Path publish/* -DestinationPath release.zip -Force`.

Relevante Stellen:

- `action.yml:20-22`: Publish.
- `action.yml:24-34`: Exe-Rename.
- `action.yml:36-45`: Publish-Guard.
- `action.yml:47-49`: ZIP-Erstellung.

Implikation: Das erwartete Update-Asset kann mit hoher Wahrscheinlichkeit `release.zip` sein. Das ZIP enthaelt den Inhalt des `publish/`-Ordners direkt auf Root-Ebene, nicht zwingend ein umschliessendes Verzeichnis.

## Semantic-Release-Konfiguration

`.releaserc.json` definiert:

- Branch `main`.
- Changelog, Git-Commit und GitHub-Release-Asset.
- Asset-Pfad `release.zip`, Label `Application Release`.

Relevante Stellen:

- `.releaserc.json:2`: Branch.
- `.releaserc.json:7-12`: Git-Assets `CHANGELOG.md`, `package.json`.
- `.releaserc.json:15-23`: GitHub-Asset `release.zip`.

`package.json` hat aktuell `"version": "0.0.0"`, wird aber als Semantic-Release-Artefakt gefuehrt. Ob diese Version nach Release-Commits fuer lokale WPF-Laufzeit verfuegbar ist, ist nicht direkt gegeben, weil `package.json` nicht als WPF-Publish-Datei referenziert wird.

## Lokale WPF-Versionierung

`src/Softwareschmiede.App/Softwareschmiede.App.csproj` enthaelt keine expliziten Properties fuer `Version`, `AssemblyVersion`, `FileVersion` oder `InformationalVersion`. Das Core-Projekt `src/Softwareschmiede/Softwareschmiede.csproj` ebenfalls nicht.

Relevante Stellen:

- `Softwareschmiede.App.csproj:9-23`: WPF-Projekt-Properties ohne Version.
- `Softwareschmiede.App.csproj:16`: `AssemblyName` ist `Softwareschmiede.App`.
- `Softwareschmiede.csproj:3-10`: Core-Projekt-Properties ohne Version.
- `src/Softwareschmiede/Properties/AssemblyInfo.cs`: nur `InternalsVisibleTo`.

Implikation: Fuer Akzeptanzkriterium "neuere Version verfuegbar" muss eine lokale Version definiert und ins Publish uebernommen werden. Optionen:

- MSBuild-Property `Version`/`InformationalVersion` in `Softwareschmiede.App` beim CI-Publish setzen.
- Eine `version.json`/`release.json` in den Publish-Output schreiben.
- Semantic Release so erweitern, dass es auch eine Datei aktualisiert, die in der WPF-App gelesen wird.

## Lokales publish.ps1

`publish.ps1` ist nicht die GitHub-Release-Pipeline fuer die WPF-App. Es liest ein IIS-Publishprofil unter `src/Softwareschmiede/Properties/PublishProfiles/Lokaler IIS.pubxml`, prueft IIS und baut/publisht `src/Softwareschmiede/Softwareschmiede.csproj`. Fuer die WPF-In-App-Updates ist dieses Skript wahrscheinlich nicht massgeblich.

Relevante Stellen:

- `publish.ps1:112-123`: IIS-Profil und IIS-Pruefung.
- `publish.ps1:126-133`: Build-Projekte.
- `publish.ps1:150-159`: Publish von `src/Softwareschmiede/Softwareschmiede.csproj`.

Implikation: Update-Planung sollte auf `.github/actions/build-and-package/action.yml` statt auf `publish.ps1` aufbauen.
