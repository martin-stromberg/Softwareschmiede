# Bestandsaufnahme: Datenmodelle

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `GitRepositoryId` | `Guid?` | Optionale ID des verknüpften Git-Repositories |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für den KI-Agenten |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe (Neu, Gestartet, Wartend, Beendet, Archiviert) |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des für diese Aufgabe verwendeten KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum der Aufgabe (null wenn noch nicht abgeschlossen) |
| `AktiveRunId` | `string?` | **[VORHANDEN]** Optionale aktive Lauf-ID einer KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | **[VORHANDEN]** Optionaler Zeitstempel des letzten Heartbeats einer Ausführung |
| `RecoveryVersion` | `int` | Concurrency-Token für Recovery-relevante Statusänderungen |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für den nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt für den nächsten Prompt |
| `Projekt` | `Projekt` | Navigationseigenschaft zum übergeordneten Projekt |
| `GitRepository` | `GitRepository?` | Navigationseigenschaft zum verknüpften Git-Repository |
| `IssueReferenz` | `IssueReferenz?` | Verknüpfte Issue-Referenz aus dem Git-Provider |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge des KI-Prozesses für diese Aufgabe |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse für diese Aufgabe |

**Hinweise:**
- Die Anforderung nutzt bereits vorhandene Properties `AktiveRunId` und `LastHeartbeatUtc` zur Bestimmung des KI-Ausführungsstatus
- Keine neuen Properties sind erforderlich für die Anforderung "Aktive Aufgaben im Menü"
- `Status` mit Werten `Gestartet` und `Wartend` sind die Filter-Kriterien für aktive Aufgaben
