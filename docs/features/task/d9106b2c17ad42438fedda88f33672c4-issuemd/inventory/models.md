# Datenmodell - Bestandsaufnahme

## `Aufgabe` (Entität)
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe |
| `ProjektId` | `Guid` | ID des zugehörigen Projekts |
| `Titel` | `string` | Titel der Aufgabe - wird als Basis für den `issue.md` Dateiinhalt verwendet |
| `AnforderungsBeschreibung` | `string?` | Anforderungsbeschreibung für den KI-Agenten - **Kerninhalt der `issue.md` Datei** |
| `Status` | `AufgabeStatus` | Aktueller Status der Aufgabe (z.B. `Neu`, `Gestartet`, `Beendet`) |
| `BranchName` | `string?` | Name des Git-Branches für diese Aufgabe |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad des geklonten Repositories - **Zielverzeichnis für `issue.md` und `.gitignore` Anpassung** |
| `AgentenpaketName` | `string?` | Name des verwendeten Agentenpakets |
| `AgentenName` | `string?` | Name des verwendeten Agenten |
| `KiPluginPrefix` | `string?` | Prefix des für diese Aufgabe verwendeten KI-Plugins |
| `ErstellungsDatum` | `DateTimeOffset` | Erstellungsdatum der Aufgabe - wird optional in `issue.md` aufgenommen |
| `AbschlussDatum` | `DateTimeOffset?` | Abschlussdatum der Aufgabe |
| `AktiveRunId` | `string?` | Aktive Lauf-ID einer KI-Ausführung |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Zeitstempel des letzten Heartbeats |
| `RecoveryVersion` | `int` | Concurrency-Token |
| `VorschlagPrompt` | `string?` | Persistierter Vorschlag für den nächsten Prompt |
| `VorschlagAusfuehrenAbUtc` | `DateTimeOffset?` | Geplanter Ausführungszeitpunkt |

### Navigationseigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Projekt` | `Projekt` | Übergeordnetes Projekt |
| `GitRepository` | `GitRepository?` | Verknüpftes Git-Repository |
| `IssueReferenz` | `IssueReferenz?` | Verknüpfte Issue-Referenz aus Git-Provider |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolleinträge des KI-Prozesses |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse |

### Relevanz für Anforderung

Die Eigenschaft `AnforderungsBeschreibung` ist der Schlüssel für die neue Funktionalität. Sie wird aus der `Aufgabe`-Entität gelesen und in die `issue.md` Datei geschrieben. 

Die Eigenschaft `LokalerKlonPfad` wird nach dem Klonen gesetzt (durch `AufgabeService.StartenAsync`) und ist der Pfad, in den die `issue.md` Datei geschrieben wird und in den die `.gitignore` Datei angepasst wird.
