# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Kurzfazit: Die Umsetzung folgt der Referenz-Implementierung (FinanceManager `UpdateOrchestratorAdapter`/`SetInitialConfiguration`) sauber und konsistent. Sicherheitsprüfpunkte erfüllt: alle neuen POST-Endpunkte tragen `RequireAuthorization(StatusRoles.AdminOnlyPolicy)` + `RequireAntiforgery()` (`AdminEndpoints.cs` Z. 52-55); das GitHub-Token kommt nur aus `GitHub:Token`-Konfiguration in den Bearer-Header des benannten HttpClients und wird nirgends geloggt oder persistiert. Datenerhalt ist über `PreserveFile("appsettings*.json")`/`PreserveFile("data/*")` abgedeckt. Der Enabled-Override ist try/finally-korrekt (Wiederherstellung auch bei Exceptions, durch Test `CheckAndDownload_OrchestratorThrows_StillRestoresEnabled` belegt). `statusserver-update.json` erzeugt das korrekte Rezepte-Schema (`assets[]` mit `platform`/`runtimeIdentifier`/`assetName`/`assetUrl`/`sha256`/`sizeBytes`), `release-metadata.json` landet in beiden Publish-Roots vor dem Zippen, das Desktop-`update.json` bleibt unverändert. Build beider Projekte (`Softwareschmiede.StatusServer`, `Softwareschmiede.StatusServer.Tests`) erfolgreich, 0 Warnungen. Die Befunde sind durchweg niedrigschwere Qualitäts-/Robustheitspunkte — kein korrektheitskritischer Fehler.

## Befunde

### AdminEndpoints.cs (AdminEndpoints)

- **Doppelter Code** — `PostUpdateSettings` (Z. 308-326) bildet die Form-Strings `"stable"`/`"prerelease"` bzw. `"off"`/`"check"`/`"install"` per `switch` auf die Enums ab; dieselben Literale und dieselbe Mapping-Logik existieren bereits als `ServerSettingsService.ParseChannel`/`ParseMode` (Z. 179-204, `private`) sowie als Optionswerte in `AdminPagesComposer.AppendUpdateSection` (Z. 298-309). Drei Stellen müssen bei einem neuen Enum-Mitglied synchron geändert werden.

  Empfehlung: Die Parse-/Format-Methoden zentralisieren — z. B. `ParseChannel`/`ParseMode` in `ServerSettingsService` auf `internal` heben oder eine kleine statische `StatusServerUpdateSettingsCodec`-Klasse im `Update`-Namespace anlegen — und in `PostUpdateSettings` wiederverwenden; der Composer kann die Werte über dieselbe Stelle (oder `FormatChannel`/`FormatMode` invers) beziehen.

- **Fehleranfälligkeit (kulturabhängige Zeitverarbeitung)** — `TimeOnly.TryParse(installTimeValue, …)` (Z. 337 und 345) parst ohne `CultureInfo`; die Aufrufkette `ToString("HH:mm")` in `ServerSettingsService.SetUpdateSettingsAsync` (Z. 160) und `AdminPagesComposer` (Z. 313) nutzt `:` als kulturabhängigen Zeitseparator. Unter Kulturen mit Zeitseparator ≠ `:` (z. B. fi-FI `.`) schlägt das Parsen des Browser-Werts `HH:mm` fehl und das gerenderte `value` ist kein gültiges `input[type=time]`-Format — „Prüfen und installieren" wäre dann nicht speicherbar.

  Empfehlung: Konsistent invariant arbeiten — `TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out parsed)` und `ToString("HH':'mm", CultureInfo.InvariantCulture)` an allen drei Stellen (Endpoint, `ServerSettingsService`, Composer).

### StatusServerUpdateService.cs (StatusServerUpdateService)

- **Doppelter Code** — Der Block „Quelle neu bauen und Vorgänger-`AutoUpdateGithubSource` disposen" (Z. 72-74) ist identisch zu `AutoUpdateApplicationExtensions.ApplyPersistedUpdateSettingsAsync` (Z. 90-94).

  Empfehlung: Die Ersetzungslogik einmal kapseln — z. B. `StatusServerUpdateSourceFactory.ReplaceSource(AutoUpdateOptions options, StatusServerUpdateSettings settings)`, die `options.Source` setzt und den Vorgänger disposed — und an beiden Stellen aufrufen. Alternativ kann `ApplyPersistedUpdateSettingsAsync` den bereits vorhandenen Scope nutzen, `StatusServerUpdateService` aufzulösen und `ApplySettingsAsync` aufzurufen (TimeRanges bleiben dort separat).

- **Concurrency / ungeschützte Mutation geteilten Zustands** — `RunWithManualOverrideAsync` (Z. 149-164) mutiert `AutoUpdateOptions.Enabled` am prozessweiten Singleton ohne Sperre. Zwei parallele manuelle Aktionen können sich den Flag mid-flight zurücksetzen: Beendet sich der erste Lauf, während der zweite noch Bibliotheksaufrufe ausführt, die `Enabled` prüfen, stellt er `Enabled = false` wieder her und der zweite Lauf kann mit `AutoUpdateDisabled`/Failed scheitern. Zusätzlich können während des Override-Fensters die Bibliotheks-Hintergrunddienste (bei `HostedServicesEnabled=true`) einen ungewollten Check auslösen, obwohl der Admin-Modus `Off` ist.

  Empfehlung: Manuelle Overrides serialisieren, z. B. einen statischen `SemaphoreSlim(1,1)` um den kompletten Override-Block legen, sodass `Enabled` erst wiederhergestellt wird, wenn kein Override mehr aktiv ist; alternativ — falls die Bibliothek es hergibt — einen Override-Mechanismus ohne Mutation der globalen Option (z. B. dediziertes Flag/Parameter) verwenden.

### AutoUpdateApplicationExtensions.cs (AutoUpdateApplicationExtensions)

- **Fehlende Validierung / Datenerhalt-Lücke in Randkonfiguration** — `PreserveFile("data/*")` (Z. 43) schützt nur den Default-Datenbankpfad. Ein per `Data:DatabasePath` konfigurierter SQLite-Pfad, der relativ im ContentRoot aber außerhalb von `data/` liegt (z. B. `"statusserver.db"` im Wurzelverzeichnis), wäre beim Dateiaustausch ungeschützt → Datenverlust-Risiko. Außerdem deckt `data/*` ggf. keine Unterverzeichnisse von `data/` ab (Wildcard ist segmentbezogen).

  Empfehlung: Den aufgelösten `Data:DatabasePath` beim Setup prüfen — liegt er relativ innerhalb des App-Verzeichnisses und außerhalb von `data/`, ihn zusätzlich per `PreserveFile` registrieren — oder in `server/README.md` verbindlich dokumentieren, dass `Data:DatabasePath` unter `data/` bzw. vollständig außerhalb des Installationsverzeichnisses liegen muss.

### StatusServerUpdateStatus.cs / AdminPagesComposer.cs (StatusServerUpdateStatus)

- **Toter Code / ungenutzte DTO-Felder** — `StatusServerUpdateStatus.ReadyToInstall` und `StatusServerUpdateStatus.UpdateLogPath` werden in `UpdateStatusMapper.MapAsync` (Z. 44-45) befüllt, aber in `AppendUpdateSection` nie gerendert; nur Tests konstruieren sie. `ReadyToInstall` dupliziert zudem `State == AutoUpdateState.ReadyToInstall`, das bereits als Text ausgegeben wird.

  Empfehlung: Entweder im Composer nutzen — z. B. `ReadyToInstall` zum Aktivieren/Ausgrauen des „Update jetzt installieren"-Buttons und `UpdateLogPath` als Hinweis auf das Skript-Protokoll — oder beide Felder aus dem Record (und aus `UpdateStatusMapper`) entfernen.

### NuGet.config / external/msTools.Updater (Paketquelle)

- **Verpackung/CI-Risiko** — `NuGet.config` registriert `external/msTools.Updater` als lokale Paketquelle; das nupkg `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` ist aktuell ungetrackt (`git status` zeigt `?? external/`). Die `.gitignore`-Ausnahme `!external/msTools.Updater/*.nupkg` ist korrekt angelegt, aber wird die Datei beim Commit vergessen, schlagen `dotnet restore` und sämtliche CI-Läufe fehl.

  Empfehlung: Das nupkg mit `git add external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` explizit einchecken und die Wiederherstellbarkeit auf einem frischen Klon bzw. in der CI verifizieren, bevor der Branch gemergt wird.

### StatusWebApplicationFactory.cs / UpdateTestFactory.cs (Test-Factories)

- **Temp-Verzeichnis-Leak im Override-Pfad** — `StatusWebApplicationFactory.DisposeAsync` (Z. 44-48) verlässt bei gesetztem `DatabasePathOverride` vorzeitig und löscht `_ownedDirectory` nicht; der neu hinzugekommene Update-Workspace (`Update:WorkingDirectory` = `_ownedDirectory/updates`, Z. 40) bleibt damit im `Restart_SameDatabase_UpdateSettingsApplyOnStartup`-Test als Temp-Rest liegen.

  Empfehlung: Im `DatabasePathOverride`-Pfad `_ownedDirectory` trotzdem best-effort löschen (gleiches `try/catch (IOException)`-Muster) oder den Update-Workspace im Restart-Test unter das vom Test verwaltete `dir` legen.

## Geprüfte Dateien

Liste aller geprüften Dateien:

- `server/Softwareschmiede.StatusServer/Update/AutoUpdateApplicationExtensions.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerAutoUpdateOptionsMapper.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerAutomationMode.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateChannel.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateOptions.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateService.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateSettings.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateSourceFactory.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateStatus.cs`
- `server/Softwareschmiede.StatusServer/Update/UpdateStatusMapper.cs`
- `server/Softwareschmiede.StatusServer/Program.cs`
- `server/Softwareschmiede.StatusServer/Endpoints/AdminEndpoints.cs`
- `server/Softwareschmiede.StatusServer/Endpoints/StatusMessages.cs`
- `server/Softwareschmiede.StatusServer/Pages/AdminPagesComposer.cs`
- `server/Softwareschmiede.StatusServer/Security/ServerSettingsService.cs`
- `server/Softwareschmiede.StatusServer/appsettings.json`
- `server/Softwareschmiede.StatusServer/Softwareschmiede.StatusServer.csproj`
- `NuGet.config`
- `.gitignore`
- `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` (API-Oberfläche via Paket-XML-Doc geprüft)
- `.github/actions/build-and-package/action.yml`
- `.github/workflows/release.yml`
- `.github/workflows/staging-ci.yml`
- `server/README.md`
- `server/Softwareschmiede.StatusServer.Tests/Update/StatusServerAutoUpdateOptionsMapperTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Update/StatusServerUpdateServiceTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Update/StatusServerUpdateSourceFactoryTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Auth/AdminUpdateSettingsTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/E2E/StatusServerUpdateFlowTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/TestSupport/TempUpdateSource.cs`
- `server/Softwareschmiede.StatusServer.Tests/TestSupport/UpdateTestFactory.cs`
- `server/Softwareschmiede.StatusServer.Tests/TestSupport/StatusServerDbSeeder.cs`
- `server/Softwareschmiede.StatusServer.Tests/StatusWebApplicationFactory.cs`
- `server/Softwareschmiede.StatusServer.Tests/Pages/AdminPagesComposerTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Security/ServerSettingsServiceTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj`
