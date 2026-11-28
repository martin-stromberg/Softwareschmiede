# Tests und Testkonventionen

Testprojekt: `server/Softwareschmiede.StatusServer.Tests` — bereits Teil des Release-Gates (`release.yml` Z. 111–114, `staging-ci.yml` Z. 127). Framework: xunit.v3 mit `TestContext.Current.CancellationToken`; Assertions per FluentAssertions; HTML-Assert auf Composer-Ebene.

## Verzeichnisstruktur

| Pfad | Inhalt |
|------|--------|
| `Auth/` | `AdminSettingsTests` (Settings-Page GET/POST über HTTP), `AdminUserTests`, `AdminTokenTests`, `ProfileTests`, `SetupAndAuthTests` |
| `Pages/` | `AdminPagesComposerTests`, `AuthPagesComposerTests` — Composer-HTML-Assertionen ohne HTTP-Schicht |
| `Security/` | `ServerSettingsServiceTests`, `RelayTokenServiceTests`, `RelayTokenValidatorTests`, `UserAdminServiceTests` — Service-Unit-Tests über `StatusServerTestDb` (In-Memory) |
| `Status/`, `GitHub/`, `Relay/` | `InstanceSnapshotCacheTests`, `StatusPageComposerTests`, `GitHubStatusServiceTests`, `StatusRelayRoundtripTests` (E2E via SignalR) |
| `E2E/` | `StatusServerUserFlowTests` — komplette HTTP-Flüsse über Factory + `AuthTestClient` |
| `TestSupport/` | `AuthTestClient`, `StatusServerDbSeeder`, `StatusServerTestDb`, `TestTimeProvider` |

## Test-Infrastruktur

| Typ | Zweck für das Update-Feature |
|-----|------------------------------|
| `StatusWebApplicationFactory` | `WebApplicationFactory<Program>`; isolierte SQLite-Datei je Instanz im Temp-Ordner (Auto-Cleanup), `UseEnvironment(Production)`, `UseSetting("Data:DatabasePath", …)` — **exakt der Mechanismus, mit dem Tests die `Update`-Sektion deaktivieren/überschreiben können** (`Update:Enabled=false`, `Update:HostedServicesEnabled=false`, eigener `DownloadPath` ins Temp-Verzeichnis). `DatabasePathOverride` erlaubt Neustart-Persistenz-Tests mit zwei Factory-Instanzen auf demselben Pfad |
| `AuthTestClient` | Cookie-fähiger HttpClient mit `HandleCookies`, `AllowAutoRedirect=false` (Redirects assertierbar); `RefreshAntiforgeryAsync(path)` extrahiert das Antiforgery-Hidden-Field per Regex aus dem gerenderten HTML; `PostFormAsync` sendet urlencoded-Formulare mit Token; `FollowRedirectAsync` für PRG; `RegisterFirstUserAsync`/`LoginAsync`/`LogoutAsync` — deckt das komplette Admin-Endpoint-Testmuster ab (auch neue POST-Endpunkte unter `/admin/*`) |
| `StatusServerDbSeeder` | Scope-basierter Zugriff auf den Factory-`IServiceProvider`: `AddServerSettingAsync(key, value)` (direkter Settings-Seed — so würden Update-Settings geseedet), `CreateUserAsync` (mit Rolle), `SetSessionDurationAsync`/`GetSessionDurationAsync` (Parallele zu künftigen Update-Settings-Methoden), User-/Token-Helfer, `MutateWithConcurrentChangeAsync` |
| `StatusServerTestDb` | In-Memory-SQLite-`StatusServerDbContext` für reine Unit-Tests ohne Web-Stack (ServerSettingsService-Persistenz) |
| `TestTimeProvider` | Injizierbarer `TimeProvider`-Ersatz — relevant, weil msTools-Komponenten `TimeProvider` nutzen und `TimeProvider.System` bereits in `Program.cs` registriert ist |

## Spezifische Testfallen bei msTools.Updater-Integration

- **Hosted Services laufen in der WebApplicationFactory mit:** Sobald `builder.UseAutoUpdate(...)` in `Program.cs` steht, startet die Factory auch `AutoUpdateCheckerService`/`AutoUpdateSchedulerService`. Ohne Gegenmaßnahme würden Tests echte GitHub-Anfragen auslösen bzw. auf die Source-Konfiguration warten. Abhilfe: `DisableHostedServices()` in `UseAutoUpdate` (unwirksam, wenn global gewünscht) oder per `UseSetting` in der Factory `Update:Enabled=false` bzw. `Update:HostedServicesEnabled=false` — die Factory-Methode `ConfigureWebHost` ist der richtige Ort. Für Update-spezifische Integrationstests ist zusätzlich `UseLocalFolderSource` (Temp-Verzeichnis mit eigenem `update.json` + ZIP) der vorgesehene Testpfad der Bibliothek.
- **`AutoUpdateOptions` ist ein normaler Singleton** — in `ConfigureTestServices` ersetzbar/vorkonfigurierbar; FinanceManager testet `AutoUpdateStatusService` mit gemocktem `IAutoUpdateStateStore` und `IInstalledVersionProvider` (Moq), Adapter-Schichten komplett ohne Netzwerk/Filesystem.
- **Persistierte Update-Einstellungen in `ServerSettings`** lassen sich exakt wie `SessionDurationMinutes` testen (Seeder + Service-Unit-Tests, keine Migration nötig).
- **Antiforgery + AdminOnlyPolicy** gelten für die neuen POST-Endpunkte identisch: `PostFormAsync` ohne Token → 400; ohne Admin-Rolle → 403/Redirect (bestehende `AdminSettingsTests` als Vorlage).
- **Composer-Tests** für die `RenderSettings`-Erweiterung laufen ohne HTTP (`AdminPagesComposerTests`-Muster, FluentAssertions auf HTML-Substrings).

## Formatierung

Der lokale `pre-push`-Hook und die PR-CI prüfen `dotnet format Softwareschmiede.slnx --verify-no-changes` — neue Dateien müssen formatiert sein (Verhaltensregel; im Inventar als Hinweis vermerkt).
