# Datenmodell

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel (Z. 11) |
| `ProjektId` | `Guid` | Fremdschlüssel zum Projekt (Z. 14) |
| `GitRepositoryId` | `Guid?` | Optionaler Fremdschlüssel zum Git-Repository (Z. 17) |
| `Titel` | `string` | Aufgabentitel (Z. 20) |
| `AnforderungsBeschreibung` | `string?` | Fachliche Beschreibung (Z. 23) |
| `Status` | `AufgabeStatus` | Lebenszyklusstatus (Neu/Gestartet/Wartend/Beendet/Archiviert) (Z. 26) |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Persistierter KI-Ausführungsstatus, Default `NichtGestartet` (Z. 29) |
| `BranchName` | `string?` | Zugehöriger Git-Branch (Z. 32) |
| `LokalerKlonPfad` | `string?` | Pfad des lokalen Klons (Z. 35) |
| `BasisBranchName` | `string?` | Basisbranch (Z. 38) |
| `GitArbeitsbereich` | `GitArbeitsbereich?` | Aktueller Arbeitsbereich (Z. 42) |
| `AgentenpaketName` | `string?` | Name des Agentenpakets (Z. 56) |
| `AgentenName` | `string?` | Name des Agenten (Z. 59) |
| `KiPluginPrefix` | `string?` | Gewähltes KI-Plugin (Prefix), z. B. `Softwareschmiede.KiSimulator` (Z. 62) |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungszeitpunkt (Z. 65) |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlusszeitpunkt (Z. 68) |
| `AktiveRunId` | `string?` | ID des aktiven CLI-Laufs (Z. 71) |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat der aktiven Ausführung (Z. 74) |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Zeitstempel des letzten echten CLI-Prozessstarts (Z. 77) |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus (`Laeuft`/`WartetAufEingabe`) (Z. 86) |
| `RecoveryVersion` | `int` | Recovery-Zähler (Z. 89) |
| `VorschlagPrompt` | `string?` | Zeitgesteuerter/vorgeschlagener Prompt-Text (Z. 92) |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Frühester Ausführungszeitpunkt des Vorschlagsprompts (Z. 95) |
| `AutonomKonfiguration` | `AutonomAufgabeKonfiguration?` | 1:1-Navigation zur Autonom-Aufgabe-Konfiguration (Z. 98) |
| `Projekt` | `Projekt` | Navigation (Z. 101) |
| `GitRepository` | `GitRepository?` | Navigation (Z. 104) |
| `IssueReferenz` | `IssueReferenz?` | Navigation (Z. 107) |
| `AlertReferenz` | `AlertReferenz?` | Navigation (Z. 110) |
| `PullRequests` | `List<PullRequestReferenz>` | Navigation (Z. 113) |
| `Protokolleintraege` | `List<Protokolleintrag>` | Navigation (Z. 116) |
| `DiffResults` | `List<DiffResult>` | Navigation (Z. 119) |
| `Todos` | `List<Todo>` | Navigation (Z. 122) |

**Anforderungsrelevanz:** Es existiert **kein** „Pausiert-bis"-Zeitstempel (z. B. `PausiertBisUtc`) auf `Aufgabe`. Der Rate-Limit-Wartezustand wird heute ausschließlich über `Status = Wartend` abgebildet.

## `Protokolleintrag`
Datei: `src/Softwareschmiede/Domain/Entities/Protokolleintrag.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel (Z. 9) |
| `AufgabeId` | `Guid` | Fremdschlüssel zur Aufgabe (Z. 12) |
| `Typ` | `ProtokollTyp` | Eintragstyp; `RateLimit` bereits vorhanden (Z. 15) |
| `Inhalt` | `string` | Freitext; bei `RateLimit` u. a. „Rate-Limit erkannt. Weiter ab: {ISO}" (Z. 18) |
| `AgentName` | `string?` | Optionaler Agentenname (Z. 21) |
| `Zeitstempel` | `DateTimeOffset` | Zeitpunkt des Eintrags (Z. 24) |
| `Aufgabe` | `Aufgabe` | Navigation (Z. 27) |
| `TestErgebnisse` | `List<TestErgebnis>` | Navigation (Z. 30) |
| `DiffResult` | `DiffResult?` | Navigation (Z. 33) |

## `PluginKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/PluginKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel (Z. 9) |
| `PluginTyp` | `string` | Technischer Plugin-Typ/Prefix (Z. 12) |
| `PluginKategorie` | `PluginKategorie` | Kategorie (SCM/KI/IDE) (Z. 15) |
| `AnzeigeName` | `string` | Anzeigename (Z. 18) |
| `CredentialStoreKey` | `string` | Schlüssel im Windows Credential Store (Z. 21) |
| `BaseUrl` | `string?` | Optionale Basis-URL (Z. 24) |
| `Aktiviert` | `bool` | Aktivierungsflag, Default `true` (Z. 27) |

**Anforderungsrelevanz:** Es existiert **kein** Session-Reset-/Limit-Zeitstempel auf `PluginKonfiguration`. Die Entity bildet nur Identität/Konfiguration ab; Laufzeit-Zustände werden aktuell nicht hier persistiert.

## `AppEinstellung`
Datei: `src/Softwareschmiede/Domain/Entities/AppEinstellung.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel (Z. 7) |
| `Schluessel` | `string` | Einstellungsschlüssel (Z. 10) |
| `Wert` | `string?` | Einstellungswert (Z. 13) |
| `AktualisiertAm` | `DateTimeOffset` | Änderungszeitpunkt (Z. 16) |

**Anforderungsrelevanz:** Generischer persistenter Key/Value-Store; wird bereits für Plugin-bezogene Laufzeit-/Konfigurationswerte genutzt (z. B. `plugins.enabled.<prefix>` in `PluginActivationService`, `ki.plugin.default` in `AppEinstellungService`).

## `AutonomAufgabeKonfiguration`
Datei: `src/Softwareschmiede/Domain/Entities/AutonomAufgabeKonfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Primärschlüssel (Z. 11) |
| `AufgabeId` | `Guid` | Fremdschlüssel zur Aufgabe (Z. 14) |
| `ProjektBranchName` | `string` | Projektbranch (Z. 17) |
| `InitialPrompt` | `string` | Initialer Prompt (Z. 20) |
| `PermissionsJsonPfad` | `string` | Pfad zur Permissions-Datei (Z. 23) |
| `TokenBudget` | `int` | Token-Budget (Z. 26) |
| `TokenBudgetErweitert` | `int?` | Erweitertes Budget (Z. 29) |
| `LaufzeitLimitMinuten` | `int` | Laufzeitlimit (Z. 32) |
| `RessourcenLimits` | `RessourcenLimits` | Ressourcenlimits (Z. 36) |
| `PersistenzModus` | `PersistenzModus` | Persistenzmodus (Z. 48) |
| `SkillAutogeneration` | `bool` | Skill-Autogenerierung (Z. 51) |
| `ArbeitsverzeichnisPfad` | `string` | Arbeitsverzeichnis (Z. 54) |
| `ProjektleiterAgentId` | `string?` | Projektleiter-Agent (Z. 57) |
| `SessionPauseUtc` | `DateTimeOffset?` | **Persistenter Pausenzeitstempel für Autonome Aufgaben** (Z. 60) |
| `AktiveUnteragenten` | `int?` | Anzahl aktiver Unteragenten (Z. 63) |
| `ExplizitGestoppt` | `bool` | Explizit gestoppt (Z. 66) |
| `Aufgabe` | `Aufgabe` | Navigation (Z. 69) |
| `Unteragenten` | `List<UnteragentSpezifikation>` | Navigation (Z. 72) |
| `Skills` | `List<SkillDefinition>` | Navigation (Z. 75) |

**Anforderungsrelevanz:** `SessionPauseUtc` ist die bestehende Referenz für einen persistierten „Pause-bis/seit"-Zeitstempel — wird in `SessionManagementService` gesetzt/gelöscht und in `SoftwareschmiededDbContext` über `NullableUnixMillisConverter` gemappt (Z. 225). Gilt nur für Autonome Aufgaben, nicht für reguläre `Aufgabe`-Instanzen.

## `AktiveAufgabePanelItem` (UI-Präsentationsmodell)
Datei: `src/Softwareschmiede.App/ViewModels/AktiveAufgabePanelItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Aufgaben-ID |
| `Titel` | `string` | Titel |
| `ProjektName` | `string` | Projektname |
| `ScmPluginName` | `string?` | Anzeigename SCM-Plugin (Fallback: gespeicherter Prefix) |
| `KiPluginName` | `string?` | Anzeigename KI-Plugin (Fallback: `KiPluginPrefix`) |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus |
| `Status` | `AufgabeStatus` | Aufgabenstatus |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Ausführungsstatus |
| `AktiveRunId` | `string?` | Aktive Lauf-ID |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Letzter CLI-Start |
| `HasScheduledPrompt` | `bool` | Zeitgesteuerter Prompt in Warteschlange |
| `OffeneTodoCount` | `int` | Anzahl offener To-Dos |
| `OffeneTodoLabelText` | `string` | Anzeigetext (abgeleitet) |
| `OffeneTodosAnzeigenCommand` | `ICommand?` | To-Do-Dialog-Command |
| `IsAktiv` | `bool` | Ob die Aufgabe aktuell im Inhaltsbereich angezeigt wird (settable, `SetProperty`) |

**Anforderungsrelevanz:** Keine Pausiert-/Countdown-Eigenschaften vorhanden; `MainWindowViewModel.MapAktiveAufgabePanelItem` (Z. 313 ff.) befüllt das Item aus `Aufgabe`.

## `SoftwareschmiededDbContext` — Persistenz-Konventionen
Datei: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`

- `UnixMillisConverter` (Z. 13): `ValueConverter<DateTimeOffset, long>` — speichert `DateTimeOffset` als Unix-Milliseconds (SQLite-sortierbar).
- `NullableUnixMillisConverter` (Z. 17): `ValueConverter<DateTimeOffset?, long?>` — analog für nullable Zeitstempel.
- `DbSet<Aufgabe> Aufgaben` (Z. 37), `DbSet<AppEinstellung> AppEinstellungen` (Z. 61).
- `Aufgabe`-Mapping (Z. 161 ff.): `ErstellungsDatum` → `UnixMillisConverter`; `AbschlussDatum`, `LastHeartbeatUtc`, `LetzterCliStartUtc`, `VorschlagAusfuehrenAbUtc` → `NullableUnixMillisConverter` (Z. 169–174).
- `AutonomAufgabeKonfiguration.SessionPauseUtc` → `NullableUnixMillisConverter` (Z. 225).
- `Protokolleintrag.Zeitstempel` → `UnixMillisConverter` (Z. 359).
- `AppEinstellung`-Mapping ab Z. 385 (`AktualisiertAm` → `UnixMillisConverter`, Z. 394).

**Anforderungsrelevanz:** Neue nullable `DateTimeOffset`-Felder (z. B. ein Pausiert-bis-Zeitstempel) folgen der Konvention: Property + `NullableUnixMillisConverter` im DbContext + EF-Core-Migration. Migrations liegen unter `src/Softwareschmiede/Infrastructure/Data/Migrations/`.
