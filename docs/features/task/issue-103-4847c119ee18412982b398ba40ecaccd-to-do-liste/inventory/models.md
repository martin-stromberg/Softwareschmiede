# Datenmodell-Bestandsaufnahme

## `Aufgabe` (Entity)
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | Guid | Eindeutige ID der Aufgabe |
| ProjektId | Guid | Fremdschlüssel zum Projekt |
| GitRepositoryId | Guid? | Optionale ID des Git-Repositories |
| Titel | string | Titel der Aufgabe |
| AnforderungsBeschreibung | string? | Beschreibung für KI-Agenten |
| Status | AufgabeStatus | Aktueller Status (Neu, Gestartet, Wartend, Beendet, Archiviert) |
| BranchName | string? | Git-Branch-Name |
| LokalerKlonPfad | string? | Lokaler Pfad des Repositories |
| AgentenpaketName | string? | Name des Agenten-Pakets |
| AgentenName | string? | Name des Agenten |
| KiPluginPrefix | string? | KI-Plugin-Prefix |
| ErstellungsDatum | DateTimeOffset | Erstellungszeitstempel |
| AbschlussDatum | DateTimeOffset? | Abschlusszeitstempel |
| AktiveRunId | string? | Aktive KI-Ausführungs-ID |
| LastHeartbeatUtc | DateTimeOffset? | Letzter Heartbeat-Zeitstempel |
| LetzterCliStartUtc | DateTimeOffset? | Zeitstempel des letzten CLI-Starts |
| LaufStatus | AufgabeLaufStatus? | Runtime-Status (Läuft/Wartet) |
| RecoveryVersion | int | Concurrency-Token für Recovery |
| VorschlagPrompt | string? | Persistierter Promptvorschlag |
| VorschlagAusfuehrenAbUtc | DateTimeOffset? | Geplanter Ausführungszeitpunkt |

**Navigationseigenschaften:** Projekt, GitRepository, IssueReferenz, AlertReferenz, PullRequests, Protokolleintraege, DiffResults

**Fehlende Navigationseigenschaft:** `List<Todo> Todos` (muss hinzugefügt werden)

## `SoftwareschmiededDbContext`
Datei: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`

Der DbContext definiert folgende DbSets:
- Projekte
- GitRepositories
- RepositoryStartKonfigurationen
- Aufgaben
- IssueReferenzen
- AlertReferenzen
- PullRequestReferenzen
- PullRequestWorkflowRuns
- Protokolleintraege
- TestErgebnisse
- PluginKonfigurationen
- AppEinstellungen
- PromptVorlagen
- BenachrichtigungsEinstellungen
- BenachrichtigungsAudioDateien
- BenachrichtigungsDispatchLogs
- DiffResults
- DiffBlocks
- DiffLines
- DiffCaches

**Fehlend:** DbSet<Todo> Todos

Die Aufgabe-Konfiguration (OnModelCreating) definiert Relationships mit Cascading Delete für PullRequests, Protokolleintraege und DiffResults. **Fehlend:** Konfiguration der 1:n-Beziehung zwischen Aufgabe und Todo.

## Bestehende Entities mit 1:n-Struktur (als Referenz)

### `PullRequestReferenz`
- Navigationseigenschaft in Aufgabe: `List<PullRequestReferenz> PullRequests`
- Navigationseigenschaft in Entity: `Aufgabe Aufgabe`
- DbContext-Konfiguration: `HasMany(a => a.PullRequests).WithOne(p => p.Aufgabe)`

### `Protokolleintrag`
- Navigationseigenschaft in Aufgabe: `List<Protokolleintrag> Protokolleintraege`
- Navigationseigenschaft in Entity: `Aufgabe Aufgabe`
- DbContext-Konfiguration: `HasMany(a => a.Protokolleintraege).WithOne(p => p.Aufgabe)`

### `DiffResult`
- Navigationseigenschaft in Aufgabe: `List<DiffResult> DiffResults`
- Navigationseigenschaft in Entity: `Aufgabe Aufgabe`
- DbContext-Konfiguration: `HasMany(a => a.DiffResults).WithOne(dr => dr.Aufgabe)`

**Muster für Todo:** Die Todo-Entity sollte dem gleichen Muster folgen wie PullRequestReferenz, Protokolleintrag und DiffResult.
