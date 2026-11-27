← [Zurück zur Übersicht](index.md)

# Status-Webseite — Architektur

## Beteiligte Komponenten

| Komponente | Typ | Rolle |
|------------|-----|-------|
| `Program` | Composition Root | DI-Registrierung (EF Core, Identity, Cookie-Auth, Antiforgery, Forwarded Headers) und Middleware-Pipeline |
| `StatusServerDbContext` | EF-Core-Kontext (`IdentityDbContext`) | Persistenz von Benutzern/Rollen (Identity-Schema), `RelayTokens`, `ServerSettings` in `data/statusserver.db` |
| `StatusServerDatabaseInitializer` | interner Initializer | Migrationen + Rollen-Seeding beim Start |
| `AuthEndpoints` / `AdminEndpoints` | Minimal-API-Endpunkte | Registrierung, Login/Logout, Profil; Admin-Verwaltung Benutzer/Tokens/Einstellungen |
| `EndpointSecurityExtensions` | interne Helfer | `RequireAntiforgery`-Filter (CSRF aller POST-Routen), `BuildUserContextAsync` |
| `StatusServerCookieEvents` | `CookieAuthenticationEvents` | Sitzungsdauer beim Ausstellen/Erneuern, Deaktivierungs-Prüfung und Rollen-Claim-Abgleich je Request, Setup-aware Umleitung |
| `UserAdminService` | Scoped-Service (Transaction Script) | Benutzeroperationen inkl. Letzter-Admin-Schutz und `SetupCompleted`-Guard |
| `RelayTokenService` / `RelayTokenValidator` | Scoped-Services | Token-Erzeugung (Hash-only), Widerruf; Prüfung eingehender `/relay`-Verbindungen |
| `RelayTokenPlaintextStore` | Singleton | Single-Use-Puffer für die Einmal-Anzeige des Token-Klartexts (2 min TTL) |
| `ServerSettingsService` | Scoped-Service | `SessionDurationMinutes` und die Update-Einstellungen (`UpdateChannel`, `UpdateAutomationMode`, `UpdateInstallTime`) aus `ServerSettings` |
| `AuthPagesComposer` / `AdminPagesComposer` / `PageShell` | statische Renderer | Serverseitig gerenderte, JS-freie HTML-Seiten (inkl. Update-Sektion und Neustart-Ergebnisseite) |
| `StatusRelayHub` | SignalR-Hub | Annahme der Snapshots; Token-Prüfung im `OnConnectedAsync` |
| `StatusServerUpdateService` | Scoped-Service (Adapter `Update/`) | Admin-Aktionen über `IAutoUpdateOrchestrator`: Status, Einstellungen anwenden, manueller Check/Download/Install (Enabled-Override), Lock-Reset |
| `StatusServerUpdateSourceFactory` | Singleton | Baut `AutoUpdateOptions.Source` token- und kanalbewusst (`AutoUpdateGithubSource` via `IHttpClientFactory`-Client `StatusServerUpdate`, oder `AutoUpdateLocalFolderSource`) |
| `StatusServerAutoUpdateOptionsMapper` / `UpdateStatusMapper` / `StatusServerUpdateSettingsCodec` | statisch / Singleton / statisch | Mapping persistierter Admin-Einstellungen auf `AutoUpdateOptions`; Snapshot→UI-DTO; String↔Enum-Konvertierung |
| `AutoUpdateCheckerService` / `AutoUpdateSchedulerService` | Bibliotheks-`IHostedService` (msTools.Updater) | Tägliche Quellprüfung/Download im Zeitfenster; tägliche Installation zur `ScheduledInstallTime` |

## Abhängigkeiten

- **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`) — lokale Datei
  `data/statusserver.db`; synchroner Zugriff pro Request, keine externe DB.
- **ASP.NET Core Identity** (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`)
  — Benutzer-/Rollenmodell, Passwort-Hashing, Lockout, Security Stamps.
- **Antiforgery** (`Microsoft.AspNetCore.Antiforgery`) — Framework-Middleware,
  CSRF-Schutz der POST-Formulare.
- **Reverse Proxy (optional)** — `ForwardedHeaders.All` übernimmt
  `X-Forwarded-Proto`/`X-Forwarded-For`; die Trusted-Proxy-Liste ist für
  Self-Hosting geleert.
- **GitHub-API** (unverändert) — `GitHubStatusService` für die
  Repository-Aggregation der Status-Seite.
- **msTools.Updater** (`0.11.0-rc.8`, lokale NuGet-Quelle
  `external/msTools.Updater/` + `NuGet.config`) — Self-Update:
  Release-Abfrage (GitHub oder `LocalFolder`), Download, SHA-256-Verifikation
  gegen `statusserver-update.json`, Dateiaustausch und Neustart über ein
  detached laufendes Skript (systemd-Transient-Unit /
  `app_offline.htm`-/Windows-Dienst-/Exe-Erkennung). Der Workspace
  `updates/` (`status.json`, `update.log`, `update.lock`, Pakete) liegt im
  ContentRoot; geschützte Dateien (`appsettings*.json`, `data/*`,
  `Data:DatabasePath` innerhalb des Anwendungsverzeichnisses) werden über
  `PreserveFile` gesichert und restauriert.

## Datenfluss

1. **Lesen:** `GET /` → Cookie-Auth (`PageAccess`) → `InstanceSnapshotCache` +
   `GitHubStatusService` → `StatusPageComposer.Render` → HTML.
2. **Schreiben (Verwaltung):** HTML-Formular → POST-Handler
   (Antiforgery-Filter) → `UserAdminService`/`RelayTokenService`/
   `ServerSettingsService` → `StatusServerDbContext` → SQLite; Antwort via
   Post/Redirect/Get.
3. **Snapshots:** App-Instanz → `wss://…/relay?access_token=…` →
   `RelayTokenValidator` (DB-Hash-Lookup) → `StatusRelayHub` →
   `InstanceSnapshotCache` (im Arbeitsspeicher).
4. **Update:** Admin-POST → `StatusServerUpdateService` →
   `IAutoUpdateOrchestrator` → `IAutoUpdateSource` (GitHub Releases bzw.
   LocalFolder) → Download+Verifikation in `updates/` → detached
   Installationsskript (Stopp → Austausch mit `PreserveFile`-Backup/Restore →
   Start). Einstellungen fließen über `ServerSettingsService` ↔
   `ServerSettings` und `StatusServerAutoUpdateOptionsMapper` →
   `AutoUpdateOptions` (Singleton).

## Diagramm

```mermaid
graph TD
    Browser -->|Cookie-Auth| EP[Auth/Admin-Endpunkte]
    EP --> UAS[UserAdminService]
    EP --> RTS[RelayTokenService]
    EP --> SSS[ServerSettingsService]
    UAS --> DB[(SQLite data/statusserver.db)]
    RTS --> DB
    SSS --> DB
    AppInstanz -->|wss + access_token| Hub[StatusRelayHub]
    Hub --> RTV[RelayTokenValidator]
    RTV --> DB
    Hub --> Cache[InstanceSnapshotCache]
    CE[StatusServerCookieEvents] --> DB
    EP --> SUS[StatusServerUpdateService]
    SUS --> ORCH[IAutoUpdateOrchestrator]
    ORCH --> SRC[IAutoUpdateSource]
    SRC --> GH[(GitHub Releases)]
    ORCH --> WS[(Workspace updates/)]
    ORCH --> SK[Detached Install-Skript]
    SUS --> OPT[AutoUpdateOptions]
    SSS --> OPT
```

## Skalierung und Zuverlässigkeit

- Der Server läuft **single-instanzig** — der Letzter-Admin-Schutz nutzt daher
  ein prozessweites `SemaphoreSlim` (Check-then-act), und der
  `RelayTokenPlaintextStore` ist ein In-Memory-Puffer.
- `OnValidatePrincipal` liest je authentifiziertem Request den Benutzer aus
  SQLite (Rollen-Abgleich + Deaktivierung) — bei den erwarteten Zugriffszahlen
  unkritisch, bewusst ohne Cache.
- Snapshot-Daten sind flüchtig (`InstanceSnapshotCache` im RAM); Benutzer-,
  Token- und Einstellungsdaten überstehen Neustarts in der SQLite-Datei.
