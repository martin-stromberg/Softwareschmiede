# Übersetzte Anforderung: Update-Download mit Fallback auf die GitHub-API-Asset-URL

**Quelle:** `issue.md` (GitHub-Issue #338)
**Branch:** `task/issue-338-f788aa4102744b928ce8a26e7f114193-update-download-fallback-auf-a`

## Fachliche Zusammenfassung

Die Update-Vorbereitung lädt das Release-Paket aktuell ausschließlich über die `browser_download_url` des Release-Assets (`github.com/{owner}/{repo}/releases/download/{tag}/release.zip`). Diese Route kann bei GitHub — wie am 2026-11-08 beobachtet — gezielt für große Assets mit transienten 5xx-Fehlern (z. B. `504 Gateway Time-out`) ausfallen, während der API-Asset-Pfad (`https://api.github.com/repos/{owner}/{repo}/releases/assets/{id}` mit `Accept: application/octet-stream`) weiterhin funktioniert. `UpdateInfo` soll daher zusätzlich die API-Asset-URL des gewählten Assets transportieren und `UpdatePackageService.DownloadAsync` soll bei einem transienten Fehler der primären URL automatisch einmalig auf diesen Fallback-Pfad ausweichen. Das bisherige Fehlerverhalten („Update konnte nicht vorbereitet werden") bleibt unverändert, wenn auch der Fallback scheitert.

## Betroffene Klassen und Komponenten

- `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs`
  - `UpdateInfo` (positionsbasiertes Record, Zeilen 21–27): Erweiterung um eine Fallback-Download-URL (z. B. `Uri? FallbackDownloadUrl`). Da es sich um ein positionelles Record handelt, sind alle Konstruktionsstellen anzupassen.
- `src/Softwareschmiede/Infrastructure/Services/Updates/GitHubReleaseClient.cs`
  - `GitHubAsset` (Zeilen 227–234): zusätzliche Deserialisierung des Asset-Felds `url` (API-Asset-URL gemäß GitHub-Schema) und/oder `id`.
  - `TryCreateCandidate` (Zeilen 109–142): Ableitung und Validierung der Fallback-URL beim Aufbau der `UpdateInfo` (analog zur bestehenden `IsAbsoluteHttpsUrl`-Prüfung der `BrowserDownloadUrl`).
- `src/Softwareschmiede/Infrastructure/Services/Updates/UpdatePackageService.cs`
  - `DownloadAsync` (Zeilen 91–118): Zweistufiger Download — primär `UpdateInfo.DownloadUrl`, bei transientem 5xx-Fehler einmaliger Zweitversuch gegen `UpdateInfo.FallbackDownloadUrl` mit Request-Header `Accept: application/octet-stream`.
  - `PreparePackageAsync` (Zeile 66): Aufrufstelle, die bisher nur `update.DownloadUrl` übergibt, muss die `UpdateInfo` (oder beide URLs) an den Download weiterreichen.
- Interfaces: `IUpdatePackageService` und `IUpdateReleaseClient` (`UpdateInterfaces.cs`) bleiben unverändert; die Erweiterung betrifft nur das übergebene Datenmodell `UpdateInfo`.
- Keine UI-Änderungen: `MainWindowUpdateFlow.InstalliereUpdateAsync` und die bestehende Fehlerbehandlung bleiben unverändert; der Fallback ist ein interner, nicht sichtbarer Mechanismus.
- Tests:
  - `src/Softwareschmiede.Tests/Application/Services/Updates/UpdatePackageServiceTests.cs`: neue Fälle für „primär 5xx → Fallback erfolgreich" und „beide URLs scheitern → Fehler wie bisher"; `CreateUpdateInfo`-Hilfsmethode (Zeilen 153–154) an das erweiterte Record anpassen.
  - `src/Softwareschmiede.Tests/Infrastructure/Services/GitHubReleaseClientTests*.cs`: Release-JSON um `url`/`id` der Assets ergänzen; Prüfung, dass die Fallback-URL in `UpdateInfo` ankommt.
  - Weitere `UpdateInfo`-Konstruktionsstellen in `UpdateServiceTests.cs`, `UpdateServiceTests_Options.cs`, `MainWindowViewModelUpdateTestBase.cs`, `MainWindowViewModelTests_UpdateStartup.cs` anpassen.
  - E2E-Infrastruktur: `UpdateE2EFixture` (Zeilen 443–460) und `E2E_UpdateSettings` (Zeilen 1423–1448) erzeugen die Asset-JSONs nur mit `browser_download_url` — um `url`/`id` ergänzen, damit `TryCreateCandidate` eine Fallback-URL liefert. `UpdateFixtureHttpMessageHandler` bildet exakte Request-URLs auf konfigurierte Antworten ab (`FindeAntwort(url)`, unbekannte URLs werfen `HttpRequestException`); Szenarien, die den Fallback auslösen sollen, benötigen einen Antwort-Eintrag für die API-Asset-URL.

## Implementierungsansatz

- **Datenmodell:** `UpdateInfo` um einen optionalen URL-Parameter erweitern. Die GitHub-API liefert pro Asset bereits das Feld `url` mit genau der benötigten API-Asset-URL; alternativ kann sie aus `id` als `https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/assets/{id}` aufgebaut werden (Repository-Daten liegen in `UpdateOptions` vor). Annahme: `url` wird bevorzugt, `id` dient ggf. als Bezugsgröße; die URL wird wie bisher auf absolute HTTPS-Form geprüft, sinnvollerweise zusätzlich auf den Host `api.github.com` gepinnt (Konsistenz zu `IsAllowedFollowUpUri`).
- **Download-Fallback:** `DownloadAsync` wird so umgebaut, dass zuerst `DownloadUrl` angefragt wird. Liefert die Antwort einen 5xx-Status, wird — statt wie bisher über `EnsureSuccessStatusCode` zu werfen — ein zweiter `HttpRequestMessage`-GET gegen `FallbackDownloadUrl` gesendet. Dieser Request benötigt `Accept: application/octet-stream` (sonst liefert die API JSON-Metadaten statt des Binärstroms) sowie einen `User-Agent`-Header (die GitHub-API lehnt Requests ohne User-Agent mit 403 ab; der geteilte `HttpClient` aus `App.xaml.cs` setzt keine Default-Header, `GitHubReleaseClient` setzt beide Header bereits pro Request). Liegt keine `FallbackDownloadUrl` vor oder scheitert auch der Fallback, wird der ursprüngliche Fehlerpfad (Exception → „Update konnte nicht vorbereitet werden") beibehalten.
- **Stream-Verarbeitung:** Der Streaming-Teil (Buffer-Kopie, Fortschrittsmeldungen über `IProgress<UpdatePreparationProgress>`) bleibt unverändert und läuft für beide Pfade identisch; die prozentuale Fortschrittsberechnung nutzt die `ContentLength` der jeweils erfolgreichen Antwort. Die partielle Zieldatei (`{AssetName}.download`) wird über `FileMode.CreateNew` geöffnet — bei einem Fallback-Eintritt vor dem Stream-Start existiert sie noch nicht.
- **Logging:** Beim Umschalten auf den Fallback ist ein `LogWarning` mit Statuscode und URLs vorzusehen (Namens- und Logging-Konvention wie im übrigen Service).
- **Keine neuen Erweiterungspunkte:** Es wird weder ein neues Interface noch eine neue Klasse benötigt; die Änderung ist eine Erweiterung von `UpdateInfo`, `GitHubAsset`/`TryCreateCandidate` und `DownloadAsync`.

## Konfiguration

Keine neue Konfiguration erforderlich. Der Fallback ist festes, immer aktives Verhalten des Update-Downloads. `UpdateOptions` liefert bereits `RepositoryOwner`/`RepositoryName` für den Fall, dass die API-URL aus der Asset-`id` aufgebaut werden soll; `CheckTimeout` gilt unverändert. (Annahme: Es wird kein Opt-out-Schalter gefordert — die Anforderung beschreibt den Fallback als bedingungslose Robustheitsverbesserung.)

## Offene Fragen

- **Auslöser des Fallbacks:** Soll nur bei empfangenen 5xx-Statuscodes umgeschaltet werden (Wortlaut der Anforderung), oder auch bei Transportfehlern ohne HTTP-Antwort (`HttpRequestException` ohne Response, Verbindungsabbruch, DNS) sowie bei `429 Too Many Requests` oder Timeout?
- **Abbruch mitten im Stream:** Soll der Fallback auch greifen, wenn der Download nach erfolgreichem Response-Header mitten im Stream abreißt? Dann müsste die bereits teilweise geschriebene `.download`-Datei verworfen und der Download komplett neu gestartet werden (Fortschritt springt zurück auf 0 %).
- **Wiederholungen:** Genau ein Fallback-Versuch (Annahme gemäß Anforderung „als Fallback ... versuchen") oder eine mehrstufige Retry-Strategie mit Verzögerung?
- **Sichtbarkeit:** Soll ein erfolgreicher Download über den Fallback dem Benutzer angezeigt werden (z. B. Hinweistext im Fortschrittsdialog) oder ausschließlich im Protokoll festgehalten werden?
- **Kleine Assets:** Der beobachtete Ausfall betraf nur `release.zip`; der Mechanismus gilt dennoch generell für den Paket-Download — gibt es weitere Download-Stellen (z. B. `update.json`-Prüfungen), die denselben Fallback nutzen sollen? (Aktuell erkennbar: nur `UpdatePackageService.DownloadAsync`.)
