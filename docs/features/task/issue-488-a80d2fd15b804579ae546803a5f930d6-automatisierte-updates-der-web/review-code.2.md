# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Kurzfazit: Alle sieben Befunde aus `review-code.1.md` sind sauber behoben — `StatusServerUpdateSettingsCodec` zentralisiert Parse/Format für Kanal/Modus/Uhrzeit (kulturinvariant, an allen drei Verwendungsstellen eingebunden: `AdminEndpoints.PostUpdateSettings`, `ServerSettingsService`, `AdminPagesComposer`); `StatusServerUpdateSourceFactory.ReplaceSource` konsolidiert den Source-Swap inkl. Dispose und wird von `ApplyPersistedUpdateSettingsAsync` und `ApplySettingsAsync` gemeinsam genutzt; der statische `SemaphoreSlim s_manualOverrideLock` serialisiert den kompletten Enabled-Override-Block korrekt (Lesen des Ausgangswerts, Erzwingen, Aktion, Wiederherstellen — alles innerhalb der Sperre); `PreserveDatabaseFiles` schließt die `Data:DatabasePath`-Lücke inkl. `-wal`/`-shm`-Seitendateien und verweigert korrekt `..`-/verankerte/anderlaufwerkige Pfade (vier Tests belegen die Fälle); `ReadyToInstall` ist aus dem DTO entfernt und `UpdateLogPath` wird als „Update-Protokoll"-Zeile gerendert; `StatusWebApplicationFactory.DisposeAsync` räumt `_ownedDirectory` jetzt auch bei `DatabasePathOverride` auf (durch `StatusWebApplicationFactoryTests` belegt). Sicherheitsprüfpunkte weiterhin erfüllt: alle neuen POST-Endpunkte tragen `RequireAuthorization(AdminOnlyPolicy)` + `RequireAntiforgery()` (`AdminEndpoints.cs` Z. 52-55; Auth-/Antiforgery-Test belegt anonym→/login, Überwacher→/access-denied, fehlendes Token→400), das GitHub-Token fließt nur via `GitHub:Token` in den Bearer-Header des benannten HttpClients. YAML-Schema stimmt (`assets[]` mit `platform`/`runtimeIdentifier`/`assetName`/`assetUrl`/`sha256`/`sizeBytes`; `release-metadata.json` vor dem Zippen in beiden Publish-Roots; Manifest-Upload in `release.yml` und `staging-ci.yml` konsistent). Der Race-Test (`Task.Delay(200)`, `StatusServerUpdateServiceTests.cs` Z. 129) ist in der Bestand-Richtung deterministisch — mit korrektem Lock bleibt `checkCalls` zwangsläufig 1, bis `releaseCheck` (TCS, nicht zeitbasiert) aufgelöst wird; der Delay dient nur dem „Chancen geben" im Fehlerfall, nicht der Erfolgsbedingung. Build (`Softwareschmiede.StatusServer.Tests` inkl. StatusServer) erfolgreich, 0 Warnungen. Die drei neuen Befunde sind niedrigschwere Restpunkte. Offener operativer Punkt (kein Code-Befund): `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` ist weiterhin ungetrackt (`?? external/`) und muss beim Commit zwingend mit eingehen — die `.gitignore`-Ausnahme ist korrekt angelegt.

## Befunde

### AdminEndpoints.cs (AdminEndpoints)

- **Fehlerbehandlung / falsche Erfolgsanzeige bei Nicht-Erfolgs-Outcome** — `PostUpdateInstall` (Z. 371-381) behandelt jede `Outcome != Failed` als erfolgreichen Installationsanlauf und rendert die statische Seite „Der Server startet neu". `IAutoUpdateOrchestrator.InstallAsync` kann aber auch `AutoUpdateOutcome.NoUpdate` (kein geladenes Paket), `Skipped` oder `Canceled` liefern — in diesen Fällen wurde nichts installiert, der Admin sieht dennoch die Neustart-Ankündigung (im Gegensatz zu `PostUpdateCheck`, wo die „abgeschlossen"-Meldung für `NoUpdate` noch zutreffend ist, ist „Der Server startet neu" hier faktisch falsch).

  Empfehlung: Nur bei `Outcome == Success` (bzw. `result.Code == AutoUpdateResultCode.InstallationStarted`) die `RenderUpdateInstallStarted`-Seite liefern; bei `NoUpdate`/`Skipped`/`Canceled` per Redirect auf `/admin/settings` mit passendem `?msg=`/`?err=`-Schlüssel (neuer `StatusMessages`-Eintrag, z. B. „Kein Update zur Installation bereit") umleiten.

### AdminPagesComposer.cs (AdminPagesComposer)

- **Kopplung/Konfiguration — hardcodierter Pfad in Hinweistext** — `RenderUpdateInstallStarted` (Z. 248) rendert den Literal `updates/update.log`. `Update:WorkingDirectory` ist aber konfigurierbar (`StatusServerUpdateOptions.WorkingDirectory`, Default `updates`); bei abweichender Konfiguration zeigt die Seite einen nicht existierenden Pfad. Der korrekte Pfad liegt als `StatusServerUpdateStatus.UpdateLogPath` bereits vor, die statische Seite hat aber keinen Zugriff darauf.

  Empfehlung: Den Logpfad entweder aus `StatusServerUpdateOptions` ableiten und als Parameter an `RenderUpdateInstallStarted` übergeben (`PostUpdateInstall` löst den Singleton auf, `Path.Combine(WorkingDirectory, "update.log")`) oder den konkreten Pfad aus dem Hinweistext entfernen und nur auf die Update-Sektion der Einstellungsseite verweisen.

### StatusServerUpdateService.cs (StatusServerUpdateService)

- **Concurrency / verbliebene Race zwischen Override-Restore und `ApplySettingsAsync`** — `RunWithManualOverrideAsync` (Z. 161-178) stellt im `finally` den beim Lock-Eintritt gelesenen `Enabled`-Wert wieder her. `ApplySettingsAsync` (Z. 76-83) mutiert denselben `AutoUpdateOptions`-Singleton jedoch **außerhalb** des Semaphores. Läuft ein manueller Lauf (u. U. mehrere Sekunden wegen Download), während ein Admin parallel die Update-Einstellungen speichert, überschreibt der `finally`-Restore den gerade persistierten Modus wieder: `wasEnabled=true` + Speichern von `Off` → nach dem manuellen Lauf steht `Enabled` fälschlich wieder auf `true` (und umgekehrt). Der Laufzeitzustand ist dann inkonsistent zur Persistenz bis zum nächsten Speichern oder Neustart.

  Empfehlung: `ApplySettingsAsync` ebenfalls unter `s_manualOverrideLock` stellen (Methode async machen und `s_manualOverrideLock.WaitAsync()` um `ApplySettings` + `ReplaceSource` legen), damit alle Mutatoren des geteilten `Enabled`-Flags dieselbe Sperre nutzen; die Start-Up-Variante `ApplyPersistedUpdateSettingsAsync` ist unkritisch (läuft vor `app.Run()` ohne Parallelität).

## Geprüfte Dateien

Liste aller geprüften Dateien:

- `server/Softwareschmiede.StatusServer/Update/AutoUpdateApplicationExtensions.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerAutoUpdateOptionsMapper.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerAutomationMode.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateChannel.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateOptions.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateService.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateSettings.cs`
- `server/Softwareschmiede.StatusServer/Update/StatusServerUpdateSettingsCodec.cs`
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
- `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` (API-Oberfläche via Paket-XML-Doc geprüft, `AutoUpdateOutcome`-Werte verifiziert)
- `.github/actions/build-and-package/action.yml`
- `.github/workflows/release.yml`
- `.github/workflows/staging-ci.yml`
- `server/README.md`
- `server/Softwareschmiede.StatusServer.Tests/Update/StatusServerAutoUpdateOptionsMapperTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Update/StatusServerUpdateServiceTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Update/StatusServerUpdateSettingsCodecTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Update/StatusServerUpdateSourceFactoryTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Update/AutoUpdateApplicationExtensionsTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/StatusWebApplicationFactoryTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Auth/AdminUpdateSettingsTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/E2E/StatusServerUpdateFlowTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/TestSupport/TempUpdateSource.cs`
- `server/Softwareschmiede.StatusServer.Tests/TestSupport/UpdateTestFactory.cs`
- `server/Softwareschmiede.StatusServer.Tests/TestSupport/StatusServerDbSeeder.cs`
- `server/Softwareschmiede.StatusServer.Tests/StatusWebApplicationFactory.cs`
- `server/Softwareschmiede.StatusServer.Tests/Pages/AdminPagesComposerTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Security/ServerSettingsServiceTests.cs`
- `server/Softwareschmiede.StatusServer.Tests/Softwareschmiede.StatusServer.Tests.csproj`
