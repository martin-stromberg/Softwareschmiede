# Datenmodell: Aufgabenworkflow Optimierung

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | Guid | Eindeutige ID der Aufgabe |
| `ProjektId` | Guid | ID des übergeordneten Projekts |
| `GitRepositoryId` | Guid? | Optionale Verknüpfung zu Git-Repository |
| `Titel` | string | Titel der Aufgabe |
| `AnforderungsBeschreibung` | string? | Anforderung für den KI-Agenten |
| `Status` | AufgabeStatus | Aktueller Status der Aufgabe |
| `BranchName` | string? | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | string? | Lokaler Pfad des geklonten Repositories |
| `AgentenpaketName` | string? | Name des verwendeten Agentenpakets |
| `AgentenName` | string? | Name des verwendeten Agenten |
| `KiPluginPrefix` | string? | Prefix des verwendeten KI-Plugins (auf Aufgaben-Ebene) |
| `ErstellungsDatum` | DateTimeOffset | Zeitpunkt der Erstellung |
| `AbschlussDatum` | DateTimeOffset? | Zeitpunkt des Abschlusses (null wenn nicht abgeschlossen) |
| `AktiveRunId` | string? | Aktive Lauf-ID einer KI-Ausführung |
| `LastHeartbeatUtc` | DateTimeOffset? | Zeitstempel des letzten Heartbeats |
| `RecoveryVersion` | int | Concurrency-Token für Recovery-Status-Änderungen |
| `VorschlagPrompt` | string? | Persistierter Vorschlag für den nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | DateTimeOffset? | Geplanter Ausführungszeitpunkt für nächsten Prompt |

**Navigationseigenschaften:**
- `Projekt` — Übergeordnetes Projekt
- `GitRepository` — Verknüpftes Git-Repository
- `IssueReferenz` — Optionale Issue-Referenz
- `Protokolleintraege` — Protokolllogs der KI-Ausführung
- `DiffResults` — Diff-Ergebnisse (z.B. für verschiedene Dateien)

**Bemerkungen zu Anforderung:**
- `KiPluginPrefix` ist auf Aufgaben-Ebene vorhanden, aber keine entsprechende Projekt-Level-Speicherung in der Entity selbst.
- Projekt-Default-Plugin wird über `PluginDefaultSettingsService` mit Projekt-ID als Scope gespeichert (nicht in `Aufgabe.`).
