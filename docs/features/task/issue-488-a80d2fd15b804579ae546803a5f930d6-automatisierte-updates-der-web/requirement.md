# Anforderung: Automatisierte Updates der Status-Webanwendung mit msTools.Updater (Issue #488)

## Fachliche Zusammenfassung

Der ASP.NET-Core-StatusServer (`server/Softwareschmiede.StatusServer`) erhält eine automatisierte Self-Update-Funktion auf Basis der Bibliothek **msTools.Updater**, die als lokale Bibliothek eingebunden wird und Download, Integritätsprüfung (SHA-256 aus dem Update-Manifest), Austausch der Programmdateien und den Prozess-/Dienst-Neustart übernimmt. Update-Quelle sind die GitHub Releases des Softwareschmiede-Repositories; der Release-Kanal ist zwischen **Stable** (`vX.Y.Z`) und **Pre-Release** (`vX.Y.Z-rc.N`, GitHub-`prerelease`-Flag) wählbar. Kanal und Automatisierung werden in der Admin-Oberfläche aus #487 konfiguriert, zusätzlich kann ein Update manuell über einen Aktionsbutton ausgelöst werden. Die Release-Pipeline erweitert das Update-Manifest so, dass die StatusServer-Assets `statusserver-win-x64.zip` und `statusserver-linux-x64.zip` vom Server konsumiert werden können, ohne den Desktop-Updater zu beeinträchtigen. Das Datenverzeichnis `data` (SQLite via `StatusServerDbContext`) und die Konfigurationsdateien `appsettings*.json` bleiben vom Update unberührt; Update-Prüfung, -Anwendung und Fehlschläge werden nachvollziehbar protokolliert.

## Betroffene Klassen und Komponenten

### StatusServer — neu zu erstellende Artefakte (`server/Softwareschmiede.StatusServer/`)

- **Update-Orchestrierung** (Arbeitsname, z. B. `Update/StatusServerUpdateService`): kapselt Update-Prüfung gegen GitHub Releases, Anstoßen der msTools.Updater-Mechanik und Protokollierung von Prüfung, Anwendung und Fehlschlägen über `ILogger`.
- **Automatisierungs-Hintergrunddienst** (Arbeitsname, z. B. `Update/UpdateCheckHostedService` als `IHostedService`/`BackgroundService`): führt die einstellbare automatische Update-Prüfung/-Anwendung aus; konkrete Auslösestrategie (periodisch/beim Start) gemäß Referenz-Repositories.
- **Admin-Endpunkte** in `Endpoints/AdminEndpoints.cs`: POST-Endpunkte für Kanal-/Automatisierungs-Einstellungen sowie der manuelle Aktionsbutton („Update prüfen/anwenden"), abgesichert über `StatusRoles.AdminOnlyPolicy` und `RequireAntiforgery`, im bestehenden Post/Redirect/Get-Muster mit `?msg=`/`?err=` über `StatusMessages`.
- **Seiten-Rendering** in `Pages/AdminPagesComposer.cs`: Erweiterung der Einstellungsseite (`RenderSettings`) um Kanal-Auswahl, Automatisierungs-Schalter, Aktionsbutton und Anzeige von Update-Status/Fehlern.
- **`Endpoints/StatusMessages.cs`**: neue feste Schlüssel für Update-Status- und Fehlermeldungen.
- **Update-Konfiguration**: neue Options-Klasse (z. B. `StatusServerUpdateOptions`) bzw. Erweiterung von `Options/StatusServerOptions.cs` um eine `Update`-Sektion (plattformabhängiger `AssetName` `statusserver-win-x64.zip`/`statusserver-linux-x64.zip`, `ManifestAssetName`, Prüf-Timeout); optionale Token-Nutzung aus der bestehenden `GitHub:Token`-Konfiguration (`GitHubOptions.Token`).
- **`Security/ServerSettingsService`**: neue persistierte Schlüssel in der `ServerSettings`-Tabelle für Release-Kanal und Automatisierungsmodus (analog `SessionDurationKey`).

### Wiederverwendbarer Bestand aus `src/Softwareschmiede` (net11.0, vom StatusServer referenzierbar — neuer `ProjectReference` im StatusServer-csproj)

- `ApplicationVersionProvider` / `IApplicationVersionProvider` (liest `version.json`), `UpdateOptions` (konfigurierbares `AssetName`/`ManifestAssetName`), `GitHubReleaseClient` / `IUpdateReleaseClient` (kanalbewusste Release-Auswahl über `UpdateCheckOptions.Channel`), `SemanticUpdateVersion`, `UpdateVersionComparer`, `UpdateChannel`, `UpdatePathChannelResolver` sowie die Modelle aus `UpdateModels.cs` (`UpdateInfo`, `UpdateCheckResult`, `UpdateReleaseLookupResult` u. a.).
- `UpdatePackageService` und `UpdateScriptService` implementieren die Desktop-spezifische Austausch-Mechanik über ein externes Skript — beim StatusServer übernimmt msTools.Updater den Dateiaustausch; ob Teile (z. B. Manifest-Verifikation `GetExpectedSha256`, Pfadabsicherung `EnsureInsideBase`) wiederverwendet werden, ist im Umsetzungslauf zu entscheiden.

### Release-Pipeline

- `.github/actions/build-and-package/action.yml`: Schritt „Create update manifest" so erweitern, dass das Manifest die StatusServer-Assets ausliefert (Rezepte-Format: `platform`, `runtimeIdentifier`, `assetName`, `assetUrl`, `sha256`, `sizeBytes` je Asset) — entweder als zusätzliche `assets`-Einträge in `update.json` oder als separates Server-Manifest (Entscheidung anhand der Referenz-Repositories; der Desktop-Eintrag für `release.zip` darf sich nicht ändern).
- `.github/workflows/release.yml`: ggf. Upload eines zusätzlichen Manifest-Assets, falls ein separates Server-Manifest gewählt wird.
- `server/README.md`: Dokumentation der Update-Konfiguration und des Dienst-Neustarts je Plattform.

### Interfaces / Enums

- Bestehende Interfaces (`IUpdateService`, `IUpdateReleaseClient`, `IApplicationVersionProvider`) sind als Referenz vorhanden; ob der StatusServer sie direkt nutzt oder eigene schmalere Verträge um die msTools.Updater-API definiert, hängt von der Bibliotheks-API ab (im Umsetzungslauf zu klären).
- `UpdateChannel` (`Stable`/`ReleaseCandidate`) ist bereits vorhanden und deckt den geforderten Kanal ab; für den Automatisierungsmodus ist ggf. ein serverseitiges Pendant zu `UpdateMode` (`Off`/`CheckOnly`/automatisch anwenden) erforderlich — Modellierung an `UpdateMode` angelehnt.

### Tests

- `server/Softwareschmiede.StatusServer.Tests`: Unit-Tests für die Update-Logik (Kanalwahl, Versionsvergleich, Einstellungspersistenz über `ServerSettingsService`), Endpunkt-Tests über `StatusWebApplicationFactory` für die neuen Admin-Endpunkte inkl. Autorisierung (`AdminOnlyPolicy`) und Antiforgery.
- `src/Softwareschmiede.Tests`: nur falls der geteilte Update-Stack geändert wird.

## Implementierungsansatz

- **Manifest-Erweiterung zuerst**: Die Pipeline erweitert `update.json` um Einträge für `statusserver-win-x64.zip` und `statusserver-linux-x64.zip` (oder erzeugt ein separates Server-Manifest im selben Rezepte-Format). Rückwärtskompatibilität ist gesichert, solange der bestehende `release.zip`-Eintrag unverändert bleibt — `UpdatePackageService.GetExpectedSha256` filtert ohnehin per `assetName`.
- **Update-Prüfung**: Der Server bestimmt sein Plattform-Asset anhand des Runtime-Identifiers (`win-x64`/`linux-x64`) und prüft Releases kanalbewusst — direkte Wiederverwendung von `GitHubReleaseClient`/`UpdateService` mit eigener `UpdateOptions`-Konfiguration (`AssetName` plattformabhängig) oder über die msTools.Updater-API, falls diese Manifest und Release-Abfrage selbst abdeckt.
- **Update-Anwendung**: msTools.Updater führt Download, SHA-256-Verifikation, Dateiaustausch und Neustart durch. Der Austausch muss das `data`-Verzeichnis und `appsettings*.json` ausnehmen — Ausschlussliste gemäß Referenz-Repositories konfigurieren.
- **Neustart**: Geordnetes Beenden der Webanwendung und Wiederanlauf durch den Prozess-Wächter der Plattform (systemd `Restart=`, Windows-Dienst/IIS) bzw. Updater-gesteuert — Umsetzung gemäß Referenz-Repositories (Rezepte, VideoPlayer, FinanceManager).
- **Admin-Integration**: Neue Einstellungen und der Aktionsbutton werden in das bestehende serverseitig gerenderte Admin-UI eingehängt (`/admin/settings` bzw. eigener Abschnitt), ausschließlich für die Rolle `StatusRoles.Administration`; Einstellungen werden über `ServerSettingsService` in `ServerSettings` persistiert.
- **Protokollierung**: Prüfungen, angewendete Updates und Fehlschläge werden über `ILogger` geführt und in der Admin-Oberfläche nachvollziehbar gemacht (Umfang der UI-Darstellung — letzter Status vs. Verlauf — im Umsetzungslauf anhand der Referenz-Repositories).

## Konfiguration

- **Admin-Oberfläche (persistiert in `ServerSettings`-Tabelle via `ServerSettingsService`)**: Release-Kanal (`Stable`/`ReleaseCandidate`, Mapping auf `UpdatePathChannelResolver`/`UpdateChannel`) und Automatisierungsmodus (deaktiviert/nur prüfen/prüfen und anwenden; Auslösezeitpunkt gemäß Referenz-Repositories).
- **`appsettings.json` (Sektion `Update`)**: technische Parameter — Repository-Koordinaten (Default: dieses Repository), `ManifestAssetName`, Prüf-Timeout, ggf. Intervall der Automatisierung. Das optionale `GitHub:Token` der bestehenden `GitHubOptions` wird für Release-Abfragen mitverwendet (Rate-Limit).
- **Nicht benutzerspezifisch**: Die Einstellungen gelten serverweit und sind nur der Rolle `Administration` zugänglich.

## Offene Fragen

- **Manifest-Struktur**: Erweiterung von `update.json` um die Server-Assets oder separates Server-Manifest — Entscheidung anhand der Referenz-Repositories; in beiden Fällen muss die Desktop-Update-Funktion unverändert weiterarbeiten.
- **`version.json` im StatusServer-Paket**: Die Anforderung geht davon aus, dass die veröffentlichte Installation eine `version.json` enthält; die aktuelle Pipeline schreibt sie nur in das Desktop-`publish/`-Verzeichnis, nicht in `publish-statusserver-*`. Zu klären, ob die Pipeline `version.json` zusätzlich in die StatusServer-Pakete schreiben muss oder msTools.Updater die Version anders ermittelt.
- **Neustart-Mechanik je Plattform**: systemd/Windows-Dienst/IIS/direkter Start — welche Variante msTools.Updater in den Referenz-Repositories nutzt und ob der Neustart vom Updater-Prozess oder vom Service-Manager (z. B. `Restart=on-failure`/`always` in der systemd-Unit) getragen wird.
- **Gesperrte Dateien**: Umgang mit der laufenden Anwendung (gesperrte Binarys unter Windows, Beenden vor Austausch) gemäß Referenz-Repositories.
- **Einbindung von msTools.Updater**: konkrete Form der „lokalen Bibliothek" (Projekt-/Assembly-Verweis, bereitgestellte DLL im Repo oder NuGet aus privater Quelle) und deren API-Oberfläche — das private Repository ist hier nicht einsehbar.
- **Automatisierungs-Auslöser**: periodischer Intervall-Check, Prüfung beim Anwendungsstart oder beides — sowie ob bei gefundenem Update sofort installiert wird oder eine Bestätigung erforderlich ist.
- **Fehlerverhalten im Detail**: Umfang von Rollback, Protokollierungsort (Logdatei vs. Persistenz in `ServerSettings`/DB für die Admin-Anzeige) und Form der Administrator-Benachrichtigung (Statusmeldung in der Admin-UI, persistente Fehleranzeige auf der Status-Seite?) gemäß Referenz-Repositories.
- **Wiederverwendung vs. Parallelstruktur**: Direkte Nutzung des Update-Stacks aus `src/Softwareschmiede` per Projektverweis (zieht u. a. EF-Core-Paketreferenzen mit) oder Verlagerung gemeinsamer Bausteine in ein geteiltes Projekt — Abwägung im Umsetzungslauf.
