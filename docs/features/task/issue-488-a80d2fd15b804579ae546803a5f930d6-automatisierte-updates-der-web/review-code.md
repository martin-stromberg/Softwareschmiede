# Code-Review

## Ergebnis

**Status:** Keine Befunde

Kurzfazit: Alle drei Befunde aus `review-code.2.md` sind sauber behoben, und die erneute frische Gesamtprüfung (inkl. Verifikation gegen die dekompilierte `msTools.Updater`-0.11.0-rc.8-API) ergab keine neuen substanziellen Befunde:

1. **Outcome-Mapping `PostUpdateInstall`** (`AdminEndpoints.cs` Z. 372-391): Nur `result.Code == AutoUpdateResultCode.InstallationStarted` rendert die Neustart-Seite; `Outcome == Failed` → `err=update-install-failed`, alle übrigen nicht-gescheiterten Outcomes (`Skipped`/`Canceled`/`NoUpdate`, Codes `AutoUpdateDisabled`/`Canceled`/`Skipped`) → PRG-Redirect `msg=update-install-not-started` mit neuem `StatusMessages`-Schlüssel (fest hinterlegter Text). Die Prüfreihenfolge (Failed vor Code-Prüfung) ist korrekt: Dekompilation von `AutoUpdateOrchestrator.InstallCoreAsync` bestätigt, dass der einzige `Outcome.Success`-Rückgabewert den Code `InstallationStarted` trägt und dass `PackageNotReady`/`PackageValidationFailed`/Lock-Konflikt/`!confirmDowntime` als `Outcome.Failed` zurückkommen — beide landen damit auf dem fachlich passenden Fehlerpfad (Detail über `LastError` in der Statussektion). Die statische Neustart-Seite kann nur noch erscheinen, wenn das Skript tatsächlich gestartet wurde.

2. **Konfigurierbarer Logpfad** (`AdminPagesComposer.cs` Z. 241-252): `RenderUpdateInstallStarted` nimmt `updateLogPath` als Parameter, befüllt aus `IAutoUpdatePackageStore.LogPath` in `PostUpdateInstall` (Z. 390). Konsistenz verifiziert: Die Bibliothek persistiert denselben Wert als `snapshot.UpdateLogPath` (`logPath = _packageStore.LogPath` in `InstallCoreAsync`) — Hinweistext und Statuszeile „Update-Protokoll" zeigen garantiert denselben Pfad, `Update:WorkingDirectory`-Abweichungen werden abgedeckt. Ausgabe über `PageShell.E` escaped.

3. **Race zwischen Override-Restore und `ApplySettingsAsync`** (`StatusServerUpdateService.cs` Z. 85-99): `ApplySettingsAsync` läuft jetzt unter `s_manualOverrideLock` — der kritische Abschnitt enthält ausschließlich die synchrone Mutation (`ApplySettings` + `ReplaceSource`), kein Netzwerk. Deadlock-Bewertung: ein einziges Semaphore, keine verschachtelte Akquisition (`ApplySettingsAsync` ruft nicht in `RunWithManualOverrideAsync`; `SetUpdateSettingsAsync`/`s_writeLock` ist bereits abgeschlossen, bevor der Override-Lock angefordert wird — keine Lock-Ordering-Problematik), `finally`-Release auf beiden Pfaden. Das locklose `WaitAsync()` ist fachlich richtig: Die Persistenz läuft in `PostUpdateSettings` vor dem Apply — ein abbrechbares Warten könnte Laufzeitzustand und Persistenz divergieren lassen; die Blockedauer ist durch HttpClient-Timeouts der Orchestrator-Aufrufe begrenzt. Dass `RunWithManualOverrideAsync` den Lock über den Netzwerk-Lauf hält, ist die fachlich korrekte Serialisierung (verhindert genau den dokumentierten Restore-Clobber). `ApplyPersistedUpdateSettingsAsync` ohne Lock bleibt unkritisch (vor `app.Run()`).

Testaussagekraft der neuen Tests: `UpdateInstall_CanceledInstall_RedirectsNotStarted` treibt den `BeforeInstall`-Abbruch über den echten `IAutoUpdateEventAggregator` durch die komplette Bibliothekspipeline (LocalFolder-Quelle → ReadyToInstall → Cancel → PRG-Redirect); `ApplySettings_DuringManualRun_WaitsAndKeepsPersistedValue` ist deterministisch (TCS-gesteuertes Warten, `Task.Delay` nur in der nicht-flaky Richtung); `RenderUpdateInstallStarted_ExplainsRestart` belegt den konfigurierbaren Pfad negativ gegen das alte Literal.

Übrige frische Prüfpunkte weiterhin erfüllt: `AutoUpdateGithubSource`-/`AutoUpdateLocalFolderSource`-/`AutoUpdateBuilder`-Signaturen gegen das Paket verifiziert (öffentliche Konstruktoren passen zum Aufruf in `StatusServerUpdateSourceFactory`/`SetInitialConfiguration`); `UpdateStateText` deckt alle neun `AutoUpdateState`-Member plus Default ab; Admin-Policy + `RequireAntiforgery()` auf allen Update-POST-Endpunkten (Z. 52-55) mit Testbeleg; `statusserver-update.json` wird vor dem Zippen in beide Publish-Roots geschrieben (release-metadata) bzw. mit `assets[]`-Schema erzeugt und in `release.yml` (npm-Pfad, `gh release create`, `--clobber`) sowie `staging-ci.yml` konsistent hochgeladen; `NuGet.config` + `.gitignore`-Ausnahme stellen die eingecheckte Paketquelle sicher; Datenerhalt (`appsettings*.json`, `data/*`, DB-Pfad inkl. `-wal`/`-shm`) durch vier Tests belegt. Kompilation des Server- und Testprojekts lokal verifiziert (0 Fehler/0 Warnungen).

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
- `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` (API via ilspycmd-Dekompilation verifiziert: `AutoUpdateResultCode`-/`AutoUpdateOutcome`-Werte, `InstallCoreAsync`-Rückgabepfade, `IAutoUpdatePackageStore.LogPath`, Source-/Builder-Signaturen)
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
