# Enums

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung. |
| `Gestartet` | Aufgabe wurde gestartet; Branch erstellt, CLI laeuft oder sollte laufen. |
| `Wartend` | CLI hat ein Rate-Limit erreicht und wartet auf Wiederaufnahme. |
| `Beendet` | Aufgabe wurde beendet. |
| `Archiviert` | Aufgabe ist archiviert und nicht mehr aktiv. |

## `AufgabeLaufStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeLaufStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Laeuft` | Die CLI laeuft und hat kuerzlich Ausgabe oder Eingabe verarbeitet. |
| `WartetAufEingabe` | Die CLI laeuft, erzeugt aber laenger keine Ausgabe und wartet vermutlich auf Eingabe. |

## `CliProcessStatus`
Datei: `src/Softwareschmiede/Application/Services/KiAusfuehrungsService.cs`

| Wert | Bedeutung |
|------|-----------|
| `Gestartet` | CLI-Prozess laeuft. |
| `Gestoppt` | CLI-Prozess wurde gestoppt oder regulaer beendet. |
| `Fehler` | CLI-Prozess wurde mit Fehler beendet. |
