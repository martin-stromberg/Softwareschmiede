# Bestandsaufnahme: Datenmodelle

## `Aufgabe`

Datei: `src\Softwareschmiede\Domain\Entities\Aufgabe.cs`

Relevante Eigenschaften für diese Anforderung:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-----------|
| `Id` | `Guid` | Eindeutige Identifier der Aufgabe |
| `Status` | `AufgabeStatus` | Status der Aufgabe (Neu, Gestartet, Wartend, Beendet, Archiviert) |
| `AusfuehrungsStatus` | `AufgabeAusfuehrungsStatus` | Lebenszyklusstatus der KI-Ausführung (NichtGestartet, Aktiv, Beendet) |
| `Titel` | `string` | Titel der Aufgabe |
| `AnforderungsBeschreibung` | `string?` | Beschreibung der Anforderung |
| `LokalerKlonPfad` | `string?` | Lokaler Pfad zum geklonten Repository |
| `BranchName` | `string` | Name des Feature-Branch für die Aufgabe |
| `KiPluginPrefix` | `string` | Prefix des verwendeten KI-Plugins |
| `AktiveRunId` | `Guid?` | ID des aktiven Laufs, wenn einer läuft |
| `LaufStatus` | `AufgabeLaufStatus?` | Laufzeit-Substatus (Läuft/Wartet auf Eingabe) – nur relevant bei aktivem Lauf |
| `LetzterCliStartUtc` | `DateTime?` | Zeitstempel des letzten CLI-Starts |
| `LastHeartbeatUtc` | `DateTime?` | Zeitstempel des letzten Heartbeats |
| `GitRepository` | `GitRepository?` | Zugehöriges Git-Repository mit Konfiguration |

### Persistierung

- Wird von `AufgabeService.AktivenLaufBeendenAsync` aktualisiert: Setzt `AusfuehrungsStatus = Beendet`, clear `AktiveRunId`, `LaufStatus`, `LetzterCliStartUtc`
- Wird von `AufgabeService.AktivenLaufSetzenAsync` aktualisiert: Setzt `AusfuehrungsStatus = Aktiv`
- Wird von `KiAusfuehrungsService.PersistAusfuehrungBeendetAsync` aktualisiert: Ruft `AufgabeService.AktivenLaufBeendenAsync` auf

---

## `CliProcessHandle`

Datei: `src\Softwareschmiede\Application\Services\KiAusfuehrungsService.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-----------|
| `AufgabeId` | `Guid` | ID der Aufgabe, zu der dieser Prozess gehört |
| `Process` | `Process` | Der verwaltete Prozess |
| `LastHeartbeat` | `DateTimeOffset` | Zeitstempel des letzten Heartbeats |
| `AbsichtlichGestoppt` | `bool` | Gibt an, ob der Prozess absichtlich beendet wurde |
| `PseudoConsoleSession` | `PseudoConsoleSession?` | Optional: Die zugehörige Pseudo-Console-Session (nur bei ConPTY-Start) |
| `NativeProcessHandle` | `IntPtr` | Natives Win32-Prozess-Handle (nur bei ConPTY-Start) |
| `SendCts` | `CancellationTokenSource?` | Koppelt verzögerten Plugin-Befehlsversand an Prozess-Lebensende |
| `OutputSink` | `ITerminalOutputSink?` | Optionale Senke für Terminal-Ausgabe |
