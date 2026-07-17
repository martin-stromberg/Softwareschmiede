# Datenmodell

## `Aufgabe`

**Datei:** `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

Entity, die eine Aufgabe innerhalb eines Projekts repräsentiert, die durch einen KI-Agenten bearbeitet werden kann.

### Relevant für die Anforderung

| Eigenschaft | Typ | Beschreibung / Zweck | Zeile |
|-------------|-----|----------------------|-------|
| `Id` | `Guid` | Eindeutige ID der Aufgabe | 9 |
| `Titel` | `string` | Titel der Aufgabe (wird in Fehlermeldung ausgegeben) | 18 |
| `AktiveRunId` | `string?` | Optionale ID des verknüpften aktiven KI-Laufs — `not null` bedeutet, dass die Aufgabe gerade aktiv läuft | 48 |
| `LaufStatus` | `AufgabeLaufStatus?` | **KERNPROPERTY** — Feingranularer Laufzeit-Substatus der aktiven CLI-Ausführung, nur relevant wenn `AktiveRunId` gesetzt ist. Mögliche Werte: `null` (kein Lauf / nicht initialisiert), `AufgabeLaufStatus.Laeuft` (CLI läuft aktiv), `AufgabeLaufStatus.WartetAufEingabe` (CLI wartet auf Eingabe) | 63 |
| `Status` | `AufgabeStatus` | Grober Lebenszyklus-Status (`Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert`) — unterschiedlich von `LaufStatus` | 24 |

### Semantik von `LaufStatus`

Dokumentiert in Zeile 56–62:

> Optional: Laufzeit-Substatus der aktiven CLI-Ausführung (nur relevant, solange `AktiveRunId` gesetzt ist). Wird von `CliProcessManager` anhand des `PseudoConsoleSession.RuntimeStatusChanged`-Ereignisses aktualisiert, damit die Seitenleisten-/Dashboard-Kachel (`KiAusfuehrungsStatusConverter`) zwischen "▶ Läuft" und "⏸ Wartet" unterscheiden kann, während der CLI-Prozess noch lebt. Null, solange kein aktiver Lauf bekannt ist.

### Beziehungen

- **Navigationseigenschaft `Projekt`** (Zeile 75) — Übergeordnetes Projekt
- **Navigationseigenschaft `GitRepository?`** (Zeile 78) — Optionales verknüpftes Git-Repository
- **Navigationseigenschaft `IssueReferenz?`** (Zeile 81) — Optionale Referenz aus dem Git-Provider
- **Navigationseigenschaft `Protokolleintraege`** (Zeile 84) — Protokolleinträge des KI-Prozesses
- **Navigationseigenschaft `DiffResults`** (Zeile 87) — Diff-Ergebnisse (z. B. für verschiedene Dateien oder Vergleiche)

---

## `CliUpdateSafetyResult`

**Datei:** `src/Softwareschmiede/Application/Services/Updates/UpdateModels.cs` (Zeile 88–95)

Record (Werttyp) für das Ergebnis der CLI-Sicherheitsprüfung vor einem Update.

| Eigenschaft | Typ | Beschreibung | Zeile |
|-------------|-----|-------------|-------|
| `RiskyTaskCount` | `int` | Anzahl der riskanten aktiven CLI-Aufgaben | 89 |
| `RiskyTasks` | `IReadOnlyList<string>` | Kurze Beschreibungen der riskanten Aufgaben (Format: `"{Titel} ({Id})"`) | 90 |
| `RequiresConfirmation` | `bool` (Eigenschaft) | Berechnet: `RiskyTaskCount > 0` | 94 |

Wird von `CliUpdateSafetyService.CheckAsync()` zurückgegeben (Zeile 33 der Logik-Datei).
