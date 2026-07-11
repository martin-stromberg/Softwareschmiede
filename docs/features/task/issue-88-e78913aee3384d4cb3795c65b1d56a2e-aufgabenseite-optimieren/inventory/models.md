# Datenmodell

## `Aufgabe`
Datei: `src/Softwareschmiede/Domain/Entities/Aufgabe.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Aufgaben-ID. |
| `ProjektId` | `Guid` | Zugehoeriges Projekt. |
| `GitRepositoryId` | `Guid?` | Optionale direkte Repository-Zuordnung. |
| `Titel` | `string` | Anzeigename der Aufgabe; fachlich relevant fuer Detailansicht und Fenstertitel. |
| `AnforderungsBeschreibung` | `string?` | Beschreibung der Aufgabe; wird auch fuer `issue.md` genutzt, wenn kein Issue-Bezug vorhanden ist. |
| `Status` | `AufgabeStatus` | Lebenszykluszustand fuer UI-Umschaltung (`Neu`, `Gestartet`, `Wartend`, `Beendet`, `Archiviert`). |
| `BranchName` | `string?` | Beim Start gesetzter Branch-Name. |
| `LokalerKlonPfad` | `string?` | Lokales Arbeitsverzeichnis fuer gestartete Aufgaben. |
| `KiPluginPrefix` | `string?` | Prefix des fuer die Aufgabe verwendeten KI-/CLI-Plugins. |
| `AktiveRunId` | `string?` | Kennung eines aktiven CLI-Laufs. |
| `LastHeartbeatUtc` | `DateTimeOffset?` | Letzter Heartbeat des aktiven Laufs. |
| `LetzterCliStartUtc` | `DateTimeOffset?` | Zeitpunkt des letzten echten CLI-Prozessstarts. |
| `LaufStatus` | `AufgabeLaufStatus?` | Feingranularer Laufzeitstatus waehrend eines aktiven CLI-Laufs. |
| `IssueReferenz` | `IssueReferenz?` | Optionale Issue-Verknuepfung; kann fuer Aufgaben ohne Issue-Bezug null sein. |
| `GitRepository` | `GitRepository?` | Optional geladene Repository-Navigation. |
| `Protokolleintraege` | `List<Protokolleintrag>` | Protokolle zur Aufgabe; in der Info-Ansicht sichtbar. |
| `DiffResults` | `List<DiffResult>` | Diff-Ergebnisse fuer beendete Aufgaben. |

## `CliProcessHandle`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AufgabeId` | `Guid` | Aufgabe, zu der der laufende CLI-Prozess gehoert. |
| `Process` | `Process` | Verwalteter Betriebssystemprozess. |
| `LastHeartbeat` | `DateTimeOffset` | Laufzeit-Heartbeat im Speicher. |
| `AbsichtlichGestoppt` | `bool` | Unterscheidet manuell gestoppte Prozesse von Fehlern. |
| `PseudoConsoleSession` | `PseudoConsoleSession?` | Zugehoerige Terminal-Session fuer die eingebettete CLI. |
