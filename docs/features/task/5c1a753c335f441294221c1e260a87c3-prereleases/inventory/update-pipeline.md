# Detailinventar: Updateprüfung und Releasequelle

## Application-Schicht

`src/Softwareschmiede/Application/Services/Updates/UpdateInterfaces.cs` definiert `IUpdateService` mit Prüfung, Vorbereitung und Start sowie `IUpdateReleaseClient` mit `GetLatestStableReleaseAsync`.

`UpdateService` liest die installierte Version, ruft die Releasequelle ab und vergleicht beide Versionen über `UpdateVersionComparer`. Die Vorbereitungs- und Startphasen sind bereits getrennt. Der Service besitzt einen Semaphore-Schutz und cached das letzte Prüfergebnis.

`UpdateModels.cs` enthält `InstalledVersionInfo`, `UpdateInfo`, Prüfstatus und Vorbereitungsmodelle. `UpdateInfo` trägt aktuell keine explizite Prerelease-Klassifikation.

## GitHub-Releasequelle

`src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs` ruft `https://api.github.com/repos/{owner}/{repo}/releases/latest` ab. Das GitHub-Objekt enthält bereits das Feld `prerelease`, aber Releases mit `Prerelease == true` werden immer verworfen. Das `/latest`-Endpoint liefert daher keine auswählbare Releasehistorie für den Prerelease-Fall.

Die Assetauswahl erwartet `UpdateOptions.AssetName` (`release.zip`). Ungültige Tags, fehlende Assets und HTTP-/JSON-/Timeoutfehler werden als nicht prüfbar behandelt.

## Versionsvergleich

`UpdateVersionComparer` akzeptiert aktuell `major.minor.patch` und optionale Build-Metadaten. Ein SemVer-Prerelease-Suffix wie `1.2.3-rc.1` wird nicht akzeptiert. Die gewünschte Prerelease-Unterstützung betrifft damit sowohl die Auswahl des Releases als auch Parsing und Vergleich.

## Konfiguration

`UpdateOptions` enthält Repository-, Asset-, Verzeichnis- und Timeoutkonfiguration. Sie ist technische Deploymentkonfiguration und derzeit nicht mit den benutzerspezifischen DB-Einstellungen verbunden.
