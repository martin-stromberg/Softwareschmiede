# Bestandsaufnahme: Datenmodell / Konfigurationsklassen

Bestand der Options- und DTO-Klassen im StatusServer-Umfeld sowie der gemeinsamen
StatusContracts-DTOs. **Es existieren derzeit keine EF-Core-Entities, kein DbContext
und kein `data`-Verzeichnis im StatusServer** — alle in der Anforderung genannten
Persistenz-Artefakte (`StatusServerDbContext`, `ApplicationUser`, `RelayToken`-Entity,
`ServerSetting`-Entity, Migrationen) sind Neubau.

## `StatusServerOptions`
Datei: `server/Softwareschmiede.StatusServer/Options/StatusServerOptions.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Relay` | `RelayOptions` | Sektion `Relay` — **entfällt laut Anforderung vollständig** (`Tokens`, `DevToken`) |
| `App` | `AppSectionOptions` | Sektion `App` — bleibt |
| `GitHub` | `GitHubOptions` | Sektion `GitHub` — bleibt |
| `Page` | `PageOptions` | Sektion `Page` — `Username`/`Password` entfallen, `RefreshSeconds` bleibt |

## `RelayOptions`
Datei: `server/Softwareschmiede.StatusServer/Options/StatusServerOptions.cs` (gleiche Datei)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Tokens` | `List<string>` | Zulässige Relay-Tokens; leer = alle Verbindungen abgelehnt (außer Dev-Modus mit `DevToken`). **Entfällt** — Tokens kommen künftig aus der Datenbank. |
| `DevToken` | `string?` | Dev-Fallback-Token, nur in `Development`-Umgebung wirksam. **Entfällt.** |

Verwendet von `RelayTokenValidator`; in `appsettings.json` als `Relay:Tokens`/`Relay:DevToken` gesetzt; in Tests über `StatusWebApplicationFactory.RelayTokens`/`DevToken` befüllt.

## `AppSectionOptions`
Datei: `server/Softwareschmiede.StatusServer/Options/StatusServerOptions.cs` (gleiche Datei)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `StaleAfterSeconds` | `int` (Default 300) | Schwelle, ab der ein fehlender Snapshot bei bestehender Verbindung als veraltet gilt; wird in `Program.cs` an `InstanceSnapshotCache` übergeben. Bleibt. |

## `GitHubOptions`
Datei: `server/Softwareschmiede.StatusServer/Options/StatusServerOptions.cs` (gleiche Datei)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Repositories` | `List<string>` | `owner/repo`-Einträge für die serverseitige Aggregation. Bleibt. |
| `Token` | `string?` | Optionales PAT für höheres Rate-Limit (Bearer-Header in `Program.cs`). Bleibt. |
| `CacheTtlSeconds` | `int` (Default 60) | Mindestabstand zwischen Abrufen desselben Endpunkts. Bleibt. |

## `PageOptions`
Datei: `server/Softwareschmiede.StatusServer/Options/StatusServerOptions.cs` (gleiche Datei)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Username` | `string?` | Basic-Auth-Benutzername; leer = Schutz deaktiviert. **Entfällt.** |
| `Password` | `string?` | Basic-Auth-Passwort; leer = Schutz deaktiviert. **Entfällt.** |
| `RefreshSeconds` | `int` (Default 15) | Meta-Refresh-Intervall der Status-Seite. Bleibt. |

## `GitHubRepositoryOptions`
Datei: `server/Softwareschmiede.StatusServer/Options/GitHubRepositoryOptions.cs`

Record `GitHubRepositoryOptions(string Owner, string Name)` — geparste Form eines
`owner/repo`-Konfigurationseintrags.

| Member | Typ | Beschreibung / Zweck |
|--------|-----|----------------------|
| `Owner`, `Name` | `string` (Positionsparameter) | Besitzer und Repository-Name |
| `DisplayName` | `string` (abgeleitet) | `owner/name`-Anzeigeform |
| `TryParse(string?, out GitHubRepositoryOptions?)` | `static bool` | Parser; verwendet von `GitHubStatusService.GetRepositoryStatusesAsync` |

## `InstanceSnapshotView`
Datei: `server/Softwareschmiede.StatusServer/Status/InstanceSnapshotCache.cs` (am Dateiende)

Read-Only-Sicht einer Instanz für Seite und JSON-Endpunkt; wird von
`InstanceSnapshotCache.GetAll()` erzeugt und von `Program.cs` (`/`, `/status.json`)
sowie `StatusPageComposer.Render` konsumiert.

| Positionsparameter | Typ | Beschreibung / Zweck |
|--------------------|-----|----------------------|
| `InstanceId` | `string` | Instanz-ID |
| `InstanceName` | `string?` | Optionaler Anzeigename |
| `Snapshot` | `StatusSnapshot?` | Letzter Snapshot; `null`, solange keiner empfangen |
| `Connected` | `bool` | Mindestens eine Relay-Verbindung besteht |
| `RegisteredUtc` | `DateTimeOffset?` | Erster Registrierungszeitpunkt in diesem Prozess |
| `LastSeenUtc` | `DateTimeOffset?` | Letzter bekannter Lebenszeitpunkt |
| `IsStale` | `bool` | Getrennt oder Snapshot veraltet |

## GitHub-DTO-Records
Datei: `server/Softwareschmiede.StatusServer/GitHub/GitHubStatusService.cs` (Dateianfang)

| Record | Positionsparameter | Zweck |
|--------|--------------------|-------|
| `GitHubPullRequestView` | `Number`, `Title`, `Url` | Read-Only-Sicht eines PRs für die Seite |
| `GitHubReleaseView` | `Name`, `TagName`, `Url`, `PublishedUtc` | Read-Only-Sicht eines Releases |
| `GitHubRepositoryStatus` | `DisplayName`, `Url`, `OpenPullRequests`, `Releases`, `OpenIssuesCount`, `FetchedUtc`, `Error`, `ErrorUtc` | Aggregierter Repository-Status; Eingang in `StatusPageComposer.Render` und `/status.json` |

## StatusContracts-DTOs (gemeinsam mit der App)
Dateien: `src/Softwareschmiede.StatusContracts/*.cs` — eigenes Projekt ohne Abhängigkeiten,
referenziert von `Softwareschmiede.StatusServer.csproj` und vom Hauptprojekt `Softwareschmiede`.

| Typ | Member | Zweck |
|-----|--------|-------|
| `RelayProtocolVersion` (static class) | `const int Current = 1` | Protokollversion des `RegisterInstance`-Handshakes; Mismatch → Ablehnung im Hub |
| `RegisterInstanceRequest` (record) | `InstanceId`, `InstanceName`, `ProtocolVersion` | Handshake-Anfrage am Hub |
| `RegisterInstanceResult` (record) | `Accepted`, `RejectionReason`; `Ok`, `Rejected(reason)` | Handshake-Ergebnis |
| `StatusSnapshot` (record) | `InstanceId`, `InstanceName`, `CreatedUtc`, `ProtocolVersion`, `AppVersion`, `RunningAutomationsCount`, `WorkItems`, `ScmEvents` (u. a.) | Periodischer Status-Push der App; enthält ausdrücklich keine Geheimnisse |
| `StatusSnapshotWorkItem`, `StatusSnapshotScmEvent`, `StatusSnapshotPullRequest` (records) | Anzeigefelder | Unter-DTOs des Snapshots |

## Nicht vorhanden (Neubau laut Anforderung)

- `StatusServerDbContext` / `IdentityDbContext<…>` + Migrationen im StatusServer
- `ApplicationUser` (`IdentityUser`-Ableger mit Deaktivierungs-Flag)
- `RelayToken`-Entity (Token-Hash, Bezeichnung, Erstellt-/Widerrufen-Zeitpunkt)
- `ServerSetting`-Entity (admin-konfigurierbare Sitzungsdauer)
- Identity-Rollen `Administration`/`Ueberwacher`, Authorization-Policies

Konventions-Vorbild dafür: siehe [EF-Core-Vorbild](efcore-vorbild.md).
