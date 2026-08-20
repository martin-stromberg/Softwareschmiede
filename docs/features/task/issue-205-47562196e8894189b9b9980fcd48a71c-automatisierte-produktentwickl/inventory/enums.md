# Enums — Bestandsaufnahme

## `AufgabeAusfuehrungsStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeAusfuehrungsStatus.cs`

**Status der Anforderung:** Erweiterung erwartet, aber noch nicht implementiert.

| Wert | Bedeutung |
|------|-----------|
| `NichtGestartet` | Die KI-Ausführung wurde noch nicht gestartet. |
| `Aktiv` | Die KI-Ausführung ist aktiv oder soll nach einem App-Neustart wiederhergestellt werden. |
| `Beendet` | Die KI-Ausführung wurde beendet; ein erneuter Start muss explizit ausgelöst werden. |

**Fehlend:** Der neue Wert `AutonomAufgabe` (für Markierung als Autonome Aufgabe mit Projektleiter-Modus) ist nicht vorhanden.

---

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

**Status der Anforderung:** Überprüfung — bereits vorhanden, Anwendbarkeit für Autonome Aufgaben muss verifiziert werden.

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung. |
| `Gestartet` | Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen). |
| `Wartend` | CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme. |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler). |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv. |

**Besonderheit:** Der Wert `Wartend` ist bereits vorhanden und könnte für Session-Pause-Status der Autonomen Aufgaben wiederverwendet werden.
