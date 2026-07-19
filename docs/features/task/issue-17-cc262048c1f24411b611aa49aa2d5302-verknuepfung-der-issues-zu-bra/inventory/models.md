# Datenmodell und Persistenz

## `Aufgabe`

**Datei:** `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Element | Typ | Zeile | Relevanz |
|---------|-----|-------|----------|
| `Id` | `Guid` | 9 | Identifiziert die Aufgabe und wird fuer generierte Branch-Namen verwendet. |
| `GitRepositoryId` | `Guid?` | 15 | Optional explizit verknuepftes Repository fuer PR-Repository-Aufloesung. |
| `Titel` | `string` | 18 | Fallback fuer PR-Titel und Slug-Basis fuer Branch-Namen. |
| `BranchName` | `string?` | 27 | Persistierter Aufgabenbranch; Pflicht fuer Push/PR. |
| `LokalerKlonPfad` | `string?` | 30 | Lokaler Arbeitsbereich fuer Git-Operationen. |
| `GitRepository` | `GitRepository?` | 78 | Navigation zum verknuepften Repository. |
| `IssueReferenz` | `IssueReferenz?` | 81 | Zentrale Verbindung zum Git-Provider-Issue. |

Die Entity enthaelt bereits beide Seiten der fachlichen Verknuepfung: Branch und Issue-Referenz liegen an derselben Aufgabe.

## `IssueReferenz`

**Datei:** `src/Softwareschmiede/Domain/Entities/IssueReferenz.cs`

| Element | Typ | Zeile | Relevanz |
|---------|-----|-------|----------|
| `Id` | `Guid` | 7 | Eindeutige Referenz-ID. |
| `AufgabeId` | `Guid` | 10 | Fremdschluessel zur Aufgabe. |
| `IssueNummer` | `int?` | 13 | Primaerer Wert fuer `Closes #<IssueNummer>`. |
| `Titel` | `string` | 16 | Kontextdaten des Issues. |
| `Body` | `string?` | 19 | Kontextdaten des Issues. |
| `LabelsJson` | `string` | 22 | Persistierte Labels als JSON. |
| `Milestone` | `string?` | 25 | Optionaler Milestone-Kontext. |
| `IssueUrl` | `string?` | 28 | Optionaler Link zum Provider-Issue. |
| `Aufgabe` | `Aufgabe` | 31 | Navigation zur Aufgabe. |

`IssueNummer` ist nullable. Implementierungen muessen daher `null`, `0` und negative Werte wie "keine gueltige Issue-Nummer" behandeln.

## EF-Core-Mapping

**Datei:** `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs`

| Mapping | Zeile | Relevanz |
|---------|-------|----------|
| `DbSet<IssueReferenz>` | 25 | Eigene Tabelle fuer Issue-Referenzen. |
| `Aufgabe` -> `IssueReferenz` via `HasOne(...).WithOne(...)` | 140-143 | 1:1-Beziehung mit `AufgabeId` als Foreign Key. |
| `IssueReferenz` Entity-Key | 154-158 | Nur der Primaerschluessel ist explizit konfiguriert; Property-Defaults kommen aus EF-Konventionen. |

Eine Datenmodell-Aenderung ist fuer die Kernanforderung voraussichtlich nicht noetig.
