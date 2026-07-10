# Domainmodell und Persistenz

## Aufgabe

`Aufgabe` enthaelt derzeit relevante Felder:

- `KiPluginPrefix`: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs:39`
- `LastHeartbeatUtc`: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs:51`
- `LaufStatus`: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs:60`
- Navigation zu `Projekt`: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs:72`
- Navigation zu `GitRepository`: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs:75`

Ein eigenes Feld fuer "Letzter Start" existiert nicht.

## EF-Konfiguration

`DateTimeOffset`-Felder werden in SQLite als Unix-Millisekunden gespeichert, damit ORDER BY funktioniert. Beispiele:

- `ErstellungsDatum`: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:113`
- `AbschlussDatum`: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:116`
- `LastHeartbeatUtc`: `src/Softwareschmiede/Infrastructure/Data/SoftwareschmiededDbContext.cs:119`

Ein neues Startzeit-Feld sollte analog konfiguriert werden, damit Sortierung auf SQLite stabil bleibt.

## Aktuelle Snapshot-Lage

Der aktuelle Model-Snapshot enthaelt:

- `KiPluginPrefix`: `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs:76`
- `LastHeartbeatUtc`: `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs:79`
- `LaufStatus`: `src/Softwareschmiede/Migrations/SoftwareschmiededDbContextModelSnapshot.cs:82`

Die letzte thematisch nahe Migration ist `20260710115042_202607100001_AddAufgabeLaufStatus`, die `LaufStatus` auf `Aufgaben` ergaenzt:

- `src/Softwareschmiede/Migrations/20260710115042_202607100001_AddAufgabeLaufStatus.cs:14`

Fuer "Letzter Start" wird eine neue Migration benoetigt.

## Aktive Aufgaben Query

`AufgabeService.GetAktiveAufgabenAsync` ist die zentrale Datenquelle fuer die Seitenleistenliste:

- Methode: `src/Softwareschmiede/Application/Services/AufgabeService.cs:536`
- Include `Projekt`: `src/Softwareschmiede/Application/Services/AufgabeService.cs:540`
- Filter aktive/wartende Aufgaben: `src/Softwareschmiede/Application/Services/AufgabeService.cs:541`
- Sortierung: `src/Softwareschmiede/Application/Services/AufgabeService.cs:542`

Die aktuelle Sortierung nutzt `LastHeartbeatUtc ?? ErstellungsDatum`. Das ist fuer die Anforderung problematisch, weil `LastHeartbeatUtc` beim laufenden Prozess periodisch aktualisiert wird und nicht "letzter echter CLI-Start" bedeutet.

## Datenmodell-Empfehlung

Naheliegendes neues Feld:

```csharp
public DateTimeOffset? LetzterCliStartUtc { get; set; }
```

Begruendung:

- Nullable, damit bestehende Aufgaben ohne Daten weiter angezeigt werden.
- UTC, passend zu `LastHeartbeatUtc`.
- Separat von Heartbeat, damit reine Aktivitaet/Heartbeat die Sortierung nicht veraendert.

Query-Fallback sollte deterministisch sein, zum Beispiel:

```csharp
.OrderByDescending(a => a.LetzterCliStartUtc ?? a.ErstellungsDatum)
.ThenBy(a => a.Titel)
.ThenBy(a => a.Id)
```

Alternativ kann `DateTimeOffset.MinValue` fuer Aufgaben ohne Start genutzt werden, wenn Altbestand immer nach Aufgaben mit echtem Start kommen soll.

