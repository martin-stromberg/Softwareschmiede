# Bestandsaufnahme: Enums

## `AufgabeAusfuehrungsStatus`

Datei: `src\Softwareschmiede\Domain\Enums\AufgabeAusfuehrungsStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | Die KI-Ausführung wurde noch nicht gestartet. |
| `Aktiv` | Die KI-Ausführung ist aktiv oder soll nach einem App-Neustart wiederhergestellt werden. |
| `Beendet` | Die KI-Ausführung wurde beendet; ein erneuter Start muss explizit ausgelöst werden. |

### Kontext
Dieser Enum ist ein persistierter Lebenszyklusstatus der KI-Ausführung einer Aufgabe und wird in der Entity `Aufgabe` als Eigenschaft `AusfuehrungsStatus` verwendet.

---

## `CliProcessStatus`

Datei: `src\Softwareschmiede\Application\Services\KiAusfuehrungsService.cs`

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | Prozess läuft. |
| `Gestoppt` | Prozess wurde gestoppt. |
| `Fehler` | Prozess ist mit einem Fehler beendet. |

### Kontext
Dieser Enum beschreibt den Status eines CLI-Prozesses innerhalb des `KiAusfuehrungsService` und wird über das `CliProcessStatusChanged`-Event signalisiert.
