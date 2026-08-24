# Enumerationen

## `AufgabeAusfuehrungsStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | Die KI-Ausführung wurde noch nicht gestartet. |
| `Aktiv` | Die KI-Ausführung ist aktiv oder soll nach einem App-Neustart wiederhergestellt werden. |
| `Beendet` | Die KI-Ausführung wurde beendet; ein erneuter Start muss explizit ausgelöst werden. |

**Verwendung**: In `Aufgabe.AusfuehrungsStatus` für die Verwaltung des Lifecycle einer autonomen oder regulären KI-Ausführung.

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung. |
| `Gestartet` | Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen). |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme. |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler). |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv. |

**Verwendung**: In `Aufgabe.Status` für die Verwaltung des allgemeinen Entwicklungs-Lifecycle.

## `CliProcessStatus`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs` (Zeile 721)

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft. |
| `Gestoppt` | Prozess wurde gestoppt. |
| `Fehler` | Prozess ist mit einem Fehler beendet. |

**Verwendung**: Wird von `KiAusfuehrungsService.CliProcessStatusChanged`-Event genutzt, um Statusänderungen von CLI-Prozessen zu signalisieren.

**Beobachtung**: Dieser Enum ist lokal im `KiAusfuehrungsService` definiert (nicht in einem separaten Enum-File).

## `AufgabeLaufStatus`
Datei: Nicht vollständig gelesen, aber referenziert in `Aufgabe.LaufStatus`

**Beschreibung** (aus Kommentar in Aufgabe.cs): Wird von `CliProcessManager` anhand des `PseudoConsoleSession.RuntimeStatusChanged`-Ereignisses aktualisiert, um zwischen "▶ Läuft" und "⏸ Wartet" zu unterscheiden, während der CLI-Prozess noch lebt.

**Beobachtung**: Der Enum-Werte sind nicht vollständig dokumentiert in dieser Analyse.
