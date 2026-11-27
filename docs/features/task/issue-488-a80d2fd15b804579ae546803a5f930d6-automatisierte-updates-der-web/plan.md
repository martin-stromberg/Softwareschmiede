# Umsetzungsplan: Automatisierte Updates der Status-Webanwendung mit msTools.Updater (Issue #488)

## Übersicht

Der StatusServer (`server/Softwareschmiede.StatusServer`) erhält eine Self-Update-Funktion auf Basis der Bibliothek `msTools.Updater` (Version `0.11.0-rc.8`, eingebunden als lokale NuGet-Quelle `external/msTools.Updater/` + `NuGet.config` nach FinanceManager-Muster). Kanal (Stable/Pre-Release) und Automatisierungsmodus (Aus / nur prüfen / prüfen und installieren) werden in der bestehenden Admin-Einstellungsseite `/admin/settings` konfiguriert und über `ServerSettingsService` in der `ServerSettings`-Tabelle persistiert; zusätzlich gibt es manuelle Aktionsbuttons (prüfen, installieren, Lock zurücksetzen). Die Release-Pipeline ergänzt `release-metadata.json` in den StatusServer-Paketen und erzeugt ein separates Server-Manifest `statusserver-update.json`. Betroffen sind `Program.cs`, `AdminEndpoints`, `AdminPagesComposer`, `StatusMessages`, `ServerSettingsService`, `appsettings.json`, `StatusWebApplicationFactory`, `.github/actions/build-and-package/action.yml`, `release.yml`, `staging-ci.yml` und `server/README.md`. Es entsteht ein neuer Ordner `server/Softwareschmiede.StatusServer/Update/` mit der Adapterschicht um die Bibliothek.

## Geklärte Ausgangspunkte (aus Inventur/Referenz, nicht mehr offen)

- **Manifest-Struktur:** `IAutoUpdatePlatformResolver.SelectPackage` wählt das Paket nur über `platform` + `runtimeIdentifier` — `update.json` mit zwei `win-x64`-Assets (`release.zip` + `statusserver-win-x64.zip`) wäre uneindeutig. ⇒ Separates Server-Manifest `statusserver-update.json` mit beiden Server-Assets; `update.json` bleibt unverändert.
- **Installierte Version:** kommt aus `release-metadata.json` im Anwendungsstamm (`ReleaseMetadataInstalledVersionProvider`), **nicht** aus `version.json`. Die Pipeline muss `release-metadata.json` je RID in `publish-statusserver-win`/`publish-statusserver-linux` schreiben.
- **Neustart-Mechanik:** wird vom Updater-Skript getragen, nicht vom Service-Manager — Linux: `systemd-run`-Transient-Unit (`UpdateUnitName`), Skript führt `systemctl stop` → Austausch → `systemctl start` aus; Windows: detached Skript via WMI `Win32_Process.Create`, IIS-Erkennung automatisch (`app_offline.htm`), Windows-Dienst (`ServiceName`) oder Executable (`ExecutablePath`). Gesperrte Dateien: Retry beim Kopieren im Skript.
- **Automatisierungs-Auslöser:** `AutoUpdateCheckerService` (periodische Prüfung/Download im konfigurierten Zeitfenster) + `AutoUpdateSchedulerService` (tägliche Installation zu `ScheduledInstallTime`). Es gibt keine Prüfung beim Anwendungsstart — die Referenzen kennen diesen Modus nicht.
- **Fehlerverhalten:** Die Bibliothek wirft keine Exceptions bei Update-Fehlschlägen, sondern liefert `AutoUpdateResult`/`AutoUpdateErrorCode`; Status persistiert als `updates/status.json`, Skript-Log als `updates/update.log`, Fehlermarker `update.failed` (Rekonziliation nach Neustart). UI zeigt letzten Status + Fehler.
- **Einbindung:** lokales `.nupkg` unter `external/msTools.Updater/` + `NuGet.config`-Feed (PO-Entscheidung, FinanceManager-Vorlage).
- **Wiederverwendung Desktop-Stack:** kein `ProjectReference` auf `src/Softwareschmiede` (zieht EF-Core-Sqlite/SignalR-Client/Plugin-Contracts transitiv mit); msTools.Updater liefert alle benötigten Primitive. Kanal-Nomenklatur Stable/ReleaseCandidate wird serverseitig nachgebildet.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Server-Manifest | Separates Release-Asset `statusserver-update.json` im Rezepte-Format mit `statusserver-win-x64.zip` + `statusserver-linux-x64.zip` | `SelectPackage`-Selektion nur über Plattform+RID → Kollision mit `release.zip` in `update.json`. Desktop-Updater bleibt unverändert. |
| Bibliotheks-Einbindung | Eingechecktes `msTools.Updater.0.11.0-rc.8.nupkg` unter `external/msTools.Updater/` + `NuGet.config` im Repo-Root | PO-Entscheidung; FinanceManager-Muster; CI-`dotnet restore` sieht die Quelle automatisch. |
| Adapter-Schicht | Eigene dünne Schicht `Update/` (Options, Mapper, Source-Factory, Service) analog `FinanceManager.Web/Services/Updates/` — kein `ProjectReference` auf `src/Softwareschmiede` | Transitive Paketlast (EF-Core, SignalR, Plugin-Contracts) und Desktop-spezifische Mechanik (PowerShell-Skript, pfadbasierte Kanalwahl) sind nicht übertragbar; die Bibliothek deckt Check/Download/Install/Neustart vollständig ab. |
| Hintergrunddienst | **Kein** eigener `IHostedService` — die Bibliotheks-`AutoUpdateCheckerService`/`AutoUpdateSchedulerService` übernehmen; Steuerung über `AutoUpdateOptions` zur Laufzeit | Referenz (FinanceManager/Rezepte/VideoPlayer) nutzt ausschließlich die Bibliotheks-Services; ein eigener Dienst wäre redundant. |
| Persistenz der Admin-Einstellungen | `ServerSettings`-Tabelle via `ServerSettingsService` (drei Schlüssel: Kanal, Modus, Installationszeit) — bewusste Abweichung vom FinanceManager-`settings.json`-Store | Anforderung legt `ServerSettingsService` explizit fest; Key/Value-Tabelle braucht keine Migration; Settings überleben Updates (liegen in `data/`). |
| Automatisierungsmodus | Neuer Enum `StatusServerAutomationMode` (`Off`/`CheckOnly`/`CheckAndInstall`), gemappt auf `Enabled`/`EnableAutomaticDownload`/`EnableAutomaticInstallation`/`ScheduledInstallTime` | msTools kennt nur diese Stellgrößen; `CheckAndInstall` benötigt zwingend `ScheduledInstallTime` (sonst feuert der Scheduler nie). |
| Kanal | `AllowPrereleaseUpdates` + `includePrereleases` der `AutoUpdateGithubSource` synchron halten; Quelle bei Kanalwechsel neu bauen | Beide Stellen müssen denselben Kanal sehen (Quelle filtert Releases, Orchestrierer blockt Prerelease-Manifeste); `AutoUpdateOptions.Source` ist zur Laufzeit ersetzbar (FinanceManager-Muster). |
| GitHub-Token | `StatusServerUpdateSourceFactory` baut `AutoUpdateGithubSource` über den öffentlichen Konstruktor mit einem `IHttpClientFactory`-Client, der das optionale `GitHub:Token` als Bearer trägt; ohne Token identisch, nur ohne Auth-Header | `UseGithubSource` hat keinen Token-Parameter; nur der Konstruktor-Weg erlaubt einen externen (factory-verwalteten, nicht vom Source disposed) HttpClient mit Auth-Header. Rate-Limit-Schutz ohne neue Konfiguration. |
| Manuelle Aktionen bei `Enabled=false` | `RunWithManualOverrideAsync`-Muster: `AutoUpdateOptions.Enabled` temporär auf `true` setzen und danach zurückstellen | Wie FinanceManager — ein explizit ausgelöster Admin-Klick muss auch bei deaktivierter Automation laufen, während die Hintergrund-Services weiter deaktiviert bleiben. |
| Antwort der Installations-POST | Dedizierte Ergebnisseite „Update wird installiert — der Server startet neu" statt PRG-Redirect | Nach Skriptstart wird der Prozess gestoppt (`systemctl stop`/Skript-Beendigung); ein Redirect-Ziel könnte nicht mehr ausgeliefert werden. |
| Prüf-Zeitfenster | `SourceCheck.Interval` = täglich (24×60 min) + Tagesfenster aus `appsettings` (`Update:SourceCheckStartTime`/`EndTime`, Default 20:00–06:00); Über-Mitternacht-Fenster in zwei Ranges gespalten (Mapper) | FinanceManager-Konvention; Fenster über Mitternacht verwirft der `AutoUpdateOptionsValidator` sonst. Fenster bleibt Deployment-Konfiguration, nicht Admin-UI — die UI enthält nur Kanal, Modus, Installationszeit. |
| Geschützte Dateien | Fluent `PreserveFile("appsettings*.json")` + `PreserveFile("data/*")` beim `UseAutoUpdate`-Aufruf | Anforderung „`data/` und `appsettings*.json` unverändert"; `Preserve` = Backup+Restore im Installationsskript (in Rezepte-/VideoPlayer-Deployments nachgewiesen). |
| Lock-Verwaltung | Admin-Aktion „Update-Sperre zurücksetzen" (`ResetLockAsync` über `IAutoUpdatePackageStore.IsLockStale`/`DeleteLockAsync`) | Referenz-Fehlerverhalten: ein verwaister `update.lock` blockiert sonst alle weiteren Updates dauerhaft; UI zeigt Lock-Status an. |
| Statusanzeige | `IAutoUpdateOrchestrator.GetStatusAsync` (mit Neustart-Rekonziliation) → `UpdateStatusMapper` → flaches `StatusServerUpdateStatus`-DTO für den Composer | `GetStatusAsync` rekonziliert `update.failed`-/Lock-Marker nach Neustart — die Referenz nutzt denselben Pfad; DTO hält Bibliothekstypen aus dem Composer. |

## Programmabläufe

### Anwendungsstart: Registrierung + Anwenden persistierter Einstellungen

1. `Program.cs` liest die `Update`-Sektion in `StatusServerUpdateOptions` und ruft `builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(updateOptions))` — die Extension (`Update/AutoUpdateApplicationExtensions.cs`) führt aus: `BindConfiguration("Update")`, `WithUpdateUnitName("SoftwareschmiedeStatusServerUpdate")`, `WithDownloadPath(updateOptions.WorkingDirectory)`, `WithSourceCheck(24*60, BuildSourceCheckTimeRanges(start, end))`, `PreserveFile("appsettings*.json")`, `PreserveFile("data/*")`, je nach `SourceType` `UseLocalFolderSource(path, manifestAssetName)` oder `UseGithubSource(owner, name, manifestAssetName)`.
2. `Program.cs` registriert zusätzlich: Named `HttpClient` `StatusServerUpdate` (BaseAddress `https://api.github.com/`, UserAgent `softwareschmiede-status-server/1.0`, Bearer aus `GitHub:Token` falls gesetzt), `StatusServerUpdateSourceFactory` (Singleton), `UpdateStatusMapper` (Singleton), `StatusServerUpdateService` (scoped).
3. Nach `builder.Build()` und dem bestehenden `StatusServerDatabaseInitializer.InitializeAsync`-Block, vor `app.Run()`: `await app.ApplyPersistedUpdateSettingsAsync()` — in einem Scope `ServerSettingsService.GetUpdateSettingsAsync` laden → `StatusServerAutoUpdateOptionsMapper.ApplySettings(autoUpdateOptions, settings)` auf den Singleton anwenden → `options.Source` per `StatusServerUpdateSourceFactory` neu aufbauen (token-aware, `includePrereleases` passend zum persistierten Kanal). Fehler werden geloggt und geschluckt — korrupte Einstellungen blockieren den Start nicht.
4. `app.Run()` startet `AutoUpdateCheckerService`/`AutoUpdateSchedulerService` — sie lesen `AutoUpdateOptions` erst beim Start und sehen so die persistierten Admin-Einstellungen, nicht nur die appsettings-Defaults.

Beteiligte Klassen/Komponenten: `Program`, `AutoUpdateApplicationExtensions`, `StatusServerUpdateOptions`, `StatusServerUpdateSourceFactory`, `StatusServerAutoUpdateOptionsMapper`, `ServerSettingsService`, `AutoUpdateOptions`, `AutoUpdateCheckerService`, `AutoUpdateSchedulerService`.

### Admin-Einstellungen speichern (Kanal + Automatisierung)

1. `POST /admin/settings/update` (`AdminOnlyPolicy` + `RequireAntiforgery`): `ReadFormAsync` → `channel`, `mode`, `installTime` lesen.
2. Validierung: `channel` ∈ {`stable`,`prerelease`}, `mode` ∈ {`off`,`check`,`install`}; `installTime` (HH:mm) Pflicht + parsebar bei `mode=install`. Fehler → Einstellungsseite direkt mit Fehlermeldung rendern (bestehendes Muster).
3. `ServerSettingsService.SetUpdateSettingsAsync(settings)` — ein Lock-geschützter Upsert der drei Schlüssel.
4. `StatusServerUpdateService.ApplySettingsAsync(settings)`: `StatusServerAutoUpdateOptionsMapper.ApplySettings` auf `AutoUpdateOptions` + `options.Source` via `StatusServerUpdateSourceFactory` neu aufbauen → Redirect `/admin/settings?msg=update-settings-saved`.

Beteiligte Klassen: `AdminEndpoints`, `ServerSettingsService`, `StatusServerUpdateService`, `StatusServerAutoUpdateOptionsMapper`, `StatusServerUpdateSourceFactory`, `AutoUpdateOptions`.

### Manuelle Update-Prüfung (Aktionsbutton)

1. `POST /admin/settings/update/check` → `StatusServerUpdateService.CheckAndDownloadAsync`.
2. `RunWithManualOverrideAsync` (Enabled-Override) → `IAutoUpdateOrchestrator.CheckForUpdateAsync` → bei `Outcome == UpdateAvailable`/`Success` mit Paket `DownloadAsync` (Download + SHA-256-Verifikation gegen `statusserver-update.json`).
3. Ergebnis: `Outcome == Failed` → Redirect `?err=update-check-failed` (Detailfehler steht im Status-Snapshot/`LastError`); sonst `?msg=update-check-done`. Anzeige von `AvailableVersion`/`ReadyToInstall` über die Status-Sektion der Seite.

Beteiligte Klassen: `AdminEndpoints`, `StatusServerUpdateService`, `IAutoUpdateOrchestrator`, `AutoUpdateStatusService`, `AutoUpdateOptions`.

### Manuelle Installation (Aktionsbutton)

1. `POST /admin/settings/update/install` mit Checkbox-Feld `confirmDowntime` — fehlt es → Fehlerseite (kein Update gestartet).
2. `StatusServerUpdateService.StartInstallAsync`: Snapshot prüfen — `UpdateAvailable` ohne Download → erst `DownloadAsync`; dann `IAutoUpdateOrchestrator.InstallAsync(confirmDowntime: true, force: false)` (Enabled-Override).
3. `Outcome == InstallationStarted`/`Success` → `AdminPagesComposer.RenderUpdateInstallStarted` (statische Seite „Server startet neu", kein Redirect). `Outcome == Failed` → Redirect `?err=update-install-failed`.
4. Das Installationsskript (`.sh`/`.ps1`) läuft detached außerhalb des Prozesses: Service/Host stoppen → Backup geschützter Dateien → Paket entpacken/kopieren → `data/*` + `appsettings*.json` restaurieren → Dienst/Prozess starten → Workspace-Bereinigung. Fehlschlag → `update.failed`-Marker; beim nächsten `GetStatusAsync` → State `Failed` + `LastError` sichtbar.

Beteiligte Klassen: `AdminEndpoints`, `StatusServerUpdateService`, `IAutoUpdateOrchestrator`, `IAutoUpdatePackageStore`, `AdminPagesComposer`, msTools-Skriptgenerator.

### Automatische Prüfung und Installation (Modus `CheckOnly`/`CheckAndInstall`)

1. `AutoUpdateCheckerService` (Bibliothek): periodisch gemäß `SourceCheck.Interval`/`TimeRanges` → `CheckForUpdateAsync`; bei neuerem Paket und `EnableAutomaticDownload` → `DownloadAsync` inkl. SHA-256-Verifikation. Installiert nie.
2. `AutoUpdateSchedulerService` (Bibliothek): täglich zu `ScheduledInstallTime` → Installation eines `ReadyToInstall`-Pakets, sofern `EnableAutomaticInstallation`.
3. Mapping: `Off` → `Enabled=false`; `CheckOnly` → `Enabled=true`, `EnableAutomaticDownload=false`, `EnableAutomaticInstallation=false`; `CheckAndInstall` → `Enabled=true`, `EnableAutomaticDownload=true`, `EnableAutomaticInstallation=true`, `ScheduledInstallTime=<persistierte Zeit>`.

Beteiligte Klassen: `AutoUpdateCheckerService`, `AutoUpdateSchedulerService`, `AutoUpdateOptions`, `StatusServerAutoUpdateOptionsMapper`.

### Update-Sperre zurücksetzen (Admin)

1. `POST /admin/settings/update/lock-reset` → `StatusServerUpdateService.ResetLockAsync`.
2. `IAutoUpdatePackageStore.GetLockCreatedAtAsync` → kein Lock oder `IsLockStale` = false → Redirect `?err=update-lock-reset-failed`; sonst `DeleteLockAsync` + Status-Korrektur via `AutoUpdateStatusService.UpdateAsync` → Redirect `?msg=update-lock-reset`.

Beteiligte Klassen: `AdminEndpoints`, `StatusServerUpdateService`, `IAutoUpdatePackageStore`, `AutoUpdateStatusService`.

### Statusanzeige in `/admin/settings`

1. `GET /admin/settings`: zusätzlich `ServerSettingsService.GetUpdateSettingsAsync` + `StatusServerUpdateService.GetStatusAsync` (`IAutoUpdateOrchestrator.GetStatusAsync` → `UpdateStatusMapper` → `StatusServerUpdateStatus`).
2. `AdminPagesComposer.RenderSettings` rendert neue Sektion „Updates": installierte Version (aus `IInstalledVersionProvider`, „unbekannt" falls keine `release-metadata.json`), Zustand, verfügbare Version, letzte Prüfung, letzter Fehler, Lock-Status (mit Reset-Button bei verwaistem Lock) sowie Formular für Kanal/Modus/Installationszeit und die Aktionsbuttons.

Beteiligte Klassen: `AdminEndpoints`, `AdminPagesComposer`, `StatusServerUpdateService`, `UpdateStatusMapper`, `IInstalledVersionProvider`.

## Akzeptanzszenarien

### Happy Path

1. Admin meldet sich an, öffnet `/admin/settings`: Sektion „Updates" zeigt installierte Version (aus `release-metadata.json`), Zustand `Idle`, Kanal `Stable`, Modus `Off`.
2. Admin wählt Kanal `Pre-Release` + Modus `Nur prüfen` → Speichern → Redirect `?msg=update-settings-saved`; `AutoUpdateOptions` zeigt `AllowPrereleaseUpdates=true`, `Enabled=true`; `ServerSettings` enthält die drei Schlüssel (über Seeder/DB verifizierbar).
3. Button „Jetzt nach Update suchen" → Check gegen die konfigurierte Quelle → Download + SHA-256-Verifikation → Status-Sektion zeigt `ReadyToInstall` + verfügbare Version (bei `Off`/`Enabled=false` läuft der manuelle Klick dank Enabled-Override trotzdem).
4. Button „Update jetzt installieren" mit bestätigter Downtime → `InstallAsync` → Skript startet detached → Ergebnisseite „Server startet neu" → Prozess/Dienst startet neu (systemd-Transient-Unit bzw. Windows-Skript).
5. Nach dem Neustart: installierte Version = neue Version (aus neuer `release-metadata.json`), Status `Success`; `data/statusserver.db` (Benutzer, Tokens, Einstellungen) und `appsettings*.json` (inkl. lokaler Anpassungen) unverändert.
6. Nachvollziehbarkeit: Status-Sektion in `/admin/settings`, `updates/status.json`, `updates/update.log`, `ILogger`-Einträge.
7. Automatik-Pfad (Modus `CheckAndInstall` + Installationszeit): Checker lädt das Paket im Zeitfenster herunter, Scheduler installiert zur konfigurierten Uhrzeit — ohne Admin-Eingriff.

### Fehlschlag-Szenarien

- **Hash-Mismatch:** manipuliertes Paket → `AutoUpdateErrorCode.HashMismatch` → State `Failed`, `LastError` in der UI, kein Dateiaustausch.
- **Netzfehler/Quelle nicht erreichbar:** `SourceUnavailable`/`ManifestDownloadFailed` → Fehler in Status-Sektion + Log; Server läuft unverändert weiter, nächster Check regulär.
- **Gesperrte Dateien (Windows):** Skript wiederholt den Kopiervorgang; dauerhafter Fehlschlag → `update.failed`-Marker → `InstallationFailed` nach Neustart-Rekonziliation; geschützte Dateien werden aus dem Backup restauriert.
- **Installations-Lock:** aktiver/verwaister Lock blockiert weitere Updates (`LockActive`); Admin sieht Lock-Status + Alter in der UI und kann bei `IsLockStale` den Reset-Button nutzen.
- **Fehlende Downtime-Bestätigung:** Installations-POST ohne `confirmDowntime` → Fehlerseite, es wird nichts gestartet.

### Datenerhalt

Nach erfolgreichem Update gilt: `data/` (SQLite-Datenbank mit Benutzern, Relay-Tokens, `ServerSettings` inkl. der Update-Einstellungen) unverändert; `appsettings*.json` mit lokalen Anpassungen (z. B. `GitHub:Token`, `Data:DatabasePath`, Ports) unverändert; `updates/` Workspace bereinigt (`pending/`/`staging/` leer), `status.json` meldet `Success` bzw. die neue installierte Version.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `Update/StatusServerUpdateOptions` | Options-Klasse | Host-Bindung der `Update`-Sektion: `RepositoryOwner` (`martin-stromberg`), `RepositoryName` (`Softwareschmiede`), `ManifestAssetName` (`statusserver-update.json`), `WorkingDirectory` (`updates`), `SourceType` (`Github`/`LocalFolder`), `LocalFolderPath`, `SourceCheckStartTime`/`EndTime` (Default 20:00/06:00), `SectionName`-Konstante |
| `Update/StatusServerUpdateChannel` | Enum | `Stable`, `ReleaseCandidate` (Persistenzwerte `stable`/`prerelease`) |
| `Update/StatusServerAutomationMode` | Enum | `Off`, `CheckOnly`, `CheckAndInstall` (Persistenzwerte `off`/`check`/`install`) |
| `Update/StatusServerUpdateSettings` | Datenmodellklasse (record) | Persistierte Admin-Einstellungen: `Channel`, `Mode`, `ScheduledInstallTime` (`TimeOnly?`) |
| `Update/StatusServerUpdateStatus` | Datenmodellklasse (record) | UI-DTO: `InstalledVersion`, `AvailableVersion`, `State` (`AutoUpdateState`), `LastCheckedAt`, `LastError`, `IsLocked`, `IsLockStale`, `ReadyToInstall`, `UpdateLogPath` |
| `Update/StatusServerAutoUpdateOptionsMapper` | Klasse (static) | `ApplySettings(AutoUpdateOptions, StatusServerUpdateSettings)` (Enabled/AllowPrereleaseUpdates/Download+Installation-Flags/ScheduledInstallTime/SourceCheck), `BuildSourceCheckTimeRanges(TimeOnly, TimeOnly)` inkl. Über-Mitternacht-Split |
| `Update/StatusServerUpdateSourceFactory` | Klasse (Singleton) | `Create(settings)` → `IAutoUpdateSource`: `AutoUpdateGithubSource` per Konstruktor mit `IHttpClientFactory`-Client `StatusServerUpdate` (Bearer aus `GitHub:Token`, `IAutoUpdatePlatformResolver`, `includePrereleases` aus Kanal) oder `AutoUpdateLocalFolderSource` bei `SourceType=LocalFolder` |
| `Update/StatusServerUpdateService` | Klasse (scoped) | Adapter über `IAutoUpdateOrchestrator`, `AutoUpdateStatusService`, `IAutoUpdatePackageStore`, `AutoUpdateOptions`: `GetStatusAsync`, `ApplySettingsAsync`, `CheckAndDownloadAsync`, `StartInstallAsync(confirmDowntime)`, `ResetLockAsync`; interner `RunWithManualOverrideAsync`-Enabled-Override |
| `Update/UpdateStatusMapper` | Klasse (Singleton) | `AutoUpdateStatusSnapshot` + `IInstalledVersionProvider` + `IAutoUpdatePlatformResolver` → `StatusServerUpdateStatus` |
| `Update/AutoUpdateApplicationExtensions` | Klasse (static) | `SetInitialConfiguration(this AutoUpdateBuilder, StatusServerUpdateOptions)` und `ApplyPersistedUpdateSettingsAsync(this WebApplication)` |

## Änderungen an bestehenden Klassen

### `ServerSettingsService` (Klasse)

- **Neue Konstanten:** `UpdateChannelKey` (`"UpdateChannel"`), `UpdateAutomationModeKey` (`"UpdateAutomationMode"`), `UpdateInstallTimeKey` (`"UpdateInstallTime"`); Defaults `DefaultUpdateChannel`/`DefaultUpdateMode`/`DefaultUpdateInstallTime` (03:00).
- **Neue Methoden:** `GetUpdateSettingsAsync` → `StatusServerUpdateSettings` (fehlende/korrupte Werte → Defaults); `SetUpdateSettingsAsync(StatusServerUpdateSettings)` — Upsert aller drei Schlüssel unter dem bestehenden `s_writeLock`.

### `AdminEndpoints` (Klasse)

- **Geänderte Methoden:** `GetSettings` — zusätzlicher `err`-Query-Parameter (bisher nur `msg`), lädt Update-Settings + Status und gibt sie an `RenderSettings` weiter; `RenderSettingsAsync` — neue Parameter `StatusServerUpdateSettings` + `StatusServerUpdateStatus`.
- **Neue Methoden (private Handler + Mapping in `MapAdminEndpoints`):** `PostUpdateSettings` (`POST /admin/settings/update`), `PostUpdateCheck` (`POST /admin/settings/update/check`), `PostUpdateInstall` (`POST /admin/settings/update/install`), `PostUpdateLockReset` (`POST /admin/settings/update/lock-reset`) — alle `AdminOnlyPolicy` + `RequireAntiforgery`, PRG über `RedirectWithMessage`/`RedirectWithError`; Fehlerfälle rendern die Seite direkt (Validierung) bzw. Redirect mit `err`-Schlüssel.

### `AdminPagesComposer` (Klasse)

- **Geänderte Methoden:** `RenderSettings` — neue Parameter `StatusServerUpdateSettings` + `StatusServerUpdateStatus`; neue Sektion „Updates" (Statusanzeige: installierte/verfügbare Version, Zustand, letzte Prüfung, letzter Fehler, Lock-Status; Formular: Kanal-Select, Modus-Select, Installationszeit-Input; Buttons: Einstellungen speichern, Jetzt prüfen, Jetzt installieren mit Downtime-Checkbox, Lock zurücksetzen bei verwaistem Lock). Alle Ausgaben HTML-escaped, Antiforgery-Hidden-Feld pro Formular.
- **Neue Methoden:** `RenderUpdateInstallStarted(PageUserContext)` — statische Ergebnisseite „Update wird installiert — der Server startet neu" mit Hinweis auf erneute Anmeldung.

### `StatusMessages` (Klasse)

- **Neue Konstanten + Texte:** `UpdateSettingsSaved` (`update-settings-saved`), `UpdateCheckDone` (`update-check-done`), `UpdateLockReset` (`update-lock-reset`), `UpdateCheckFailed` (`update-check-failed`), `UpdateInstallFailed` (`update-install-failed`), `UpdateLockResetFailed` (`update-lock-reset-failed`).

### `Program` (Program.cs)

- **Neue Aufrufe/Registrierungen:** `Update`-Sektion lesen; `builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(updateOptions))`; `AddHttpClient("StatusServerUpdate", …)` (UserAgent, Bearer aus `GitHub:Token`); `AddSingleton<StatusServerUpdateSourceFactory>()`; `AddSingleton<UpdateStatusMapper>()`; `AddScoped<StatusServerUpdateService>()`; nach DB-Initialisierung `await app.ApplyPersistedUpdateSettingsAsync()` vor `app.Run()`.

### `Softwareschmiede.StatusServer.csproj` / `Softwareschmiede.StatusServer.Tests.csproj`

- **Neue Paketreferenzen:** `msTools.Updater` `0.11.0-rc.8` (StatusServer; Tests für Bibliothekstypen in Mocks) und `Moq` (nur Tests).

### `StatusWebApplicationFactory` (Testklasse)

- **Geänderte Methoden:** `ConfigureWebHost` — zusätzlich `UseSetting("Update:Enabled", "false")`, `UseSetting("Update:HostedServicesEnabled", "false")`, `UseSetting("Update:WorkingDirectory", <Temp-Pfad unter `_ownedDirectory`>) damit keine echten GitHub-Checks/Workspace-Schreibzugriffe in Produktivpfaden laufen.

## Datenbankmigrationen

Keine — die Update-Einstellungen nutzen die bestehende Key/Value-Tabelle `ServerSettings` (wie `SessionDurationMinutes`); neue Schlüssel brauchen kein Schema-Update.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `channel` (Form) | Muss `stable` oder `prerelease` sein | Einstellungsseite mit Fehlermeldung direkt rendern |
| `mode` (Form) | Muss `off`, `check` oder `install` sein | Einstellungsseite mit Fehlermeldung direkt rendern |
| `installTime` (Form) | Pflicht und als `HH:mm` parsebar bei `mode=install`; ignorierbar sonst | Einstellungsseite mit Fehlermeldung direkt rendern |
| `confirmDowntime` (Form, Install-POST) | Checkbox muss gesetzt sein | Einstellungsseite mit Fehlermeldung „Downtime bestätigen"; kein Update gestartet |
| Persistierte `ServerSettings`-Werte | Unbekannte/korrupte Werte beim Lesen → Default | Kein Fehler — `GetUpdateSettingsAsync` fällt auf `Stable`/`Off`/03:00 zurück |
| Lock-Reset | Nur wenn Lock existiert **und** `IsLockStale` | Redirect `?err=update-lock-reset-failed` |

## Konfigurationsänderungen

`appsettings.json` — neue Sektion `Update` (gebunden auf `StatusServerUpdateOptions` **und** via `BindConfiguration` auf `AutoUpdateOptions`):

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Update:Enabled` | `bool` | `false` | Master-Schalter der Automation (Admin-UI-Modus überschreibt persistiert) |
| `Update:HostedServicesEnabled` | `bool` | `true` | Registrierung der Bibliotheks-Hintergrunddienste (Tests: `false`) |
| `Update:RepositoryOwner` | `string` | `martin-stromberg` | GitHub-Owner der Release-Quelle |
| `Update:RepositoryName` | `string` | `Softwareschmiede` | GitHub-Repository der Release-Quelle |
| `Update:ManifestAssetName` | `string` | `statusserver-update.json` | Manifest-Asset im Release (separat von `update.json`) |
| `Update:WorkingDirectory` | `string` | `updates` | Workspace-Wurzel (Pakete, `status.json`, `update.log`, Locks) |
| `Update:SourceType` | `string` | `Github` | `Github` oder `LocalFolder` (Testpfad der Bibliothek) |
| `Update:LocalFolderPath` | `string?` | `null` | Lokale Update-Quelle für Tests/Offline |
| `Update:SourceCheckStartTime` / `SourceCheckEndTime` | `TimeOnly` | `20:00` / `06:00` | Tägliches Prüfzeitfenster (Deployment-Konfiguration) |
| `Update:ServiceName` / `ExecutablePath` | `string?` | `null` | Neustart-Ziel: Windows-Dienst-Name bzw. Exe-Pfad (Linux: Auto-Detect der systemd-Unit; IIS: automatisch) |
| `Update:HealthTimeoutSeconds` | `int` | `120` | Altersschwelle für stale Installations-Locks |
| `Update:MaxAssetBytes` | `long` | `536870912` | Max. Paketgröße |
| `Update:EnableAutomaticDownload` | `bool` | `true` | Auto-Download bei Fund (Default; Modus-Mapping überschreibt persistiert) |
| `Update:EnableAutomaticInstallation` | `bool` | `false` | Auto-Installation zum `ScheduledInstallTime` |
| `Update:StopHostAfterScriptStart` | `bool` | `false` | Host-Selbstop nach Skriptstart (Default: Skript steuert Stopp/Start) |

Persistierte Schlüssel (`ServerSettings`-Tabelle, Admin-UI): `UpdateChannel` (`stable`/`prerelease`), `UpdateAutomationMode` (`off`/`check`/`install`), `UpdateInstallTime` (`HH:mm`, Default `03:00`). Das optionale `GitHub:Token` der bestehenden `GitHubOptions` wird vom Update-HttpClient mitverwendet — kein neuer Token-Eintrag.

## Seiteneffekte und Risiken

- **Alle WebApplicationFactory-Tests:** Sobald `builder.UseAutoUpdate` in `Program.cs` steht, würden `AutoUpdateCheckerService`/`AutoUpdateSchedulerService` in Tests echte GitHub-Anfragen starten — die Factory-Anpassung (`Update:Enabled=false`, `Update:HostedServicesEnabled=false`, Temp-`WorkingDirectory`) ist Pflicht und betrifft sämtliche Factory-basierten Tests (kein Testcode ändert sich, aber die Hermetik hängt daran).
- **Restore-Verhalten:** `NuGet.config` im Repo-Root gilt für alle Projekte; die lokale Quelle wird nur ergänzt (nuget.org bleibt konfiguriert). Das `nupkg` muss in Git eingecheckt werden — `external/` muss von `.gitignore` nicht ausgeschlossen sein (prüfen).
- **TFM-Gefälle:** msTools.Updater zielt `net10.0` (Extensions-Pakete 10.0.7), StatusServer `net11.0` — referenzierbar, Paketversionen werden auf 11.x hochgezogen; Compiler-Warnung/Restore-Verhalten beim ersten Build beobachten.
- **AdminPagesComposerTests:** `RenderSettings`-Signatur ändert sich → bestehende Composer-Test-Aufrufe müssen angepasst werden.
- **SignalR/Relay während Installation:** Laufende `/relay`-Verbindungen brechen beim Update-Neustart kurz ab; die App-Instanzen reconnecten selbstständig (Client-seitig, kein Eingriff nötig).
- **Persistierte Einstellungen vs. appsettings:** Persistierte Admin-Werte gewinnen nach jedem Start (`ApplyPersistedUpdateSettingsAsync`); appsettings-`Update:`-Werte gelten nur solange nichts persistiert ist bzw. für nicht persistierte Felder (Repository, WorkingDirectory, Zeitfenster, ServiceName).
- **Ohne `release-metadata.json` deployte Server:** installierte Version „unbekannt" → Update-Prüfung wirkungslos; erst Pakete aus der geänderten Pipeline sind update-fähig (Changelog/README-Hinweis).
- **`data/`-Wildcard:** `PreserveFile("data/*")` deckt Dateien direkt unter `data/` (db, `-wal`, `-shm`); geschachtelte Unterverzeichnisse existieren dort nicht.
- **CS1591 als Fehler:** Alle neuen public Member brauchen XML-Dokumentation.
- **Formatierung:** `dotnet format Softwareschmiede.slnx --verify-no-changes` (pre-push/CI) muss sauber bleiben.
- **IIS-Betrieb:** `app_offline.htm`-Mechanik und In-/Out-of-Process-Erkennung laufen automatisch; `Update:ServiceName`/`ExecutablePath` werden unter IIS von der Erkennung mit Warnung überschrieben (Doku im README).

## Umsetzungsreihenfolge

1. **Lokale NuGet-Quelle einrichten**
   - Voraussetzungen: `msTools.Updater.0.11.0-rc.8.nupkg` aus `C:\Users\Martin\.nuget\packages\mstools.updater\0.11.0-rc.8\` bzw. `D:\Repositories\FinanceManager\Current\external\msTools.Updater\` (vorhanden, muss nur kopiert werden).
   - Beschreibung: `external/msTools.Updater/msTools.Updater.0.11.0-rc.8.nupkg` einchecken; `NuGet.config` im Repo-Root mit Quelle `external/msTools.Updater` (nuget.org bleibt); `PackageReference msTools.Updater 0.11.0-rc.8` in `Softwareschmiede.StatusServer.csproj` und `Softwareschmiede.StatusServer.Tests.csproj`; `Moq` ins Testprojekt; `dotnet restore` verifizieren.
2. **`StatusServerUpdateOptions` + `appsettings.json`-Sektion**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `Update/StatusServerUpdateOptions.cs` anlegen; `Update`-Sektion in `appsettings.json` ergänzen (Defaults siehe Konfigurationstabelle).
3. **Update-Enums, Settings-Record und `ServerSettingsService`-Erweiterung**
   - Voraussetzungen: keine (unabhängig von Schritt 1/2, fachlich aber auf 2 aufbauend).
   - Beschreibung: `StatusServerUpdateChannel`, `StatusServerAutomationMode`, `StatusServerUpdateSettings` anlegen; `ServerSettingsService` um die drei Schlüssel + `GetUpdateSettingsAsync`/`SetUpdateSettingsAsync` erweitern.
4. **Mapper und Source-Factory**
   - Voraussetzungen: Schritte 1–3 (Bibliothekstypen `AutoUpdateOptions`/`AutoUpdateGithubSource`/`SourceCheckTimeRange` benötigt).
   - Beschreibung: `StatusServerAutoUpdateOptionsMapper` (`ApplySettings`, `BuildSourceCheckTimeRanges`) und `StatusServerUpdateSourceFactory` (token-aware GitHub-Quelle via `IHttpClientFactory`, LocalFolder-Quelle) anlegen.
5. **Status-DTO, `UpdateStatusMapper` und `StatusServerUpdateService`**
   - Voraussetzungen: Schritt 4.
   - Beschreibung: `StatusServerUpdateStatus`, `UpdateStatusMapper`, `StatusServerUpdateService` (Get/Apply/Check+Download/Install/ResetLock inkl. Enabled-Override) anlegen.
6. **`AutoUpdateApplicationExtensions` + `Program.cs`-Verdrahtung**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `SetInitialConfiguration` (BindConfiguration, UpdateUnitName, DownloadPath, SourceCheck, PreserveFile×2, Quelle) und `ApplyPersistedUpdateSettingsAsync`; `Program.cs` um `UseAutoUpdate`, HttpClient `StatusServerUpdate`, DI-Registrierungen und den Aufruf vor `app.Run()` erweitern.
7. **Admin-Oberfläche: `StatusMessages`, `AdminEndpoints`, `AdminPagesComposer`**
   - Voraussetzungen: Schritte 3–6.
   - Beschreibung: neue StatusMessage-Schlüssel; `GET /admin/settings` um `err`+Update-Daten erweitern; vier neue POST-Endpunkte; `RenderSettings`-Sektion „Updates" + `RenderUpdateInstallStarted`.
8. **Release-Pipeline: `release-metadata.json` + `statusserver-update.json` + Uploads**
   - Voraussetzungen: keine (unabhängig vom Server-Code).
   - Beschreibung: In `action.yml` nach dem StatusServer-Publish `release-metadata.json` je RID (`version`, `publishedAt`, `commitSha`, `repository`, `runtimeIdentifier`) in `publish-statusserver-win`/`publish-statusserver-linux` schreiben + Verify-Zusatz; neuen Schritt „Create StatusServer update manifest" (SHA-256/size/assetUrl je ZIP). `release.yml`: `statusserver-update.json` zu `RELEASE_EXTRA_ASSET_PATHS`, zum manuellen `gh release create` und zum `gh release upload` hinzufügen. `staging-ci.yml`: zum `gh release create` hinzufügen.
9. **Test-Infrastruktur und Tests**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: `StatusWebApplicationFactory.ConfigureWebHost` um die drei `Update:*`-`UseSetting`s erweitern; Unit-/Integrations-/E2E-Tests gemäß Testplan anlegen.
10. **Dokumentation `server/README.md`**
    - Voraussetzungen: Schritte 6–8.
    - Beschreibung: Abschnitt „Automatische Updates" (Admin-UI, `Update`-Konfigurationssektion, Restart-Verhalten je Plattform systemd/Windows-Dienst/IIS, geschützte Dateien, Workspace/Logs `updates/`, Lock-Reset, `release-metadata.json`-Voraussetzung).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetUpdateSettings_Defaults_*` | `Security/ServerSettingsServiceTests.cs` (erweitern) | Keine persistierten Werte → `Stable`/`Off`/`03:00`; korrupte Werte → Defaults |
| `SetUpdateSettings_Roundtrip_*` | `Security/ServerSettingsServiceTests.cs` (erweitern) | `SetUpdateSettingsAsync` persistiert alle drei Schlüssel; `GetUpdateSettingsAsync` liefert sie zurück (via `StatusServerTestDb`) |
| `ApplySettings_MapsModes` | `Update/StatusServerAutoUpdateOptionsMapperTests.cs` | `Off`→`Enabled=false`; `CheckOnly`→`Enabled`, kein Auto-Install; `CheckAndInstall`→`EnableAutomaticInstallation` + `ScheduledInstallTime` |
| `ApplySettings_MapsChannel` | `Update/StatusServerAutoUpdateOptionsMapperTests.cs` | `ReleaseCandidate` → `AllowPrereleaseUpdates=true` |
| `BuildSourceCheckTimeRanges_SplitsOvernight` | `Update/StatusServerAutoUpdateOptionsMapperTests.cs` | Fenster 20:00–06:00 → zwei Ranges je Tag |
| `Create_GithubSource_UsesToken` / `Create_GithubSource_WithoutToken` | `Update/StatusServerUpdateSourceFactoryTests.cs` | Bearer-Header des erzeugten Clients je `GitHub:Token`; `includePrereleases` passend zum Kanal; `ManifestAssetName` |
| `CheckAndDownload_*` / `StartInstall_*` / `ResetLock_*` | `Update/StatusServerUpdateServiceTests.cs` | Enabled-Override bei `Enabled=false`; Check→Download-Verkettung; Install lädt bei `UpdateAvailable` ohne Download nach; `confirmDowntime`-Durchreichung; Lock-Reset nur bei stale Lock (Mocks via `Moq` über `IAutoUpdateOrchestrator`/`IAutoUpdatePackageStore`/`AutoUpdateStatusService`) |
| `RenderSettings_ShowsUpdateSection` | `Pages/AdminPagesComposerTests.cs` (erweitern) | Kanal-/Modus-Selects mit aktuellen Werten, Aktionsbuttons, Statusfelder, Lock-Reset-Button nur bei stale Lock |
| `AdminUpdateSettings_Post_PersistsAndApplies` | `Auth/AdminUpdateSettingsTests.cs` (neu) | POST valid → Redirect `?msg=update-settings-saved`, Seeder-Assert auf `ServerSettings`, `AutoUpdateOptions`-Singleton reflektiert Werte; ungültige Werte → Fehlerseite |
| `AdminUpdateSettings_RequiresAdminAndAntiforgery` | `Auth/AdminUpdateSettingsTests.cs` | POST ohne Token → 400; Rolle `Ueberwacher` → 403/`access-denied` |
| `PostUpdateCheck_*` / `PostUpdateInstall_*` | `Auth/AdminUpdateSettingsTests.cs` | Check-POST → Redirect msg/err; Install-POST ohne `confirmDowntime` → Fehlerseite, kein `InstallAsync` |
| `TempUpdateSource` (Hilfsmethode) | `TestSupport/TempUpdateSource.cs` (neu) | Baut Temp-Verzeichnis mit `statusserver-update.json` + Fake-ZIP + echtem SHA-256 für `LocalFolder`-Source-Tests |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `StatusWebApplicationFactory` | `ConfigureWebHost` um `Update:Enabled=false`, `Update:HostedServicesEnabled=false`, `Update:WorkingDirectory=<Temp>` erweitern — sonst starten Bibliotheks-Hosted-Services echte Checks in jedem Test |
| `Pages/AdminPagesComposerTests.cs` (bestehende `RenderSettings`-Aufrufe) | Signaturänderung von `RenderSettings` (neue Parameter) |
| `Auth/AdminSettingsTests.cs` | `GetSettings` lädt jetzt Update-Status — kein Code-Eingriff nötig, aber der Handler hängt von `StatusServerUpdateService`/`IAutoUpdateOrchestrator` ab (Factory deckt das ab); bei Bedarf minimale Anpassung |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Happy Path bis `ReadyToInstall`: Admin registriert sich → setzt Kanal/Modus → POST „Jetzt prüfen" gegen `LocalFolder`-Quelle (Temp-Manifest + ZIP) → Status zeigt verfügbare Version/`ReadyToInstall` | `E2E/StatusServerUpdateFlowTests.cs` (neu, über `StatusWebApplicationFactory` mit `UseSetting("Update:SourceType","LocalFolder")` + `Update:LocalFolderPath`) | Admin stellt Kanal ein → manueller Button → Update wird gefunden → Download + SHA-256-Verifikation → Ergebnis in UI sichtbar |
| Fehlschlag: `LocalFolder`-Manifest mit falschem `sha256` → Check/Download schlägt fehl → `?err=update-check-failed` + `LastError` in der Seite; Server läuft weiter | `E2E/StatusServerUpdateFlowTests.cs` | Hash-Mismatch-Szenario, Fehler-Nachvollziehbarkeit |
| Persistenz über „Neustart": Einstellungen setzen → zweite Factory-Instanz auf demselben `DatabasePath` → `ApplyPersistedUpdateSettingsAsync` liefert identische Werte in `AutoUpdateOptions` | `E2E/StatusServerUpdateFlowTests.cs` (mit `DatabasePathOverride`) | Datenerhalt der Einstellungen über Neustart |

Die **tatsächliche Installation mit Prozess-Neustart** ist nicht test-automatisierbar, ohne das Update-Skript gegen die laufende Test-Host-Installation laufen zu lassen — reale E2E-Abnahme stattdessen manuell dokumentiert (kein FlaUI für den Server vorhanden, die Admin-UI ist ein HTML-Frontend ohne eigner UI-Test-Spur): StatusServer lokal bzw. als systemd-Unit/Windows-Dienst starten → Admin-Login → Kanal/Modus setzen → „Jetzt prüfen"/„Jetzt installieren" auslösen → Neustart + unverändertes `data/` + `appsettings*.json` anhand `updates/update.log`, `updates/status.json` und Screenshot der Einstellungsseite nachweisen.

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E/StatusServerUserFlowTests.cs` | Keine Code-Änderung — läuft über die angepasste `StatusWebApplicationFactory` (Hosted Services deaktiviert); nur indirekt betroffen |

## Offene Punkte

Keine.
