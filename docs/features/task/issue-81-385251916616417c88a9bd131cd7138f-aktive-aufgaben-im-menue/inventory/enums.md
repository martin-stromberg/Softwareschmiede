# Bestandsaufnahme: Enums

## `AufgabeStatus`
Datei: `src/Softwareschmiede/Domain/Enums/AufgabeStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Neu` | Aufgabe wurde erstellt und wartet auf Bearbeitung |
| `Gestartet` | **[RELEVANT]** Aufgabe wurde gestartet (Branch erstellt, CLI läuft oder sollte laufen) |
| `Wartend` | **[RELEVANT]** CLI hat Rate-Limit erreicht; wartet auf Wiederaufnahme |
| `Beendet` | Aufgabe wurde beendet (erfolgreich oder mit Fehler) |
| `Archiviert` | Aufgabe wurde archiviert und ist nicht mehr aktiv |

**Hinweise:**
- Die Anforderung filtert Aufgaben mit Status `Gestartet` oder `Wartend` als "aktive Aufgaben"
- Diese beiden Werte sind bereits in der Enum vorhanden und werden in `GetAktiveUndWartendeCountAsync()` des `AufgabeService` verwendet
